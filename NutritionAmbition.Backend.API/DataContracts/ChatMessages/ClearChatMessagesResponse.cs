namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ClearChatMessagesResponse : Response
    {
        public bool Success { get; set; }
        public int MessagesDeleted { get; set; }
    }
} 