using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class CreateFoodEntryResponse : Response
    {
        public FoodEntry FoodEntry { get; set; }
    }
} 