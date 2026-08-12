using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.State;

namespace AgentBrowser.Kernel;

internal sealed record RpcRequest(long Id, string Method, JsonElement Params);

internal sealed class KernelRpcDispatcher(
    BrowserRuntimeDescriptor runtime,
    CdpClient cdp,
    BrowserStateEngine state)
{
    public async Task<object?> DispatchAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        var p = request.Params;
        return request.Method switch
        {
            "browser.status" => new
            {
                runtime.ProfileName,
                runtime.UserDataDir,
                runtime.Port,
                runtime.BrowserId,
                runtime.BrowserVersion,
                runtime.ProtocolVersion,
                pipe = BrowserRuntime.PipeName,
                artifactRoot = BrowserRuntime.ArtifactRoot,
                downloadRoot = BrowserRuntime.DownloadRoot,
                kernelPid = Environment.ProcessId
            },
            "context.current" => await state.CurrentContextAsync(cancellationToken),
            "target.list" => await state.ListTargetsAsync(cancellationToken),
            "target.cognition" => await state.ListCognitionAsync(cancellationToken),
            "target.open" => await OpenTargetAsync(GetRequiredString(p, "url"), cancellationToken),
            "target.activate" => await state.ActivateTargetAsync(GetRequiredString(p, "target"), cancellationToken),
            "target.close" => await state.CloseTargetAsync(GetRequiredString(p, "target"), cancellationToken),
            "target.demote" => await state.DemoteTargetAsync(GetRequiredString(p, "target"), GetRequiredString(p, "to"), cancellationToken),
            "lifecycle.status" => await state.LifecycleStatusAsync(GetRequiredString(p, "target"), cancellationToken),
            "observe.surface" => await state.ObserveAsync(GetRequiredString(p, "target"), cancellationToken),
            "observe.delta" => await state.DeltaAsync(GetRequiredString(p, "target"), GetRequiredInt64(p, "since"), cancellationToken),
            "query.find" => await state.QueryAsync(ParseQuery(p), cancellationToken),
            "inspect.element" => state.Inspect(GetRequiredString(p, "id")),
            "identity.resolve" => state.IdentityStatus(GetRequiredString(p, "id")),
            "action.click" => await ClickAsync(p, cancellationToken),
            "action.fill" => await FillAsync(p, cancellationToken),
            "action.type" => await TypeAsync(p, cancellationToken),
            "action.key" => await KeyAsync(p, cancellationToken),
            "action.scroll" => await ScrollAsync(p, cancellationToken),
            "action.hover" => await ElementActionAsync(p, state.HoverAsync, cancellationToken),
            "action.double_click" => await ElementActionAsync(p, state.DoubleClickAsync, cancellationToken),
            "action.context_click" => await ElementActionAsync(p, state.ContextClickAsync, cancellationToken),
            "action.focus" => await ElementActionAsync(p, state.FocusAsync, cancellationToken),
            "action.select" => await state.SelectAsync(GetRequiredString(p, "id"), GetRequiredStringArray(p, "values"), cancellationToken),
            "action.check" => await state.CheckAsync(GetRequiredString(p, "id"), true, cancellationToken),
            "action.uncheck" => await state.CheckAsync(GetRequiredString(p, "id"), false, cancellationToken),
            "file.upload" => await UploadAsync(p, cancellationToken),
            "js.evaluate" => await state.EvaluateAsync(GetRequiredString(p, "target"), GetRequiredString(p, "expression"), cancellationToken),
            "wait.until" => await WaitAsync(p, cancellationToken),
            "wait.any" => await WaitAnyAsync(p, cancellationToken),
            "wait.all" => await WaitAllAsync(p, cancellationToken),
            "wait.sequence" => await WaitSequenceAsync(p, cancellationToken),
            "wait.quiet_for" => await WaitQuietAsync(p, cancellationToken),
            "network.search" => await NetworkSearchAsync(p, cancellationToken),
            "network.body" => await state.NetworkBodyAsync(GetRequiredString(p, "id"), cancellationToken),
            "console.list" => await state.ConsoleListAsync(GetRequiredString(p, "target"), GetOptionalInt32(p, "limit") ?? 100, cancellationToken),
            "console.get" => state.ConsoleGet(GetRequiredInt64(p, "id")),
            "exception.list" => await state.ExceptionListAsync(GetRequiredString(p, "target"), GetOptionalInt32(p, "limit") ?? 100, cancellationToken),
            "exception.get" => state.ExceptionGet(GetRequiredInt64(p, "id")),
            "download.list" => state.DownloadList(),
            "download.wait" => await state.DownloadWaitAsync(GetRequiredString(p, "id"), GetOptionalInt32(p, "timeoutMs") ?? 120000, cancellationToken),
            "download.save" => await state.DownloadSaveAsync(GetRequiredString(p, "id"), GetRequiredString(p, "destination"), cancellationToken),
            "download.cancel" => await state.DownloadCancelAsync(GetRequiredString(p, "id"), cancellationToken),
            "artifact.list" => state.ArtifactList(),
            "artifact.get" => state.ArtifactGet(GetRequiredString(p, "id")),
            "screenshot.full_page" => await state.ScreenshotFullPageAsync(GetRequiredString(p, "target"), GetOptionalString(p, "destination"), cancellationToken),
            "screenshot.element" => await state.ScreenshotElementAsync(GetRequiredString(p, "id"), GetOptionalString(p, "destination"), cancellationToken),
            "performance.metrics" => await state.PerformanceMetricsAsync(GetRequiredString(p, "target"), cancellationToken),
            "webmcp.list" => await state.WebMcpListAsync(GetRequiredString(p, "target"), cancellationToken),
            "webmcp.inspect" => await state.WebMcpInspectAsync(GetRequiredString(p, "target"), GetRequiredString(p, "name"), GetOptionalString(p, "frameId"), cancellationToken),
            "webmcp.execute" => await WebMcpExecuteAsync(p, cancellationToken),
            "runtime_tools.list" => await state.RuntimeToolsListAsync(GetRequiredString(p, "target"), cancellationToken),
            "runtime_tools.inspect" => await state.RuntimeToolsInspectAsync(GetRequiredString(p, "target"), GetRequiredString(p, "name"), GetOptionalString(p, "group"), cancellationToken),
            "runtime_tools.execute" => await RuntimeToolExecuteAsync(p, cancellationToken),
            "cdp.subscribe" => await state.CdpSubscribeAsync(GetRequiredStringArray(p, "methods"), GetOptionalString(p, "target"), cancellationToken),
            "cdp.next" => await state.CdpNextAsync(GetRequiredString(p, "id"), GetOptionalInt32(p, "timeoutMs") ?? 5000, GetOptionalInt32(p, "limit") ?? 50, cancellationToken),
            "cdp.unsubscribe" => state.CdpUnsubscribe(GetRequiredString(p, "id")),
            "cdp.send" => await RawCdpAsync(p, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown RPC method '{request.Method}'.")
        };
    }

    private async Task<object> OpenTargetAsync(string url, CancellationToken cancellationToken)
    {
        var result = await cdp.SendAsync("Target.createTarget", new { url }, cancellationToken: cancellationToken);
        var targetId = result.GetProperty("targetId").GetString() ?? "";
        var target = (await state.ListTargetsAsync(cancellationToken)).FirstOrDefault(x => x.TargetId == targetId);
        return new { targetId, target };
    }

    private async Task<object> ClickAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var id = GetRequiredString(p, "id");
        var target = state.Inspect(id).Target;
        await state.ClickAsync(id, cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { id, surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> FillAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var id = GetRequiredString(p, "id");
        var target = state.Inspect(id).Target;
        await state.FillAsync(id, GetRequiredString(p, "text"), cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { id, surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> TypeAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var id = GetRequiredString(p, "id");
        var target = state.Inspect(id).Target;
        await state.TypeAsync(id, GetRequiredString(p, "text"), cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { id, surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> KeyAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        await state.KeyAsync(target, GetRequiredString(p, "key"), cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> ScrollAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        await state.ScrollAsync(target, GetOptionalDouble(p, "deltaX") ?? 0, GetOptionalDouble(p, "deltaY") ?? 0, cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> ElementActionAsync(JsonElement p, Func<string, CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        var id = GetRequiredString(p, "id");
        var target = state.Inspect(id).Target;
        await action(id, cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { id, surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> UploadAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var id = GetRequiredString(p, "id");
        await state.UploadFilesAsync(id, GetRequiredStringArray(p, "files"), cancellationToken);
        var target = state.Inspect(id).Target;
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { id, surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<object> WaitAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        var expression = GetRequiredString(p, "expression");
        var timeoutMs = GetOptionalInt32(p, "timeoutMs") ?? 5000;
        var intervalMs = GetOptionalInt32(p, "intervalMs") ?? 100;
        var started = DateTimeOffset.UtcNow;
        var matched = await state.WaitUntilAsync(target, expression, timeoutMs, intervalMs, cancellationToken);
        return new { matched, elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, target, expression };
    }

    private async Task<object> WaitAnyAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var target = GetRequiredString(p, "target");
        var expressions = GetRequiredStringArray(p, "expressions");
        var matched = await state.WaitAnyAsync(target, expressions, GetOptionalInt32(p, "timeoutMs") ?? 5000, GetOptionalInt32(p, "intervalMs") ?? 100, cancellationToken);
        return new { matched, elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, target, expressions };
    }

    private async Task<object> WaitAllAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var target = GetRequiredString(p, "target");
        var expressions = GetRequiredStringArray(p, "expressions");
        var matched = await state.WaitAllAsync(target, expressions, GetOptionalInt32(p, "timeoutMs") ?? 5000, GetOptionalInt32(p, "intervalMs") ?? 100, cancellationToken);
        return new { matched, elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, target, expressions };
    }

    private async Task<object> WaitSequenceAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var target = GetRequiredString(p, "target");
        var expressions = GetRequiredStringArray(p, "expressions");
        var matched = await state.WaitSequenceAsync(target, expressions, GetOptionalInt32(p, "timeoutMs") ?? 5000, GetOptionalInt32(p, "intervalMs") ?? 100, cancellationToken);
        return new { matched, complete = matched.Count == expressions.Count && matched.All(x => x), elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, target, expressions };
    }

    private async Task<object> WaitQuietAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var target = GetRequiredString(p, "target");
        var quietMs = GetRequiredInt32(p, "quietMs");
        var matched = await state.WaitQuietForAsync(target, quietMs, GetOptionalInt32(p, "timeoutMs") ?? Math.Max(5000, quietMs), cancellationToken);
        return new { matched, elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds, target, quietMs };
    }

    private async Task<object> NetworkSearchAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        var contains = GetOptionalString(p, "contains");
        var method = GetOptionalString(p, "method");
        int? status = p.TryGetProperty("status", out var statusValue) && statusValue.TryGetInt32(out var statusInt) ? statusInt : null;
        return await state.NetworkSearchAsync(target, contains, method, status, GetOptionalInt32(p, "limit") ?? 50, cancellationToken);
    }

    private async Task<object> WebMcpExecuteAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var input = p.TryGetProperty("input", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(new { });
        return await state.WebMcpExecuteAsync(
            GetRequiredString(p, "target"),
            GetRequiredString(p, "name"),
            input,
            GetOptionalString(p, "frameId"),
            GetOptionalInt32(p, "timeoutMs") ?? 30000,
            cancellationToken);
    }

    private async Task<object> RuntimeToolExecuteAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var input = p.TryGetProperty("input", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(new { });
        return await state.RuntimeToolsExecuteAsync(
            GetRequiredString(p, "target"),
            GetRequiredString(p, "name"),
            input,
            GetOptionalString(p, "group"),
            cancellationToken);
    }

    private async Task<JsonElement> RawCdpAsync(JsonElement p, CancellationToken cancellationToken)
    {
        JsonElement? parameters = p.TryGetProperty("params", out var raw) && raw.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? raw.Clone() : null;
        return await state.RawCdpAsync(GetRequiredString(p, "method"), parameters, GetOptionalString(p, "target"), cancellationToken);
    }

    private static ElementQuery ParseQuery(JsonElement p) => new(
        Target: GetOptionalString(p, "target"),
        Role: GetOptionalString(p, "role"),
        Name: GetOptionalString(p, "name"),
        Contains: GetOptionalString(p, "contains"),
        Limit: GetOptionalInt32(p, "limit") ?? 50);

    private static string GetRequiredString(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new ArgumentException($"String parameter '{name}' is required.");

    private static string? GetOptionalString(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetRequiredInt64(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetInt64(out var result)
            ? result
            : throw new ArgumentException($"Integer parameter '{name}' is required.");

    private static int GetRequiredInt32(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw new ArgumentException($"Integer parameter '{name}' is required.");

    private static int? GetOptionalInt32(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static double? GetOptionalDouble(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetDouble(out var result)
            ? result
            : null;

    private static IReadOnlyList<string> GetRequiredStringArray(JsonElement p, string name)
    {
        if (p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"Array parameter '{name}' is required.");
        var items = value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
        if (items.Length == 0) throw new ArgumentException($"Array parameter '{name}' must contain at least one string.");
        return items;
    }
}
internal sealed class PipeRpcServer(string pipeName, KernelRpcDispatcher dispatcher)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(pipe, cancellationToken), CancellationToken.None);
            }
            catch
            {
                await pipe.DisposeAsync();
                throw;
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken serverCancellationToken)
    {
        await using (pipe)
        using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 64 * 1024, leaveOpen: true))
        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 64 * 1024, leaveOpen: true) { AutoFlush = true })
        {
            while (pipe.IsConnected && !serverCancellationToken.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync(serverCancellationToken); }
                catch (OperationCanceledException) { return; }
                if (line is null) return;

                long id = 0;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    id = root.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var parsed) ? parsed : 0;
                    var method = root.GetProperty("method").GetString() ?? throw new ArgumentException("RPC method is required.");
                    var parameters = root.TryGetProperty("params", out var p)
                        ? p.Clone()
                        : JsonSerializer.SerializeToElement(new { });
                    var result = await dispatcher.DispatchAsync(new RpcRequest(id, method, parameters), serverCancellationToken);
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { id, ok = true, result }));
                }
                catch (Exception ex)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        id,
                        ok = false,
                        error = new { type = ex.GetType().Name, message = ex.Message }
                    }));
                }
            }
        }
    }
}

internal static class PipeRpcClient
{
    public static async Task<JsonElement> CallAsync(
        string pipeName,
        string method,
        JsonElement parameters,
        CancellationToken cancellationToken = default)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(5000, cancellationToken);
        using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 64 * 1024, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 64 * 1024, leaveOpen: true) { AutoFlush = true };
        var id = DateTime.UtcNow.Ticks;
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
        var line = await reader.ReadLineAsync(cancellationToken) ?? throw new EndOfStreamException("Kernel closed RPC pipe without a response.");
        using var document = JsonDocument.Parse(line);
        return document.RootElement.Clone();
    }
}
