using System;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    public class DailyGoal : Model
    {
        public string AccountId { get; set; }
        public DateTime EffectiveDateUtc { get; set; } = DateTime.UtcNow.Date;
        public double BaseCalories { get; set; } = 2000; // daily default
        public List<NutrientGoal> NutrientGoals { get; set; } = new();
    }
} 