using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class PostLogHintRequest : Request
    {
        public DateTime? LastLoggedDate { get; set; }
        public bool HasLoggedFirstMeal { get; set; }
    }
} 