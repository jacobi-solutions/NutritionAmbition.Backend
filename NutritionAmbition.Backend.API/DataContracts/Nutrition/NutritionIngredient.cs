using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutritionIngredient : Request
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("quantity")]
        public string Quantity { get; set; } = string.Empty;
        
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }
}
