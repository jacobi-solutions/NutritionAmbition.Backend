using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutrientBreakdown
    {
        public string Name { get; set; }
        public double TotalAmount { get; set; }
        public string Unit { get; set; }
        public List<FoodContribution> Foods { get; set; } = new List<FoodContribution>();
    }
} 