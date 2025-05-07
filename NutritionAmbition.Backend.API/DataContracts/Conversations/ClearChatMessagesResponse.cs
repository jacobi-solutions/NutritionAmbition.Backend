namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ClearChatMessagesResponse : Response
    {
        public bool Success { get; set; }
        public long MessagesDeleted { get; set; }
    }
} 