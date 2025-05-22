using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class ParseFoodTextResponse : Response
    {
        public List<ParsedFoodItem> Foods { get; set; } = new List<ParsedFoodItem>();
    }

    public class ParsedFoodItem
    {
        public string Name { get; set; } = string.Empty;
        public string? Brand { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsBranded { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
