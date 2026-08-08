using System.Net;
using System.Text.Json;

namespace AgentBrowser.Cdp;

public sealed record CdpEndpoint(
    int Port,
    string BrowserWebSocketUrl,
    string BrowserId,
    string Browser,
    string ProtocolVersion,
    string UserAgent);

public sealed record ProtocolSummary(
    int DomainCount,
    string? Major,
    string? Minor,
    IReadOnlyList<string> Domains);

public static class CdpDiscovery
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    public static async Task<CdpEndpoint> DiscoverAsync(int port, CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync($"http://127.0.0.1:{port}/json/version", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        var root = document.RootElement;

        var webSocket = root.GetProperty("webSocketDebuggerUrl").GetString()
            ?? throw new InvalidOperationException("Chrome did not expose webSocketDebuggerUrl.");
        var browserId = new Uri(webSocket).AbsolutePath.Split('/').Last();

        return new CdpEndpoint(
            port,
            webSocket,
            browserId,
            root.TryGetProperty("Browser", out var browser) ? browser.GetString() ?? "" : "",
            root.TryGetProperty("Protocol-Version", out var protocol) ? protocol.GetString() ?? "" : "",
            root.TryGetProperty("User-Agent", out var agent) ? agent.GetString() ?? "" : "");
    }

    public static async Task<ProtocolSummary> GetProtocolSummaryAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync($"http://127.0.0.1:{port}/json/protocol", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken));
        var root = document.RootElement;
        var domains = root.GetProperty("domains")
            .EnumerateArray()
            .Select(x => x.GetProperty("domain").GetString() ?? "")
            .Where(x => x.Length > 0)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        string? major = null;
        string? minor = null;
        if (root.TryGetProperty("version", out var version))
        {
            if (version.TryGetProperty("major", out var majorElement))
                major = majorElement.GetString();
            if (version.TryGetProperty("minor", out var minorElement))
                minor = minorElement.GetString();
        }

        return new ProtocolSummary(domains.Length, major, minor, domains);
    }

    public static async Task<bool> IsAliveAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"http://127.0.0.1:{port}/json/version", cancellationToken);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }
}
