using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class SearchInstantResponse
    {
        [JsonPropertyName("branded")]
        public List<NutritionixFood> Branded { get; set; } = new List<NutritionixFood>();

        [JsonPropertyName("common")]
        public List<NutritionixFood> Common { get; set; } = new List<NutritionixFood>();
    }
}
