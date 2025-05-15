using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class BotMessageRequest : Request
    {
        public DateTime? LastLoggedDate { get; set; }
        public bool HasLoggedFirstMeal { get; set; }
    }
} 