using AiAssistant.Api.Contracts;
using AiAssistant.Api.Infrastructure.Llm;

namespace AiAssistant.Api.Services;

public sealed class ChatService
{
    private const int MaxHistoryMessages = 8;
    private readonly RetrievalService _retrieval;
    private readonly PromptBuilder _prompt;
    private readonly IChatCompletionClient _chat;

    public ChatService(RetrievalService retrieval, PromptBuilder prompt, IChatCompletionClient chat)
    {
        _retrieval = retrieval;
        _prompt = prompt;
        _chat = chat;
    }

    public async Task<ChatResponse> AskAsync(ChatRequest req, CancellationToken ct)
    {
        var topK = req.TopK ?? 6;
        if (topK is < 1 or > 10) topK = 6;

        var chunks = await _retrieval.RetrieveAsync(req.Question, topK, ct);

        var system = _prompt.BuildSystemPrompt();
        var sources = _prompt.BuildSourcesBlock(chunks);
        var answerStyleInstruction = _prompt.BuildAnswerStyleInstruction(req.Question);

        var messages = new List<LlmChatMessage>
        {
            new("system", system),
            new("system", answerStyleInstruction),
            new("system", "SOURCES:\n" + sources)
        };

        if (req.Messages is { Count: > 0 })
        {
            foreach (var m in req.Messages.TakeLast(MaxHistoryMessages))
            {
                var role = (m.Role ?? "").Trim().ToLowerInvariant();
                if (role is not ("system" or "user" or "assistant")) continue;
                if (string.IsNullOrWhiteSpace(m.Content)) continue;
                messages.Add(new LlmChatMessage(role, m.Content));
            }
        }

        var normalizedQuestion = req.Question.Trim();
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage is null || !string.Equals(lastUserMessage.Content.Trim(), normalizedQuestion, StringComparison.Ordinal))

            messages.Add(new("user", req.Question));

        var answer = await _chat.CompleteAsync(messages, ct);

        if (string.IsNullOrWhiteSpace(answer))
            answer = BuildGroundedFallbackAnswer(req.Question, chunks);

        var src = chunks.Select(c => new SourceHit(
            Title: c.Title,
            Url: c.Url,
            ContentSnippet: c.Content.Length <= 240 ? c.Content : c.Content[..240] + "…"
        )).ToList();

        return new ChatResponse(answer, src);
    }

    private static string BuildGroundedFallbackAnswer(string question, IReadOnlyList<RetrievedChunk> chunks)
    {
        if (chunks.Count == 0)
            return "Jeg finner ingen relevante kilder akkurat nå. Prøv å omformulere spørsmålet.";

        static string Clean(string text)
            => text.Replace("\r", " ").Replace("\n", " ").Trim();

        var highlights = chunks
            .Select(c => Clean(c.Content))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Take(2)
            .Select(c => c.Length <= 260 ? c : c[..260] + "…")
            .ToList();

        if (highlights.Count == 0)
            return "Jeg fant relevante kilder, men teksten kunne ikke oppsummeres automatisk. Prøv å omformulere spørsmålet.";

        var joined = string.Join("\n- ", highlights);
        return $"Jeg fant relevante kilder for spørsmålet \"{question.Trim()}\". Her er det viktigste jeg fant:\n- {joined}";
    }
}
