using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class UpdateFoodEntryResponse : Response
    {
        public FoodEntry UpdatedEntry { get; set; }
    }
} 