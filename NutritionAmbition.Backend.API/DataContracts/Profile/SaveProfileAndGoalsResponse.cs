using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SaveProfileAndGoalsResponse : Response
    {
        public bool IsCreated { get; set; }
        public DailyGoal? DailyGoal { get; set; }
    }
} 