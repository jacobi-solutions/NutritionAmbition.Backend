using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class SetDailyGoalResponse : Response
    {
        public DailyGoal DailyGoal { get; set; }
    }
} 