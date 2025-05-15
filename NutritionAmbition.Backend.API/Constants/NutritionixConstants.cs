namespace NutritionAmbition.Backend.API.Constants
{
    /// <summary>
    /// Constants for Nutritionix API endpoints and related configuration values
    /// </summary>
    public static class NutritionixConstants
    {
        /// <summary>
        /// Header name for Nutritionix application ID
        /// </summary>
        public const string AppIdHeader = "x-app-id";
        
        /// <summary>
        /// Header name for Nutritionix API key
        /// </summary>
        public const string AppKeyHeader = "x-app-key";
        
        /// <summary>
        /// Query parameter for branded food items
        /// </summary>
        public const string BrandedParam = "branded";
        
        /// <summary>
        /// Query parameter for common food items
        /// </summary>
        public const string CommonParam = "common";
        
        /// <summary>
        /// Query parameter for detailed results
        /// </summary>
        public const string DetailedParam = "detailed";
        
        /// <summary>
        /// Query parameter for the search text
        /// </summary>
        public const string QueryParam = "query";
        
        /// <summary>
        /// Query parameter for Nutritionix item ID
        /// </summary>
        public const string NixItemIdParam = "nix_item_id";
    }
} 