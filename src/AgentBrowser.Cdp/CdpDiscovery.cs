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

public sealed record ProtocolFacet(
    string Name,
    string Kind,
    bool Experimental,
    bool Deprecated);

public sealed record ProtocolSummary(
    int DomainCount,
    string? Major,
    string? Minor,
    IReadOnlyList<string> Domains,
    IReadOnlySet<string> Commands)
{
    public IReadOnlySet<string> Events { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, ProtocolFacet> Facets { get; init; } = new Dictionary<string, ProtocolFacet>(StringComparer.Ordinal);

    public bool Supports(string qualifiedCommand) => Commands.Contains(qualifiedCommand);
    public bool HasDomain(string domain) => Domains.Contains(domain, StringComparer.Ordinal);
    public ProtocolFacet? Describe(string qualifiedName) => Facets.TryGetValue(qualifiedName, out var value) ? value : null;
}

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
        return ParseProtocolSummary(document.RootElement);
    }

    public static ProtocolSummary ParseProtocolSummary(JsonElement root)
    {
        var domainElements = root.GetProperty("domains").EnumerateArray().ToArray();
        var domains = domainElements
            .Select(x => x.GetProperty("domain").GetString() ?? "")
            .Where(x => x.Length > 0)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var commands = new HashSet<string>(StringComparer.Ordinal);
        var events = new HashSet<string>(StringComparer.Ordinal);
        var facets = new Dictionary<string, ProtocolFacet>(StringComparer.Ordinal);

        foreach (var domain in domainElements)
        {
            var domainName = domain.GetProperty("domain").GetString();
            if (string.IsNullOrWhiteSpace(domainName)) continue;
            facets[domainName] = new ProtocolFacet(
                domainName,
                "domain",
                Bool(domain, "experimental"),
                Bool(domain, "deprecated"));

            if (domain.TryGetProperty("commands", out var domainCommands) && domainCommands.ValueKind == JsonValueKind.Array)
            {
                foreach (var command in domainCommands.EnumerateArray())
                {
                    var commandName = command.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(commandName)) continue;
                    var qualified = $"{domainName}.{commandName}";
                    commands.Add(qualified);
                    facets[qualified] = new ProtocolFacet(qualified, "command", Bool(command, "experimental"), Bool(command, "deprecated"));
                }
            }

            if (domain.TryGetProperty("events", out var domainEvents) && domainEvents.ValueKind == JsonValueKind.Array)
            {
                foreach (var evt in domainEvents.EnumerateArray())
                {
                    var eventName = evt.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(eventName)) continue;
                    var qualified = $"{domainName}.{eventName}";
                    events.Add(qualified);
                    facets[qualified] = new ProtocolFacet(qualified, "event", Bool(evt, "experimental"), Bool(evt, "deprecated"));
                }
            }
        }

        string? major = null;
        string? minor = null;
        if (root.TryGetProperty("version", out var version))
        {
            if (version.TryGetProperty("major", out var majorElement)) major = majorElement.GetString();
            if (version.TryGetProperty("minor", out var minorElement)) minor = minorElement.GetString();
        }

        return new ProtocolSummary(domains.Length, major, minor, domains, commands)
        {
            Events = events,
            Facets = facets
        };
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

    private static bool Bool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
