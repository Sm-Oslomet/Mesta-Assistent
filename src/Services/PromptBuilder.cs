using System.Text;

namespace AiAssistant.Api.Services;

public sealed class PromptBuilder
{
    private const int MaxChunkChars = 900;
    private const int MaxTotalSourceChars = 4500;

    public string BuildSystemPrompt() =>
        "You are a helpful assistant answering questions about Mesta work processes and system usage. " +
        "Use ONLY the provided sources. Do not use outside knowledge. " +
        "If the sources clearly support an answer, give the best direct answer in Norwegian. " +
        "For 'how' questions, prefer a short step-by-step explanation. " +
        "For definition or overview questions, give a short summary with the most important points first. " +
        "If the sources are incomplete but still useful, give the best possible answer and state any uncertainty briefly. " +
        "Only say that you do not know if the sources do not contain enough information to answer at all. " +
        "Ignore irrelevant or repeated source text. " +
        "Do not include citation markers like [S1] or [S2]. " +
        "Use the provided sources to answer, but write a clean, natural answer without referencing source numbers. " +
        "Do not add a separate source section; file links are handled by the system. " +
        "Keep the answer clear, grounded, and concise.";

    public string BuildAnswerStyleInstruction(string question)
    {
        var q = (question ?? string.Empty).Trim().ToLowerInvariant();

        if (q.StartsWith("hvordan") || q.Contains("hvordan "))
        {
            return "Answer as a short step-by-step guide in Norwegian. Start with the direct answer, then list the main steps. Cite sources inline.";
        }

        if (q.StartsWith("hva er") || q.StartsWith("hva betyr"))
        {
            return "Answer in Norwegian with a short definition first, then the most important supporting details. Cite sources inline.";
        }

        return "Answer in Norwegian as clearly and directly as possible. If the sources support practical guidance, structure the answer in short bullets or steps. Cite sources inline.";
    }

    public string BuildSourcesBlock(IReadOnlyList<RetrievedChunk> chunks)
    {
        var sb = new StringBuilder();
        var usedChars = 0;

        for (int i = 0; i < chunks.Count; i++)
        {
            if (usedChars >= MaxTotalSourceChars) break;

            var c = chunks[i];
            var content = c.Content ?? string.Empty;

            if (content.Length > MaxChunkChars)
                content = content[..MaxChunkChars] + "…";

            var remaining = MaxTotalSourceChars - usedChars;
            if (remaining <= 0) break;

            if (content.Length > remaining)
                content = content[..remaining] + "…";

            sb.AppendLine($"[S{i + 1}] {c.Title ?? "(untitled)"}");

            if (!string.IsNullOrWhiteSpace(c.Url))
                sb.AppendLine($"URL: {c.Url}");

            sb.AppendLine(content);
            sb.AppendLine();

            usedChars += content.Length;
        }

        return sb.ToString();
    }
}

public sealed record RetrievedChunk(
    string Content,
    string? Title,
    string? Url,
    string? DocumentId
);