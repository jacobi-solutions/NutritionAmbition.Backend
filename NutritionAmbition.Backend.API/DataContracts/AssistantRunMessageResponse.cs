using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class AssistantRunMessageResponse : Response
    {
        public string AssistantMessage { get; set; }
        public string AccountId { get; set; }
        public string RunStatus { get; set; }
    }
} 