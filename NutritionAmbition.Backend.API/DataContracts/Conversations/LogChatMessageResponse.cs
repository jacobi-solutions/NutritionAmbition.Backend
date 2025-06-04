using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LogChatMessageResponse : Response
    {
        public ChatMessage Message { get; set; }
    }
} 