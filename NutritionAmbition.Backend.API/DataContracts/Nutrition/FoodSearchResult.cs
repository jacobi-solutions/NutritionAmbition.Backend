namespace NutritionAmbition.Backend.API.DataContracts
{
    // Represents a single food item result from a search (originally USDA, structure kept for OpenAI interaction)
    public class FoodSearchResult
    {
        public int FdcId { get; set; } // FoodData Central ID (or equivalent identifier if source changes)
        public string Description { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? FoodCategory { get; set; }
        public string? Ingredients { get; set; }
        // Add other relevant properties if needed based on the actual search source
    }
}

