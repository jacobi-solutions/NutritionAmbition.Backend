using System;
using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for creating a new food entry
    public class CreateFoodEntryRequest : Request
    {
        public string Description { get; set; } = string.Empty;
        public MealType Meal { get; set; } = MealType.Unknown;
        public DateTime LoggedDateUtc { get; set; } = DateTime.UtcNow;
        public List<FoodItem> ParsedItems { get; set; } = new List<FoodItem>();
    }

    // Response for creating a new food entry
    public class CreateFoodEntryResponse : Response
    {
        public FoodEntry? FoodEntry { get; set; }
    }

    // Request for retrieving food entries
    public class GetFoodEntriesRequest : Request
    {
        public DateTime? LoggedDateUtc { get; set; } // Optional: Filter by specific date
        public MealType? Meal { get; set; } // Optional: Filter by meal type
        // Add other filters as needed (e.g., date range)
    }

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

    // Request for updating a food entry
    public class UpdateFoodEntryRequest : Request
    {
        public string FoodEntryId { get; set; } = string.Empty;
        public string? Description { get; set; }
        public MealType? Meal { get; set; }
        public DateTime? LoggedDateUtc { get; set; }
        public List<FoodItem>? ParsedItems { get; set; }
    }

    // Response for updating a food entry
    public class UpdateFoodEntryResponse : Response
    {
        public FoodEntry? UpdatedEntry { get; set; }
    }

    // Request for deleting a food entry
    public class DeleteFoodEntryRequest : Request
    {
        public string FoodEntryId { get; set; } = string.Empty;
    }

    // Response for deleting a food entry
    public class DeleteFoodEntryResponse : Response
    {
        // Inherits Success and Errors from base Response class
    }
}

