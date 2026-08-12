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
            "call" => await CallAsync(args.Skip(1).ToArray()),
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
        apc = protocol.Supports("Page.getAnnotatedPageContent"),
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
        domains = protocol.Domains,
        apc = protocol.Supports("Page.getAnnotatedPageContent")
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
    PrintJson(new { ok = true, runtime.BrowserId, targetId = result.GetProperty("targetId").GetString(), url = args[0] });
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
    await cdp.SendAsync("Runtime.enable", sessionId: sessionId);
    var evaluation = await cdp.SendAsync("Runtime.evaluate", new
    {
        expression,
        returnByValue = true,
        awaitPromise = true,
        userGesture = true
    }, sessionId);
    try { await cdp.SendAsync("Target.detachFromTarget", new { sessionId }); } catch { }
    PrintJson(new { ok = !evaluation.TryGetProperty("exceptionDetails", out _), runtime.BrowserId, targetId, result = evaluation });
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

static async Task<int> CallAsync(string[] args)
{
    if (args.Length < 1)
        throw new ArgumentException("Usage: call <method> [params-json]");

    JsonElement parameters;
    if (args.Length >= 2)
    {
        using var document = JsonDocument.Parse(args[1]);
        parameters = document.RootElement.Clone();
    }
    else
    {
        parameters = JsonSerializer.SerializeToElement(new { });
    }

    var response = await PipeRpcClient.CallAsync(BrowserRuntime.PipeName, args[0], parameters);
    Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    return response.TryGetProperty("ok", out var ok) && ok.GetBoolean() ? 0 : 4;
}

static async Task<int> ServeAsync()
{
    var runtime = await BrowserRuntime.StartOrAttachAsync();
    await using var cdp = await ConnectAsync(runtime);
    var protocol = await CdpDiscovery.GetProtocolSummaryAsync(runtime.Port);
    await using var state = new BrowserStateEngine(cdp, protocol);
    await state.InitializeAsync();
    var dispatcher = new KernelRpcDispatcher(runtime, cdp, state);
    var server = new PipeRpcServer(BrowserRuntime.PipeName, dispatcher);
    var version = await cdp.SendAsync("Browser.getVersion");
    await BrowserRuntime.WriteKernelDescriptorAsync(runtime);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        ready = true,
        pid = Environment.ProcessId,
        pipe = BrowserRuntime.PipeName,
        runtime.BrowserId,
        runtime.Port,
        product = version.GetProperty("product").GetString(),
        apc = protocol.Supports("Page.getAnnotatedPageContent")
    }));
    Console.Out.Flush();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    try
    {
        await server.RunAsync(cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
    }
    return 0;
}

static async Task<BrowserRuntimeDescriptor> RequireRuntimeAsync() =>
    await BrowserRuntime.TryReadLiveDescriptorAsync()
        ?? throw new InvalidOperationException("No live eyebrowse Chrome runtime. Run 'start' first.");

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
    Console.WriteLine("eyeBROWSE Build 002 candidate kernel (Build 001-compatible defaults)");
    Console.WriteLine("Commands: start | status | protocol | targets | open <url> | eval <targetId> <expression> | raw <method> [params-json] | call <rpc-method> [params-json] | serve");
}

static void PrintJson(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
