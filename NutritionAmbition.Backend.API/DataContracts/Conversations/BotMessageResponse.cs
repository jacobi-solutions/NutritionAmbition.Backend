using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class BotMessageResponse : Response
    {
        public string Message { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
        public string? ResponseId { get; set; }
    }

    public class ToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("function")]
        public ToolFunctionCall Function { get; set; } = new ToolFunctionCall();
    }

    public class ToolFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string ArgumentsJson { get; set; } = "{}";
    }
} 