using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, ScreencastCapture> _screencastsByTarget = new(StringComparer.Ordinal);

    private void HandleAccessibilityScreencastEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String ||
            !string.Equals(methodValue.GetString(), "Page.screencastFrame", StringComparison.Ordinal))
            return;
        if (!message.TryGetProperty("sessionId", out var outerSession) || outerSession.ValueKind != JsonValueKind.String ||
            !_sessions.TryGetValue(outerSession.GetString() ?? "", out var state) ||
            !_screencastsByTarget.TryGetValue(state.TargetId, out var capture) ||
            !message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        var ackId = p.TryGetProperty("sessionId", out var ackValue) && ackValue.TryGetInt32(out var ack) ? ack : 0;
        var data = p.TryGetProperty("data", out var dataValue) && dataValue.ValueKind == JsonValueKind.String ? dataValue.GetString() : null;
        if (!string.IsNullOrWhiteSpace(data) && capture.FrameCount < capture.MaxFrames)
        {
            try
            {
                var bytes = Convert.FromBase64String(data!);
                var framePath = Path.Combine(capture.Directory, $"frame-{capture.FrameCount + 1:D5}.{capture.Extension}");
                File.WriteAllBytes(framePath, bytes);
                capture.AddFrame(framePath, p.TryGetProperty("metadata", out var metadata) ? metadata.Clone() : null);
            }
            catch { }
        }

        if (ackId != 0)
        {
            _ = Task.Run(async () =>
            {
                try { await _cdp.SendAsync("Page.screencastFrameAck", new { sessionId = ackId }, state.SessionId, CancellationToken.None); }
                catch { }
            });
        }
    }

    public async Task<AccessibilityElementInfo> AccessibilityInspectAsync(string elementId, CancellationToken cancellationToken = default)
    {
        var (state, element) = ResolveElement(elementId);
        var result = await _cdp.SendAsync("Accessibility.getPartialAXTree", new
        {
            backendNodeId = element.BackendNodeId,
            fetchRelatives = true
        }, state.SessionId, cancellationToken);
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(element.Name) && element.Role is not ("generic" or "none" or "presentation"))
            issues.Add("Interactive/semantic object has no accessible name.");
        if (element.Disabled) issues.Add("Object is disabled.");
        return new AccessibilityElementInfo(element.Id, element.Target, element.Document, element.Role, element.Name, result, issues);
    }

    public async Task<AccessibilityAuditResult> AccessibilityAuditAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var surface = await ObserveAsync(targetReference, cancellationToken);
        var unnamed = surface.Elements
            .Where(x => string.IsNullOrWhiteSpace(x.Name) && x.Actions.Count > 0)
            .Select(x => $"{x.Id}@{x.Incarnation}:{x.Role}")
            .ToArray();
        var issues = new List<string>();
        if (unnamed.Length > 0) issues.Add($"{unnamed.Length} actionable semantic objects have no accessible name.");
        var duplicateNames = surface.Elements
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => (x.Role, x.Name), StringComparerTuple.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Take(20)
            .Select(g => $"duplicate {g.Key.Role} name '{g.Key.Name}' x{g.Count()}")
            .ToArray();
        issues.AddRange(duplicateNames);
        return new AccessibilityAuditResult(surface.Target, surface.Document, surface.Elements.Count, unnamed.Length, unnamed, issues);
    }

    public async Task<object> ScreencastStartAsync(
        string targetReference,
        string format = "jpeg",
        int quality = 70,
        int? maxWidth = null,
        int? maxHeight = null,
        int everyNthFrame = 1,
        int maxFrames = 300,
        CancellationToken cancellationToken = default)
    {
        if (format is not ("jpeg" or "png")) throw new ArgumentException("Screencast format must be jpeg or png.");
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        if (_screencastsByTarget.ContainsKey(target.TargetId))
            throw new InvalidOperationException($"A screencast is already active for {target.Id}.");
        var directory = Path.Combine(BrowserRuntime.ArtifactRoot, "screencast", $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var capture = new ScreencastCapture(target.Id, target.TargetId, directory, format == "jpeg" ? "jpg" : "png", Math.Clamp(maxFrames, 1, 5000));
        if (!_screencastsByTarget.TryAdd(target.TargetId, capture))
            throw new InvalidOperationException($"Unable to register screencast for {target.Id}.");
        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["format"] = format,
                ["quality"] = Math.Clamp(quality, 0, 100),
                ["everyNthFrame"] = Math.Clamp(everyNthFrame, 1, 60)
            };
            if (maxWidth is > 0) parameters["maxWidth"] = maxWidth.Value;
            if (maxHeight is > 0) parameters["maxHeight"] = maxHeight.Value;
            await _cdp.SendAsync("Page.startScreencast", parameters, state.SessionId, cancellationToken);
            return new { target = target.Id, directory, format, maxFrames = capture.MaxFrames, startedAtUtc = capture.StartedAtUtc };
        }
        catch
        {
            _screencastsByTarget.TryRemove(target.TargetId, out _);
            throw;
        }
    }

    public async Task<ArtifactInfo> ScreencastStopAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        if (!_screencastsByTarget.TryRemove(target.TargetId, out var capture))
            throw new InvalidOperationException($"No screencast is active for {target.Id}.");
        await _cdp.SendAsync("Page.stopScreencast", sessionId: state.SessionId, cancellationToken: cancellationToken);
        var manifestPath = Path.Combine(capture.Directory, "manifest.json");
        var manifest = new
        {
            capture.Target,
            capture.StartedAtUtc,
            finishedAtUtc = DateTimeOffset.UtcNow,
            capture.FrameCount,
            capture.MaxFrames,
            frames = capture.Frames
        };
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return RegisterArtifact("screencast", manifestPath, target.Id, capture.Directory);
    }

    private sealed class ScreencastCapture(string target, string targetId, string directory, string extension, int maxFrames)
    {
        private readonly object _gate = new();
        private readonly List<object> _frames = new();
        public string Target { get; } = target;
        public string TargetId { get; } = targetId;
        public string Directory { get; } = directory;
        public string Extension { get; } = extension;
        public int MaxFrames { get; } = maxFrames;
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
        public int FrameCount { get { lock (_gate) return _frames.Count; } }
        public IReadOnlyList<object> Frames { get { lock (_gate) return _frames.ToArray(); } }

        public void AddFrame(string path, JsonElement? metadata)
        {
            lock (_gate)
            {
                if (_frames.Count >= MaxFrames) return;
                _frames.Add(new { index = _frames.Count + 1, path, metadata, atUtc = DateTimeOffset.UtcNow });
            }
        }
    }

    private sealed class StringComparerTuple : IEqualityComparer<(string Role, string Name)>
    {
        public static readonly StringComparerTuple OrdinalIgnoreCase = new();
        public bool Equals((string Role, string Name) x, (string Role, string Name) y) =>
            string.Equals(x.Role, y.Role, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Role, string Name) obj) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Role), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
