using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetDetailedSummaryRequest : Request
    {
        public DateTime LoggedDateUtc { get; set; }
    }
} 