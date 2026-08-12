using System.Text.Json;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    public async Task<IReadOnlyList<RuntimeToolInfo>> RuntimeToolsListAsync(
        string targetReference,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var document = await EnsureDocumentIdentityAsync(state, target, cancellationToken);

        const string discoveryScript = """
(async()=>{
  const groups=[];
  const pending=[];
  const root=window.__dtmcp ?? (window.__dtmcp={});
  root.toolGroups=[];
  const register=(group)=>{
    if(!group || typeof group.name!=='string' || !Array.isArray(group.tools)) return;
    for(const tool of group.tools){
      if(!tool || typeof tool.name!=='string' || typeof tool.description!=='string' || typeof tool.execute!=='function') return;
    }
    root.toolGroups.push(group);
    groups.push(group);
  };
  const event=new CustomEvent('devtoolstooldiscovery');
  event.respondWith=(value)=>{
    const task=Promise.resolve(value).then(register);
    pending.push(task);
    return task;
  };
  window.dispatchEvent(event);
  await Promise.allSettled(pending);
  await new Promise(resolve=>setTimeout(resolve,0));
  if(!root.executeTool){
    root.executeTool=async(toolName,args)=>{
      for(const group of root.toolGroups ?? []){
        const tool=(group.tools ?? []).find(t=>t.name===toolName);
        if(tool) return await tool.execute(args);
      }
      throw new Error(`Tool ${toolName} not found`);
    };
  }
  return groups.map(group=>({
    name:group.name,
    description:typeof group.description==='string'?group.description:null,
    tools:(group.tools ?? []).map(tool=>({
      name:tool.name,
      description:tool.description,
      inputSchema:tool.inputSchema ?? {type:'object'}
    }))
  }));
})()
""";

        var result = await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression = discoveryScript,
            returnByValue = true,
            awaitPromise = true,
            userGesture = false
        }, state.SessionId, cancellationToken);

        if (!TryRemoteValue(result, out var value) || value.ValueKind != JsonValueKind.Array)
            return Array.Empty<RuntimeToolInfo>();

        var tools = new List<RuntimeToolInfo>();
        foreach (var group in value.EnumerateArray())
        {
            var groupName = GetString(group, "name");
            var groupDescription = NullIfEmpty(GetString(group, "description"));
            if (!group.TryGetProperty("tools", out var groupTools) || groupTools.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var tool in groupTools.EnumerateArray())
            {
                var name = GetString(tool, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var schema = tool.TryGetProperty("inputSchema", out var schemaValue)
                    ? schemaValue.Clone()
                    : JsonSerializer.SerializeToElement(new { type = "object" });
                tools.Add(new RuntimeToolInfo(
                    state.LogicalId,
                    document,
                    groupName,
                    groupDescription,
                    name,
                    GetString(tool, "description"),
                    schema));
            }
        }
        return tools;
    }

    public async Task<RuntimeToolInfo> RuntimeToolsInspectAsync(
        string targetReference,
        string name,
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var tools = await RuntimeToolsListAsync(targetReference, cancellationToken);
        var matches = tools.Where(x =>
            string.Equals(x.Name, name, StringComparison.Ordinal) &&
            (string.IsNullOrWhiteSpace(group) || string.Equals(x.Group, group, StringComparison.Ordinal))).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"Runtime tool '{name}' is not advertised by the current document."),
            _ => throw new InvalidOperationException($"Runtime tool '{name}' is ambiguous across tool groups; specify group.")
        };
    }

    public async Task<RuntimeToolExecutionResult> RuntimeToolsExecuteAsync(
        string targetReference,
        string name,
        JsonElement input,
        string? group = null,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var tool = await RuntimeToolsInspectAsync(targetReference, name, group, cancellationToken);
        var currentDocument = await EnsureDocumentIdentityAsync(state, target, cancellationToken);
        if (!string.Equals(tool.Document, currentDocument, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime tool scope changed before execution; rediscover tools in the current document.");

        var expression = $"globalThis.__dtmcp?.executeTool?.({JsonSerializer.Serialize(name)},{input.GetRawText()})";
        var result = await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = false,
            awaitPromise = true,
            userGesture = true
        }, state.SessionId, cancellationToken);

        if (result.TryGetProperty("exceptionDetails", out var exceptionDetails))
            throw new InvalidOperationException($"Runtime tool '{name}' threw: {exceptionDetails}");
        if (!result.TryGetProperty("result", out var remote) || remote.ValueKind != JsonValueKind.Object)
            return new RuntimeToolExecutionResult(state.LogicalId, currentDocument, name, null, null, null, null, "No remote result object.");

        var type = NullIfEmpty(GetString(remote, "type"));
        var subtype = NullIfEmpty(GetString(remote, "subtype"));
        var description = NullIfEmpty(GetString(remote, "description"));
        var objectId = NullIfEmpty(GetString(remote, "objectId"));

        try
        {
            if (string.Equals(subtype, "node", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(objectId))
            {
                var described = await _cdp.SendAsync("DOM.describeNode", new { objectId, depth = 0, pierce = true }, state.SessionId, cancellationToken);
                int? backendNodeId = described.TryGetProperty("node", out var node) &&
                    node.TryGetProperty("backendNodeId", out var backendValue) && backendValue.TryGetInt32(out var backend)
                        ? backend
                        : null;
                string? elementId = null;
                if (backendNodeId is not null)
                {
                    elementId = state.ElementsByLogicalId.Values.FirstOrDefault(x => x.BackendNodeId == backendNodeId.Value)?.Id;
                    if (elementId is null)
                    {
                        await ObserveAsync(targetReference, cancellationToken);
                        elementId = state.ElementsByLogicalId.Values.FirstOrDefault(x => x.BackendNodeId == backendNodeId.Value)?.Id;
                    }
                }
                return new RuntimeToolExecutionResult(state.LogicalId, currentDocument, name, null, elementId, backendNodeId, subtype, description);
            }

            if (remote.TryGetProperty("value", out var directValue))
                return new RuntimeToolExecutionResult(state.LogicalId, currentDocument, name, directValue.Clone(), null, null, type, description);

            if (!string.IsNullOrWhiteSpace(objectId))
            {
                var byValue = await _cdp.SendAsync("Runtime.callFunctionOn", new
                {
                    objectId,
                    functionDeclaration = "function(){try{return JSON.parse(JSON.stringify(this));}catch{return String(this);}}",
                    returnByValue = true,
                    awaitPromise = true
                }, state.SessionId, cancellationToken);
                if (TryRemoteValue(byValue, out var value))
                    return new RuntimeToolExecutionResult(state.LogicalId, currentDocument, name, value.Clone(), null, null, type, description);
            }

            return new RuntimeToolExecutionResult(state.LogicalId, currentDocument, name, null, null, null, type, description);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                try { await _cdp.SendAsync("Runtime.releaseObject", new { objectId }, state.SessionId, CancellationToken.None); } catch { }
            }
        }
    }

    private async Task<string> EnsureDocumentIdentityAsync(TargetState state, BrowserTarget target, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(state.DocumentLogicalId)) return state.DocumentLogicalId;
        var surface = await ObserveAsync(target.Id, cancellationToken);
        return surface.Document;
    }
}