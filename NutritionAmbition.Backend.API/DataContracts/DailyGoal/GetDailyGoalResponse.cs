using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetDailyGoalResponse : Response
    {
        public DailyGoal DailyGoal { get; set; }
    }
} 