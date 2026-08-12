using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<NetworkMessage>> _networkMessagesByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, NetworkStreamCapture> _networkStreams = new(StringComparer.Ordinal);
    private long _nextNetworkMessageId;

    private void HandleNetworkDepthEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String ||
            !message.TryGetProperty("sessionId", out var sessionValue) || sessionValue.ValueKind != JsonValueKind.String ||
            !_sessions.TryGetValue(sessionValue.GetString() ?? "", out var state) ||
            !message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        var method = methodValue.GetString() ?? "";
        var rawRequestId = GetString(p, "requestId");
        NetworkEntry? entry = string.IsNullOrWhiteSpace(rawRequestId) ? null : GetOrCreateNetworkEntry(state, rawRequestId);

        if (method == "Network.requestWillBeSent" && entry is not null)
        {
            if (p.TryGetProperty("request", out var request) && request.ValueKind == JsonValueKind.Object)
            {
                if (request.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object) entry.RequestHeaders = headers.Clone();
                if (request.TryGetProperty("postData", out var postData) && postData.ValueKind == JsonValueKind.String) entry.RequestPostData = postData.GetString();
                ClassifyGraphQl(entry);
            }
            if (p.TryGetProperty("initiator", out var initiator) && initiator.ValueKind == JsonValueKind.Object) entry.Initiator = initiator.Clone();
            if (p.TryGetProperty("redirectResponse", out var redirect) && redirect.ValueKind == JsonValueKind.Object)
            {
                var redirectUrl = GetString(redirect, "url");
                if (!string.IsNullOrWhiteSpace(redirectUrl) && !entry.RedirectChain.Contains(redirectUrl, StringComparer.Ordinal)) entry.RedirectChain.Add(redirectUrl);
            }
        }
        else if (method == "Network.responseReceived" && entry is not null && p.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Object)
        {
            if (response.TryGetProperty("headers", out var headers) && headers.ValueKind == JsonValueKind.Object) entry.ResponseHeaders = headers.Clone();
            if (response.TryGetProperty("timing", out var timing) && timing.ValueKind == JsonValueKind.Object) entry.Timing = timing.Clone();
            entry.FromServiceWorker = response.TryGetProperty("fromServiceWorker", out var serviceWorker) && serviceWorker.ValueKind == JsonValueKind.True;
            entry.FromDiskCache = response.TryGetProperty("fromDiskCache", out var disk) && disk.ValueKind == JsonValueKind.True;
            entry.FromPrefetchCache = response.TryGetProperty("fromPrefetchCache", out var prefetch) && prefetch.ValueKind == JsonValueKind.True;
            entry.Protocol = NullIfEmpty(GetString(response, "protocol"));
            entry.RemoteIpAddress = NullIfEmpty(GetString(response, "remoteIPAddress"));
            entry.RemotePort = response.TryGetProperty("remotePort", out var port) && port.TryGetInt32(out var portValue) ? portValue : null;
        }
        else if (method == "Network.requestServedFromCache" && entry is not null)
        {
            entry.FromDiskCache = true;
        }
        else if (method is "Network.webSocketFrameReceived" or "Network.webSocketFrameSent" && entry is not null &&
                 p.TryGetProperty("response", out var frame) && frame.ValueKind == JsonValueKind.Object)
        {
            var data = GetString(frame, "payloadData");
            var opcode = frame.TryGetProperty("opcode", out var opcodeValue) ? opcodeValue.ToString() : null;
            EnqueueBounded(_networkMessagesByTarget.GetOrAdd(state.TargetId, _ => new ConcurrentQueue<NetworkMessage>()), new NetworkMessage(
                Interlocked.Increment(ref _nextNetworkMessageId), state.LogicalId, entry.Id, "websocket",
                method.EndsWith("Received", StringComparison.Ordinal) ? "received" : "sent", opcode, data, DateTimeOffset.UtcNow), 2000);
        }
        else if (method == "Network.eventSourceMessageReceived" && entry is not null)
        {
            var data = GetString(p, "data");
            var eventName = NullIfEmpty(GetString(p, "eventName"));
            EnqueueBounded(_networkMessagesByTarget.GetOrAdd(state.TargetId, _ => new ConcurrentQueue<NetworkMessage>()), new NetworkMessage(
                Interlocked.Increment(ref _nextNetworkMessageId), state.LogicalId, entry.Id, "sse", "received", eventName, data, DateTimeOffset.UtcNow), 2000);
        }
        else if (method == "Network.dataReceived" && entry is not null &&
                 p.TryGetProperty("data", out var dataValue) && dataValue.ValueKind == JsonValueKind.String &&
                 _networkStreams.TryGetValue(StreamKey(state.SessionId, entry.RawRequestId), out var capture))
        {
            capture.WriteChunk(dataValue.GetString() ?? "");
        }

        if (entry is not null && method is "Network.loadingFinished" or "Network.loadingFailed" &&
            _networkStreams.TryGetValue(StreamKey(state.SessionId, entry.RawRequestId), out var completionCapture))
        {
            completionCapture.Completion.TrySetResult(method == "Network.loadingFinished");
        }
    }

    public async Task<NetworkDetail> NetworkDetailAsync(string logicalRequestId, CancellationToken cancellationToken = default)
    {
        var (state, entry) = FindNetworkEntry(logicalRequestId);
        if (entry.RequestPostData is null && entry.Method is "POST" or "PUT" or "PATCH")
        {
            try
            {
                var post = await _cdp.SendAsync("Network.getRequestPostData", new { requestId = entry.RawRequestId }, state.SessionId, cancellationToken);
                entry.RequestPostData = post.TryGetProperty("postData", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                ClassifyGraphQl(entry);
            }
            catch (CdpException) { }
        }
        return new NetworkDetail(
            NetworkSnapshot(state, entry),
            entry.RequestHeaders,
            entry.ResponseHeaders,
            entry.Timing,
            entry.Initiator,
            entry.RequestPostData,
            entry.RedirectChain.ToArray(),
            entry.FromServiceWorker,
            entry.FromDiskCache,
            entry.FromPrefetchCache,
            entry.Protocol,
            entry.RemoteIpAddress,
            entry.RemotePort,
            entry.GraphQlOperationName,
            entry.GraphQlOperationType);
    }

    public async Task<JsonElement> NetworkSearchBodyAsync(
        string logicalRequestId,
        string query,
        bool caseSensitive = false,
        bool isRegex = false,
        CancellationToken cancellationToken = default)
    {
        RequireProtocolCommand("Network.searchInResponseBody");
        var (state, entry) = FindNetworkEntry(logicalRequestId);
        try
        {
            return await _cdp.SendAsync("Network.searchInResponseBody", new
            {
                requestId = entry.RawRequestId,
                query,
                caseSensitive,
                isRegex
            }, state.SessionId, cancellationToken);
        }
        catch (CdpException)
        {
            var body = await NetworkBodyAsync(logicalRequestId, cancellationToken);
            var text = body.Base64Encoded
                ? Encoding.UTF8.GetString(Convert.FromBase64String(body.Body))
                : body.Body;
            var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            var pattern = isRegex ? query : Regex.Escape(query);
            var regex = new Regex(pattern, options | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var matches = new List<object>();
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (var i = 0; i < lines.Length && matches.Count < 1000; i++)
            {
                if (regex.IsMatch(lines[i])) matches.Add(new { lineNumber = i, lineContent = lines[i] });
            }
            return JsonSerializer.SerializeToElement(new { result = matches, provider = "local-fallback", chromeSearchSupportedForRequest = false });
        }
    }

    public async Task<IReadOnlyList<NetworkMessage>> NetworkMessagesAsync(
        string targetReference,
        string? kind = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        await EnsureTargetStateAsync(target, cancellationToken);
        if (!_networkMessagesByTarget.TryGetValue(target.TargetId, out var queue)) return Array.Empty<NetworkMessage>();
        IEnumerable<NetworkMessage> values = queue;
        if (!string.IsNullOrWhiteSpace(kind)) values = values.Where(x => string.Equals(x.Kind, kind, StringComparison.OrdinalIgnoreCase));
        return values.Reverse().Take(Math.Clamp(limit, 1, 2000)).Reverse().ToArray();
    }

    public async Task<ArtifactInfo> NetworkBodySaveAsync(
        string logicalRequestId,
        string? destination = null,
        int timeoutMs = 120_000,
        CancellationToken cancellationToken = default)
    {
        var (state, entry) = FindNetworkEntry(logicalRequestId);
        var directory = Path.Combine(BrowserRuntime.ArtifactRoot, "network");
        Directory.CreateDirectory(directory);
        var path = string.IsNullOrWhiteSpace(destination)
            ? Path.Combine(directory, $"{entry.Id}-{Guid.NewGuid():N}.body")
            : Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (entry.Completed || entry.Failed || !_protocol.Supports("Network.streamResourceContent"))
        {
            var body = await NetworkBodyAsync(logicalRequestId, cancellationToken);
            var bytes = body.Base64Encoded ? Convert.FromBase64String(body.Body) : Encoding.UTF8.GetBytes(body.Body);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            return RegisterArtifact("network-body", path, state.LogicalId, entry.Url);
        }

        var key = StreamKey(state.SessionId, entry.RawRequestId);
        var capture = new NetworkStreamCapture(path);
        if (!_networkStreams.TryAdd(key, capture)) throw new InvalidOperationException($"Request {entry.Id} is already being streamed.");
        try
        {
            var start = await _cdp.SendAsync("Network.streamResourceContent", new { requestId = entry.RawRequestId }, state.SessionId, cancellationToken);
            var buffered = start.TryGetProperty("bufferedData", out var bufferedValue) && bufferedValue.ValueKind == JsonValueKind.String ? bufferedValue.GetString() ?? "" : "";
            capture.WriteBufferedAndActivate(buffered);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Math.Clamp(timeoutMs, 1000, 600_000));
            await capture.Completion.Task.WaitAsync(timeout.Token);
            capture.Flush();
        }
        finally
        {
            _networkStreams.TryRemove(key, out _);
            capture.Dispose();
        }
        return RegisterArtifact("network-stream", path, state.LogicalId, entry.Url);
    }

    private (TargetState State, NetworkEntry Entry) FindNetworkEntry(string logicalRequestId)
    {
        foreach (var state in _targets.Values)
            if (state.NetworkByLogicalId.TryGetValue(logicalRequestId, out var entry)) return (state, entry);
        throw new KeyNotFoundException($"Unknown network request '{logicalRequestId}'.");
    }

    private static string StreamKey(string sessionId, string rawRequestId) => sessionId + "|" + rawRequestId;

    private static void ClassifyGraphQl(NetworkEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.RequestPostData)) return;
        try
        {
            using var document = JsonDocument.Parse(entry.RequestPostData);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("operationName", out var operationName) && operationName.ValueKind == JsonValueKind.String)
                entry.GraphQlOperationName = operationName.GetString();
            if (root.TryGetProperty("query", out var query) && query.ValueKind == JsonValueKind.String)
            {
                var text = query.GetString()?.TrimStart() ?? "";
                entry.GraphQlOperationType = text.StartsWith("mutation", StringComparison.OrdinalIgnoreCase) ? "mutation" :
                    text.StartsWith("subscription", StringComparison.OrdinalIgnoreCase) ? "subscription" :
                    text.Length > 0 ? "query" : null;
            }
        }
        catch (JsonException) { }
    }

    private sealed class NetworkStreamCapture : IDisposable
    {
        private readonly object _gate = new();
        private readonly FileStream _stream;
        private readonly List<byte[]> _pending = new();
        private bool _active;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public NetworkStreamCapture(string path) => _stream = File.Create(path);

        public void WriteBufferedAndActivate(string base64)
        {
            lock (_gate)
            {
                if (!string.IsNullOrWhiteSpace(base64)) _stream.Write(Convert.FromBase64String(base64));
                foreach (var bytes in _pending) _stream.Write(bytes);
                _pending.Clear();
                _active = true;
            }
        }

        public void WriteChunk(string base64)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64); }
            catch { return; }
            lock (_gate)
            {
                if (_active) _stream.Write(bytes);
                else _pending.Add(bytes);
            }
        }

        public void Flush() { lock (_gate) _stream.Flush(); }
        public void Dispose() { lock (_gate) _stream.Dispose(); }
    }
}
