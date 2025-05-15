using System;
using System.Collections.Generic;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    // Request for creating a new food entry
    public class CreateFoodEntryRequest : Request
    {
        public string Description { get; set; } = string.Empty;
        public MealType Meal { get; set; } = MealType.Unknown;
        public DateTime LoggedDateUtc { get; set; } = DateTime.UtcNow;
        
        // 🟢 Replace ParsedItems with GroupedItems
        // public List<FoodItem> ParsedItems { get; set; } = new List<FoodItem>();
        public List<FoodGroup> GroupedItems { get; set; } = new List<FoodGroup>();
    }
}
