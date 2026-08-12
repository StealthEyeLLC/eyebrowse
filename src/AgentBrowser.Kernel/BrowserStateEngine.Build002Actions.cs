using System.Text.Json;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    public async Task<bool> WaitAnyAsync(
        string targetReference,
        IReadOnlyList<string> expressions,
        int timeoutMs = 5000,
        int intervalMs = 100,
        CancellationToken cancellationToken = default)
    {
        if (expressions.Count == 0) throw new ArgumentException("wait.any requires at least one expression.");
        var combined = string.Join(" || ", expressions.Select(x => $"Boolean((0,eval)({JsonSerializer.Serialize(x)}))"));
        return await WaitUntilAsync(targetReference, combined, timeoutMs, intervalMs, cancellationToken);
    }

    public async Task<bool> WaitAllAsync(
        string targetReference,
        IReadOnlyList<string> expressions,
        int timeoutMs = 5000,
        int intervalMs = 100,
        CancellationToken cancellationToken = default)
    {
        if (expressions.Count == 0) throw new ArgumentException("wait.all requires at least one expression.");
        var combined = string.Join(" && ", expressions.Select(x => $"Boolean((0,eval)({JsonSerializer.Serialize(x)}))"));
        return await WaitUntilAsync(targetReference, combined, timeoutMs, intervalMs, cancellationToken);
    }

    public async Task<IReadOnlyList<bool>> WaitSequenceAsync(
        string targetReference,
        IReadOnlyList<string> expressions,
        int timeoutMs = 5000,
        int intervalMs = 100,
        CancellationToken cancellationToken = default)
    {
        if (expressions.Count == 0) throw new ArgumentException("wait.sequence requires at least one expression.");
        timeoutMs = Math.Clamp(timeoutMs, 1, 300_000);
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        var results = new List<bool>(expressions.Count);
        foreach (var expression in expressions)
        {
            var remaining = Math.Max(1, (int)(deadline - DateTimeOffset.UtcNow).TotalMilliseconds);
            var matched = await WaitUntilAsync(targetReference, expression, remaining, intervalMs, cancellationToken);
            results.Add(matched);
            if (!matched) break;
        }
        return results;
    }

    public async Task<bool> WaitQuietForAsync(
        string targetReference,
        int quietMs,
        int timeoutMs = 5000,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        quietMs = Math.Clamp(quietMs, 1, 120_000);
        timeoutMs = Math.Clamp(timeoutMs, quietMs, 300_000);
        var script = $"new Promise(resolve=>{{let done=false,quiet=null;const finish=v=>{{if(done)return;done=true;try{{observer.disconnect();}}catch{{}};if(quiet)clearTimeout(quiet);clearTimeout(deadline);resolve(v);}};const arm=()=>{{if(quiet)clearTimeout(quiet);quiet=setTimeout(()=>finish(true),{quietMs});}};const observer=new MutationObserver(arm);try{{observer.observe(document.documentElement||document,{{subtree:true,childList:true,attributes:true,characterData:true}});}}catch{{}};const deadline=setTimeout(()=>finish(false),{timeoutMs});arm();}})";
        try
        {
            var result = await _cdp.SendAsync("Runtime.evaluate", new
            {
                expression = script,
                returnByValue = true,
                awaitPromise = true,
                userGesture = false
            }, state.SessionId, cancellationToken);
            return TryRemoteValue(result, out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (AgentBrowser.Cdp.CdpException ex) when (
            ex.Message.Contains("Execution context was destroyed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("navigated or closed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task HoverAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var (x, y) = await ElementCenterAsync(state, element, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y }, state.SessionId, cancellationToken);
    }

    public async Task DoubleClickAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var (x, y) = await ElementCenterAsync(state, element, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", buttons = 1, clickCount = 1 }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", buttons = 0, clickCount = 1 }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", buttons = 1, clickCount = 2 }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", buttons = 0, clickCount = 2 }, state.SessionId, cancellationToken);
    }

    public async Task ContextClickAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var (x, y) = await ElementCenterAsync(state, element, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseMoved", x, y }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "right", buttons = 2, clickCount = 1 }, state.SessionId, cancellationToken);
        await _cdp.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "right", buttons = 0, clickCount = 1 }, state.SessionId, cancellationToken);
    }

    public async Task FocusAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        await _cdp.SendAsync("DOM.focus", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
    }

    public async Task<JsonElement> SelectAsync(string elementId, IReadOnlyList<string> values, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var objectId = await ResolveRuntimeObjectAsync(state, element, cancellationToken);
        try
        {
            return await _cdp.SendAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = "function(values){const wanted=new Set(values.map(String));if(!(this instanceof HTMLSelectElement))throw new Error('Element is not a select');for(const option of this.options) option.selected=wanted.has(option.value)||wanted.has(option.text);this.dispatchEvent(new Event('input',{bubbles:true}));this.dispatchEvent(new Event('change',{bubbles:true}));return {value:this.value,selected:[...this.selectedOptions].map(x=>({value:x.value,text:x.text}))};}",
                arguments = new[] { new { value = values } },
                returnByValue = true,
                userGesture = true
            }, state.SessionId, cancellationToken);
        }
        finally { await ReleaseObjectAsync(state, objectId); }
    }

    public async Task<JsonElement> CheckAsync(string elementId, bool desired, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var objectId = await ResolveRuntimeObjectAsync(state, element, cancellationToken);
        try
        {
            return await _cdp.SendAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = "function(desired){if(!('checked' in this))throw new Error('Element has no checked state');if(Boolean(this.checked)!==Boolean(desired))this.click();return {checked:Boolean(this.checked)};}",
                arguments = new[] { new { value = desired } },
                returnByValue = true,
                userGesture = true
            }, state.SessionId, cancellationToken);
        }
        finally { await ReleaseObjectAsync(state, objectId); }
    }

    public async Task UploadFilesAsync(string elementId, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        if (files.Count == 0) throw new ArgumentException("file.upload requires at least one path.");
        foreach (var file in files)
            if (!File.Exists(file) && !Directory.Exists(file)) throw new FileNotFoundException("Upload material does not exist.", file);
        var (state, element) = ResolveElement(elementId);
        await _cdp.SendAsync("DOM.setFileInputFiles", new { files, backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
    }

    public async Task<ArtifactInfo> ScreenshotFullPageAsync(
        string targetReference,
        string? destination = null,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var result = await _cdp.SendAsync("Page.captureScreenshot", new { format = "png", captureBeyondViewport = true, fromSurface = true }, state.SessionId, cancellationToken);
        var bytes = Convert.FromBase64String(GetString(result, "data"));
        var path = ArtifactPath(destination, "screenshot", ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return RegisterArtifact("screenshot", path, target.Id, target.Url);
    }

    public async Task<ArtifactInfo> ScreenshotElementAsync(
        string elementId,
        string? destination = null,
        CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var model = await _cdp.SendAsync("DOM.getBoxModel", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        if (!model.TryGetProperty("model", out var box) || !box.TryGetProperty("border", out var border) || border.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Element {elementId} has no browser box model.");
        var p = border.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (p.Length < 8) throw new InvalidOperationException($"Element {elementId} returned an invalid border quad.");
        var minX = Math.Max(0, new[] { p[0], p[2], p[4], p[6] }.Min());
        var maxX = new[] { p[0], p[2], p[4], p[6] }.Max();
        var minY = Math.Max(0, new[] { p[1], p[3], p[5], p[7] }.Min());
        var maxY = new[] { p[1], p[3], p[5], p[7] }.Max();
        var result = await _cdp.SendAsync("Page.captureScreenshot", new
        {
            format = "png",
            fromSurface = true,
            clip = new { x = minX, y = minY, width = Math.Max(1, maxX - minX), height = Math.Max(1, maxY - minY), scale = 1 }
        }, state.SessionId, cancellationToken);
        var bytes = Convert.FromBase64String(GetString(result, "data"));
        var path = ArtifactPath(destination, $"element-{elementId}", ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return RegisterArtifact("screenshot", path, element.Target, elementId);
    }

    public async Task<JsonElement> PerformanceMetricsAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        try { await _cdp.SendAsync("Performance.enable", sessionId: state.SessionId, cancellationToken: cancellationToken); } catch { }
        return await _cdp.SendAsync("Performance.getMetrics", sessionId: state.SessionId, cancellationToken: cancellationToken);
    }

    private async Task<(double X, double Y)> ElementCenterAsync(TargetState state, SemanticElement element, CancellationToken cancellationToken)
    {
        try
        {
            await _cdp.SendAsync("DOM.scrollIntoViewIfNeeded", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        }
        catch (AgentBrowser.Cdp.CdpException)
        {
            var objectId = await ResolveRuntimeObjectAsync(state, element, cancellationToken);
            try
            {
                await _cdp.SendAsync("Runtime.callFunctionOn", new
                {
                    objectId,
                    functionDeclaration = "function(){this.scrollIntoView({block:'center',inline:'center'});return true;}",
                    returnByValue = true,
                    userGesture = false
                }, state.SessionId, cancellationToken);
            }
            finally
            {
                await ReleaseObjectAsync(state, objectId);
            }
        }

        var quads = await _cdp.SendAsync("DOM.getContentQuads", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        var firstQuad = quads.GetProperty("quads").EnumerateArray().FirstOrDefault();
        if (firstQuad.ValueKind != JsonValueKind.Array) throw new InvalidOperationException($"Element {element.Id} has no content quad.");
        var points = firstQuad.EnumerateArray().Select(x => x.GetDouble()).ToArray();
        if (points.Length < 8) throw new InvalidOperationException($"Element {element.Id} returned an invalid content quad.");
        return ((points[0]+points[2]+points[4]+points[6])/4d, (points[1]+points[3]+points[5]+points[7])/4d);
    }

    private async Task<string> ResolveRuntimeObjectAsync(TargetState state, SemanticElement element, CancellationToken cancellationToken)
    {
        var resolved = await _cdp.SendAsync("DOM.resolveNode", new { backendNodeId = element.BackendNodeId }, state.SessionId, cancellationToken);
        return resolved.GetProperty("object").GetProperty("objectId").GetString()
            ?? throw new InvalidOperationException($"Unable to resolve {element.Id} to a runtime object.");
    }

    private async Task ReleaseObjectAsync(TargetState state, string objectId)
    {
        try { await _cdp.SendAsync("Runtime.releaseObject", new { objectId }, state.SessionId, CancellationToken.None); } catch { }
    }

    private static string ArtifactPath(string? destination, string stem, string extension)
    {
        if (!string.IsNullOrWhiteSpace(destination)) return Path.GetFullPath(destination);
        var safe = string.Concat(stem.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
        return Path.Combine(BrowserRuntime.ArtifactRoot, $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{safe}{extension}");
    }
}