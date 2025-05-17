namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class SaveUserProfileRequest : Request
    {
        public string AccountId { get; set; }
        public int Age { get; set; }
        public string Sex { get; set; }
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public string ActivityLevel { get; set; }
    }
} 