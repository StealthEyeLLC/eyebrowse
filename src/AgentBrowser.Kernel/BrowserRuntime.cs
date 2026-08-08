using System.Diagnostics;
using System.Text.Json;
using AgentBrowser.Cdp;

namespace AgentBrowser.Kernel;

internal sealed record BrowserRuntimeDescriptor(
    string ProfileName,
    string UserDataDir,
    string ChromePath,
    int Port,
    string BrowserWebSocketUrl,
    string BrowserId,
    int? LaunchPid,
    DateTimeOffset AttachedAtUtc,
    string BrowserVersion,
    string ProtocolVersion);

internal static class BrowserRuntime
{
    public const string ProfileName = "dev";
    public const string UserDataDir = @"C:\AgentBrowser\Profiles\dev";
    public const string RuntimeDir = @"C:\AgentBrowser\runtime";
    public const string RuntimePath = @"C:\AgentBrowser\runtime\dev.json";
    public const string KernelRuntimePath = @"C:\AgentBrowser\runtime\kernel-dev.json";
    public const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<BrowserRuntimeDescriptor> StartOrAttachAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(UserDataDir);
        Directory.CreateDirectory(RuntimeDir);

        var existing = await TryReadLiveDescriptorAsync(cancellationToken);
        if (existing is not null)
            return existing with { AttachedAtUtc = DateTimeOffset.UtcNow };

        var activePortPath = Path.Combine(UserDataDir, "DevToolsActivePort");
        try
        {
            if (File.Exists(activePortPath))
                File.Delete(activePortPath);
        }
        catch
        {
        }

        if (!File.Exists(ChromePath))
            throw new FileNotFoundException("Chrome executable not found.", ChromePath);

        var arguments = string.Join(' ', new[]
        {
            $"--user-data-dir=\"{UserDataDir}\"",
            "--remote-debugging-port=0",
            "--remote-debugging-address=127.0.0.1",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-session-crashed-bubble",
            "--new-window",
            "about:blank"
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = ChromePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(ChromePath)!
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Chrome.");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(activePortPath))
            {
                try
                {
                    var lines = await File.ReadAllLinesAsync(activePortPath, cancellationToken);
                    if (lines.Length >= 1 && int.TryParse(lines[0].Trim(), out var port))
                    {
                        var endpoint = await CdpDiscovery.DiscoverAsync(port, cancellationToken);
                        var descriptor = new BrowserRuntimeDescriptor(
                            ProfileName,
                            UserDataDir,
                            ChromePath,
                            port,
                            endpoint.BrowserWebSocketUrl,
                            endpoint.BrowserId,
                            process.Id,
                            DateTimeOffset.UtcNow,
                            endpoint.Browser,
                            endpoint.ProtocolVersion);

                        await WriteDescriptorAsync(descriptor, cancellationToken);
                        return descriptor;
                    }
                }
                catch when (DateTimeOffset.UtcNow < deadline)
                {
                }
            }

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Chrome did not publish a usable DevToolsActivePort within 15 seconds.");
    }

    public static async Task<BrowserRuntimeDescriptor?> TryReadLiveDescriptorAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(RuntimePath))
                return null;

            var descriptor = JsonSerializer.Deserialize<BrowserRuntimeDescriptor>(
                await File.ReadAllTextAsync(RuntimePath, cancellationToken),
                JsonOptions);

            if (descriptor is null || !await CdpDiscovery.IsAliveAsync(descriptor.Port, cancellationToken))
                return null;

            var endpoint = await CdpDiscovery.DiscoverAsync(descriptor.Port, cancellationToken);
            if (!string.Equals(endpoint.BrowserId, descriptor.BrowserId, StringComparison.Ordinal))
                return null;

            return descriptor with
            {
                BrowserWebSocketUrl = endpoint.BrowserWebSocketUrl,
                BrowserVersion = endpoint.Browser,
                ProtocolVersion = endpoint.ProtocolVersion
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task WriteDescriptorAsync(
        BrowserRuntimeDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RuntimeDir);
        await File.WriteAllTextAsync(
            RuntimePath,
            JsonSerializer.Serialize(descriptor, JsonOptions),
            cancellationToken);
    }

    public static async Task WriteKernelDescriptorAsync(
        BrowserRuntimeDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(RuntimeDir);
        var state = new
        {
            pid = Environment.ProcessId,
            startedAtUtc = DateTimeOffset.UtcNow,
            browserId = descriptor.BrowserId,
            browserWebSocketUrl = descriptor.BrowserWebSocketUrl,
            port = descriptor.Port,
            profileName = descriptor.ProfileName
        };
        await File.WriteAllTextAsync(
            KernelRuntimePath,
            JsonSerializer.Serialize(state, JsonOptions),
            cancellationToken);
    }
}
