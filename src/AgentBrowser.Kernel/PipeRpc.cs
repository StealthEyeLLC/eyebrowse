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
                runtime.Port,
                runtime.BrowserId,
                runtime.BrowserVersion,
                runtime.ProtocolVersion,
                kernelPid = Environment.ProcessId
            },
            "target.list" => await state.ListTargetsAsync(cancellationToken),
            "target.open" => await OpenTargetAsync(GetRequiredString(p, "url"), cancellationToken),
            "observe.surface" => await state.ObserveAsync(GetRequiredString(p, "target"), cancellationToken),
            "observe.delta" => await state.DeltaAsync(GetRequiredString(p, "target"), GetRequiredInt64(p, "since"), cancellationToken),
            "query.find" => await state.QueryAsync(ParseQuery(p), cancellationToken),
            "inspect.element" => state.Inspect(GetRequiredString(p, "id")),
            "action.click" => await ClickAsync(p, cancellationToken),
            "action.fill" => await FillAsync(p, cancellationToken),
            "action.type" => await TypeAsync(p, cancellationToken),
            "action.key" => await KeyAsync(p, cancellationToken),
            "action.scroll" => await ScrollAsync(p, cancellationToken),
            "js.evaluate" => await state.EvaluateAsync(GetRequiredString(p, "target"), GetRequiredString(p, "expression"), cancellationToken),
            "wait.until" => await WaitAsync(p, cancellationToken),
            "network.search" => await NetworkSearchAsync(p, cancellationToken),
            "network.body" => await state.NetworkBodyAsync(GetRequiredString(p, "id"), cancellationToken),
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

    private async Task<object> NetworkSearchAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        var contains = GetOptionalString(p, "contains");
        var method = GetOptionalString(p, "method");
        int? status = p.TryGetProperty("status", out var statusValue) && statusValue.TryGetInt32(out var statusInt) ? statusInt : null;
        var limit = p.TryGetProperty("limit", out var limitValue) && limitValue.TryGetInt32(out var limitInt) ? limitInt : 50;
        return await state.NetworkSearchAsync(target, contains, method, status, limit, cancellationToken);
    }
    private async Task<object> WaitAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        var expression = GetRequiredString(p, "expression");
        var timeoutMs = (int)(GetOptionalDouble(p, "timeoutMs") ?? 5000);
        var intervalMs = (int)(GetOptionalDouble(p, "intervalMs") ?? 100);
        var started = DateTimeOffset.UtcNow;
        var matched = await state.WaitUntilAsync(target, expression, timeoutMs, intervalMs, cancellationToken);
        return new
        {
            matched,
            elapsedMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            target,
            expression
        };
    }
    private async Task<object> ScrollAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var target = GetRequiredString(p, "target");
        var deltaX = GetOptionalDouble(p, "deltaX") ?? 0;
        var deltaY = GetOptionalDouble(p, "deltaY") ?? 0;
        await state.ScrollAsync(target, deltaX, deltaY, cancellationToken);
        var surface = await state.ObserveAsync(target, cancellationToken);
        return new { surface.Cursor, surface.Target, surface.Document };
    }

    private async Task<JsonElement> RawCdpAsync(JsonElement p, CancellationToken cancellationToken)
    {
        var method = GetRequiredString(p, "method");
        object? parameters = null;
        if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("params", out var raw) && raw.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            parameters = raw.Clone();
        return await cdp.SendAsync(method, parameters, cancellationToken: cancellationToken);
    }

    private static ElementQuery ParseQuery(JsonElement p) => new(
        Target: GetOptionalString(p, "target"),
        Role: GetOptionalString(p, "role"),
        Name: GetOptionalString(p, "name"),
        Contains: GetOptionalString(p, "contains"),
        Limit: p.TryGetProperty("limit", out var limit) && limit.TryGetInt32(out var n) ? n : 50);

    private static string GetRequiredString(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? throw new ArgumentException($"Parameter '{name}' is empty.")
            : throw new ArgumentException($"String parameter '{name}' is required.");

    private static string? GetOptionalString(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long GetRequiredInt64(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetInt64(out var result)
            ? result
            : throw new ArgumentException($"Integer parameter '{name}' is required.");

    private static double? GetOptionalDouble(JsonElement p, string name) =>
        p.ValueKind == JsonValueKind.Object && p.TryGetProperty(name, out var value) && value.TryGetDouble(out var result)
            ? result
            : null;
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
