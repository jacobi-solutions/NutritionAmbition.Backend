using NutritionAmbition.Backend.API.Models;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SetDefaultGoalProfileRequest : Request
    {
        public string AccountId { get; set; } = string.Empty;
        public double BaseCalories { get; set; }
        public List<NutrientGoal> NutrientGoals { get; set; } = new();
    }
} 