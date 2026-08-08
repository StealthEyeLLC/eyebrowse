namespace AgentBrowser.State;

public sealed record NetworkRequestSummary(
    string Id,
    string Target,
    string? Document,
    string RawRequestId,
    string Url,
    string Method,
    string ResourceType,
    string? InitiatorType,
    int? Status,
    string? MimeType,
    bool Completed,
    bool Failed,
    string? ErrorText,
    long? EncodedDataLength,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record NetworkBody(
    string Id,
    string RawRequestId,
    string Body,
    bool Base64Encoded);