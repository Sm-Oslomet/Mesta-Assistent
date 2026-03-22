using AiAssistant.Api.Infrastructure.Search;
using AiAssistant.Api.Utils;

namespace AiAssistant.Api.Services;

public sealed class RetrievalService
{
    private readonly IAzureSearchClient _search;
    private readonly IConfiguration _config;

    public RetrievalService(IAzureSearchClient search, IConfiguration config)
    {
        _search = search;
        _config = config;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK, CancellationToken ct)
    {
        var contentField = _config["AzureSearch:ContentField"] ?? "content_text";
        var titleField = _config["AzureSearch:TitleField"] ?? "document_title";
        var documentIdField = _config["AzureSearch:DocumentIdField"] ?? "text_document_id";

        var urlFieldsRaw = _config["AzureSearch:UrlField"] ?? "sharepoint_url,content_path";
        var urlFields = urlFieldsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var chunks = await _search.SearchAsync(query, topK, contentField, titleField, urlFields, documentIdField, ct);

        return chunks
            .Select(c => new RetrievedChunk(
                c.Content,
                c.Title,
                SharePointUrlMapper.ToSharePointUrl(c.Url),
                c.DocumentId))
            .ToList();
    }
}