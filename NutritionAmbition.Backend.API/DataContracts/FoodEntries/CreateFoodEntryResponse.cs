using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Response for creating a new food entry
    public class CreateFoodEntryResponse : Response
    {
        public FoodEntry? FoodEntry { get; set; }
    }
}

