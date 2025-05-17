using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class AssistantRunMessageRequest : Request
    {
        public string Message { get; set; }
        public int? TimezoneOffsetMinutes { get; set; }
    }
} 