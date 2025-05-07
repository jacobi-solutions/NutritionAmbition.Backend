using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutritionixResponse
    {
        [JsonPropertyName("foods")]
        public List<NutritionixFood> Foods { get; set; } = new List<NutritionixFood>();
    }
}
