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
    public const string DefaultProfileName = "dev";
    public const string DefaultUserDataDir = @"C:\AgentBrowser\Profiles\dev";
    public const string DefaultRuntimeDir = @"C:\AgentBrowser\runtime";
    public const string DefaultChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    public const string DefaultArtifactRoot = @"X:\AgentBrowser\Artifacts";

    public static string ProfileName => Env("EYEBROWSE_PROFILE_NAME", DefaultProfileName);
    public static string UserDataDir => Env("EYEBROWSE_USER_DATA_DIR", ProfileName == DefaultProfileName
        ? DefaultUserDataDir
        : Path.Combine(@"C:\AgentBrowser\Profiles", ProfileName));
    public static string RuntimeDir => Env("EYEBROWSE_RUNTIME_DIR", DefaultRuntimeDir);
    public static string RuntimePath => Path.Combine(RuntimeDir, $"{ProfileName}.json");
    public static string KernelRuntimePath => Path.Combine(RuntimeDir, $"kernel-{ProfileName}.json");
    public static string ChromePath => Env("EYEBROWSE_CHROME_PATH", DefaultChromePath);
    public static string PipeName => Env("EYEBROWSE_PIPE_NAME", $"eyebrowse-{ProfileName}");
    public static string ArtifactRoot => Env("EYEBROWSE_ARTIFACT_ROOT", DefaultArtifactRoot);
    public static string DownloadRoot => Env("EYEBROWSE_DOWNLOAD_ROOT", Path.Combine(ArtifactRoot, "downloads", ProfileName));
    public static string? ExtensionPath => NullIfWhiteSpace(Environment.GetEnvironmentVariable("EYEBROWSE_EXTENSION_PATH"));
    public static bool Headless => ParseBool(Environment.GetEnvironmentVariable("EYEBROWSE_HEADLESS"));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static async Task<BrowserRuntimeDescriptor> StartOrAttachAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(UserDataDir);
        Directory.CreateDirectory(RuntimeDir);
        Directory.CreateDirectory(ArtifactRoot);
        Directory.CreateDirectory(DownloadRoot);

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

        var arguments = new List<string>
        {
            $"--user-data-dir=\"{UserDataDir}\"",
            "--remote-debugging-port=0",
            "--remote-debugging-address=127.0.0.1",
            "--no-first-run",
            "--no-default-browser-check",
            "--disable-session-crashed-bubble"
        };

        if (Headless)
            arguments.Add("--headless=new");
        else
            arguments.Add("--new-window");

        if (!string.IsNullOrWhiteSpace(ExtensionPath))
        {
            if (!Directory.Exists(ExtensionPath))
                throw new DirectoryNotFoundException($"EYEBROWSE_EXTENSION_PATH does not exist: {ExtensionPath}");
            arguments.Add($"--disable-extensions-except=\"{ExtensionPath}\"");
            arguments.Add($"--load-extension=\"{ExtensionPath}\"");
        }

        arguments.Add("about:blank");

        var startInfo = new ProcessStartInfo
        {
            FileName = ChromePath,
            Arguments = string.Join(' ', arguments),
            UseShellExecute = false,
            CreateNoWindow = Headless,
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

            if (descriptor is null ||
                !string.Equals(descriptor.ProfileName, ProfileName, StringComparison.Ordinal) ||
                !PathEquals(descriptor.UserDataDir, UserDataDir) ||
                !await CdpDiscovery.IsAliveAsync(descriptor.Port, cancellationToken))
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
            profileName = descriptor.ProfileName,
            userDataDir = descriptor.UserDataDir,
            pipe = PipeName,
            artifactRoot = ArtifactRoot,
            downloadRoot = DownloadRoot
        };
        await File.WriteAllTextAsync(
            KernelRuntimePath,
            JsonSerializer.Serialize(state, JsonOptions),
            cancellationToken);
    }

    private static string Env(string name, string fallback) =>
        NullIfWhiteSpace(Environment.GetEnvironmentVariable(name)) ?? fallback;

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ParseBool(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
}