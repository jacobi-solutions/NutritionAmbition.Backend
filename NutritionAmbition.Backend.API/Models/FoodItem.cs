using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    public class FoodItem
    {
        public string Name { get; set; } // Required
        public string? BrandName { get; set; }
        public double Quantity { get; set; } = 0.0;
        public string Unit { get; set; } = string.Empty;
        public int Calories { get; set; } = 0;
        public double Protein { get; set; } = 0.0;
        public double Carbohydrates { get; set; } = 0.0;
        public double Fat { get; set; } = 0.0;
        public double Fiber { get; set; } = 0.0;
        public double Sugar { get; set; } = 0.0;
        public double SaturatedFat { get; set; } = 0.0;
        public double UnsaturatedFat { get; set; } = 0.0;
        public double TransFat { get; set; } = 0.0;
        public Dictionary<string, double> Micronutrients { get; set; } = new Dictionary<string, double>();
    }

}