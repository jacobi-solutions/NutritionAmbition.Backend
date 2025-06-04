namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class OverrideDailyGoalsRequest : Request
    {
        public double NewBaseCalories { get; set; }
    }
} 