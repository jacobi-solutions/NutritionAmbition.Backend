using NutritionAmbition.Backend.API.Models;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SetDefaultGoalProfileRequest : Request
    {
        public double BaseCalories { get; set; }
        public List<NutrientGoal> NutrientGoals { get; set; } = new();
    }
} 