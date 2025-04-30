using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ClearCoachMessagesRequest : Request
    {
        public DateTime? LoggedDateUtc { get; set; }
    }
} 