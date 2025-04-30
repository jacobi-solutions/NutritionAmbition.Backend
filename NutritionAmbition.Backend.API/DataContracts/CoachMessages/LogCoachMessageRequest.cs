using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LogCoachMessageRequest : Request
    {
        [Required]
        public string Message { get; set; }

        [Required]
        public string Role { get; set; }

        public string? FoodEntryId { get; set; }
    }
} 