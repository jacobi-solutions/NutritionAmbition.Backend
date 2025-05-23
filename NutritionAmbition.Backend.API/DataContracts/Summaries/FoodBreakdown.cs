using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class FoodBreakdown
    {
        public List<string> FoodItemIds { get; set; }
        public string Name { get; set; }
        public string BrandName { get; set; }
        public double TotalAmount { get; set; } // grams
        public string Unit { get; set; } = string.Empty; // The display unit for this food group (from first item's unit)
        public List<NutrientContribution> Nutrients { get; set; } = new List<NutrientContribution>();
    }
} 