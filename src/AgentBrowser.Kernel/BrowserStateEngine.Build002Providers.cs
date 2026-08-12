using System.Collections.Concurrent;
using System.Text.Json;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed partial class BrowserStateEngine
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ConsoleEntry>> _consoleByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentQueue<BrowserException>> _exceptionsByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebMcpToolInfo>> _webMcpToolsByTarget = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WebMcpInvocationResult>> _webMcpInvocations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DownloadInfo> _downloadsByGuid = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DownloadInfo>> _downloadCompletion = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ArtifactInfo> _artifacts = new(StringComparer.Ordinal);
    private long _nextConsoleId;
    private long _nextExceptionId;
    private long _nextDownloadId;
    private long _nextArtifactId;
    private int _downloadBehaviorRearmScheduled;

    private void HandleBuild002ProviderEvent(JsonElement message)
    {
        if (!message.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
            return;
        var method = methodValue.GetString() ?? "";
        if (!message.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object)
            return;

        if (method == "Browser.downloadWillBegin")
        {
            var guid = GetString(p, "guid");
            if (string.IsNullOrWhiteSpace(guid)) return;
            var info = new DownloadInfo(
                $"dl_{Interlocked.Increment(ref _nextDownloadId)}",
                guid,
                GetString(p, "url"),
                NullIfEmpty(GetString(p, "suggestedFilename")),
                "inProgress",
                0,
                0,
                NullIfEmpty(GetString(p, "frameId")),
                Path.Combine(BrowserRuntime.DownloadStagingRoot, guid),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            _downloadsByGuid[guid] = info;
            _downloadCompletion.TryAdd(guid, new TaskCompletionSource<DownloadInfo>(TaskCreationOptions.RunContinuationsAsynchronously));
            return;
        }

        if (method == "Browser.downloadProgress")
        {
            var guid = GetString(p, "guid");
            if (string.IsNullOrWhiteSpace(guid)) return;
            if (!_downloadsByGuid.TryGetValue(guid, out var current))
            {
                current = new DownloadInfo(
                    $"dl_{Interlocked.Increment(ref _nextDownloadId)}", guid, "", null, "inProgress", 0, 0, null,
                    Path.Combine(BrowserRuntime.DownloadStagingRoot, guid), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            }
            var state = GetString(p, "state");
            var received = p.TryGetProperty("receivedBytes", out var receivedValue) && receivedValue.TryGetDouble(out var receivedDouble) ? (long)receivedDouble : current.ReceivedBytes;
            var total = p.TryGetProperty("totalBytes", out var totalValue) && totalValue.TryGetDouble(out var totalDouble) ? (long)totalDouble : current.TotalBytes;
            var updated = current with { State = string.IsNullOrWhiteSpace(state) ? current.State : state, ReceivedBytes = received, TotalBytes = total, UpdatedAtUtc = DateTimeOffset.UtcNow };
            _downloadsByGuid[guid] = updated;
            if (state is "completed" or "canceled")
            {
                var completion = _downloadCompletion.GetOrAdd(guid, _ => new TaskCompletionSource<DownloadInfo>(TaskCreationOptions.RunContinuationsAsynchronously));
                completion.TrySetResult(updated);
                _ = RearmDownloadBehaviorAfterTerminalEventAsync();
            }
            return;
        }

        if (!message.TryGetProperty("sessionId", out var sessionValue) || sessionValue.ValueKind != JsonValueKind.String ||
            !_sessions.TryGetValue(sessionValue.GetString() ?? "", out var targetState))
            return;

        if (method == "Runtime.consoleAPICalled")
        {
            var level = GetString(p, "type");
            var text = p.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array
                ? string.Join(" ", args.EnumerateArray().Select(RemoteObjectText))
                : "";
            string? url = null;
            int? line = null;
            int? column = null;
            string? stack = null;
            if (p.TryGetProperty("stackTrace", out var stackTrace) && stackTrace.ValueKind == JsonValueKind.Object)
            {
                stack = stackTrace.ToString();
                if (stackTrace.TryGetProperty("callFrames", out var frames) && frames.ValueKind == JsonValueKind.Array)
                {
                    var first = frames.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object)
                    {
                        url = NullIfEmpty(GetString(first, "url"));
                        line = first.TryGetProperty("lineNumber", out var l) && l.TryGetInt32(out var li) ? li : null;
                        column = first.TryGetProperty("columnNumber", out var c) && c.TryGetInt32(out var ci) ? ci : null;
                    }
                }
            }
            EnqueueBounded(_consoleByTarget.GetOrAdd(targetState.TargetId, _ => new ConcurrentQueue<ConsoleEntry>()), new ConsoleEntry(
                Interlocked.Increment(ref _nextConsoleId), targetState.LogicalId, "console", level, text, url, line, column, DateTimeOffset.UtcNow, stack));
            return;
        }

        if (method == "Log.entryAdded" && p.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
        {
            EnqueueBounded(_consoleByTarget.GetOrAdd(targetState.TargetId, _ => new ConcurrentQueue<ConsoleEntry>()), new ConsoleEntry(
                Interlocked.Increment(ref _nextConsoleId),
                targetState.LogicalId,
                GetString(entry, "source"),
                GetString(entry, "level"),
                GetString(entry, "text"),
                NullIfEmpty(GetString(entry, "url")),
                entry.TryGetProperty("lineNumber", out var lineValue) && lineValue.TryGetInt32(out var lineInt) ? lineInt : null,
                null,
                DateTimeOffset.UtcNow,
                entry.TryGetProperty("stackTrace", out var stackValue) ? stackValue.ToString() : null,
                NullIfEmpty(GetString(entry, "networkRequestId"))));
            return;
        }

        if (method == "Runtime.exceptionThrown" && p.TryGetProperty("exceptionDetails", out var details) && details.ValueKind == JsonValueKind.Object)
        {
            var exception = new BrowserException(
                Interlocked.Increment(ref _nextExceptionId),
                targetState.LogicalId,
                GetString(details, "text"),
                NullIfEmpty(GetString(details, "url")),
                details.TryGetProperty("lineNumber", out var lineValue) && lineValue.TryGetInt32(out var lineInt) ? lineInt : null,
                details.TryGetProperty("columnNumber", out var columnValue) && columnValue.TryGetInt32(out var columnInt) ? columnInt : null,
                DateTimeOffset.UtcNow,
                details.TryGetProperty("stackTrace", out var stackValue) ? stackValue.ToString() : null);
            EnqueueBounded(_exceptionsByTarget.GetOrAdd(targetState.TargetId, _ => new ConcurrentQueue<BrowserException>()), exception);
            return;
        }

        if (method == "WebMCP.toolsAdded" && p.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Array)
        {
            var targetTools = _webMcpToolsByTarget.GetOrAdd(targetState.TargetId, _ => new ConcurrentDictionary<string, WebMcpToolInfo>(StringComparer.Ordinal));
            foreach (var tool in tools.EnumerateArray())
            {
                var name = GetString(tool, "name");
                var frameId = GetString(tool, "frameId");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(frameId)) continue;
                var schema = tool.TryGetProperty("inputSchema", out var schemaValue) ? schemaValue.Clone() : JsonSerializer.SerializeToElement(new { type = "object" });
                JsonElement? annotations = tool.TryGetProperty("annotations", out var annotationsValue) ? annotationsValue.Clone() : null;
                int? backendNodeId = tool.TryGetProperty("backendNodeId", out var backendValue) && backendValue.TryGetInt32(out var backend) ? backend : null;
                targetTools[$"{frameId}|{name}"] = new WebMcpToolInfo(targetState.LogicalId, frameId, name, GetString(tool, "description"), schema, annotations, backendNodeId);
            }
            return;
        }

        if (method == "WebMCP.toolsRemoved" && p.TryGetProperty("tools", out var removed) && removed.ValueKind == JsonValueKind.Array)
        {
            if (!_webMcpToolsByTarget.TryGetValue(targetState.TargetId, out var targetTools)) return;
            foreach (var tool in removed.EnumerateArray())
                targetTools.TryRemove($"{GetString(tool, "frameId")}|{GetString(tool, "name")}", out _);
            return;
        }

        if (method == "WebMCP.toolResponded")
        {
            var invocationId = GetString(p, "invocationId");
            if (string.IsNullOrWhiteSpace(invocationId)) return;
            JsonElement? output = p.TryGetProperty("output", out var outputValue) ? outputValue.Clone() : null;
            var result = new WebMcpInvocationResult(invocationId, GetString(p, "status"), output, NullIfEmpty(GetString(p, "errorText")));
            if (_webMcpInvocations.TryGetValue(invocationId, out var completion))
                completion.TrySetResult(result);
            return;
        }
    }

    private async Task RearmDownloadBehaviorAfterTerminalEventAsync()
    {
        if (Interlocked.Exchange(ref _downloadBehaviorRearmScheduled, 1) != 0)
            return;
        try
        {
            // Never await a CDP command inline from the receive-loop event handler.
            await Task.Yield();
            await ArmDownloadBehaviorAsync(CancellationToken.None);
        }
        catch
        {
            // Provider support is dynamic; explicit initialization can re-arm if this attempt fails.
        }
        finally
        {
            Volatile.Write(ref _downloadBehaviorRearmScheduled, 0);
        }
    }

    private bool HasWebMcpTools(string targetId) =>
        _webMcpToolsByTarget.TryGetValue(targetId, out var tools) && !tools.IsEmpty;

    public async Task<IReadOnlyList<WebMcpToolInfo>> WebMcpListAsync(string targetReference, CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        await EnsureTargetStateAsync(target, cancellationToken);
        await Task.Delay(25, cancellationToken);
        return _webMcpToolsByTarget.TryGetValue(target.TargetId, out var tools)
            ? tools.Values.OrderBy(x => x.FrameId, StringComparer.Ordinal).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray()
            : Array.Empty<WebMcpToolInfo>();
    }

    public async Task<WebMcpToolInfo> WebMcpInspectAsync(string targetReference, string name, string? frameId = null, CancellationToken cancellationToken = default)
    {
        var tools = await WebMcpListAsync(targetReference, cancellationToken);
        var matches = tools.Where(x => string.Equals(x.Name, name, StringComparison.Ordinal) &&
                                       (string.IsNullOrWhiteSpace(frameId) || string.Equals(x.FrameId, frameId, StringComparison.Ordinal))).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException($"WebMCP tool '{name}' was not advertised by the current document."),
            _ => throw new InvalidOperationException($"WebMCP tool '{name}' is ambiguous across frames; provide frameId.")
        };
    }

    public async Task<WebMcpInvocationResult> WebMcpExecuteAsync(
        string targetReference,
        string name,
        JsonElement input,
        string? frameId = null,
        int timeoutMs = 30_000,
        CancellationToken cancellationToken = default)
    {
        var target = await ResolveTargetAsync(targetReference, cancellationToken);
        var state = await EnsureTargetStateAsync(target, cancellationToken);
        var tool = await WebMcpInspectAsync(targetReference, name, frameId, cancellationToken);
        var result = await _cdp.SendAsync("WebMCP.invokeTool", new
        {
            frameId = tool.FrameId,
            toolName = tool.Name,
            input = input.Clone()
        }, state.SessionId, cancellationToken);
        var invocationId = GetString(result, "invocationId");
        if (string.IsNullOrWhiteSpace(invocationId))
            throw new InvalidOperationException("WebMCP.invokeTool did not return an invocationId.");
        var completion = _webMcpInvocations.GetOrAdd(invocationId, _ => new TaskCompletionSource<WebMcpInvocationResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Math.Clamp(timeoutMs, 1, 300_000));
        try
        {
            return await completion.Task.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try { await _cdp.SendAsync("WebMCP.cancelInvocation", new { invocationId }, state.SessionId, CancellationToken.None); } catch { }
            throw new TimeoutException($"WebMCP invocation {invocationId} did not respond within {timeoutMs} ms.");
        }
        finally
        {
            _webMcpInvocations.TryRemove(invocationId, out _);
        }
    }

    public async Task<IReadOnlyList<ConsoleEntry>> ConsoleListAsync(string targetReference, int limit = 100, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        return _consoleByTarget.TryGetValue(target.TargetId, out var queue)
            ? queue.Reverse().Take(Math.Clamp(limit, 1, 500)).Reverse().ToArray()
            : Array.Empty<ConsoleEntry>();
    }

    public ConsoleEntry ConsoleGet(long id)
    {
        foreach (var queue in _consoleByTarget.Values)
            if (queue.FirstOrDefault(x => x.Id == id) is { } found) return found;
        throw new KeyNotFoundException($"Unknown console entry {id}.");
    }

    public async Task<IReadOnlyList<BrowserException>> ExceptionListAsync(string targetReference, int limit = 100, CancellationToken cancellationToken = default)
    {
        var target = await ResolveAnyTargetAsync(targetReference, cancellationToken);
        await EnsureRuntimeTargetStateAsync(target, cancellationToken);
        return _exceptionsByTarget.TryGetValue(target.TargetId, out var queue)
            ? queue.Reverse().Take(Math.Clamp(limit, 1, 500)).Reverse().ToArray()
            : Array.Empty<BrowserException>();
    }

    public BrowserException ExceptionGet(long id)
    {
        foreach (var queue in _exceptionsByTarget.Values)
            if (queue.FirstOrDefault(x => x.Id == id) is { } found) return found;
        throw new KeyNotFoundException($"Unknown exception {id}.");
    }

    public IReadOnlyList<DownloadInfo> DownloadList() =>
        _downloadsByGuid.Values.OrderByDescending(x => x.StartedAtUtc).ToArray();

    public async Task<DownloadInfo> DownloadWaitAsync(string idOrGuid, int timeoutMs = 120_000, CancellationToken cancellationToken = default)
    {
        var current = FindDownload(idOrGuid);
        if (current.State is "completed" or "canceled") return current;
        var completion = _downloadCompletion.GetOrAdd(current.Guid, _ => new TaskCompletionSource<DownloadInfo>(TaskCreationOptions.RunContinuationsAsynchronously));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Math.Clamp(timeoutMs, 1, 600_000));
        try { return await completion.Task.WaitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Download {current.Id} did not complete within {timeoutMs} ms.");
        }
    }

    public async Task<DownloadInfo> DownloadCancelAsync(string idOrGuid, CancellationToken cancellationToken = default)
    {
        var current = FindDownload(idOrGuid);
        await _cdp.SendAsync("Browser.cancelDownload", new { guid = current.Guid }, cancellationToken: cancellationToken);
        return _downloadsByGuid.TryGetValue(current.Guid, out var updated) ? updated : current;
    }

    public async Task<ArtifactInfo> DownloadSaveAsync(string idOrGuid, string destination, CancellationToken cancellationToken = default)
    {
        var completed = await DownloadWaitAsync(idOrGuid, cancellationToken: cancellationToken);
        if (!string.Equals(completed.State, "completed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Download {completed.Id} is {completed.State}, not completed.");
        var source = completed.Path ?? Path.Combine(BrowserRuntime.DownloadStagingRoot, completed.Guid);
        if (!File.Exists(source))
            throw new FileNotFoundException("Completed Chrome download material was not found.", source);
        var fullDestination = Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(fullDestination)!);
        File.Copy(source, fullDestination, overwrite: true);
        return RegisterArtifact("download", fullDestination, null, completed.Url);
    }

    public IReadOnlyList<ArtifactInfo> ArtifactList() => _artifacts.Values.OrderByDescending(x => x.CreatedAtUtc).ToArray();

    public ArtifactInfo ArtifactGet(string id) =>
        _artifacts.TryGetValue(id, out var artifact) ? artifact : throw new KeyNotFoundException($"Unknown artifact '{id}'.");

    private DownloadInfo FindDownload(string idOrGuid)
    {
        if (_downloadsByGuid.TryGetValue(idOrGuid, out var byGuid)) return byGuid;
        var byId = _downloadsByGuid.Values.FirstOrDefault(x => string.Equals(x.Id, idOrGuid, StringComparison.Ordinal));
        return byId ?? throw new KeyNotFoundException($"Unknown download '{idOrGuid}'.");
    }

    public ArtifactInfo ArtifactRegister(string type, string path, string? target = null, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("Artifact type is required.", nameof(type));
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException("Artifact material file does not exist.", full);
        return RegisterArtifact(type.Trim(), full, target, source);
    }
    private ArtifactInfo RegisterArtifact(string type, string path, string? target, string? source)
    {
        var info = new FileInfo(path);
        var artifact = new ArtifactInfo(
            $"a_{Interlocked.Increment(ref _nextArtifactId)}", type, path, info.Exists ? info.Length : 0, target, source, DateTimeOffset.UtcNow);
        _artifacts[artifact.Id] = artifact;
        return artifact;
    }

    private static string RemoteObjectText(JsonElement remote)
    {
        if (remote.TryGetProperty("value", out var value))
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        if (remote.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
            return description.GetString() ?? "";
        return GetString(remote, "type");
    }

    private static void EnqueueBounded<T>(ConcurrentQueue<T> queue, T item, int max = 500)
    {
        queue.Enqueue(item);
        while (queue.Count > max) queue.TryDequeue(out _);
    }
}
