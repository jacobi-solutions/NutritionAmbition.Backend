using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutritionApiRequest : Request
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
        
        [JsonPropertyName("ingredients")]
        public List<NutritionIngredient> Ingredients { get; set; } = new List<NutritionIngredient>();
    }
}
