using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ParseFoodTextRequest : Request
    {
        [Required]
        public string FoodDescription { get; set; } = string.Empty;
    }
}
