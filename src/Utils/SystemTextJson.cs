using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiAssistant.Api.Utils;

public sealed class SystemTextJson
{
    public JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
