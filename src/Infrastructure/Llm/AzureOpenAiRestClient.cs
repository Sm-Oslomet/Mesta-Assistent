using AiAssistant.Api.Utils;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AiAssistant.Api.Infrastructure.Llm;

/// <summary>
/// Minimal Azure OpenAI REST client (no database).
/// Configure:
/// - AzureOpenAI:Endpoint (e.g. https://YOUR-RESOURCE.openai.azure.com)
/// - AzureOpenAI:ApiKey
/// - AzureOpenAI:ChatDeployment
/// - AzureOpenAI:EmbeddingDeployment
/// - AzureOpenAI:ApiVersion (optional)
/// </summary>
public sealed class AzureOpenAiRestClient : IEmbeddingClient, IChatCompletionClient
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly SystemTextJson _json;

    public AzureOpenAiRestClient(IHttpClientFactory http, IConfiguration config, SystemTextJson json)
    {
        _http = http;
        _config = config;
        _json = json;
    }

    public async Task<float[]> EmbedAsync(string input, CancellationToken ct)
    {
        var endpoint = Require("AzureOpenAI:Endpoint").TrimEnd('/');
        var key = Require("AzureOpenAI:ApiKey");
        var deployment = Require("AzureOpenAI:EmbeddingDeployment");
        var apiVersion = _config["AzureOpenAI:ApiVersion"] ?? "2024-02-15-preview";

        var url = $"{endpoint}/openai/deployments/{deployment}/embeddings?api-version={apiVersion}";

        var payload = new
        {
            input
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", key);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, _json.Options), Encoding.UTF8, "application/json");

        var client = _http.CreateClient();
        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI embeddings failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        var emb = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();

        return emb;
    }

    public async Task<string> CompleteAsync(IReadOnlyList<LlmChatMessage> messages, CancellationToken ct)
    {
        var endpoint = Require("AzureOpenAI:Endpoint").TrimEnd('/');
        var key = Require("AzureOpenAI:ApiKey");
        var deployment = Require("AzureOpenAI:ChatDeployment");
        var apiVersion = _config["AzureOpenAI:ApiVersion"] ?? "2024-02-15-preview";

        var url = $"{endpoint}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";

        /*
        Context window (input + output)  ~128,000 tokens
        input tokens + completion tokens ? context window
            Input prompt: 4,500 tokens 
            Max completion: 16,000 tokens
            Total: 20,500 tokens ? within 128k
         */

        var payload = new
        {
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = 1,
            max_completion_tokens = 16000
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("api-key", key);
        req.Content = new StringContent(JsonSerializer.Serialize(payload, _json.Options), Encoding.UTF8, "application/json");

        var client = _http.CreateClient();
        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Azure OpenAI chat failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        return ExtractChatContent(doc.RootElement);
    }

    private static string ExtractChatContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            return string.Empty;

        var firstChoice = choices[0];

        if (firstChoice.TryGetProperty("message", out var message))
        {
            if (message.TryGetProperty("content", out var contentElement))
            {
                var extracted = ExtractText(contentElement);
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }

            if (message.TryGetProperty("refusal", out var refusalElement))
            {
                var refusal = ExtractText(refusalElement);
                if (!string.IsNullOrWhiteSpace(refusal))
                    return refusal;
            }
        }

        if (firstChoice.TryGetProperty("text", out var fallbackText))
        {
            var extracted = ExtractText(fallbackText);
            if (!string.IsNullOrWhiteSpace(extracted))
                return extracted;
        }

        return string.Empty;
    }

    private static string ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? string.Empty;

        if (element.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var child in element.EnumerateArray())
                sb.Append(ExtractText(child));

            return sb.ToString();
        }

        if (element.ValueKind != JsonValueKind.Object)
            return string.Empty;

        // Typical text part shapes include:
        // { "type": "text", "text": "..." }
        // { "type": "output_text", "text": "..." }
        // { "text": { "value": "..." } }
        if (element.TryGetProperty("text", out var textValue))
        {
            var text = ExtractText(textValue);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        if (element.TryGetProperty("value", out var valueElement))
        {
            var value = ExtractText(valueElement);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        if (element.TryGetProperty("content", out var contentElement))
        {
            var content = ExtractText(contentElement);
            if (!string.IsNullOrWhiteSpace(content))
                return content;
        }
        return string.Empty;
    }

    private string Require(string key)
        => _config[key] ?? throw new InvalidOperationException($"Missing configuration: {key}");

    private int GetInt(string key, int defaultValue)
       => int.TryParse(_config[key], out var value) ? value : defaultValue;

    private double GetDouble(string key, double defaultValue)
        => double.TryParse(_config[key], out var value) ? value : defaultValue;
}
