using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class GetChatMessagesResponse : Response
    {
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
} 