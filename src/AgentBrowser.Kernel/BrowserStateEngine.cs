using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed class BrowserStateEngine : IAsyncDisposable
{
    private static readonly HashSet<string> InteractableRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "button", "link", "textbox", "searchbox", "checkbox", "radio", "combobox",
        "listbox", "option", "menuitem", "menuitemcheckbox", "menuitemradio", "tab",
        "switch", "slider", "spinbutton", "treeitem"
    };

    private readonly CdpClient _cdp;
    private readonly bool _apcAvailable;
    private readonly DocumentIdentityBridge _identity;
    private readonly LogicalIdStore _ids = new();
    private readonly ConcurrentDictionary<string, TargetState> _targets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TargetState> _sessions = new(StringComparer.Ordinal);
    private long _nextRequestId;
    private long _cursor;

    public BrowserStateEngine(CdpClient cdp, bool apcAvailable)
    {
        _cdp = cdp;
        _apcAvailable = apcAvailable;
        _identity = new DocumentIdentityBridge(cdp);
        _cdp.EventReceived += OnCdpEventAsync;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _cdp.SendAsync("Target.setDiscoverTargets", new { discover = true }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<BrowserTarget>> ListTargetsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _cdp.SendAsync("Target.getTargets", cancellationToken: cancellationToken);
        var list = new List<BrowserTarget>();

        foreach (var info in result.GetProperty("targetInfos").EnumerateArray())
        {
            var targetId = info.GetProperty("targetId").GetString() ?? "";
            var logicalId = _ids.TargetIdFor(targetId);
            list.Add(new BrowserTarget(
                logicalId,
                targetId,
                GetString(info, "type"),
                GetString(info, "title"),
                GetString(info, "url"),
                info.TryGetProperty("attached", out var attached) && attached.GetBoolean(),
                info.TryGetProperty("openerId", out var opener) ? opener.GetString() : null));
        }

        return list.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }

    public async Task<SemanticSurface> ObserveAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            return await CaptureSurfaceLockedAsync(state, target, cancellationToken);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<SemanticDelta> DeltaAsync(string targetReference, long since, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (!state.History.TryGetValue(since, out var before))
                throw new InvalidOperationException($"Cursor {since} is not available for {state.LogicalId}.");

            var after = await CaptureSurfaceLockedAsync(state, target, cancellationToken);
            var beforeById = before.Elements.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var afterById = after.Elements.ToDictionary(x => x.Id, StringComparer.Ordinal);

            var added = afterById.Keys.Except(beforeById.Keys, StringComparer.Ordinal)
                .Select(x => afterById[x]).ToArray();
            var removed = beforeById.Keys.Except(afterById.Keys, StringComparer.Ordinal)
                .Select(x => beforeById[x]).ToArray();
            var changed = beforeById.Keys.Intersect(afterById.Keys, StringComparer.Ordinal)
                .Where(id => !SemanticEquivalent(beforeById[id], afterById[id]))
                .Select(id => new SemanticChange(id, beforeById[id], afterById[id]))
                .ToArray();

            return new SemanticDelta(since, after.Cursor, after.Target, after.Document, added, removed, changed);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    public async Task<IReadOnlyList<SemanticElement>> QueryAsync(ElementQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Target))
            throw new ArgumentException("query.target is required in Build 001 Milestone B.");

        var surface = await ObserveAsync(query.Target, cancellationToken);
        IEnumerable<SemanticElement> items = surface.Elements;

        if (!string.IsNullOrWhiteSpace(query.Role))
            items = items.Where(x => string.Equals(x.Role, query.Role, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Name))
            items = items.Where(x => string.Equals(x.Name, query.Name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.Contains))
            items = items.Where(x =>
                x.Name.Contains(query.Contains, StringComparison.OrdinalIgnoreCase) ||
                (x.Description?.Contains(query.Contains, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Value?.Contains(query.Contains, StringComparison.OrdinalIgnoreCase) ?? false));

        return items.Take(Math.Clamp(query.Limit, 1, 500)).ToArray();
    }

    public SemanticElement Inspect(string elementId)
    {
        foreach (var target in _targets.Values)
        {
            if (target.ElementsByLogicalId.TryGetValue(elementId, out var element))
                return element;
        }

        throw new KeyNotFoundException($"Unknown semantic element '{elementId}'. Observe the target first.");
    }

    public async Task<JsonElement> EvaluateAsync(string targetReference, string expression, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        return await _cdp.SendAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = true,
            awaitPromise = true,
            userGesture = true
        }, state.SessionId, cancellationToken);
    }

    public async Task ClickAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var quads = await _cdp.SendAsync("DOM.getContentQuads", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        var firstQuad = quads.GetProperty("quads").EnumerateArray().FirstOrDefault();
        if (firstQuad.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Element {elementId} has no clickable content quad.");

        var points = firstQuad.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (points.Length < 8)
            throw new InvalidOperationException($"Element {elementId} returned an invalid content quad.");

        var x = (points[0] + points[2] + points[4] + points[6]) / 4d;
        var y = (points[1] + points[3] + points[5] + points[7]) / 4d;

        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", buttons = 1, clickCount = 1 }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", buttons = 0, clickCount = 1 }, state.SessionId, cancellationToken);
    }

    public async Task TypeAsync(string elementId, string text, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        await _cdp.SendAsync("DOM.focus", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.insertText", new { text }, state.SessionId, cancellationToken);
    }

    public async Task FillAsync(string elementId, string text, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        await _cdp.SendAsync("DOM.focus", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        var resolved = await _cdp.SendAsync("DOM.resolveNode", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        var objectId = resolved.GetProperty("object").GetProperty("objectId").GetString()
            ?? throw new InvalidOperationException($"Unable to resolve {elementId} to a runtime object.");

        const string functionDeclaration = "function(value){const el=this;const p=Object.getPrototypeOf(el);const d=p&&Object.getOwnPropertyDescriptor(p,'value');if(d&&d.set){d.set.call(el,value);}else if('value' in el){el.value=value;}else{el.textContent=value;}el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));}";
        await _cdp.SendAsync("Runtime.callFunctionOn", new
        {
            objectId,
            functionDeclaration,
            arguments = new[] { new { value = text } },
            returnByValue = true,
            userGesture = true
        }, state.SessionId, cancellationToken);
    }

    public async Task KeyAsync(string targetReference, string key, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var (code, vk, text) = KeyMetadata(key);
        var keyDown = new Dictionary<string, object?>
        {
            ["type"] = "keyDown",
            ["key"] = key,
            ["code"] = code,
            ["windowsVirtualKeyCode"] = vk,
            ["nativeVirtualKeyCode"] = vk
        };
        if (text is not null)
            keyDown["text"] = text;
        await _cdp.SendAsync("Input.dispatchKeyEvent", keyDown, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchKeyEvent", new
        {
            type = "keyUp",
            key,
            code,
            windowsVirtualKeyCode = vk,
            nativeVirtualKeyCode = vk
        }, state.SessionId, cancellationToken);
    }

    public async Task<IReadOnlyList<NetworkRequestSummary>> NetworkSearchAsync(
        string targetReference,
        string? contains = null,
        string? method = null,
        int? status = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        IEnumerable<NetworkEntry> entries = state.NetworkByLogicalId.Values;

        if (!string.IsNullOrWhiteSpace(contains))
            entries = entries.Where(x => x.Url.Contains(contains, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(method))
            entries = entries.Where(x => string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase));
        if (status is not null)
            entries = entries.Where(x => x.Status == status);

        return entries
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(x => NetworkSnapshot(state, x))
            .ToArray();
    }

    public async Task<NetworkBody> NetworkBodyAsync(
        string logicalRequestId,
        CancellationToken cancellationToken = default)
    {
        foreach (var state in _targets.Values)
        {
            if (!state.NetworkByLogicalId.TryGetValue(logicalRequestId, out var entry))
                continue;

            var result = await _cdp.SendAsync(
                "Network.getResponseBody",
                new { requestId = entry.RawRequestId },
                state.SessionId,
                cancellationToken);
            return new NetworkBody(
                entry.Id,
                entry.RawRequestId,
                result.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "",
                result.TryGetProperty("base64Encoded", out var encoded) && encoded.GetBoolean());
        }

        throw new KeyNotFoundException($"Unknown network request '{logicalRequestId}'.");
    }
    public async Task<bool> WaitUntilAsync(
        string targetReference,
        string expression,
        int timeoutMs = 5000,
        int intervalMs = 100,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        timeoutMs = Math.Clamp(timeoutMs, 1, 300_000);
        intervalMs = Math.Clamp(intervalMs, 25, 10_000);
        var predicateJson = JsonSerializer.Serialize(expression);
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingMs = Math.Max(1, (int)(deadline - DateTimeOffset.UtcNow).TotalMilliseconds);
            var source = "new Promise((resolve)=>{" +
                "const source=" + predicateJson + ";" +
                "let finished=false,observer=null,interval=null,timeout=null;" +
                "const cleanup=()=>{if(observer)observer.disconnect();if(interval)clearInterval(interval);if(timeout)clearTimeout(timeout);};" +
                "const finish=(value)=>{if(finished)return;finished=true;cleanup();resolve(value);};" +
                "const check=()=>{try{if(Boolean((0,eval)(source)))finish(true);}catch{}};" +
                "try{observer=new MutationObserver(check);observer.observe(document.documentElement||document,{subtree:true,childList:true,attributes:true,characterData:true});}catch{}" +
                $"interval=setInterval(check,{intervalMs});" +
                $"timeout=setTimeout(()=>finish(false),{remainingMs});" +
                "check();" +
                "})";

            try
            {
                var result = await _cdp.SendAsync("Runtime.evaluate", new
                {
                    expression = source,
                    returnByValue = true,
                    awaitPromise = true,
                    userGesture = false
                }, state.SessionId, cancellationToken);

                return result.TryGetProperty("result", out var remote) &&
                       remote.TryGetProperty("value", out var value) &&
                       value.ValueKind is JsonValueKind.True;
            }
            catch (CdpException ex) when (
                ex.Message.Contains("Execution context was destroyed", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("default execution context", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("navigated or closed", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTimeOffset.UtcNow >= deadline)
                    return false;
                await Task.Delay(Math.Min(intervalMs, 100), cancellationToken);
            }
        }

        return false;
    }

    public async Task ScrollAsync(string targetReference, double deltaX, double deltaY, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new
        {
            type = "mouseWheel",
            x = 1,
            y = 1,
            deltaX,
            deltaY
        }, state.SessionId, cancellationToken);
    }

    private async Task<SemanticSurface> CaptureSurfaceLockedAsync(TargetState state, BrowserTarget target, CancellationToken cancellationToken)
    {
        var frameTree = await _cdp.SendAsync("Page.getFrameTree", sessionId: state.SessionId, cancellationToken: cancellationToken);
        var frame = frameTree.GetProperty("frameTree").GetProperty("frame");
        var frameId = GetString(frame, "id");
        var loaderId = frame.TryGetProperty("loaderId", out var loader) ? loader.GetString() : null;
        var url = GetString(frame, "url");
        var bridgeDocument = await _identity.ReadDocumentStateAsync(state.SessionId, cancellationToken);
        var documentKey = bridgeDocument?.DocumentToken ??
            (!string.IsNullOrWhiteSpace(loaderId) ? loaderId! : $"{frameId}|{url}");

        if (!string.Equals(state.DocumentKey, documentKey, StringComparison.Ordinal))
        {
            state.DocumentKey = documentKey;
            if (!string.IsNullOrWhiteSpace(bridgeDocument?.DocumentLogicalId))
            {
                state.DocumentLogicalId = bridgeDocument!.DocumentLogicalId!;
                _ids.ObserveExisting(state.DocumentLogicalId);
            }
            else
            {
                state.DocumentLogicalId = _ids.NewDocumentId();
                if (bridgeDocument is not null)
                    await _identity.SetDocumentLogicalIdAsync(state.SessionId, state.DocumentLogicalId, cancellationToken);
            }
            state.BackendToLogicalId.Clear();
            state.ElementsByLogicalId.Clear();
            state.History.Clear();
        }

        var ax = await _cdp.SendAsync("Accessibility.getFullAXTree", sessionId: state.SessionId, cancellationToken: cancellationToken);
        var axNodes = ax.GetProperty("nodes").EnumerateArray().ToArray();

        int snapshotDocuments = 0;
        int snapshotNodes = 0;
        try
        {
            var snapshot = await _cdp.SendAsync("DOMSnapshot.captureSnapshot", new
            {
                computedStyles = Array.Empty<string>(),
                includePaintOrder = false,
                includeDOMRects = false
            }, state.SessionId, cancellationToken);
            if (snapshot.TryGetProperty("documents", out var documents))
            {
                snapshotDocuments = documents.GetArrayLength();
                foreach (var document in documents.EnumerateArray())
                {
                    if (document.TryGetProperty("nodes", out var nodes) && nodes.TryGetProperty("backendNodeId", out var backendIds))
                        snapshotNodes += backendIds.GetArrayLength();
                }
            }
        }
        catch (CdpException)
        {
            // DOMSnapshot is a provider, not a prerequisite for an AX semantic surface.
        }

        var elements = new List<SemanticElement>();
        foreach (var node in axNodes)
        {
            if (node.TryGetProperty("ignored", out var ignored) && ignored.GetBoolean())
                continue;
            if (!node.TryGetProperty("backendDOMNodeId", out var backendElement) || !backendElement.TryGetInt32(out var backendNodeId))
                continue;

            var role = AxValue(node, "role");
            if (string.IsNullOrWhiteSpace(role) || !InteractableRoles.Contains(role))
                continue;

            var name = AxValue(node, "name");
            var description = NullIfEmpty(AxValue(node, "description"));
            var value = NullIfEmpty(AxValue(node, "value"));
            var disabled = AxPropertyBool(node, "disabled");
            var focused = AxPropertyBool(node, "focused");
            if (!state.BackendToLogicalId.TryGetValue(backendNodeId, out var elementId))
            {
                var proposedId = _ids.NewElementId();
                elementId = await _identity.GetOrBindLogicalIdAsync(state.SessionId, backendNodeId, proposedId, cancellationToken)
                    ?? proposedId;
                _ids.ObserveExisting(elementId);
                state.BackendToLogicalId[backendNodeId] = elementId;
            }
            var actions = ActionsForRole(role, disabled);

            var semantic = new SemanticElement(
                elementId,
                state.LogicalId,
                state.DocumentLogicalId,
                backendNodeId,
                node.TryGetProperty("nodeId", out var axId) ? axId.GetString() : null,
                role,
                name,
                description,
                value,
                disabled,
                focused,
                actions);
            elements.Add(semantic);
            state.ElementsByLogicalId[elementId] = semantic;
        }

        var currentIds = elements.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in state.ElementsByLogicalId.Keys.ToArray())
        {
            if (!currentIds.Contains(id))
                state.ElementsByLogicalId.TryRemove(id, out _);
        }

        var cursor = Interlocked.Increment(ref _cursor);
        var surface = new SemanticSurface(
            cursor,
            state.LogicalId,
            target.TargetId,
            state.DocumentLogicalId,
            frameId,
            loaderId,
            url,
            target.Title,
            DateTimeOffset.UtcNow,
            new ProviderStats(axNodes.Length, elements.Count, snapshotDocuments, snapshotNodes, _apcAvailable),
            elements.OrderBy(x => NumericSuffix(x.Id)).ToArray());

        state.History[cursor] = surface;
        while (state.History.Count > 32)
            state.History.TryRemove(state.History.Keys.Min(), out _);

        return surface;
    }

    private async Task<BrowserTarget> ResolveTargetAsync(string reference, CancellationToken cancellationToken)
    {
        var targets = await ListTargetsAsync(cancellationToken);
        var target = targets.FirstOrDefault(x =>
            string.Equals(x.Id, reference, StringComparison.Ordinal) ||
            string.Equals(x.TargetId, reference, StringComparison.Ordinal));
        if (target is null)
            throw new KeyNotFoundException($"Unknown target '{reference}'.");
        if (!string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Target {reference} is type '{target.Type}', not a page.");
        return target;
    }

    private async Task<TargetState> EnsureTargetStateAsync(BrowserTarget target, CancellationToken cancellationToken)
    {
        if (_targets.TryGetValue(target.TargetId, out var existing))
            return existing;

        var attach = await _cdp.SendAsync("Target.attachToTarget", new { targetId = target.TargetId, flatten = true }, cancellationToken: cancellationToken);
        var sessionId = attach.GetProperty("sessionId").GetString()
            ?? throw new InvalidOperationException("Target.attachToTarget did not return a sessionId.");

        var created = new TargetState(target.Id, target.TargetId, sessionId);
        var state = _targets.GetOrAdd(target.TargetId, created);
        if (!ReferenceEquals(state, created))
        {
            try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId }, cancellationToken: cancellationToken); } catch { }
            return state;
        }

        _sessions[sessionId] = state;
        _identity.TrackSession(sessionId);
        await _cdp.SendAsync("Page.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await _cdp.SendAsync("Runtime.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await _cdp.SendAsync("DOM.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await _cdp.SendAsync("Network.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        await _cdp.SendAsync("Accessibility.enable", sessionId: sessionId, cancellationToken: cancellationToken);
        return state;
    }

    private (TargetState State, SemanticElement Element) ResolveElement(string elementId)
    {
        foreach (var state in _targets.Values)
        {
            if (state.ElementsByLogicalId.TryGetValue(elementId, out var element))
                return (state, element);
        }
        throw new KeyNotFoundException($"Unknown semantic element '{elementId}'. Observe the target first.");
    }

    private Task OnCdpEventAsync(JsonElement message)
    {
        if (!message.TryGetProperty("sessionId", out var sessionElement) || sessionElement.ValueKind != JsonValueKind.String)
            return Task.CompletedTask;
        var sessionId = sessionElement.GetString();
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var state))
            return Task.CompletedTask;
        if (!message.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
            return Task.CompletedTask;
        if (!message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return Task.CompletedTask;

        var method = methodElement.GetString();
        try
        {
            if (method == "Network.requestWillBeSent")
            {
                var rawRequestId = GetString(p, "requestId");
                if (rawRequestId.Length == 0 || !p.TryGetProperty("request", out var request))
                    return Task.CompletedTask;
                var entry = GetOrCreateNetworkEntry(state, rawRequestId);
                entry.Url = GetString(request, "url");
                entry.Method = GetString(request, "method");
                entry.ResourceType = GetString(p, "type");
                entry.FrameId = GetString(p, "frameId");
                entry.LoaderId = GetString(p, "loaderId");
                if (p.TryGetProperty("initiator", out var initiator))
                    entry.InitiatorType = NullIfEmpty(GetString(initiator, "type"));
            }
            else if (method == "Network.responseReceived")
            {
                var rawRequestId = GetString(p, "requestId");
                if (rawRequestId.Length == 0 || !p.TryGetProperty("response", out var response))
                    return Task.CompletedTask;
                var entry = GetOrCreateNetworkEntry(state, rawRequestId);
                if (response.TryGetProperty("status", out var statusValue) && statusValue.TryGetDouble(out var status))
                    entry.Status = (int)status;
                entry.MimeType = NullIfEmpty(GetString(response, "mimeType"));
                if (entry.Url.Length == 0)
                    entry.Url = GetString(response, "url");
                if (entry.ResourceType.Length == 0)
                    entry.ResourceType = GetString(p, "type");
            }
            else if (method == "Network.loadingFinished")
            {
                var rawRequestId = GetString(p, "requestId");
                if (rawRequestId.Length == 0) return Task.CompletedTask;
                var entry = GetOrCreateNetworkEntry(state, rawRequestId);
                entry.Completed = true;
                entry.FinishedAtUtc = DateTimeOffset.UtcNow;
                if (p.TryGetProperty("encodedDataLength", out var length) && length.TryGetDouble(out var bytes))
                    entry.EncodedDataLength = (long)bytes;
            }
            else if (method == "Network.loadingFailed")
            {
                var rawRequestId = GetString(p, "requestId");
                if (rawRequestId.Length == 0) return Task.CompletedTask;
                var entry = GetOrCreateNetworkEntry(state, rawRequestId);
                entry.Failed = true;
                entry.FinishedAtUtc = DateTimeOffset.UtcNow;
                entry.ErrorText = NullIfEmpty(GetString(p, "errorText"));
            }
        }
        catch
        {
            // Network observation is best-effort state normalization; malformed/unknown events do not stop the CDP receive loop.
        }

        return Task.CompletedTask;
    }

    private NetworkEntry GetOrCreateNetworkEntry(TargetState state, string rawRequestId)
    {
        return state.NetworkByRawId.GetOrAdd(rawRequestId, id =>
        {
            var created = new NetworkEntry($"req_{Interlocked.Increment(ref _nextRequestId)}", id, DateTimeOffset.UtcNow);
            state.NetworkByLogicalId[created.Id] = created;
            while (state.NetworkByLogicalId.Count > 1000)
            {
                var oldest = state.NetworkByLogicalId.Values.OrderBy(x => x.StartedAtUtc).FirstOrDefault();
                if (oldest is null) break;
                state.NetworkByLogicalId.TryRemove(oldest.Id, out _);
                state.NetworkByRawId.TryRemove(oldest.RawRequestId, out _);
            }
            return created;
        });
    }

    private static NetworkRequestSummary NetworkSnapshot(TargetState state, NetworkEntry entry) => new(
        entry.Id,
        state.LogicalId,
        string.IsNullOrWhiteSpace(state.DocumentLogicalId) ? null : state.DocumentLogicalId,
        entry.RawRequestId,
        entry.Url,
        entry.Method,
        entry.ResourceType,
        entry.InitiatorType,
        entry.Status,
        entry.MimeType,
        entry.Completed,
        entry.Failed,
        entry.ErrorText,
        entry.EncodedDataLength,
        entry.StartedAtUtc,
        entry.FinishedAtUtc);
    private static string AxValue(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
            return "";
        if (!value.TryGetProperty("value", out var raw))
            return "";
        return raw.ValueKind switch
        {
            JsonValueKind.String => raw.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => raw.ToString(),
            _ => raw.ToString()
        };
    }

    private static bool AxPropertyBool(JsonElement node, string name)
    {
        if (!node.TryGetProperty("properties", out var properties))
            return false;
        foreach (var property in properties.EnumerateArray())
        {
            if (!string.Equals(GetString(property, "name"), name, StringComparison.Ordinal))
                continue;
            if (property.TryGetProperty("value", out var value) && value.TryGetProperty("value", out var raw))
            {
                if (raw.ValueKind == JsonValueKind.True) return true;
                if (raw.ValueKind == JsonValueKind.False) return false;
            }
        }
        return false;
    }

    private static IReadOnlyList<string> ActionsForRole(string role, bool disabled)
    {
        if (disabled) return Array.Empty<string>();
        return role.ToLowerInvariant() switch
        {
            "textbox" or "searchbox" or "spinbutton" => new[] { "click", "focus", "fill", "type", "key" },
            "slider" => new[] { "click", "focus", "key" },
            _ => new[] { "click", "focus", "key" }
        };
    }

    private static bool SemanticEquivalent(SemanticElement a, SemanticElement b) =>
        a.Role == b.Role && a.Name == b.Name && a.Description == b.Description && a.Value == b.Value &&
        a.Disabled == b.Disabled && a.Focused == b.Focused && a.Document == b.Document;

    private static (string Code, int Vk, string? Text) KeyMetadata(string key) => key switch
    {
        "Enter" => ("Enter", 13, "\r"),
        "Tab" => ("Tab", 9, null),
        "Escape" => ("Escape", 27, null),
        "Backspace" => ("Backspace", 8, null),
        "ArrowUp" => ("ArrowUp", 38, null),
        "ArrowDown" => ("ArrowDown", 40, null),
        "ArrowLeft" => ("ArrowLeft", 37, null),
        "ArrowRight" => ("ArrowRight", 39, null),
        _ when key.Length == 1 => ($"Key{char.ToUpperInvariant(key[0])}", char.ToUpperInvariant(key[0]), key),
        _ => (key, 0, null)
    };

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static long NumericSuffix(string id) =>
        long.TryParse(id.AsSpan(id.LastIndexOf('_') + 1), out var value) ? value : long.MaxValue;

    public async ValueTask DisposeAsync()
    {
        foreach (var state in _targets.Values)
        {
            try { await _cdp.SendAsync("Target.detachFromTarget", new { sessionId = state.SessionId }); } catch { }
            state.Gate.Dispose();
        }
        _cdp.EventReceived -= OnCdpEventAsync;
        _identity.Dispose();
    }

    private sealed class TargetState(string logicalId, string targetId, string sessionId)
    {
        public string LogicalId { get; } = logicalId;
        public string TargetId { get; } = targetId;
        public string SessionId { get; } = sessionId;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string DocumentKey { get; set; } = "";
        public string DocumentLogicalId { get; set; } = "";
        public ConcurrentDictionary<int, string> BackendToLogicalId { get; } = new();
        public ConcurrentDictionary<string, SemanticElement> ElementsByLogicalId { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<long, SemanticSurface> History { get; } = new();
        public ConcurrentDictionary<string, NetworkEntry> NetworkByRawId { get; } = new(StringComparer.Ordinal);
        public ConcurrentDictionary<string, NetworkEntry> NetworkByLogicalId { get; } = new(StringComparer.Ordinal);
    }

    private sealed class NetworkEntry(string id, string rawRequestId, DateTimeOffset startedAtUtc)
    {
        public string Id { get; } = id;
        public string RawRequestId { get; } = rawRequestId;
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
        public string Url { get; set; } = "";
        public string Method { get; set; } = "";
        public string ResourceType { get; set; } = "";
        public string FrameId { get; set; } = "";
        public string LoaderId { get; set; } = "";
        public string? InitiatorType { get; set; }
        public int? Status { get; set; }
        public string? MimeType { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public string? ErrorText { get; set; }
        public long? EncodedDataLength { get; set; }
        public DateTimeOffset? FinishedAtUtc { get; set; }
    }
}

