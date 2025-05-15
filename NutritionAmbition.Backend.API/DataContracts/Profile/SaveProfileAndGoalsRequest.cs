
namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SaveProfileAndGoalsRequest : Request
    {
        public string AccountId { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Sex { get; set; } = string.Empty;
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public string ActivityLevel { get; set; } = "moderate"; // options: sedentary, light, moderate, active, very active
    }
} 