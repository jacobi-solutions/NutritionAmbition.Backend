using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ClearChatMessagesRequest : Request
    {
        public DateTime? LoggedDateUtc { get; set; }
    }
} 