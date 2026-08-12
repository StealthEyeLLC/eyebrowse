using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, RuntimeScriptInfo>> _scriptsByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RuntimePausedState> _pausedByTarget = new(StringComparer.Ordinal);

    private void HandleExtensionsDebuggerEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
            return;
        var method = methodValue.GetString() ?? "";
        if (!message.TryGetProperty("sessionId", out var sessionValue) || sessionValue.ValueKind != JsonValueKind.String ||
            !_sessions.TryGetValue(sessionValue.GetString() ?? "", out var state))
            return;
        if (!message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        if (method == "Debugger.scriptParsed")
        {
            var id = GetString(p, "scriptId");
            if (string.IsNullOrWhiteSpace(id)) return;
            long? contextId = p.TryGetProperty("executionContextId", out var context) && context.TryGetInt64(out var value) ? value : null;
            var info = new RuntimeScriptInfo(
                state.LogicalId,
                id,
                GetString(p, "url"),
                NullIfEmpty(GetString(p, "sourceMapURL")),
                GetString(p, "hash"),
                p.TryGetProperty("isModule", out var module) && module.ValueKind == JsonValueKind.True,
                contextId,
                DateTimeOffset.UtcNow);
            _scriptsByTarget.GetOrAdd(state.TargetId, _ => new ConcurrentDictionary<string, RuntimeScriptInfo>(StringComparer.Ordinal))[id] = info;
        }
        else if (method == "Debugger.paused")
        {
            var frames = p.TryGetProperty("callFrames", out var callFrames) && callFrames.ValueKind == JsonValueKind.Array
                ? callFrames.Clone()
                : JsonSerializer.SerializeToElement(Array.Empty<object>());
            JsonElement? data = p.TryGetProperty("data", out var rawData) ? rawData.Clone() : null;
            _pausedByTarget[state.TargetId] = new RuntimePausedState(state.LogicalId, GetString(p, "reason"), frames, data, DateTimeOffset.UtcNow);
        }
        else if (method == "Debugger.resumed")
        {
            _pausedByTarget.TryRemove(state.TargetId, out _);
        }
    }

    public async Task<object> RuntimeDebuggerEnableAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        var state = await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Debugger.enable", new { maxScriptsCacheSize = 32 * 1024 * 1024 }, state.SessionId, cancellationToken);
        return new { target = target.Id, enabled = true };
    }

    public async Task<IReadOnlyList<RuntimeScriptInfo>> RuntimeScriptsAsync(
        string targetReference,
        string? contains = null,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        await RuntimeDebuggerEnableAsync(target.Id, cancellationToken);
        if (!_scriptsByTarget.TryGetValue(target.TargetId, out var scripts)) return Array.Empty<RuntimeScriptInfo>();
        IEnumerable<RuntimeScriptInfo> values = scripts.Values;
        if (!string.IsNullOrWhiteSpace(contains))
            values = values.Where(x => x.Url.Contains(contains, StringComparison.OrdinalIgnoreCase));
        return values.OrderByDescending(x => x.ObservedAtUtc).Take(Math.Clamp(limit, 1, 5000)).ToArray();
    }

    public async Task<JsonElement> RuntimeScriptSourceAsync(string targetReference, string scriptId, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        var state = await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        await RuntimeDebuggerEnableAsync(target.Id, cancellationToken);
        return await _cdp.SendAsync("Debugger.getScriptSource", new { scriptId }, state.SessionId, cancellationToken);
    }

    public async Task<JsonElement> RuntimeScriptSearchAsync(
        string targetReference,
        string scriptId,
        string query,
        bool caseSensitive = false,
        bool isRegex = false,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        var state = await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        await RuntimeDebuggerEnableAsync(target.Id, cancellationToken);
        return await _cdp.SendAsync("Debugger.searchInContent", new { scriptId, query, caseSensitive, isRegex }, state.SessionId, cancellationToken);
    }

    public async Task<RuntimePausedState?> RuntimePausedAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        await RuntimeDebuggerEnableAsync(target.Id, cancellationToken);
        return _pausedByTarget.TryGetValue(target.TargetId, out var paused) ? paused : null;
    }

    public async Task<IReadOnlyList<ExtensionInfo>> ExtensionListAsync(CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Extensions.getExtensions");
        var result = await _cdp.SendAsync("Extensions.getExtensions", cancellationToken: cancellationToken);
        if (!result.TryGetProperty("extensions", out var extensions) || extensions.ValueKind != JsonValueKind.Array)
            return Array.Empty<ExtensionInfo>();
        return extensions.EnumerateArray().Select(x => new ExtensionInfo(
            GetString(x, "id"),
            GetString(x, "name"),
            GetString(x, "version"),
            GetString(x, "path"),
            x.TryGetProperty("enabled", out var enabled) && enabled.ValueKind == JsonValueKind.True)).ToArray();
    }

    public async Task<ExtensionInfo> ExtensionLoadUnpackedAsync(
        string path,
        bool enableInIncognito = false,
        CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Extensions.loadUnpacked");
        var full = Path.GetFullPath(path);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        var result = await _cdp.SendAsync("Extensions.loadUnpacked", new { path = full, enableInIncognito }, cancellationToken: cancellationToken);
        var id = GetString(result, "id");
        var loaded = (await ExtensionListAsync(cancellationToken)).FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.Ordinal));
        return loaded ?? new ExtensionInfo(id, "", "", full, true);
    }

    public async Task<object> ExtensionUninstallAsync(string id, CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Extensions.uninstall");
        await _cdp.SendAsync("Extensions.uninstall", new { id }, cancellationToken: cancellationToken);
        return new { id, uninstalled = true };
    }

    public async Task<object> ExtensionTriggerActionAsync(string id, string targetReference, CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Extensions.triggerAction");
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Extension action target {targetReference} is type '{target.Type}', not a page.");
        var tabTargetId = await ResolveProviderTabTargetIdAsync(target, cancellationToken);
        await _cdp.SendAsync("Extensions.triggerAction", new { id, targetId = tabTargetId }, cancellationToken: cancellationToken);
        return new { id, target = target.Id, providerTabTargetId = tabTargetId, triggered = true };
    }

    public async Task<JsonElement> ExtensionStorageAsync(
        string id,
        string storageArea,
        IReadOnlyList<string>? keys = null,
        CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Extensions.getStorageItems");
        if (storageArea is not ("session" or "local" or "sync" or "managed"))
            throw new ArgumentException("storageArea must be session, local, sync, or managed.");
        var state = await ResolveExtensionRuntimeStateAsync(id, cancellationToken);
        var parameters = new Dictionary<string, object?> { ["id"] = id, ["storageArea"] = storageArea };
        if (keys is { Count: > 0 }) parameters["keys"] = keys;
        return await _cdp.SendAsync("Extensions.getStorageItems", parameters, state.SessionId, cancellationToken);
    }

    private async Task<TargetState> ResolveExtensionRuntimeStateAsync(string extensionId, CancellationToken cancellationToken)
    {
        var prefix = $"chrome-extension://{extensionId}/";
        var targets = await ListTargetsAsync(cancellationToken);
        var candidate = targets
            .Where(x => x.Url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => string.Equals(x.Type, "service_worker", StringComparison.OrdinalIgnoreCase) ? 0 :
                          string.Equals(x.Type, "background_page", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .FirstOrDefault();
        if (candidate is null)
            throw new InvalidOperationException($"Extension {extensionId} has no live DevTools target; extension storage requires an extension-associated target.");
        return await EnsureRuntimeTargetStateAsync(candidate, cancellationToken);
    }

    private async Task<string> ResolveProviderTabTargetIdAsync(BrowserTarget pageTarget, CancellationToken cancellationToken)
    {
        var pageInfoResult = await _cdp.SendAsync("Target.getTargetInfo", new { targetId = pageTarget.TargetId }, cancellationToken: cancellationToken);
        if (!pageInfoResult.TryGetProperty("targetInfo", out var pageInfo) || pageInfo.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"Chrome did not return targetInfo for {pageTarget.Id}.");
        var browserContextId = GetString(pageInfo, "browserContextId");
        var url = GetString(pageInfo, "url");
        var title = GetString(pageInfo, "title");

        var tabsResult = await _cdp.SendAsync("Target.getTargets", new
        {
            filter = new[] { new { type = "tab", exclude = false } }
        }, cancellationToken: cancellationToken);
        if (!tabsResult.TryGetProperty("targetInfos", out var targetInfos) || targetInfos.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Chrome did not return tab targetInfos.");

        var candidates = targetInfos.EnumerateArray()
            .Where(x => string.Equals(GetString(x, "browserContextId"), browserContextId, StringComparison.Ordinal) &&
                        string.Equals(GetString(x, "url"), url, StringComparison.Ordinal))
            .ToArray();
        var titled = candidates.Where(x => string.Equals(GetString(x, "title"), title, StringComparison.Ordinal)).ToArray();
        if (titled.Length == 1) return GetString(titled[0], "targetId");
        if (candidates.Length == 1) return GetString(candidates[0], "targetId");

        var active = (titled.Length > 0 ? titled : candidates)
            .Where(x => x.TryGetProperty("embedderData", out var embedder) && embedder.ValueKind == JsonValueKind.Object &&
                        embedder.TryGetProperty("tabActive", out var activeValue) && activeValue.ValueKind == JsonValueKind.True)
            .ToArray();
        if (active.Length == 1) return GetString(active[0], "targetId");
        throw new InvalidOperationException($"Could not conservatively resolve a unique Chrome tab target for {pageTarget.Id}; found {candidates.Length} URL-matching candidates.");
    }

    public object CapabilitySummary(string? prefix = null)
    {
        IEnumerable<KeyValuePair<string, ProtocolFacet>> facets = _protocol.Facets;
        if (!string.IsNullOrWhiteSpace(prefix))
            facets = facets.Where(x => x.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        var values = facets.OrderBy(x => x.Key, StringComparer.Ordinal).Take(1000).Select(x => new
        {
            name = x.Key,
            x.Value.Kind,
            x.Value.Experimental,
            x.Value.Deprecated
        }).ToArray();
        return new
        {
            version = $"{_protocol.Major}.{_protocol.Minor}",
            domainCount = _protocol.DomainCount,
            commandCount = _protocol.Commands.Count,
            eventCount = _protocol.Events.Count,
            prefix,
            facets = values
        };
    }
    private void RequireProtocolCommand(string command)
    {
        if (!_protocol.Supports(command))
            throw new NotSupportedException($"The running Chrome protocol does not advertise {command}.");
    }
}
