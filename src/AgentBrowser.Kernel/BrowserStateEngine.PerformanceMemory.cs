using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<PerformanceTimelineEntry>> _performanceTimelineByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HeapSnapshotCapture> _heapCaptureBySession = new(StringComparer.Ordinal);
    private readonly object _traceGate = new();
    private TraceCapture? _activeTrace;
    private long _nextPerformanceTimelineId;

    private void HandlePerformanceMemoryEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
            return;
        var method = methodValue.GetString() ?? "";

        if (method == "Tracing.tracingComplete" && message.TryGetProperty("params", out var traceParams) && traceParams.ValueKind == JsonValueKind.Object)
        {
            TraceCapture? capture;
            lock (_traceGate) capture = _activeTrace;
            if (capture is not null)
            {
                var stream = NullIfEmpty(GetString(traceParams, "stream"));
                var format = NullIfEmpty(GetString(traceParams, "traceFormat"));
                var compression = NullIfEmpty(GetString(traceParams, "streamCompression"));
                var dataLoss = traceParams.TryGetProperty("dataLossOccurred", out var loss) && loss.ValueKind == JsonValueKind.True;
                capture.Completion.TrySetResult(new TraceCompletion(stream, format, compression, dataLoss));
            }
            return;
        }

        if (!message.TryGetProperty("sessionId", out var sessionValue) || sessionValue.ValueKind != JsonValueKind.String)
            return;
        var sessionId = sessionValue.GetString() ?? "";

        if (method == "HeapProfiler.addHeapSnapshotChunk" &&
            _heapCaptureBySession.TryGetValue(sessionId, out var heapCapture) &&
            message.TryGetProperty("params", out var heapParams) && heapParams.ValueKind == JsonValueKind.Object &&
            heapParams.TryGetProperty("chunk", out var chunkValue) && chunkValue.ValueKind == JsonValueKind.String)
        {
            heapCapture.Write(chunkValue.GetString() ?? "");
            return;
        }

        if (!_sessions.TryGetValue(sessionId, out var state) ||
            !message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        if (method == "PerformanceTimeline.timelineEventAdded" &&
            p.TryGetProperty("event", out var evt) && evt.ValueKind == JsonValueKind.Object)
        {
            var details = evt.Clone();
            var entry = new PerformanceTimelineEntry(
                Interlocked.Increment(ref _nextPerformanceTimelineId),
                state.LogicalId,
                GetString(evt, "type"),
                GetString(evt, "name"),
                GetDouble(evt, "time"),
                GetDouble(evt, "duration"),
                details,
                DateTimeOffset.UtcNow);
            EnqueueBounded(_performanceTimelineByTarget.GetOrAdd(state.TargetId, _ => new ConcurrentQueue<PerformanceTimelineEntry>()), entry, 1000);
        }
    }

    public async Task<object> PerformanceTimelineEnableAsync(
        string targetReference,
        IReadOnlyList<string> eventTypes,
        CancellationToken cancellationToken = default)
    {
        var requested = eventTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(32).ToArray();
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var supported = new List<string>();
        var unsupported = new List<string>();
        foreach (var type in requested)
        {
            try
            {
                await _cdp.SendAsync("PerformanceTimeline.enable", new { eventTypes = new[] { type } }, state.SessionId, cancellationToken);
                supported.Add(type);
            }
            catch (CdpException)
            {
                unsupported.Add(type);
            }
        }
        await _cdp.SendAsync("PerformanceTimeline.enable", new { eventTypes = supported.ToArray() }, state.SessionId, cancellationToken);
        return new { target = target.Id, requested, supported = supported.ToArray(), unsupported = unsupported.ToArray() };
    }

    public async Task<IReadOnlyList<PerformanceTimelineEntry>> PerformanceTimelineListAsync(
        string targetReference,
        string? type = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        await EnsureTargetStateAsync(target, cancellationToken);
        if (!_performanceTimelineByTarget.TryGetValue(target.TargetId, out var queue))
            return Array.Empty<PerformanceTimelineEntry>();
        IEnumerable<PerformanceTimelineEntry> values = queue;
        if (!string.IsNullOrWhiteSpace(type)) values = values.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        return values.Reverse().Take(Math.Clamp(limit, 1, 1000)).Reverse().ToArray();
    }

    public async Task<object> PerformanceTraceStartAsync(
        string targetReference,
        IReadOnlyList<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var traceCategories = (categories is { Count: > 0 } ? categories : new[]
        {
            "devtools.timeline", "v8", "blink.user_timing", "loading", "disabled-by-default-devtools.timeline"
        }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(64).ToArray();

        var directory = Path.Combine(BrowserRuntime.ArtifactRoot, "traces");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"trace-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json");
        var capture = new TraceCapture(target.Id, state.SessionId, path, DateTimeOffset.UtcNow);
        lock (_traceGate)
        {
            if (_activeTrace is not null)
                throw new InvalidOperationException($"A trace is already active for {_activeTrace.Target}.");
            _activeTrace = capture;
        }

        try
        {
            await _cdp.SendAsync("Tracing.start", new
            {
                transferMode = "ReturnAsStream",
                streamFormat = "json",
                streamCompression = "none",
                traceConfig = new
                {
                    recordMode = "recordAsMuchAsPossible",
                    enableSampling = true,
                    includedCategories = traceCategories
                }
            }, state.SessionId, cancellationToken);
            return new { target = target.Id, startedAtUtc = capture.StartedAtUtc, categories = traceCategories, path };
        }
        catch
        {
            lock (_traceGate) if (ReferenceEquals(_activeTrace, capture)) _activeTrace = null;
            throw;
        }
    }

    public async Task<PerformanceTraceResult> PerformanceTraceStopAsync(
        string targetReference,
        int timeoutMs = 60_000,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        TraceCapture capture;
        lock (_traceGate)
        {
            capture = _activeTrace ?? throw new InvalidOperationException("No performance trace is active.");
            if (!string.Equals(capture.Target, target.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"The active trace belongs to {capture.Target}, not {target.Id}.");
        }

        try
        {
            await _cdp.SendAsync("Tracing.end", sessionId: state.SessionId, cancellationToken: cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Math.Clamp(timeoutMs, 1000, 300_000));
            var completion = await capture.Completion.Task.WaitAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(completion.Stream))
                throw new InvalidOperationException("Tracing completed without a ReturnAsStream handle.");
            await CopyIoStreamToFileAsync(completion.Stream!, capture.Path, state.SessionId, cancellationToken);
            var artifact = RegisterArtifact("performance-trace", capture.Path, target.Id, $"dataLoss={completion.DataLossOccurred}");
            return new PerformanceTraceResult(artifact, artifact.Size, capture.StartedAtUtc, DateTimeOffset.UtcNow, "ReturnAsStream");
        }
        finally
        {
            lock (_traceGate) if (ReferenceEquals(_activeTrace, capture)) _activeTrace = null;
        }
    }

    public async Task<MemoryCurrentInfo> MemoryCurrentAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        long? used = null;
        long? total = null;
        int? documents = null;
        int? nodes = null;
        int? listeners = null;

        try
        {
            var heap = await _cdp.SendAsync("Runtime.getHeapUsage", sessionId: state.SessionId, cancellationToken: cancellationToken);
            used = ToInt64(heap, "usedSize");
            total = ToInt64(heap, "totalSize");
        }
        catch (CdpException) { }

        try
        {
            var counters = await _cdp.SendAsync("Memory.getDOMCounters", sessionId: state.SessionId, cancellationToken: cancellationToken);
            documents = ToInt32(counters, "documents");
            nodes = ToInt32(counters, "nodes");
            listeners = ToInt32(counters, "jsEventListeners");
        }
        catch (CdpException) { }

        return new MemoryCurrentInfo(target.Id, used, total, documents, nodes, listeners, null);
    }

    public async Task<MemorySnapshotResult> MemoryHeapSnapshotAsync(
        string targetReference,
        bool captureNumericValue = true,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var directory = Path.Combine(BrowserRuntime.ArtifactRoot, "heap");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"heap-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.heapsnapshot");
        var capture = new HeapSnapshotCapture(path, DateTimeOffset.UtcNow);
        if (!_heapCaptureBySession.TryAdd(state.SessionId, capture))
            throw new InvalidOperationException($"A heap snapshot is already active for {target.Id}.");

        try
        {
            await _cdp.SendAsync("HeapProfiler.enable", sessionId: state.SessionId, cancellationToken: cancellationToken);
            await _cdp.SendAsync("HeapProfiler.takeHeapSnapshot", new
            {
                reportProgress = false,
                captureNumericValue
            }, state.SessionId, cancellationToken);
            capture.Flush();
        }
        finally
        {
            _heapCaptureBySession.TryRemove(state.SessionId, out _);
            capture.Dispose();
        }

        var artifact = RegisterArtifact("heap-snapshot", path, target.Id, null);
        return new MemorySnapshotResult(artifact, target.Id, artifact.Size, capture.StartedAtUtc, DateTimeOffset.UtcNow);
    }

    public async Task<object> MemorySamplingStartAsync(
        string targetReference,
        double samplingInterval = 32768,
        int stackDepth = 128,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        await _cdp.SendAsync("HeapProfiler.enable", sessionId: state.SessionId, cancellationToken: cancellationToken);
        await _cdp.SendAsync("HeapProfiler.startSampling", new
        {
            samplingInterval = Math.Clamp(samplingInterval, 1024, 64 * 1024 * 1024),
            stackDepth = Math.Clamp(stackDepth, 1, 1024)
        }, state.SessionId, cancellationToken);
        return new { target = target.Id, samplingInterval, stackDepth };
    }

    public async Task<JsonElement> MemorySamplingStopAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        return await _cdp.SendAsync("HeapProfiler.stopSampling", sessionId: state.SessionId, cancellationToken: cancellationToken);
    }

    private async Task CopyIoStreamToFileAsync(string stream, string path, string? sessionId, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = File.Create(path);
        try
        {
            while (true)
            {
                var result = await _cdp.SendAsync("IO.read", new { handle = stream, size = 1024 * 1024 }, sessionId, cancellationToken);
                var data = result.TryGetProperty("data", out var dataValue) && dataValue.ValueKind == JsonValueKind.String ? dataValue.GetString() ?? "" : "";
                var base64 = result.TryGetProperty("base64Encoded", out var encoded) && encoded.ValueKind == JsonValueKind.True;
                if (data.Length > 0)
                {
                    var bytes = base64 ? Convert.FromBase64String(data) : Encoding.UTF8.GetBytes(data);
                    await output.WriteAsync(bytes, cancellationToken);
                }
                if (result.TryGetProperty("eof", out var eof) && eof.ValueKind == JsonValueKind.True) break;
            }
        }
        finally
        {
            try { await _cdp.SendAsync("IO.close", new { handle = stream }, sessionId, CancellationToken.None); } catch { }
        }
    }

    private static double GetDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDouble(out var result) ? result : 0;

    private static long? ToInt64(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? checked((long)number) : null;

    private static int? ToInt32(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;

    private sealed record TraceCompletion(string? Stream, string? Format, string? Compression, bool DataLossOccurred);

    private sealed class TraceCapture(string target, string sessionId, string path, DateTimeOffset startedAtUtc)
    {
        public string Target { get; } = target;
        public string SessionId { get; } = sessionId;
        public string Path { get; } = path;
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
        public TaskCompletionSource<TraceCompletion> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class HeapSnapshotCapture : IDisposable
    {
        private readonly object _gate = new();
        private readonly StreamWriter _writer;
        public string Path { get; }
        public DateTimeOffset StartedAtUtc { get; }

        public HeapSnapshotCapture(string path, DateTimeOffset startedAtUtc)
        {
            Path = path;
            StartedAtUtc = startedAtUtc;
            _writer = new StreamWriter(File.Create(path), new UTF8Encoding(false), 1024 * 1024);
        }

        public void Write(string chunk)
        {
            lock (_gate) _writer.Write(chunk);
        }

        public void Flush()
        {
            lock (_gate) _writer.Flush();
        }

        public void Dispose()
        {
            lock (_gate) _writer.Dispose();
        }
    }
}
