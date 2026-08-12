using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, CognitionRecord> _cognition = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DocumentLifecycleInfo> _lifecycle = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ElementIdentityResolution> _identityOutcomes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _bridgeEventSequences = new(StringComparer.Ordinal);
    private string? _lastActivatedTargetId;
    private long _rendererIncarnation;
    private long _executionRealmIncarnation;

    private async Task InitializeBuild002BrowserProvidersAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BrowserRuntime.ArtifactRoot);
        Directory.CreateDirectory(BrowserRuntime.DownloadRoot);
        Directory.CreateDirectory(BrowserRuntime.DownloadStagingRoot);
        try
        {
            await ArmDownloadBehaviorAsync(cancellationToken);
        }
        catch (CdpException)
        {
            // Download behavior is capability-detected. Browser.download* remains available through raw CDP.
        }
    }

    private async Task ArmDownloadBehaviorAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(BrowserRuntime.DownloadStagingRoot);
        await _cdp.SendAsync("Browser.setDownloadBehavior", new
        {
            behavior = "allowAndName",
            downloadPath = BrowserRuntime.DownloadStagingRoot,
            eventsEnabled = true
        }, cancellationToken: cancellationToken);
    }

    private async Task InitializeBuild002TargetProvidersAsync(TargetState state, CancellationToken cancellationToken)
    {
        try
        {
            await _cdp.SendAsync("Page.setLifecycleEventsEnabled", new { enabled = true }, state.SessionId, cancellationToken);
        }
        catch (CdpException) { }

        try { await _cdp.SendAsync("Network.configureDurableMessages", new { maxTotalBufferSize = 16 * 1024 * 1024, maxResourceBufferSize = 4 * 1024 * 1024 }, state.SessionId, cancellationToken); }
        catch (CdpException) { }

        try { await _cdp.SendAsync("Log.enable", sessionId: state.SessionId, cancellationToken: cancellationToken); }
        catch (CdpException) { }

        try { await _cdp.SendAsync("WebMCP.enable", sessionId: state.SessionId, cancellationToken: cancellationToken); }
        catch (CdpException) { }

        UpdateLifecycle(state, LifecycleStates.Active, "target-attached", javaScriptAvailable: true);
    }

    private string GetCognitionState(string targetId) =>
        _cognition.TryGetValue(targetId, out var record) ? record.State : CognitionStates.Cold;

    private string GetLifecycleState(string targetId) =>
        _lifecycle.TryGetValue(targetId, out var record) ? record.Lifecycle : LifecycleStates.Unknown;

    private void MarkTargetHot(TargetState state)
    {
        _cognition[state.TargetId] = new CognitionRecord(
            CognitionStates.Hot,
            DateTimeOffset.UtcNow,
            state.ElementsByLogicalId.Count,
            state.NetworkByLogicalId.Count);
    }

    public async Task<IReadOnlyList<TargetCognitionInfo>> ListCognitionAsync(CancellationToken cancellationToken = default)
    {
        var targets = await ListTargetsAsync(cancellationToken);
        return targets.Select(target =>
        {
            _targets.TryGetValue(target.TargetId, out var state);
            _cognition.TryGetValue(target.TargetId, out var cognition);
            return new TargetCognitionInfo(
                target.Id,
                target.TargetId,
                cognition?.State ?? CognitionStates.Cold,
                state is not null,
                cognition?.LastHotAtUtc,
                state?.ElementsByLogicalId.Count ?? cognition?.SemanticObjectCount ?? 0,
                state?.NetworkByLogicalId.Count ?? cognition?.NetworkRequestCount ?? 0);
        }).ToArray();
    }

    public async Task<TargetCognitionInfo> DemoteTargetAsync(
        string targetReference,
        string to,
        CancellationToken cancellationToken = default)
    {
        var normalized = to.ToLowerInvariant();
        if (normalized is not (CognitionStates.Warm or CognitionStates.Cold))
            throw new ArgumentException("target.demote to must be 'warm' or 'cold'.");

        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var semanticCount = 0;
        var networkCount = 0;
        DateTimeOffset? lastHot = null;
        if (_cognition.TryGetValue(target.TargetId, out var previous))
            lastHot = previous.LastHotAtUtc;

        if (_targets.TryRemove(target.TargetId, out var state))
        {
            semanticCount = state.ElementsByLogicalId.Count;
            networkCount = state.NetworkByLogicalId.Count;
            _sessions.TryRemove(state.SessionId, out _);
            try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId = state.SessionId }, cancellationToken: cancellationToken); }
            catch { }
            state.Gate.Dispose();
        }

        _cognition[target.TargetId] = new CognitionRecord(normalized, lastHot, semanticCount, networkCount);
        return new TargetCognitionInfo(target.Id, target.TargetId, normalized, false, lastHot, semanticCount, networkCount);
    }

    public async Task<object> ActivateTargetAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        await _cdp.SendAsync("Target.activateTarget", new { targetId = target.TargetId }, cancellationToken: cancellationToken);
        _lastActivatedTargetId = target.TargetId;
        return new { target = target.Id, targetId = target.TargetId, activated = true };
    }

    public async Task<object> CloseTargetAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var result = await _cdp.SendAsync("Target.closeTarget", new { targetId = target.TargetId }, cancellationToken: cancellationToken);
        var success = !result.TryGetProperty("success", out var successValue) || successValue.GetBoolean();
        if (success)
        {
            _lifecycle[target.TargetId] = new DocumentLifecycleInfo(
                target.Id, null, LifecycleStates.Unavailable, null, null, null,
                Interlocked.Read(ref _rendererIncarnation), Interlocked.Read(ref _executionRealmIncarnation),
                false, DateTimeOffset.UtcNow, "target-closed");
        }
        return new { target = target.Id, targetId = target.TargetId, success };
    }

    public async Task<DocumentLifecycleInfo> LifecycleStatusAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        if (_lifecycle.TryGetValue(target.TargetId, out var info))
            return info;
        return new DocumentLifecycleInfo(
            target.Id, null, LifecycleStates.Unknown, null, null, null,
            Interlocked.Read(ref _rendererIncarnation), Interlocked.Read(ref _executionRealmIncarnation),
            false, DateTimeOffset.UtcNow, "target-not-materialized");
    }

    public async Task<BrowserCurrentContext> CurrentContextAsync(CancellationToken cancellationToken = default)
    {
        var targets = await ListTargetsAsync(cancellationToken);
        var pages = targets.Where(x => string.Equals(x.Type, "page", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (pages.Length == 0)
            return new BrowserCurrentContext(null, null, LifecycleStates.Unavailable, "", "", "", null, null, null, Array.Empty<string>(), Array.Empty<string>(), true, "No page target exists.");

        JsonElement? extensionContext = await TryReadExtensionCurrentContextAsync(cancellationToken);
        BrowserTarget? current = null;
        var extensionDiscarded = false;
        var extensionFrozen = false;

        if (extensionContext is { } ec && ec.ValueKind == JsonValueKind.Object)
        {
            var rawTargetId = GetString(ec, "targetId");
            if (!string.IsNullOrWhiteSpace(rawTargetId))
                current = pages.FirstOrDefault(x => string.Equals(x.TargetId, rawTargetId, StringComparison.Ordinal));
            extensionDiscarded = ec.TryGetProperty("discarded", out var discarded) && discarded.ValueKind == JsonValueKind.True;
            extensionFrozen = ec.TryGetProperty("frozen", out var frozen) && frozen.ValueKind == JsonValueKind.True;
        }

        if (current is null && !string.IsNullOrWhiteSpace(_lastActivatedTargetId))
            current = pages.FirstOrDefault(x => string.Equals(x.TargetId, _lastActivatedTargetId, StringComparison.Ordinal));

        if (current is null)
        {
            var visible = new List<BrowserTarget>();
            foreach (var page in pages)
            {
                if (!_targets.TryGetValue(page.TargetId, out var state))
                    continue;
                try
                {
                    var result = await _cdp.SendAsync("Runtime.evaluate", new
                    {
                        expression = "document.visibilityState === 'visible' && document.hasFocus()",
                        returnByValue = true
                    }, state.SessionId, cancellationToken);
                    if (TryRemoteValue(result, out var value) && value.ValueKind == JsonValueKind.True)
                        visible.Add(page);
                }
                catch { }
            }
            if (visible.Count == 1)
                current = visible[0];
        }

        if (current is null && pages.Length == 1)
            current = pages[0];

        if (current is null)
            return new BrowserCurrentContext(null, null, LifecycleStates.Unknown, "", "", "", null, null, null, Array.Empty<string>(), Array.Empty<string>(), true, "Current tab is ambiguous; the extension active-tab provider was unavailable or did not yield an exact target.");

        var lifecycle = extensionDiscarded ? LifecycleStates.Discarded : extensionFrozen ? LifecycleStates.Frozen : GetLifecycleState(current.TargetId);
        if (lifecycle == LifecycleStates.Unknown)
            lifecycle = LifecycleStates.Active;

        if (extensionDiscarded || extensionFrozen)
        {
            _lifecycle[current.TargetId] = new DocumentLifecycleInfo(
                current.Id,
                _lifecycle.TryGetValue(current.TargetId, out var old) ? old.Document : null,
                lifecycle,
                old?.FrameId,
                old?.LoaderId,
                old?.DocumentToken,
                old?.RendererIncarnation ?? Interlocked.Read(ref _rendererIncarnation),
                old?.ExecutionRealmIncarnation ?? Interlocked.Read(ref _executionRealmIncarnation),
                false,
                DateTimeOffset.UtcNow,
                extensionDiscarded ? "chrome.tabs.discarded" : "chrome.tabs.frozen");

            return new BrowserCurrentContext(
                current.Id,
                old?.Document,
                lifecycle,
                current.Url,
                Origin(current.Url),
                current.Title,
                null,
                null,
                null,
                Array.Empty<string>(),
                AvailableProviders(current.TargetId, false),
                false,
                null);
        }

        SemanticSurface surface;
        try
        {
            surface = await ObserveAsync(current.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            return new BrowserCurrentContext(
                current.Id,
                _lifecycle.TryGetValue(current.TargetId, out var old) ? old.Document : null,
                GetLifecycleState(current.TargetId),
                current.Url,
                Origin(current.Url),
                current.Title,
                null,
                null,
                null,
                Array.Empty<string>(),
                AvailableProviders(current.TargetId, false),
                false,
                $"Current target resolved, but compact semantic refresh failed: {ex.Message}");
        }

        string? canonical = null;
        try
        {
            var result = await EvaluateAsync(current.Id, "document.querySelector('link[rel=canonical]')?.href ?? location.href", cancellationToken);
            if (TryRemoteValue(result, out var value) && value.ValueKind == JsonValueKind.String)
                canonical = value.GetString();
        }
        catch { }

        var focus = surface.Elements.FirstOrDefault(x => x.Focused)?.Id;
        return new BrowserCurrentContext(
            current.Id,
            surface.Document,
            GetLifecycleState(current.TargetId) is var live && live != LifecycleStates.Unknown ? live : LifecycleStates.Active,
            surface.Url,
            Origin(surface.Url),
            current.Title,
            canonical ?? surface.Url,
            focus,
            null,
            Array.Empty<string>(),
            AvailableProviders(current.TargetId, true),
            false,
            null);
    }

    private IReadOnlyList<string> AvailableProviders(string targetId, bool attached)
    {
        var providers = new List<string> { "target" };
        if (attached)
            providers.AddRange(["runtime", "dom", "accessibility", "network", "agent-bridge"]);
        if (_apcAvailable) providers.Add("apc");
        if (HasWebMcpTools(targetId)) providers.Add("webmcp");
        providers.Add("raw-cdp");
        return providers;
    }

    private async Task<JsonElement?> TryReadExtensionCurrentContextAsync(CancellationToken cancellationToken)
    {
        JsonElement targets;
        try { targets = await _cdp.SendAsync("Target.getTargets", cancellationToken: cancellationToken); }
        catch { return null; }

        foreach (var info in targets.GetProperty("targetInfos").EnumerateArray())
        {
            if (!string.Equals(GetString(info, "type"), "service_worker", StringComparison.OrdinalIgnoreCase))
                continue;
            var url = GetString(info, "url");
            if (!url.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) || !url.EndsWith("/service-worker.js", StringComparison.OrdinalIgnoreCase))
                continue;

            var targetId = GetString(info, "targetId");
            if (string.IsNullOrWhiteSpace(targetId)) continue;
            string? sessionId = null;
            try
            {
                var attach = await _cdp.SendAsync("Target.attachToTarget", new { targetId, flatten = true }, cancellationToken: cancellationToken);
                sessionId = GetString(attach, "sessionId");
                if (string.IsNullOrWhiteSpace(sessionId)) continue;
                await _cdp.SendAsync("Runtime.enable", sessionId: sessionId, cancellationToken: cancellationToken);
                var probe = await _cdp.SendAsync("Runtime.evaluate", new
                {
                    expression = "Number(globalThis.__eyebrowseExtensionBridge?.version ?? 0) >= 2",
                    returnByValue = true
                }, sessionId, cancellationToken);
                if (!TryRemoteValue(probe, out var probeValue) || probeValue.ValueKind != JsonValueKind.True)
                    continue;

                var result = await _cdp.SendAsync("Runtime.evaluate", new
                {
                    expression = "globalThis.__eyebrowseExtensionBridge.currentContext()",
                    returnByValue = true,
                    awaitPromise = true
                }, sessionId, cancellationToken);
                return TryRemoteValue(result, out var value) && value.ValueKind == JsonValueKind.Object ? value.Clone() : null;
            }
            catch { }
            finally
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId }, cancellationToken: CancellationToken.None); }
                    catch { }
                }
            }
        }
        return null;
    }

    private void HandleBuild002Event(JsonElement message)
    {
        RecordSubscriptionEvent(message);
        try
        {
            if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
                return;
            var method = methodValue.GetString() ?? "";
            var hasParams = message.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object;

            if (method == "Target.targetDestroyed" && hasParams)
            {
                var targetId = GetString(p, "targetId");
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    var logical = _ids.TargetIdFor(targetId);
                    _lifecycle[targetId] = new DocumentLifecycleInfo(
                        logical, null, LifecycleStates.Unavailable, null, null, null,
                        Interlocked.Increment(ref _rendererIncarnation), Interlocked.Read(ref _executionRealmIncarnation),
                        false, DateTimeOffset.UtcNow, "Target.targetDestroyed");
                }
            }

            if (message.TryGetProperty("sessionId", out var sessionValue) && sessionValue.ValueKind == JsonValueKind.String &&
                _sessions.TryGetValue(sessionValue.GetString() ?? "", out var state))
            {
                if (method == "Runtime.executionContextCreated")
                    UpdateLifecycle(state, GetLifecycleState(state.TargetId) is var current && current != LifecycleStates.Unknown ? current : LifecycleStates.Active, "Runtime.executionContextCreated", javaScriptAvailable: true, realmBump: true);
                else if (method == "Runtime.executionContextsCleared")
                    UpdateLifecycle(state, GetLifecycleState(state.TargetId), "Runtime.executionContextsCleared", javaScriptAvailable: false, realmBump: true);
                else if (method == "Inspector.targetCrashed")
                    UpdateLifecycle(state, LifecycleStates.Unavailable, "Inspector.targetCrashed", javaScriptAvailable: false, rendererBump: true, realmBump: true);
                else if (method == "Page.navigatedWithinDocument")
                    UpdateLifecycle(state, LifecycleStates.Active, "Page.navigatedWithinDocument", javaScriptAvailable: true);
                else if (method == "Page.frameNavigated" && hasParams)
                {
                    var frame = p.TryGetProperty("frame", out var frameValue) ? frameValue : default;
                    var loaderId = frame.ValueKind == JsonValueKind.Object ? NullIfEmpty(GetString(frame, "loaderId")) : null;
                    var frameId = frame.ValueKind == JsonValueKind.Object ? NullIfEmpty(GetString(frame, "id")) : null;
                    UpdateLifecycle(state, LifecycleStates.Active, "Page.frameNavigated", javaScriptAvailable: true, frameId: frameId, loaderId: loaderId);
                }
                else if (method == "Page.lifecycleEvent" && hasParams)
                {
                    var name = GetString(p, "name");
                    if (name is "init" or "DOMContentLoaded" or "load")
                        UpdateLifecycle(state, LifecycleStates.Active, $"Page.lifecycleEvent:{name}", javaScriptAvailable: true, frameId: NullIfEmpty(GetString(p, "frameId")), loaderId: NullIfEmpty(GetString(p, "loaderId")));
                }
            }

            HandleDevToolsBasicEvent(message);
            HandlePerformanceMemoryEvent(message);
            HandleExtensionsDebuggerEvent(message);
            HandleAccessibilityScreencastEvent(message);
            HandleNetworkDepthEvent(message);
            HandleBuild002ProviderEvent(message);
        }
        catch
        {
            // Build 002 observers normalize provider state best-effort and never break the CDP receive loop.
        }
    }

    private void UpdateLifecycle(
        TargetState state,
        string lifecycle,
        string reason,
        bool javaScriptAvailable,
        bool rendererBump = false,
        bool realmBump = false,
        string? frameId = null,
        string? loaderId = null,
        string? documentToken = null)
    {
        _lifecycle.TryGetValue(state.TargetId, out var old);
        var renderer = rendererBump ? Interlocked.Increment(ref _rendererIncarnation) : old?.RendererIncarnation ?? Interlocked.Read(ref _rendererIncarnation);
        var realm = realmBump ? Interlocked.Increment(ref _executionRealmIncarnation) : old?.ExecutionRealmIncarnation ?? Interlocked.Read(ref _executionRealmIncarnation);
        _lifecycle[state.TargetId] = new DocumentLifecycleInfo(
            state.LogicalId,
            string.IsNullOrWhiteSpace(state.DocumentLogicalId) ? old?.Document : state.DocumentLogicalId,
            string.IsNullOrWhiteSpace(lifecycle) ? old?.Lifecycle ?? LifecycleStates.Unknown : lifecycle,
            frameId ?? old?.FrameId,
            loaderId ?? old?.LoaderId,
            documentToken ?? old?.DocumentToken,
            renderer,
            realm,
            javaScriptAvailable,
            DateTimeOffset.UtcNow,
            reason);
    }

    private async Task RefreshBridgeLifecycleAsync(TargetState state, BridgeDocumentState? bridge, CancellationToken cancellationToken)
    {
        if (bridge is null) return;
        var since = _bridgeEventSequences.TryGetValue(state.TargetId, out var sequence) ? sequence : 0;
        var events = await _identity.ReadEventsAsync(state.SessionId, since, cancellationToken);
        if (events is null || events.Value.ValueKind != JsonValueKind.Object) return;
        if (events.Value.TryGetProperty("sequence", out var sequenceValue) && sequenceValue.TryGetInt64(out var newSequence))
            _bridgeEventSequences[state.TargetId] = newSequence;
        if (!events.Value.TryGetProperty("events", out var array) || array.ValueKind != JsonValueKind.Array) return;

        foreach (var e in array.EnumerateArray())
        {
            var kind = GetString(e, "kind");
            switch (kind)
            {
                case "bfcache-enter":
                    UpdateLifecycle(state, LifecycleStates.Cached, kind, javaScriptAvailable: false, documentToken: bridge.DocumentToken);
                    break;
                case "bfcache-restore":
                    UpdateLifecycle(state, LifecycleStates.Active, kind, javaScriptAvailable: true, documentToken: bridge.DocumentToken);
                    break;
                case "freeze":
                    UpdateLifecycle(state, LifecycleStates.Frozen, kind, javaScriptAvailable: false, documentToken: bridge.DocumentToken);
                    break;
                case "resume":
                case "prerender-activate":
                    UpdateLifecycle(state, LifecycleStates.Active, kind, javaScriptAvailable: true, documentToken: bridge.DocumentToken);
                    break;
                case "bridge-ready":
                    if (e.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
                        data.TryGetProperty("prerendering", out var prerendering) && prerendering.ValueKind == JsonValueKind.True)
                        UpdateLifecycle(state, LifecycleStates.Prerender, kind, javaScriptAvailable: true, documentToken: bridge.DocumentToken);
                    else
                        UpdateLifecycle(state, LifecycleStates.Active, kind, javaScriptAvailable: true, documentToken: bridge.DocumentToken);
                    break;
            }
        }
    }

    public ElementIdentityResolution IdentityStatus(string id)
    {
        foreach (var state in _targets.Values)
        {
            if (state.ElementsByLogicalId.TryGetValue(id, out var element))
                return new ElementIdentityResolution(id, element.Identity, element.Incarnation, element.Target, element.Document, element.BackendNodeId, null, "Current live binding.");
        }
        if (_identityOutcomes.TryGetValue(id, out var outcome)) return outcome;
        return new ElementIdentityResolution(id, IdentityOutcomes.Stale, 1, null, null, null, null, "No current or historical binding is known in this kernel incarnation.");
    }

    private void RegisterLiveIdentity(SemanticElement element)
    {
        _identityOutcomes[element.Id] = new ElementIdentityResolution(
            element.Id, element.Identity, element.Incarnation, element.Target, element.Document, element.BackendNodeId, null,
            element.Identity == IdentityOutcomes.Rebound ? "Concept preserved through conservative semantic rebind." : "Exact current binding.");
    }

    private void RegisterRemovedIdentity(SemanticElement previous, IReadOnlyList<SemanticElement> current)
    {
        var plausible = current
            .Where(x => string.Equals(x.Role, previous.Role, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(previous.Name) &&
                        string.Equals(x.Name, previous.Name, StringComparison.Ordinal))
            .Select(x => x.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var outcome = plausible.Length > 1 ? IdentityOutcomes.Ambiguous : IdentityOutcomes.Stale;
        _identityOutcomes[previous.Id] = new ElementIdentityResolution(
            previous.Id, outcome, previous.Incarnation, previous.Target, previous.Document, null,
            plausible.Length > 0 ? plausible : null,
            outcome == IdentityOutcomes.Ambiguous ? "Multiple plausible successors remain." : "The prior browser node no longer has sufficient successor evidence.");
    }

    private async Task<IReadOnlyDictionary<string, string>> GetIdentityAttributesAsync(TargetState state, int backendNodeId, CancellationToken cancellationToken)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var result = await _cdp.SendAsync("DOM.describeNode", new { backendNodeId, depth = 0, pierce = true }, state.SessionId, cancellationToken);
            if (!result.TryGetProperty("node", out var node) || !node.TryGetProperty("attributes", out var array) || array.ValueKind != JsonValueKind.Array)
                return attributes;
            var values = array.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
            for (var i = 0; i + 1 < values.Length; i += 2)
                attributes[values[i]] = values[i + 1];
        }
        catch { }
        return attributes;
    }

    private static RebindingDecision DecideRebind(
        IEnumerable<SemanticElement> previousElements,
        RebindingCandidate current,
        IEnumerable<RebindingCandidate> currentPeers,
        IReadOnlySet<int> currentBackendNodeIds)
    {
        var previous = previousElements.Select(x => new RebindingCandidate(
            x.Id, x.Incarnation, x.BackendNodeId, x.Role, x.Name, x.Description, x.Value, x.IdentityAttributes)).ToArray();
        return ConservativeRebinding.ResolveAgainstSurface(current, previous, currentPeers, currentBackendNodeIds);
    }

    public async Task<JsonElement> RawCdpAsync(
        string method,
        JsonElement? parameters = null,
        string? targetReference = null,
        CancellationToken cancellationToken = default)
    {
        object? payload = parameters?.Clone();
        if (string.IsNullOrWhiteSpace(targetReference))
            return await _cdp.SendAsync(method, payload, cancellationToken: cancellationToken);
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        var state = await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        return await _cdp.SendAsync(method, payload, state.SessionId, cancellationToken);
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

    private static string Origin(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Authority);
        return "";
    }

    private sealed record CognitionRecord(string State, DateTimeOffset? LastHotAtUtc, int SemanticObjectCount, int NetworkRequestCount);
}
