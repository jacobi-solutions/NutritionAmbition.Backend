using System;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class NutritionSummaryResponse
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double TotalCalories { get; set; }
        public MacronutrientsSummary Macronutrients { get; set; }
        public IDictionary<string, double> Micronutrients { get; set; }
    }
} 