using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    public class DefaultGoalProfile : Model
    {
        public string AccountId { get; set; } = string.Empty;
        public double BaseCalories { get; set; } = 2000;
        public List<NutrientGoal> NutrientGoals { get; set; } = new();
    }
} 