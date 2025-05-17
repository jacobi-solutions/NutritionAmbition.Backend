namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class OverrideDailyGoalsRequest : Request
    {
        public string AccountId { get; set; }
        public double NewBaseCalories { get; set; }
    }
} 