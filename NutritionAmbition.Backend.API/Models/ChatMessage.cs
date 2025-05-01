using System;

namespace NutritionAmbition.Backend.API.Models
{
    public class ChatMessage : Model
    {
        public string AccountId { get; set; } = string.Empty;
        public MessageRoleTypes Role { get; set; } = MessageRoleTypes.User;
        public string Content { get; set; } = string.Empty;
        public DateTime LoggedDateUtc { get; set; } = DateTime.UtcNow;
        public string? FoodEntryId { get; set; }
        public bool IsRead { get; set; } = false;
    }
} 