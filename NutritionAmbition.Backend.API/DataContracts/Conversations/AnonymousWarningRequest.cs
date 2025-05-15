using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class AnonymousWarningRequest : Request
    {
        public DateTime? LastLoggedDate { get; set; }
        public bool HasLoggedFirstMeal { get; set; }
    }
} 