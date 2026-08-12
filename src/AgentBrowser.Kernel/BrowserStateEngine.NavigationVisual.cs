using System.Text.Json;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    public async Task<object> NavigateGoAsync(string targetReference, string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) throw new ArgumentException("navigate.go requires an absolute URL.", nameof(url));
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var result = await _cdp.SendAsync("Page.navigate", new { url }, state.SessionId, cancellationToken);
        return new
        {
            target = target.Id,
            url,
            frameId = NullIfEmpty(GetString(result, "frameId")),
            loaderId = NullIfEmpty(GetString(result, "loaderId")),
            errorText = NullIfEmpty(GetString(result, "errorText"))
        };
    }

    public async Task<object> NavigateReloadAsync(string targetReference, bool ignoreCache = false, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("Page.reload", new { ignoreCache }, state.SessionId, cancellationToken);
        return new { target = target.Id, ignoreCache, requested = true };
    }

    public async Task<object> NavigateHistoryAsync(string targetReference, int delta, CancellationToken cancellationToken = default)
    {
        if (delta is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(delta), "History delta must be -1 or 1.");
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var history = await _cdp.SendAsync("Page.getNavigationHistory", sessionId: state.SessionId, cancellationToken: cancellationToken);
        var currentIndex = history.TryGetProperty("currentIndex", out var currentValue) && currentValue.TryGetInt32(out var current) ? current : -1;
        if (!history.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Chrome returned no navigation history entries.");
        var values = entries.EnumerateArray().ToArray();
        var next = currentIndex + delta;
        if (next < 0 || next >= values.Length)
            return new { target = target.Id, requested = false, reason = delta < 0 ? "no-back-entry" : "no-forward-entry" };
        var entryId = values[next].GetProperty("id").GetInt32();
        var url = GetString(values[next], "url");
        await _cdp.SendAsync("Page.navigateToHistoryEntry", new { entryId }, state.SessionId, cancellationToken);
        return new { target = target.Id, requested = true, direction = delta < 0 ? "back" : "forward", entryId, url };
    }

    public async Task<ArtifactInfo> ScreenshotRegionAsync(
        string targetReference,
        double x,
        double y,
        double width,
        double height,
        string? destination = null,
        CancellationToken cancellationToken = default)
    {
        if (x < 0 || y < 0 || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Screenshot region must be non-negative with positive dimensions.");
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var result = await _cdp.SendAsync("Page.captureScreenshot", new
        {
            format = "png",
            fromSurface = true,
            captureBeyondViewport = true,
            clip = new { x, y, width, height, scale = 1 }
        }, state.SessionId, cancellationToken);
        var bytes = Convert.FromBase64String(GetString(result, "data"));
        var path = ArtifactPath(destination, $"region-{target.Id}", ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return RegisterArtifact("screenshot", path, target.Id, $"region:{x},{y},{width},{height}");
    }
}
