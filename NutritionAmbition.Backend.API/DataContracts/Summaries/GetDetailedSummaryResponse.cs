using System.Collections.Generic;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetDetailedSummaryResponse : Response
    {
        public List<NutrientBreakdown> Nutrients { get; set; } = new List<NutrientBreakdown>();
        public List<FoodBreakdown> Foods { get; set; } = new List<FoodBreakdown>();
        public SummaryTotals SummaryTotals { get; set; } = new();
    }
} 