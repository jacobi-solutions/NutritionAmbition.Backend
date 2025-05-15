using System;

namespace NutritionAmbition.Backend.API.Models
{
    public class NutrientGoal 
    {
        public string NutrientName { get; set; } = string.Empty; // e.g., "Saturated Fat", "Vitamin D"
        public double? MaxValue { get; set; } // fixed upper limit (e.g., 13g)
        public double? MinValue { get; set; } // optional lower limit
        public double? PercentageOfCalories { get; set; } // e.g., 0.06 for 6% of 2000 kcal
        public string Unit { get; set; } = "g"; // default to grams
    }
} 