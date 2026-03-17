using AiAssistant.Api.Infrastructure.Search;

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

    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK, CancellationToken ct)
    {
        // Your actual index fields:
        var contentField = _config["AzureSearch:ContentField"] ?? "content_text";
        var titleField = _config["AzureSearch:TitleField"] ?? "document_title";
        var urlField = _config["AzureSearch:UrlField"] ?? "content_path";

        return _search.SearchAsync(query, topK, contentField, titleField, urlField, ct);
    }
}
