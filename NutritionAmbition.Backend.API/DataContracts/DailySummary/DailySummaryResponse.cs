using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class DailySummaryResponse : Response
    {
        public double TotalCalories { get; set; }
        public double TotalProtein { get; set; }
        public double TotalCarbohydrates { get; set; }
        public double TotalFat { get; set; }
        public double TotalSaturatedFat { get; set; }
        public Dictionary<string, double> TotalMicronutrients { get; set; } = new Dictionary<string, double>();
    }
} 