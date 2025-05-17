using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetInitialMessageRequest : Request
    {
        public DateTime? LastLoggedDate { get; set; }
        public bool HasLoggedFirstMeal { get; set; }
        public int? TimezoneOffsetMinutes { get; set; }
    }
} 