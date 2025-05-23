using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class FocusInChatRequest : Request
    {
        public string FocusText { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }
} 