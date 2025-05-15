using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetDetailedSummaryResponse : Response
    {
        public List<NutrientBreakdown> Nutrients { get; set; } = new List<NutrientBreakdown>();
        public List<FoodBreakdown> Foods { get; set; } = new List<FoodBreakdown>();
    }
} 