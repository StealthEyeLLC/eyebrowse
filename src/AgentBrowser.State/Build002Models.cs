using System.Text.Json;

namespace AgentBrowser.State;

public static class IdentityOutcomes
{
    public const string Exact = "exact";
    public const string Rebound = "rebound";
    public const string Stale = "stale";
    public const string Ambiguous = "ambiguous";
}

public static class LifecycleStates
{
    public const string Unknown = "unknown";
    public const string Active = "active";
    public const string Prerender = "prerender";
    public const string Cached = "cached";
    public const string Frozen = "frozen";
    public const string Discarded = "discarded";
    public const string Unavailable = "unavailable";
}

public static class CognitionStates
{
    public const string Hot = "hot";
    public const string Warm = "warm";
    public const string Cold = "cold";
}

public sealed record BrowserCurrentContext(
    string? Target,
    string? Document,
    string Lifecycle,
    string Url,
    string Origin,
    string Title,
    string? CanonicalUrl,
    string? Focus,
    string? SelectedConcept,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> AvailableProviders,
    bool Ambiguous = false,
    string? AmbiguityReason = null);

public sealed record TargetCognitionInfo(
    string Target,
    string TargetId,
    string State,
    bool Attached,
    DateTimeOffset? LastHotAtUtc,
    int SemanticObjectCount,
    int NetworkRequestCount);

public sealed record DocumentLifecycleInfo(
    string Target,
    string? Document,
    string Lifecycle,
    string? FrameId,
    string? LoaderId,
    string? DocumentToken,
    long RendererIncarnation,
    long ExecutionRealmIncarnation,
    bool JavaScriptAvailable,
    DateTimeOffset UpdatedAtUtc,
    string? LastReason = null);

public sealed record ElementIdentityResolution(
    string Id,
    string Outcome,
    int Incarnation,
    string? Target,
    string? Document,
    int? BackendNodeId,
    IReadOnlyList<string>? Candidates = null,
    string? Reason = null);

public sealed record ConsoleEntry(
    long Id,
    string Target,
    string Kind,
    string Level,
    string Text,
    string? Url,
    int? Line,
    int? Column,
    DateTimeOffset AtUtc,
    string? Stack = null,
    string? NetworkRequestId = null);

public sealed record BrowserException(
    long Id,
    string Target,
    string Text,
    string? Url,
    int? Line,
    int? Column,
    DateTimeOffset AtUtc,
    string? Stack = null);

public sealed record DownloadInfo(
    string Id,
    string Guid,
    string Url,
    string? SuggestedFilename,
    string State,
    long ReceivedBytes,
    long TotalBytes,
    string? FrameId,
    string? Path,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ArtifactInfo(
    string Id,
    string Type,
    string Path,
    long Size,
    string? Target,
    string? Source,
    DateTimeOffset CreatedAtUtc);

public sealed record WebMcpToolInfo(
    string Target,
    string FrameId,
    string Name,
    string Description,
    JsonElement InputSchema,
    JsonElement? Annotations,
    int? BackendNodeId);

public sealed record WebMcpInvocationResult(
    string InvocationId,
    string Status,
    JsonElement? Output,
    string? ErrorText);

public sealed record RuntimeToolInfo(
    string Target,
    string Document,
    string Group,
    string? GroupDescription,
    string Name,
    string Description,
    JsonElement InputSchema);

public sealed record RuntimeToolExecutionResult(
    string Target,
    string Document,
    string Name,
    JsonElement? Value,
    string? Element,
    int? BackendNodeId,
    string? Type,
    string? Description);

public sealed record RebindingCandidate(
    string Id,
    int Incarnation,
    int BackendNodeId,
    string Role,
    string Name,
    string? Description,
    string? Value,
    IReadOnlyDictionary<string, string>? Attributes);

public sealed record RebindingDecision(
    string Outcome,
    string? Id,
    int Incarnation,
    int Score,
    IReadOnlyList<string> Candidates,
    string Reason);

public static class ConservativeRebinding
{
    private static readonly string[] StrongAttributeNames =
    [
        "id", "name", "data-testid", "data-test", "data-key", "data-id", "href", "for", "aria-controls"
    ];

    public static RebindingDecision Resolve(
        RebindingCandidate current,
        IEnumerable<RebindingCandidate> previous,
        IReadOnlySet<int>? currentBackendNodeIds = null)
    {
        var scored = previous
            .Where(x => x.BackendNodeId != current.BackendNodeId)
            .Where(x => currentBackendNodeIds is null || !currentBackendNodeIds.Contains(x.BackendNodeId))
            .Select(x => (Candidate: x, Score: Score(current, x)))
            .Where(x => x.Score >= 70)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .ToArray();

        if (scored.Length == 0)
            return new RebindingDecision(IdentityOutcomes.Stale, null, 1, 0, Array.Empty<string>(), "No strong successor evidence.");

        var top = scored[0];
        var contenders = scored.Where(x => top.Score - x.Score < 25).ToArray();
        if (contenders.Length != 1)
            return new RebindingDecision(
                IdentityOutcomes.Ambiguous,
                null,
                1,
                top.Score,
                contenders.Select(x => x.Candidate.Id).ToArray(),
                "Multiple plausible successor concepts remain within the conservative score margin.");

        return new RebindingDecision(
            IdentityOutcomes.Rebound,
            top.Candidate.Id,
            checked(top.Candidate.Incarnation + 1),
            top.Score,
            new[] { top.Candidate.Id },
            "Unique strong semantic successor evidence.");
    }

    private static int Score(RebindingCandidate a, RebindingCandidate b)
    {
        if (!string.Equals(a.Role, b.Role, StringComparison.OrdinalIgnoreCase))
            return 0;

        var score = 20;
        if (!string.IsNullOrWhiteSpace(a.Name) && string.Equals(a.Name, b.Name, StringComparison.Ordinal))
            score += 30;
        if (!string.IsNullOrWhiteSpace(a.Description) && string.Equals(a.Description, b.Description, StringComparison.Ordinal))
            score += 10;
        if (!string.IsNullOrWhiteSpace(a.Value) && string.Equals(a.Value, b.Value, StringComparison.Ordinal))
            score += 5;

        foreach (var key in StrongAttributeNames)
        {
            var av = Attribute(a.Attributes, key);
            var bv = Attribute(b.Attributes, key);
            if (!string.IsNullOrWhiteSpace(av) && string.Equals(av, bv, StringComparison.Ordinal))
                score += key is "id" or "data-testid" or "data-key" or "data-id" ? 55 : 35;
        }

        return score;
    }

    private static string? Attribute(IReadOnlyDictionary<string, string>? attributes, string name) =>
        attributes is not null && attributes.TryGetValue(name, out var value) ? value : null;
}