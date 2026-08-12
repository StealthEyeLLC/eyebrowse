using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private async Task<BrowserTarget> ResolveAnyTargetAsync(string reference, CancellationToken cancellationToken)
    {
        var targets = await ListTargetsAsync(cancellationToken);
        return targets.FirstOrDefault(x =>
            string.Equals(x.Id, reference, StringComparison.Ordinal) ||
            string.Equals(x.TargetId, reference, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Unknown target '{reference}'.");
    }

    private async Task<TargetState> EnsureRuntimeTargetStateAsync(BrowserTarget target, CancellationToken cancellationToken)
    {
        if (string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
            return await EnsureTargetStateAsync(target, cancellationToken);

        if (_targets.TryGetValue(target.TargetId, out var existing))
            return existing;

        var attach = await _cdp.SendAsync(
            "Target.attachToTarget",
            new { targetId = target.TargetId, flatten = true },
            cancellationToken: cancellationToken);
        var sessionId = attach.GetProperty("sessionId").GetString()
            ?? throw new InvalidOperationException("Target.attachToTarget did not return a sessionId.");

        var created = new TargetState(target.Id, target.TargetId, sessionId);
        var state = _targets.GetOrAdd(target.TargetId, created);
        if (!ReferenceEquals(state, created))
        {
            try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId }, cancellationToken: cancellationToken); }
            catch { }
            return state;
        }

        _sessions[sessionId] = state;
        try { await _cdp.SendAsync("Runtime.enable", sessionId: sessionId, cancellationToken: cancellationToken); }
        catch
        {
            _sessions.TryRemove(sessionId, out _);
            _targets.TryRemove(target.TargetId, out _);
            try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId }, cancellationToken: cancellationToken); } catch { }
            throw;
        }
        try { await _cdp.SendAsync("Log.enable", sessionId: sessionId, cancellationToken: cancellationToken); } catch { }
        _cognition[target.TargetId] = new CognitionRecord(CognitionStates.Hot, DateTimeOffset.UtcNow, 0, 0);
        return state;
    }

    public async Task<object> DebugTargetAttachAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        var state = await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        return new
        {
            target = target.Id,
            target.Type,
            target.Url,
            sessionId = state.SessionId,
            providers = string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase)
                ? new[] { "Page", "Runtime", "DOM", "Network", "Accessibility", "Log" }
                : new[] { "Runtime", "Log" }
        };
    }
}
