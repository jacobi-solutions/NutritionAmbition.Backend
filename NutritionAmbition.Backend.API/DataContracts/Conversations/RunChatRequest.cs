using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class RunChatRequest : Request
    {
        public string Message { get; set; }
    }
} 