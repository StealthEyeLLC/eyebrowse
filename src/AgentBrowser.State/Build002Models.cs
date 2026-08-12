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

    public static RebindingDecision ResolveAgainstSurface(
        RebindingCandidate current,
        IEnumerable<RebindingCandidate> previous,
        IEnumerable<RebindingCandidate> currentPeers,
        IReadOnlySet<int>? currentBackendNodeIds = null)
    {
        var previousArray = previous.ToArray();
        var initial = Resolve(current, previousArray, currentBackendNodeIds);
        if (initial.Outcome != IdentityOutcomes.Rebound || string.IsNullOrWhiteSpace(initial.Id))
            return initial;

        var prior = previousArray.FirstOrDefault(x => string.Equals(x.Id, initial.Id, StringComparison.Ordinal));
        if (prior is null)
            return new RebindingDecision(IdentityOutcomes.Stale, null, 1, 0, Array.Empty<string>(), "The proposed prior concept disappeared during surface adjudication.");

        var scoredSuccessors = currentPeers
            .Select(x => (Candidate: x, Score: Score(x, prior)))
            .Where(x => x.Score >= 70)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.BackendNodeId)
            .ToArray();

        if (scoredSuccessors.Length == 0)
            return new RebindingDecision(IdentityOutcomes.Stale, null, 1, 0, Array.Empty<string>(), "No current successor has strong evidence for the proposed prior concept.");

        var top = scoredSuccessors[0];
        var currentScore = scoredSuccessors.FirstOrDefault(x => x.Candidate.BackendNodeId == current.BackendNodeId).Score;
        if (currentScore < 70)
            return new RebindingDecision(IdentityOutcomes.Stale, null, 1, currentScore, Array.Empty<string>(), "Another current object may preserve the prior concept; this object does not have strong successor evidence.");

        var contenders = scoredSuccessors.Where(x => top.Score - x.Score < 25).ToArray();
        if (contenders.Length > 1 && contenders.Any(x => x.Candidate.BackendNodeId == current.BackendNodeId))
            return new RebindingDecision(
                IdentityOutcomes.Ambiguous,
                null,
                checked(prior.Incarnation + 1),
                top.Score,
                contenders.Select(x => $"backend:{x.Candidate.BackendNodeId}").ToArray(),
                "Multiple current browser objects are plausible successors to the same prior concept.");

        if (top.Candidate.BackendNodeId != current.BackendNodeId)
            return new RebindingDecision(
                IdentityOutcomes.Stale,
                null,
                checked(prior.Incarnation + 1),
                currentScore,
                new[] { $"backend:{top.Candidate.BackendNodeId}" },
                "A different current browser object has stronger successor evidence for the prior concept.");

        return initial;
    }
    public static int Score(RebindingCandidate a, RebindingCandidate b)
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
public sealed record BrowserDialogInfo(
    string Target,
    string Type,
    string Message,
    string? DefaultPrompt,
    string? Url,
    bool Open,
    DateTimeOffset OpenedAtUtc);

public sealed record PerformanceTimelineEntry(
    long Id,
    string Target,
    string Type,
    string Name,
    double Time,
    double Duration,
    JsonElement? Details,
    DateTimeOffset ObservedAtUtc);

public sealed record PerformanceTraceResult(
    ArtifactInfo Artifact,
    long Bytes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string TransferMode);

public sealed record MemorySnapshotResult(
    ArtifactInfo Artifact,
    string Target,
    long Bytes,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc);

public sealed record MemoryCurrentInfo(
    string Target,
    long? UsedHeapSize,
    long? TotalHeapSize,
    int? Documents,
    int? Nodes,
    int? JsEventListeners,
    JsonElement? SamplingProfile);

public sealed record ExtensionInfo(
    string Id,
    string Name,
    string Version,
    string Path,
    bool Enabled);

public sealed record AccessibilityElementInfo(
    string Element,
    string Target,
    string Document,
    string Role,
    string Name,
    JsonElement AxTree,
    IReadOnlyList<string> Issues);

public sealed record AccessibilityAuditResult(
    string Target,
    string Document,
    int SemanticObjects,
    int UnnamedInteractables,
    IReadOnlyList<string> UnnamedElements,
    IReadOnlyList<string> Issues);

public sealed record RuntimeScriptInfo(
    string Target,
    string ScriptId,
    string Url,
    string? SourceMapUrl,
    string Hash,
    bool IsModule,
    long? ExecutionContextId,
    DateTimeOffset ObservedAtUtc);

public sealed record RuntimePausedState(
    string Target,
    string Reason,
    JsonElement CallFrames,
    JsonElement? Data,
    DateTimeOffset AtUtc);

public sealed record NetworkDetail(
    NetworkRequestSummary Summary,
    JsonElement? RequestHeaders,
    JsonElement? ResponseHeaders,
    JsonElement? Timing,
    JsonElement? Initiator,
    string? RequestPostData,
    IReadOnlyList<string> RedirectChain,
    bool FromServiceWorker,
    bool FromDiskCache,
    bool FromPrefetchCache,
    string? Protocol,
    string? RemoteIpAddress,
    int? RemotePort,
    string? GraphQlOperationName,
    string? GraphQlOperationType);

public sealed record NetworkMessage(
    long Id,
    string Target,
    string RequestId,
    string Kind,
    string Direction,
    string? Opcode,
    string Data,
    DateTimeOffset AtUtc);

public sealed record CapabilityFacet(
    string Name,
    string Kind,
    bool Experimental,
    bool Deprecated);
