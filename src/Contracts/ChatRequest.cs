namespace AiAssistant.Api.Contracts;

/// <summary>
/// Stateless by default. For multi-turn chat without a database,
/// send the last N messages (role: system|user|assistant).
/// </summary>
public sealed record ChatRequest(
    string Question,
    IReadOnlyList<ChatMessageDto>? Messages = null,
    int? TopK = null
);

public sealed record ChatMessageDto(string Role, string Content);
