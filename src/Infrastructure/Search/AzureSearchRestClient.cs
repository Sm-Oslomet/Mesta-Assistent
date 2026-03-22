using AiAssistant.Api.Services;
using AiAssistant.Api.Utils;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AiAssistant.Api.Infrastructure.Search;

public sealed class AzureSearchRestClient : IAzureSearchClient
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly SystemTextJson _json;

    public AzureSearchRestClient(IHttpClientFactory http, IConfiguration config, SystemTextJson json)
    {
        _http = http;
        _config = config;
        _json = json;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string queryText,
        int topK,
        string contentField,
        string titleField,
        IReadOnlyList<string> urlFields,
        string documentIdField,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            return Array.Empty<RetrievedChunk>();

        var endpointRaw = Require("AzureSearch:Endpoint");
        var apiKey = Require("AzureSearch:ApiKey");
        var indexName = Require("AzureSearch:IndexName");

        var apiVersion = (_config["AzureSearch:ApiVersion"] ?? "2023-11-01").Trim();
        if (string.IsNullOrWhiteSpace(apiVersion))
            apiVersion = "2023-11-01";

        var semanticConfig =
            _config["AzureSearch:SemanticConfiguration"]
            ?? "multimodal-rag-index-ai-assistent-semantic-configuration";

        var endpoint = endpointRaw.Trim().TrimEnd('/');
        if (!endpoint.Contains(".search.windows.net", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"AzureSearch:Endpoint must be https://<name>.search.windows.net but was: '{endpointRaw}'");

        var ub = new UriBuilder(endpoint);
        var basePath = (ub.Path ?? string.Empty).TrimEnd('/');
        ub.Path = $"{basePath}/indexes/{indexName}/docs/search";
        ub.Query = $"api-version={Uri.EscapeDataString(apiVersion)}";
        var url = ub.Uri.ToString();

        var effectiveUrlFields = (urlFields ?? Array.Empty<string>())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selectFields = new[] { contentField, titleField, documentIdField }
            .Concat(effectiveUrlFields)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var semanticPayload = new
        {
            search = queryText,
            top = topK,
            queryType = "semantic",
            semanticConfiguration = semanticConfig,
            captions = "extractive|highlight-false",
            answers = "extractive|count-3",
            select = string.Join(',', selectFields)
        };

        var semanticResults = await SendAndParseAsync(
            url, apiKey, semanticPayload, contentField, titleField, effectiveUrlFields, documentIdField, ct);
        if (semanticResults.Count > 0)
            return semanticResults;

        var simplePayload = new
        {
            search = queryText,
            top = topK,
            select = string.Join(',', selectFields)
        };

        var simpleResults = await SendAndParseAsync(
            url, apiKey, simplePayload, contentField, titleField, effectiveUrlFields, documentIdField, ct);
        if (simpleResults.Count > 0)
            return simpleResults;

        var wildcardPayload = new
        {
            search = "*",
            top = topK,
            select = string.Join(',', selectFields)
        };

        return await SendAndParseAsync(
            url, apiKey, wildcardPayload, contentField, titleField, effectiveUrlFields, documentIdField, ct);
    }

    private async Task<List<RetrievedChunk>> SendAndParseAsync(
        string url,
        string apiKey,
        object payload,
        string contentField,
        string titleField,
        IReadOnlyList<string> urlFields,
        string documentIdField,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.TryAddWithoutValidation("api-key", apiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload, _json.Options),
            Encoding.UTF8,
            "application/json");

        var client = _http.CreateClient();
        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Azure AI Search query failed. URL: {url}. Status: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.Array)
            return new List<RetrievedChunk>();

        var results = new List<RetrievedChunk>();

        foreach (var item in valueEl.EnumerateArray())
        {
            var content = TryGetString(item, contentField) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                continue;

            var title = TryGetString(item, titleField);
            var documentId = TryGetString(item, documentIdField);

            string? urlVal = null;
            foreach (var f in urlFields)
            {
                var val = TryGetString(item, f);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    urlVal = val;
                    break;
                }
            }

            results.Add(new RetrievedChunk(content, title, urlVal, documentId));
        }

        return results;
    }

    private static string? TryGetString(JsonElement obj, string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        if (!obj.TryGetProperty(field, out var el)) return null;

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => el.ToString()
        };
    }

    private string Require(string key)
        => _config[key] ?? throw new InvalidOperationException($"Missing configuration: {key}");
}