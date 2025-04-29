using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;

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
                var response = await _nutritionixClient.PostAsync("", requestBody);

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
    }
}
