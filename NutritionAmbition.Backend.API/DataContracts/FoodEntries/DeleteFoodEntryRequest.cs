using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class DeleteFoodEntryRequest : Request
    {

        [Required]
        public string FoodEntryId { get; set; }
    }
} 