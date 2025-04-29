using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Response for updating a food entry
    public class UpdateFoodEntryResponse : Response
    {
        public FoodEntry? UpdatedEntry { get; set; }
    }
}

