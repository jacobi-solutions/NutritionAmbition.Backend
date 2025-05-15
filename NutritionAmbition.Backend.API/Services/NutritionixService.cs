using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionixService
    {
        Task<NutritionixResponse?> GetNutritionDataAsync(string query);
        Task<SearchInstantResponse> SearchInstantAsync(string query);
    }
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

                // Ensure query is trimmed and has no leading/trailing spaces
                query = query.Trim();
                
                var requestBody = new { query = query };
                var response = await _nutritionixClient.PostAsync(_nutritionixClient.Settings.NaturalNutrientsPath, requestBody);

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
    }
}
