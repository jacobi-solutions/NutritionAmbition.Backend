using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for updating a food entry
    public class UpdateFoodEntryRequest : Request
    {
        public string FoodEntryId { get; set; } = string.Empty;
        public string? Description { get; set; }
        public MealType? Meal { get; set; }
        public DateTime? LoggedDateUtc { get; set; }
        public List<FoodItem>? ParsedItems { get; set; }
    }
}

