using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionixService : INutritionixService
    {
        private readonly NutritionixClient _nutritionixClient;
        private readonly ILogger<NutritionixService> _logger;

        public NutritionixService(
            NutritionixClient nutritionixClient,
            ILogger<NutritionixService> logger)
        {
            _nutritionixClient = nutritionixClient;
            _logger = logger;
        }

        public async Task<NutritionixResponse?> GetNutritionDataAsync(string query)
        {
            try
            {
                _logger.LogInformation("Querying Nutritionix API for: {Query}", query);

                var requestBody = new { query = query };
                var response = await _nutritionixClient.PostAsync("natural/nutrients", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Nutritionix API request failed with status code {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    return null;
                }

                var responseStream = await response.Content.ReadAsStreamAsync();
                var nutritionixResponse = await JsonSerializer.DeserializeAsync<NutritionixResponse>(responseStream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (nutritionixResponse == null || nutritionixResponse.Foods == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix API returned no food data for query: {Query}", query);
                    return null;
                }

                _logger.LogInformation("Successfully retrieved nutrition data from Nutritionix for query: {Query}", query);
                return nutritionixResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Nutritionix API for query: {Query}", query);
                return null;
            }
        }
        
        public async Task<SearchInstantResponse> SearchInstantAsync(string query)
        {
            try 
            {
                _logger.LogInformation("Searching Nutritionix API for branded products: {Query}", query);
                
                var response = await _nutritionixClient.SearchInstantAsync(query);
                
                if (response.Branded.Count == 0 && response.Common.Count == 0)
                {
                    _logger.LogWarning("Nutritionix search returned no results for query: {Query}", query);
                }
                else
                {
                    _logger.LogInformation("Nutritionix search returned {BrandedCount} branded and {CommonCount} common results for query: {Query}", 
                        response.Branded.Count, response.Common.Count, query);
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Nutritionix API for query: {Query}", query);
                return new SearchInstantResponse();
            }
        }
        
        public async Task<bool> IsBrandedItemConfident(string query, BrandedFoodItem brandedItem)
        {
            // Simple implementation of confidence check for branded items
            if (string.IsNullOrEmpty(brandedItem.FoodName) || string.IsNullOrEmpty(brandedItem.BrandName))
            {
                return false;
            }
            
            // Clean up for comparison
            var lowerQuery = query.ToLowerInvariant();
            var lowerFoodName = brandedItem.FoodName.ToLowerInvariant();
            var lowerBrandName = brandedItem.BrandName.ToLowerInvariant();
            
            // Check if query contains both the brand name and food name
            var containsBrandName = lowerQuery.Contains(lowerBrandName, StringComparison.OrdinalIgnoreCase);
            var containsFoodName = lowerQuery.Contains(lowerFoodName, StringComparison.OrdinalIgnoreCase);
            
            // If query directly contains both brand and food name, we're very confident
            if (containsBrandName && containsFoodName)
            {
                _logger.LogInformation("High confidence match for query '{Query}': {BrandName} {FoodName}", 
                    query, brandedItem.BrandName, brandedItem.FoodName);
                return true;
            }
            
            // Check if the query contains individual words from the brand name
            var queryWords = lowerQuery.Split(new[] { ' ', ',', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var brandWords = lowerBrandName.Split(new[] { ' ', ',', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            
            int brandWordMatches = brandWords.Count(brandWord => queryWords.Any(qw => qw.Equals(brandWord, StringComparison.OrdinalIgnoreCase)));
            
            // If all brand words are in the query, we have high confidence
            if (brandWords.Length > 0 && brandWordMatches == brandWords.Length) 
            {
                _logger.LogInformation("Brand word match for query '{Query}': {BrandName} {FoodName}", 
                    query, brandedItem.BrandName, brandedItem.FoodName);
                return true;
            }
            
            // If query contains the brand name exactly and the food name is similar
            if (containsBrandName && SimilarityCheck(lowerQuery, lowerFoodName))
            {
                _logger.LogInformation("Brand + similar food name match for query '{Query}': {BrandName} {FoodName}", 
                    query, brandedItem.BrandName, brandedItem.FoodName);
                return true;
            }
            
            _logger.LogInformation("Low confidence match for query '{Query}': {BrandName} {FoodName}", 
                query, brandedItem.BrandName, brandedItem.FoodName);
            return false;
        }
        
        private bool SimilarityCheck(string source, string target)
        {
            // Simple word-by-word similarity check
            var sourceWords = source.Split(new[] { ' ', ',', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            var targetWords = target.Split(new[] { ' ', ',', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            
            // If the target has multiple words, check if at least half are in the source
            if (targetWords.Length > 1)
            {
                int matches = targetWords.Count(tw => sourceWords.Any(sw => sw.Equals(tw, StringComparison.OrdinalIgnoreCase)));
                return matches >= targetWords.Length / 2;
            }
            
            // For single word targets, check if it's in the source
            return sourceWords.Any(sw => sw.Equals(targetWords[0], StringComparison.OrdinalIgnoreCase));
        }
    }
}
