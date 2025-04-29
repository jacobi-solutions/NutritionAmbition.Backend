using System;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Models
{
    // Define MealType enum
    public enum MealType
    {
        Unknown,
        Breakfast,
        Lunch,
        Dinner,
        Snack
    }

    public class FoodEntry : Model
    {
        public string AccountId { get; set; } // Required
        public string Description { get; set; } // Required, raw user input
        public DateTime LoggedDateUtc { get; set; } = DateTime.UtcNow;
        public MealType Meal { get; set; } = MealType.Unknown; // Added MealType
        
        // 🟢 Replace ParsedItems with GroupedItems
        // public List<FoodItem> ParsedItems { get; set; } = new List<FoodItem>();
        public List<FoodGroup> GroupedItems { get; set; } = new List<FoodGroup>();
        
    }
} 

