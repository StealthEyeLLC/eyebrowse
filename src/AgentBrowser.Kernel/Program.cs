using System.Text.Json;
using AgentBrowser.Cdp;
using AgentBrowser.Kernel;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    try
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "start" => await StartAsync(),
            "status" => await StatusAsync(),
            "protocol" => await ProtocolAsync(),
            "targets" => await TargetsAsync(),
            "open" => await OpenAsync(args.Skip(1).ToArray()),
            "eval" => await EvalAsync(args.Skip(1).ToArray()),
            "serve" => await ServeAsync(),
            "raw" => await RawAsync(args.Skip(1).ToArray()),
            _ => Unknown(args[0])
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 2;
    }
}

static async Task<int> StartAsync()
{
    var runtime = await BrowserRuntime.StartOrAttachAsync();
    var protocol = await CdpDiscovery.GetProtocolSummaryAsync(runtime.Port);
    PrintJson(new
    {
        ok = true,
        runtime.ProfileName,
        runtime.UserDataDir,
        runtime.Port,
        runtime.BrowserId,
        runtime.BrowserVersion,
        runtime.ProtocolVersion,
        protocolDomains = protocol.DomainCount,
        runtime.LaunchPid
    });
    return 0;
}

static async Task<int> StatusAsync()
{
    var runtime = await BrowserRuntime.TryReadLiveDescriptorAsync();
    if (runtime is null)
    {
        PrintJson(new { ok = false, live = false });
        return 3;
    }

    PrintJson(new
    {
        ok = true,
        live = true,
        runtime.ProfileName,
        runtime.Port,
        runtime.BrowserId,
        runtime.BrowserVersion,
        runtime.ProtocolVersion,
        runtime.LaunchPid
    });
    return 0;
}

static async Task<int> ProtocolAsync()
{
    var runtime = await RequireRuntimeAsync();
    var protocol = await CdpDiscovery.GetProtocolSummaryAsync(runtime.Port);
    PrintJson(new
    {
        ok = true,
        version = $"{protocol.Major}.{protocol.Minor}",
        domainCount = protocol.DomainCount,
        domains = protocol.Domains
    });
    return 0;
}

static async Task<int> TargetsAsync()
{
    var runtime = await RequireRuntimeAsync();
    await using var cdp = await ConnectAsync(runtime);
    var result = await cdp.SendAsync("Target.getTargets");
    var targets = result.GetProperty("targetInfos")
        .EnumerateArray()
        .Select(x => new
        {
            targetId = x.GetProperty("targetId").GetString(),
            type = x.GetProperty("type").GetString(),
            title = x.GetProperty("title").GetString(),
            url = x.GetProperty("url").GetString(),
            attached = x.GetProperty("attached").GetBoolean(),
            openerId = x.TryGetProperty("openerId", out var opener) ? opener.GetString() : null
        })
        .ToArray();
    PrintJson(new { ok = true, runtime.BrowserId, targets });
    return 0;
}

static async Task<int> OpenAsync(string[] args)
{
    if (args.Length < 1)
        throw new ArgumentException("Usage: open <url>");

    var runtime = await RequireRuntimeAsync();
    await using var cdp = await ConnectAsync(runtime);
    var result = await cdp.SendAsync("Target.createTarget", new { url = args[0] });
    var targetId = result.GetProperty("targetId").GetString();
    PrintJson(new { ok = true, runtime.BrowserId, targetId, url = args[0] });
    return 0;
}

static async Task<int> EvalAsync(string[] args)
{
    if (args.Length < 2)
        throw new ArgumentException("Usage: eval <targetId> <expression>");

    var targetId = args[0];
    var expression = string.Join(' ', args.Skip(1));
    var runtime = await RequireRuntimeAsync();
    await using var cdp = await ConnectAsync(runtime);

    var attach = await cdp.SendAsync("Target.attachToTarget", new { targetId, flatten = true });
    var sessionId = attach.GetProperty("sessionId").GetString()
        ?? throw new InvalidOperationException("Target.attachToTarget did not return a sessionId.");

    await cdp.SendAsync("Runtime.enable", null, sessionId);
    var evaluation = await cdp.SendAsync(
        "Runtime.evaluate",
        new
        {
            expression,
            returnByValue = true,
            awaitPromise = true,
            userGesture = true
        },
        sessionId);

    try { await cdp.SendAsync("Target.detachFromTarget", new { sessionId }); } catch { }

    PrintJson(new
    {
        ok = !evaluation.TryGetProperty("exceptionDetails", out _),
        runtime.BrowserId,
        targetId,
        result = evaluation
    });
    return 0;
}

static async Task<int> RawAsync(string[] args)
{
    if (args.Length < 1)
        throw new ArgumentException("Usage: raw <method> [params-json]");

    var runtime = await RequireRuntimeAsync();
    await using var cdp = await ConnectAsync(runtime);
    object? parameters = null;
    JsonDocument? document = null;

    if (args.Length >= 2)
    {
        document = JsonDocument.Parse(args[1]);
        parameters = document.RootElement.Clone();
    }

    var result = await cdp.SendAsync(args[0], parameters);
    document?.Dispose();
    PrintJson(new { ok = true, method = args[0], result });
    return 0;
}

static async Task<int> ServeAsync()
{
    var runtime = await BrowserRuntime.StartOrAttachAsync();
    await using var cdp = await ConnectAsync(runtime);

    var eventCount = 0L;
    cdp.EventReceived += _ =>
    {
        Interlocked.Increment(ref eventCount);
        return Task.CompletedTask;
    };

    await cdp.SendAsync("Target.setDiscoverTargets", new { discover = true });
    await cdp.SendAsync("Target.setAutoAttach", new
    {
        autoAttach = true,
        waitForDebuggerOnStart = false,
        flatten = true
    });

    var version = await cdp.SendAsync("Browser.getVersion");
    await BrowserRuntime.WriteKernelDescriptorAsync(runtime);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        ready = true,
        pid = Environment.ProcessId,
        runtime.BrowserId,
        runtime.Port,
        product = version.GetProperty("product").GetString()
    }));
    Console.Out.Flush();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    while (!cts.IsCancellationRequested && cdp.IsConnected)
    {
        try { await Task.Delay(1000, cts.Token); }
        catch (OperationCanceledException) { }
    }

    PrintJson(new { stopped = true, events = eventCount });
    return 0;
}

static async Task<BrowserRuntimeDescriptor> RequireRuntimeAsync()
{
    return await BrowserRuntime.TryReadLiveDescriptorAsync()
        ?? throw new InvalidOperationException("No live eyebrowse Chrome runtime. Run 'start' first.");
}

static async Task<CdpClient> ConnectAsync(BrowserRuntimeDescriptor runtime)
{
    var cdp = new CdpClient();
    await cdp.ConnectAsync(new Uri(runtime.BrowserWebSocketUrl));
    return cdp;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("eyebrowse Build 001 kernel");
    Console.WriteLine("Commands: start | status | protocol | targets | open <url> | eval <targetId> <expression> | raw <method> [params-json] | serve");
}

static void PrintJson(object value)
{
    Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}
