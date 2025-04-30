using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ParseFoodTextResponse : Response
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
