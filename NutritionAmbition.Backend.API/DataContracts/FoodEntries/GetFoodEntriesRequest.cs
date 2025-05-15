using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for retrieving food entries
    public class GetFoodEntriesRequest : Request
    {
        public DateTime? LoggedDateUtc { get; set; } // Optional: Filter by specific date
        public MealType? Meal { get; set; } // Optional: Filter by meal type
        // Add other filters as needed (e.g., date range)
    }
}

