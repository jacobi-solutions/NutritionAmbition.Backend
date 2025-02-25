using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class UpdateFoodEntryRequest : Request
    {
        [Required]
        public string FoodEntryId { get; set; }

        public string Description { get; set; }
        public List<FoodItem> ParsedItems { get; set; }
    }
} 