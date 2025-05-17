namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SetDefaultGoalProfileRequest : Request
    {
        public string AccountId { get; set; }
        public double BaseCalories { get; set; }
    }
} 