namespace NutritionAmbition.Backend.API.Settings
{
    /// <summary>
    /// Configuration settings for Nutritionix API
    /// </summary>
    public class NutritionixSettings
    {
        /// <summary>
        /// Nutritionix Application ID for authentication
        /// </summary>
        public string ApplicationId { get; set; } = string.Empty;
        
        /// <summary>
        /// Nutritionix API Key for authentication
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Base URL for Nutritionix API
        /// </summary>
        public string ApiEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Path for natural language nutrients endpoint
        /// </summary>
        public string NaturalNutrientsPath { get; set; } = "natural/nutrients";
        
        /// <summary>
        /// Path for instant search endpoint
        /// </summary>
        public string SearchInstantPath { get; set; } = "search/instant";
        
        /// <summary>
        /// Path for item search endpoint
        /// </summary>
        public string SearchItemPath { get; set; } = "search/item";
        
        /// <summary>
        /// Default value for branded parameter in search
        /// </summary>
        public string BrandedDefault { get; set; } = "true";
        
        /// <summary>
        /// Default value for common parameter in search
        /// </summary>
        public string CommonDefault { get; set; } = "true";
        
        /// <summary>
        /// Default value for detailed parameter in search
        /// </summary>
        public string DetailedDefault { get; set; } = "true";
    }
}
