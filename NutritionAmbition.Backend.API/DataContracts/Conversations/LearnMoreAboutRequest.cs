using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LearnMoreAboutRequest : Request
    {
        public string Topic { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
    }
} 