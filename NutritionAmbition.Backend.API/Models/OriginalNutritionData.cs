using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    /// <summary>
    /// Stores the original nutrition data as received from Nutritionix API
    /// to support future rescaling without requiring additional API calls
    /// </summary>
    public class OriginalNutritionData
    {
        public double Calories { get; set; } = 0;
        public double Protein { get; set; } = 0.0;
        public double Carbohydrates { get; set; } = 0.0;
        public double Fat { get; set; } = 0.0;
        public double Fiber { get; set; } = 0.0;
        public double Sugar { get; set; } = 0.0;
        public double SaturatedFat { get; set; } = 0.0;
        public double UnsaturatedFat { get; set; } = 0.0;
        public double TransFat { get; set; } = 0.0;
        public Dictionary<string, double> Micronutrients { get; set; } = new Dictionary<string, double>();
        
        // API serving metadata for rescaling
        public double? ApiServingQty { get; set; }
        public string? ApiServingUnit { get; set; }
        public double? ApiServingWeightG { get; set; }
    }
} 