using System;

namespace NutritionAmbition.Backend.API.Models
{
    public class CoachMessage : Model
    {
        public string AccountId { get; set; }
        public string? FoodEntryId { get; set; }
        public string Message { get; set; }
        public string Role { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
} 