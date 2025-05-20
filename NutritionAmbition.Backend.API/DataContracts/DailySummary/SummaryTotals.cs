

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class SummaryTotals
    {
        public int TotalCalories { get; set; }
        public MacronutrientsSummary Macronutrients { get; set; } = new();
    }
} 