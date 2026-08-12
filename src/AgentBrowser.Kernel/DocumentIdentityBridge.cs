using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.Cdp;

namespace AgentBrowser.Kernel;

internal sealed record BridgeDocumentState(
    string DocumentToken,
    string? DocumentLogicalId,
    long Sequence,
    int BindingCount);

internal sealed record BridgeElementBinding(string LogicalId, int Incarnation, long Serial);

internal sealed class DocumentIdentityBridge : IDisposable
{
    private readonly CdpClient _cdp;
    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);

    public DocumentIdentityBridge(CdpClient cdp)
    {
        _cdp = cdp;
        _cdp.EventReceived += OnEventAsync;
    }

    public void TrackSession(string sessionId)
    {
        _sessions.TryAdd(sessionId, new SessionState());
    }

    public async Task<BridgeDocumentState?> ReadDocumentStateAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var contextId = await FindBridgeContextAsync(sessionId, cancellationToken);
        if (contextId is null) return null;

        var result = await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression = "globalThis.__eyebrowseIdentity?.exportBindings?.() ?? null",
            contextId,
            returnByValue = true,
            awaitPromise = true
        }, sessionId, cancellationToken);

        if (!TryRemoteValue(result, out var value) || value.ValueKind != JsonValueKind.Object)
            return null;

        var token = GetString(value, "documentToken");
        if (string.IsNullOrWhiteSpace(token)) return null;
        var documentLogicalId = value.TryGetProperty("documentLogicalId", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;
        var sequence = value.TryGetProperty("sequence", out var s) && s.TryGetInt64(out var seq) ? seq : 0;
        var bindingCount = value.TryGetProperty("bindings", out var b) && b.ValueKind == JsonValueKind.Array ? b.GetArrayLength() : 0;
        return new BridgeDocumentState(token, documentLogicalId, sequence, bindingCount);
    }

    public async Task SetDocumentLogicalIdAsync(
        string sessionId,
        string logicalId,
        CancellationToken cancellationToken = default)
    {
        var contextId = await FindBridgeContextAsync(sessionId, cancellationToken);
        if (contextId is null) return;
        await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression = $"globalThis.__eyebrowseIdentity?.setDocumentLogicalId?.({JsonSerializer.Serialize(logicalId)})",
            contextId,
            returnByValue = true
        }, sessionId, cancellationToken);
    }

    public async Task<BridgeElementBinding?> TryGetLogicalBindingAsync(
        string sessionId,
        int backendNodeId,
        CancellationToken cancellationToken = default)
    {
        var contextId = await FindBridgeContextAsync(sessionId, cancellationToken);
        if (contextId is null) return null;

        string? objectId = null;
        try
        {
            var resolved = await _cdp.SendAsync("DOM.resolveNode", new
            {
                backendNodeId,
                executionContextId = contextId.Value
            }, sessionId, cancellationToken);
            objectId = resolved.GetProperty("object").GetProperty("objectId").GetString();
            if (string.IsNullOrWhiteSpace(objectId)) return null;

            var lookup = await _cdp.SendAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = "function(){return globalThis.__eyebrowseIdentity?.lookup?.(this) ?? null;}",
                returnByValue = true
            }, sessionId, cancellationToken);

            if (!TryRemoteValue(lookup, out var value) || value.ValueKind != JsonValueKind.Object)
                return null;
            var id = value.TryGetProperty("logicalId", out var logical) && logical.ValueKind == JsonValueKind.String
                ? logical.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(id)) return null;
            var incarnation = value.TryGetProperty("incarnation", out var incarnationValue) && incarnationValue.TryGetInt32(out var i)
                ? Math.Max(1, i)
                : 1;
            var serial = value.TryGetProperty("serial", out var serialValue) && serialValue.TryGetInt64(out var s) ? s : 0;
            return new BridgeElementBinding(id!, incarnation, serial);
        }
        catch (CdpException)
        {
            return null;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                try { await _cdp.SendAsync("Runtime.releaseObject", new { objectId }, sessionId, CancellationToken.None); } catch { }
            }
        }
    }

    public async Task<BridgeElementBinding?> BindLogicalIdAsync(
        string sessionId,
        int backendNodeId,
        string logicalId,
        int incarnation,
        CancellationToken cancellationToken = default)
    {
        var contextId = await FindBridgeContextAsync(sessionId, cancellationToken);
        if (contextId is null) return null;

        string? objectId = null;
        try
        {
            var resolved = await _cdp.SendAsync("DOM.resolveNode", new
            {
                backendNodeId,
                executionContextId = contextId.Value
            }, sessionId, cancellationToken);
            objectId = resolved.GetProperty("object").GetProperty("objectId").GetString();
            if (string.IsNullOrWhiteSpace(objectId)) return null;

            var bind = await _cdp.SendAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = "function(logicalId,incarnation){return globalThis.__eyebrowseIdentity?.bind?.(this,logicalId,incarnation) ?? null;}",
                arguments = new object[] { new { value = logicalId }, new { value = Math.Max(1, incarnation) } },
                returnByValue = true
            }, sessionId, cancellationToken);

            if (!TryRemoteValue(bind, out var value) || value.ValueKind != JsonValueKind.Object)
                return new BridgeElementBinding(logicalId, Math.Max(1, incarnation), 0);
            var boundId = value.TryGetProperty("logicalId", out var logical) && logical.ValueKind == JsonValueKind.String
                ? logical.GetString() ?? logicalId
                : logicalId;
            var boundIncarnation = value.TryGetProperty("incarnation", out var incarnationValue) && incarnationValue.TryGetInt32(out var i)
                ? Math.Max(1, i)
                : Math.Max(1, incarnation);
            var serial = value.TryGetProperty("serial", out var serialValue) && serialValue.TryGetInt64(out var s) ? s : 0;
            return new BridgeElementBinding(boundId, boundIncarnation, serial);
        }
        catch (CdpException)
        {
            return null;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                try { await _cdp.SendAsync("Runtime.releaseObject", new { objectId }, sessionId, CancellationToken.None); } catch { }
            }
        }
    }

    public async Task<string?> GetOrBindLogicalIdAsync(
        string sessionId,
        int backendNodeId,
        string proposedLogicalId,
        CancellationToken cancellationToken = default)
    {
        var existing = await TryGetLogicalBindingAsync(sessionId, backendNodeId, cancellationToken);
        if (existing is not null) return existing.LogicalId;
        return (await BindLogicalIdAsync(sessionId, backendNodeId, proposedLogicalId, 1, cancellationToken))?.LogicalId;
    }

    public async Task<JsonElement?> ReadEventsAsync(
        string sessionId,
        long since,
        CancellationToken cancellationToken = default)
    {
        var contextId = await FindBridgeContextAsync(sessionId, cancellationToken);
        if (contextId is null) return null;
        var result = await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression = $"globalThis.__eyebrowseIdentity?.eventsSince?.({since}) ?? null",
            contextId,
            returnByValue = true
        }, sessionId, cancellationToken);
        return TryRemoteValue(result, out var value) ? value.Clone() : null;
    }

    private async Task<long?> FindBridgeContextAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var state))
            return null;
        if (state.BridgeContextId is { } cached && state.Contexts.ContainsKey(cached))
            return cached;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            foreach (var contextId in state.Contexts.Keys.OrderBy(x => x))
            {
                try
                {
                    var result = await _cdp.SendAsync("Runtime.evaluate", new
                    {
                        expression = "Number(globalThis.__eyebrowseIdentity?.version ?? 0) >= 1",
                        contextId,
                        returnByValue = true
                    }, sessionId, cancellationToken);
                    if (TryRemoteValue(result, out var value) && value.ValueKind == JsonValueKind.True)
                    {
                        state.BridgeContextId = contextId;
                        return contextId;
                    }
                }
                catch (CdpException)
                {
                }
            }

            await Task.Delay(40, cancellationToken);
        }

        return null;
    }

    private Task OnEventAsync(JsonElement message)
    {
        if (!message.TryGetProperty("sessionId", out var sessionElement) || sessionElement.ValueKind != JsonValueKind.String)
            return Task.CompletedTask;
        var sessionId = sessionElement.GetString();
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
            return Task.CompletedTask;
        if (!message.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
            return Task.CompletedTask;

        var method = methodElement.GetString();
        if (method == "Runtime.executionContextCreated")
        {
            try
            {
                var context = message.GetProperty("params").GetProperty("context");
                if (context.GetProperty("id").TryGetInt64(out var id))
                    state.Contexts[id] = 0;
            }
            catch { }
        }
        else if (method == "Runtime.executionContextDestroyed")
        {
            try
            {
                var p = message.GetProperty("params");
                if (p.TryGetProperty("executionContextId", out var idElement) && idElement.TryGetInt64(out var id))
                {
                    state.Contexts.TryRemove(id, out _);
                    if (state.BridgeContextId == id) state.BridgeContextId = null;
                }
            }
            catch { }
        }
        else if (method == "Runtime.executionContextsCleared")
        {
            state.Contexts.Clear();
            state.BridgeContextId = null;
        }

        return Task.CompletedTask;
    }

    private static bool TryRemoteValue(JsonElement commandResult, out JsonElement value)
    {
        value = default;
        if (!commandResult.TryGetProperty("result", out var remote) || remote.ValueKind != JsonValueKind.Object)
            return false;
        if (!remote.TryGetProperty("value", out var raw))
            return false;
        value = raw;
        return true;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    public void Dispose()
    {
        _cdp.EventReceived -= OnEventAsync;
    }

    private sealed class SessionState
    {
        public ConcurrentDictionary<long, byte> Contexts { get; } = new();
        public long? BridgeContextId { get; set; }
    }
}