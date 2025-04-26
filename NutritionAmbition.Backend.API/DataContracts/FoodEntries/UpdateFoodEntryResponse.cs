using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class UpdateFoodEntryResponse : Response
    {
        public FoodEntry UpdatedEntry { get; set; }
    }
} 