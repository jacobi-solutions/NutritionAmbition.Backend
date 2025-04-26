using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class CreateFoodEntryResponse : Response
    {
        public FoodEntry FoodEntry { get; set; }
    }
} 