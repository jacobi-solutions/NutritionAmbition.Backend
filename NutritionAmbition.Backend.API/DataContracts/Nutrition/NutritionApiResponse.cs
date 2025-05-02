using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutritionApiResponse : Response
    {
        [JsonPropertyName("foods")]
        public List<FoodNutrition> Foods { get; set; } = new List<FoodNutrition>();

        // 🟢 Add property for AI coach response
        [JsonPropertyName("aiCoachResponse")]
        public string? AiCoachResponse { get; set; }
        
        // Property to track source of nutrition data
        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public class FoodNutrition : Response
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("brandName")]
        public string? BrandName { get; set; }
        
        [JsonPropertyName("quantity")]
        public string Quantity { get; set; } = string.Empty;
        
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
        
        [JsonPropertyName("calories")]
        public double Calories { get; set; }
        
        [JsonPropertyName("macronutrients")]
        public Macronutrients Macronutrients { get; set; } = new Macronutrients();
        
        [JsonPropertyName("micronutrients")]
        public Dictionary<string, Micronutrient> Micronutrients { get; set; } = new Dictionary<string, Micronutrient>();
    }

    public class Macronutrients : Response
    {
        [JsonPropertyName("protein")]
        public NutrientInfo Protein { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("carbohydrates")]
        public NutrientInfo Carbohydrates { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("fat")]
        public NutrientInfo Fat { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("fiber")]
        public NutrientInfo Fiber { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("sugar")]
        public NutrientInfo Sugar { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("saturated_fat")]
        public NutrientInfo SaturatedFat { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("unsaturated_fat")]
        public NutrientInfo UnsaturatedFat { get; set; } = new NutrientInfo();
        
        [JsonPropertyName("trans_fat")]
        public NutrientInfo TransFat { get; set; } = new NutrientInfo();
    }

    public class Micronutrient
    {
        [JsonPropertyName("amount")]
        public double Amount { get; set; }
        
        [JsonPropertyName("unit")]
        public string Unit { get; set; } = string.Empty;
        
        [JsonPropertyName("daily_value_percent")]
        public double? DailyValuePercent { get; set; }
    }

    public class NutrientInfo
    {
        [JsonPropertyName("amount")]
        public double Amount { get; set; }
        
        [JsonPropertyName("unit")]
        public string Unit { get; set; } = "g";
        
        [JsonPropertyName("daily_value_percent")]
        public double? DailyValuePercent { get; set; }
    }
}

