using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace AgentBrowser.Cdp;

public sealed class CdpClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _receiveLoop;
    private long _nextId;

    public bool IsConnected => _socket.State == WebSocketState.Open;

    public event Func<JsonElement, Task>? EventReceived;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        if (_socket.State != WebSocketState.None)
            throw new InvalidOperationException($"CDP socket is already {_socket.State}.");

        await _socket.ConnectAsync(endpoint, cancellationToken);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_lifetime.Token));
    }

    public async Task<JsonElement> SendAsync(
        string method,
        object? parameters = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("CDP socket is not connected.");

        var id = Interlocked.Increment(ref _nextId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
            throw new InvalidOperationException("Unable to register CDP command.");

        var message = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["method"] = method
        };
        if (parameters is not null)
            message["params"] = parameters;
        if (!string.IsNullOrWhiteSpace(sessionId))
            message["sessionId"] = sessionId;

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonDefaults.Options);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }

        using var registration = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(id, out var tcs))
                tcs.TrySetCanceled(cancellationToken);
        });

        return await completion.Task;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   _socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        if (_socket.State == WebSocketState.CloseReceived)
                            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", cancellationToken);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                using var document = JsonDocument.Parse(message.ToArray());
                var root = document.RootElement;

                if (root.TryGetProperty("id", out var idElement) &&
                    idElement.TryGetInt64(out var id) &&
                    _pending.TryRemove(id, out var completion))
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        completion.TrySetException(new CdpException(error.Clone()));
                    }
                    else if (root.TryGetProperty("result", out var commandResult))
                    {
                        completion.TrySetResult(commandResult.Clone());
                    }
                    else
                    {
                        completion.TrySetResult(JsonSerializer.SerializeToElement(new { }));
                    }

                    continue;
                }

                var handler = EventReceived;
                if (handler is not null)
                    await handler(root.Clone());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            foreach (var pair in _pending.ToArray())
            {
                if (_pending.TryRemove(pair.Key, out var completion))
                    completion.TrySetException(ex);
            }
        }
        finally
        {
            foreach (var pair in _pending.ToArray())
            {
                if (_pending.TryRemove(pair.Key, out var completion))
                    completion.TrySetException(new WebSocketException("CDP receive loop ended."));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();

        try
        {
            if (_socket.State == WebSocketState.Open)
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "disposed", CancellationToken.None);
        }
        catch
        {
        }

        if (_receiveLoop is not null)
        {
            try { await _receiveLoop; } catch { }
        }

        _socket.Dispose();
        _sendLock.Dispose();
        _lifetime.Dispose();
    }
}

public sealed class CdpException(JsonElement error) : Exception(BuildMessage(error))
{
    public JsonElement Error { get; } = error;

    private static string BuildMessage(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("message", out var message))
            return $"CDP error: {message.GetString()}";

        return $"CDP error: {error}";
    }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
