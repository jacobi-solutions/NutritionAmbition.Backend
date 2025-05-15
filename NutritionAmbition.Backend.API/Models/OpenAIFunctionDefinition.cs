using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.Models
{
    /// <summary>
    /// Represents a function definition for OpenAI Assistant API
    /// </summary>
    public class OpenAIFunctionDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("parameters")]
        public FunctionParameters Parameters { get; set; }
    }

    public class FunctionParameters
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, PropertyDefinition> Properties { get; set; } = new Dictionary<string, PropertyDefinition>();

        [JsonPropertyName("required")]
        public List<string> Required { get; set; } = new List<string>();
    }

    public class PropertyDefinition
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
} 