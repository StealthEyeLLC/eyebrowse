using System.Collections.Concurrent;
using System.Text.Json;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, CdpSubscription> _cdpSubscriptions = new(StringComparer.Ordinal);
    private long _nextSubscriptionId;

    public async Task<object> CdpSubscribeAsync(
        IReadOnlyList<string> methods,
        string? targetReference = null,
        CancellationToken cancellationToken = default)
    {
        if (methods.Count == 0) throw new ArgumentException("cdp.subscribe requires at least one method pattern.");
        string? targetId = null;
        string? targetLogical = null;
        if (!string.IsNullOrWhiteSpace(targetReference))
        {
            var target = await ResolveTargetAsync(targetReference, cancellationToken);
            targetId = target.TargetId;
            targetLogical = target.Id;
        }
        var id = $"sub_{Interlocked.Increment(ref _nextSubscriptionId)}";
        _cdpSubscriptions[id] = new CdpSubscription(id, methods.Distinct(StringComparer.Ordinal).ToArray(), targetId, targetLogical);
        return new { id, methods = methods.Distinct(StringComparer.Ordinal).ToArray(), target = targetLogical, maxBufferedEvents = 256 };
    }

    public async Task<IReadOnlyList<JsonElement>> CdpNextAsync(
        string subscriptionId,
        int timeoutMs = 5000,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_cdpSubscriptions.TryGetValue(subscriptionId, out var subscription))
            throw new KeyNotFoundException($"Unknown CDP subscription '{subscriptionId}'.");
        timeoutMs = Math.Clamp(timeoutMs, 0, 300_000);
        limit = Math.Clamp(limit, 1, 256);
        if (subscription.Events.IsEmpty && timeoutMs > 0)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(timeoutMs);
            try { await subscription.Signal.WaitAsync(timeout.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }
        var result = new List<JsonElement>();
        while (result.Count < limit && subscription.Events.TryDequeue(out var message))
            result.Add(message);
        return result;
    }

    public object CdpUnsubscribe(string subscriptionId)
    {
        if (!_cdpSubscriptions.TryRemove(subscriptionId, out var subscription))
            return new { id = subscriptionId, removed = false };
        subscription.Signal.Dispose();
        return new { id = subscriptionId, removed = true };
    }

    private void RecordSubscriptionEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
            return;
        var method = methodValue.GetString() ?? "";
        string? eventTargetId = null;
        if (message.TryGetProperty("sessionId", out var sessionValue) && sessionValue.ValueKind == JsonValueKind.String &&
            _sessions.TryGetValue(sessionValue.GetString() ?? "", out var targetState))
            eventTargetId = targetState.TargetId;

        foreach (var subscription in _cdpSubscriptions.Values)
        {
            if (subscription.TargetId is not null && !string.Equals(subscription.TargetId, eventTargetId, StringComparison.Ordinal))
                continue;
            if (!subscription.Methods.Any(pattern => MethodMatches(pattern, method)))
                continue;
            subscription.Events.Enqueue(message.Clone());
            while (subscription.Events.Count > 256) subscription.Events.TryDequeue(out _);
            try { subscription.Signal.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private static bool MethodMatches(string pattern, string method)
    {
        if (pattern == "*") return true;
        if (pattern.EndsWith(".*", StringComparison.Ordinal))
            return method.StartsWith(pattern[..^1], StringComparison.Ordinal);
        return string.Equals(pattern, method, StringComparison.Ordinal);
    }

    private void DisposeBuild002Subscriptions()
    {
        foreach (var subscription in _cdpSubscriptions.Values)
            subscription.Signal.Dispose();
        _cdpSubscriptions.Clear();
    }

    private sealed class CdpSubscription(
        string id,
        IReadOnlyList<string> methods,
        string? targetId,
        string? targetLogical)
    {
        public string Id { get; } = id;
        public IReadOnlyList<string> Methods { get; } = methods;
        public string? TargetId { get; } = targetId;
        public string? TargetLogical { get; } = targetLogical;
        public ConcurrentQueue<JsonElement> Events { get; } = new();
        public SemaphoreSlim Signal { get; } = new(0, 1);
    }
}