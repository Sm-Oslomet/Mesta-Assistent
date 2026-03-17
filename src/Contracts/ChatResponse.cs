namespace AiAssistant.Api.Contracts;

public sealed record ChatResponse(
    string Answer,
    IReadOnlyList<SourceHit> Sources
);

public sealed record SourceHit(
    string? Title,
    string? Url,
    string ContentSnippet
);
