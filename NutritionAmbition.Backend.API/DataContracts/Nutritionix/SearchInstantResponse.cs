using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class SearchInstantResponse
    {
        [JsonPropertyName("branded")]
        public List<BrandedFoodItem> Branded { get; set; } = new List<BrandedFoodItem>();

        [JsonPropertyName("common")]
        public List<CommonFoodItem> Common { get; set; } = new List<CommonFoodItem>();
    }
}
