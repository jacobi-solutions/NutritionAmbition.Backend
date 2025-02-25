using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class CreateFoodEntryRequest : Request
    {

        [Required]
        public string Description { get; set; }

        public List<FoodItem> ParsedItems { get; set; }
    }
} 