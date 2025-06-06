namespace NutritionAmbition.Backend.API.DataContracts.Profile
{
    public class GetProfileAndGoalsResponse : Response
    {
        public int? Age { get; set; }
        public string? Sex { get; set; }
        public int? HeightFeet { get; set; }
        public int? HeightInches { get; set; }
        public double? WeightLbs { get; set; }
        public string? ActivityLevel { get; set; }
        public double? BaseCalories { get; set; }
        public bool HasGoals { get; set; }
    }
} 