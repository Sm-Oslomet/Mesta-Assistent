using AiAssistant.Api.Contracts;
using AiAssistant.Api.Infrastructure.Llm;
using System.Runtime.CompilerServices;

namespace AiAssistant.Api.Services;

public sealed class ChatService
{
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
        if (topK is < 1 or > 20) topK = 6;

        var chunks = await _retrieval.RetrieveAsync(req.Question, topK, ct);
        var distinctChunks = DistinctChunks(chunks);

        var system = _prompt.BuildSystemPrompt();
        var sources = _prompt.BuildSourcesBlock(distinctChunks);
        var messages = BuildMessages(req, system, sources);

        var answer = await _chat.CompleteAsync(messages, ct);

        if (string.IsNullOrWhiteSpace(answer))
            answer = BuildGroundedFallbackAnswer(req.Question, distinctChunks);

        var src = BuildSourceHits(distinctChunks);

        return new ChatResponse(answer, src);
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(
        ChatRequest req,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var topK = req.TopK ?? 6;
        if (topK is < 1 or > 20) topK = 6;

        var chunks = await _retrieval.RetrieveAsync(req.Question, topK, ct);
        var distinctChunks = DistinctChunks(chunks);

        var system = _prompt.BuildSystemPrompt();
        var sources = _prompt.BuildSourcesBlock(distinctChunks);
        var messages = BuildMessages(req, system, sources);

        var gotAnyContent = false;

        await foreach (var chunk in _chat.CompleteStreamingAsync(messages, ct).WithCancellation(ct))
        {
            if (string.IsNullOrWhiteSpace(chunk))
                continue;

            gotAnyContent = true;
            yield return chunk;
        }

        if (!gotAnyContent)
            yield return BuildGroundedFallbackAnswer(req.Question, distinctChunks);
    }

    public async Task<IReadOnlyList<SourceHit>> GetSourcesAsync(ChatRequest req, CancellationToken ct)
    {
        var topK = req.TopK ?? 6;
        if (topK is < 1 or > 20) topK = 6;

        var chunks = await _retrieval.RetrieveAsync(req.Question, topK, ct);
        var distinctChunks = DistinctChunks(chunks);

        return BuildSourceHits(distinctChunks);
    }

    private static List<LlmChatMessage> BuildMessages(ChatRequest req, string system, string sources)
    {
        var messages = new List<LlmChatMessage>
        {
            new("system", system),
            new("system", "SOURCES:\n" + sources)
        };

        if (req.Messages is { Count: > 0 })
        {
            foreach (var m in req.Messages)
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

        return messages;
    }

    private static List<RetrievedChunk> DistinctChunks(IReadOnlyList<RetrievedChunk> chunks)
    {
        return chunks
            .GroupBy(c =>
            {
                if (!string.IsNullOrWhiteSpace(c.DocumentId))
                    return $"doc:{c.DocumentId.Trim().ToLowerInvariant()}";

                if (!string.IsNullOrWhiteSpace(c.Url))
                    return $"url:{c.Url.Trim().ToLowerInvariant()}";

                return $"title:{(c.Title ?? string.Empty).Trim().ToLowerInvariant()}";
            })
            .Select(g => g.First())
            .ToList();
    }

    private static List<SourceHit> BuildSourceHits(IReadOnlyList<RetrievedChunk> distinctChunks)
    {
        return distinctChunks
            .GroupBy(c =>
            {
                if (!string.IsNullOrWhiteSpace(c.DocumentId))
                    return $"doc:{c.DocumentId.Trim().ToLowerInvariant()}";

                if (!string.IsNullOrWhiteSpace(c.Url))
                    return $"url:{c.Url.Trim().ToLowerInvariant()}";

                return $"title:{(c.Title ?? string.Empty).Trim().ToLowerInvariant()}";
            })
            .Select(g =>
            {
                var c = g.First();
                var displayTitle = c.Title;

                if (!string.IsNullOrWhiteSpace(c.Url) && !string.IsNullOrWhiteSpace(c.Title))
                {
                    if (c.Url.Contains("Brukerveiledning%2FRapporter%2F", StringComparison.OrdinalIgnoreCase))
                        displayTitle = $"{c.Title} — Rapporter";
                    else if (c.Url.Contains("Brukerveiledning%2FVinter%20og%20%C3%98kt-behandling%2F", StringComparison.OrdinalIgnoreCase))
                        displayTitle = $"{c.Title} — Vinter og Økt-behandling";
                }

                return new SourceHit(
                    Title: displayTitle,
                    Url: c.Url,
                    ContentSnippet: c.Content.Length <= 240 ? c.Content : c.Content[..240] + "…"
                );
            })
            .ToList();
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