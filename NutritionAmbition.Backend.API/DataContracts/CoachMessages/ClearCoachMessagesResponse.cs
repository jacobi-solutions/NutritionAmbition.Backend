namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ClearCoachMessagesResponse : Response
    {
        public bool Success { get; set; }
        public int MessagesDeleted { get; set; }
    }
} 