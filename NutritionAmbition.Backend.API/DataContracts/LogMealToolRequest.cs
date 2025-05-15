using System;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class LogMealToolRequest : Request
    {
        public string Meal { get; set; }
    }
} 