using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ParseFoodTextResponse : Response
    {
        public List<MealItem> MealItems { get; set; } = new List<MealItem>();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
