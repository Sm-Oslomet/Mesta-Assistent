using AiAssistant.Api.Services;

namespace AiAssistant.Api.Infrastructure.Search;

public interface IAzureSearchClient
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string queryText,
        int topK,
        string contentField,
        string titleField,
        IReadOnlyList<string> urlFields,
        string documentIdField,
        CancellationToken ct);
}