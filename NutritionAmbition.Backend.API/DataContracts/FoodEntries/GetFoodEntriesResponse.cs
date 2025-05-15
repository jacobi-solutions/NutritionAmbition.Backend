using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Response for retrieving food entries
    public class GetFoodEntriesResponse : Response
    {
        public List<FoodEntry> FoodEntries { get; set; } = new List<FoodEntry>();
        // Add summary data if needed
        public double? TotalCalories { get; set; }
        public double? TotalProtein { get; set; }
        public double? TotalCarbs { get; set; }
        public double? TotalFat { get; set; }
    }
}

