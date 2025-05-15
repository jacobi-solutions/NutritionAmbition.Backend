using System.ComponentModel.DataAnnotations;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LogChatMessageRequest : Request
    {
        [Required]
        public string Content { get; set; }

        [Required]
        public string Role { get; set; }

        public string? FoodEntryId { get; set; }
    }
} 