namespace AgentBrowser.State;

public sealed record BrowserTarget(
    string Id,
    string TargetId,
    string Type,
    string Title,
    string Url,
    bool Attached,
    string? OpenerId);

public sealed record ProviderStats(
    int AccessibilityNodes,
    int SemanticObjects,
    int DomSnapshotDocuments,
    int DomSnapshotNodes,
    bool ApcAvailable);

public sealed record SemanticElement(
    string Id,
    string Target,
    string Document,
    int BackendNodeId,
    string? AxNodeId,
    string Role,
    string Name,
    string? Description,
    string? Value,
    bool Disabled,
    bool Focused,
    IReadOnlyList<string> Actions);

public sealed record SemanticSurface(
    long Cursor,
    string Target,
    string TargetId,
    string Document,
    string FrameId,
    string? LoaderId,
    string Url,
    string Title,
    DateTimeOffset CapturedAtUtc,
    ProviderStats Providers,
    IReadOnlyList<SemanticElement> Elements);

public sealed record SemanticChange(
    string Id,
    SemanticElement? Before,
    SemanticElement? After);

public sealed record SemanticDelta(
    long Since,
    long Cursor,
    string Target,
    string Document,
    IReadOnlyList<SemanticElement> Added,
    IReadOnlyList<SemanticElement> Removed,
    IReadOnlyList<SemanticChange> Changed);

public sealed record ElementQuery(
    string? Target = null,
    string? Role = null,
    string? Name = null,
    string? Contains = null,
    int Limit = 50);
