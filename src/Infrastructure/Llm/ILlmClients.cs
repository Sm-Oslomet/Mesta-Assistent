namespace AiAssistant.Api.Infrastructure.Llm;

public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string input, CancellationToken ct);
}

public interface IChatCompletionClient
{
    Task<string> CompleteAsync(IReadOnlyList<LlmChatMessage> messages, CancellationToken ct);
}

public sealed record LlmChatMessage(string Role, string Content);
