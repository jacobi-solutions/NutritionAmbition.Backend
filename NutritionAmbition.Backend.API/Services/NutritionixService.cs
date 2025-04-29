using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionixService : INutritionixService
    {
        private readonly HttpClient _httpClient;
        private readonly NutritionixSettings _settings;
        private readonly ILogger<NutritionixService> _logger;

        public NutritionixService(
            HttpClient httpClient, 
            IOptions<NutritionixSettings> settings,
            ILogger<NutritionixService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            // Configure HttpClient headers for Nutritionix API
            _httpClient.DefaultRequestHeaders.Add("x-app-id", _settings.ApplicationId);
            _httpClient.DefaultRequestHeaders.Add("x-app-key", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<NutritionixResponse?> GetNutritionDataAsync(string query)
        {
            try
            {
                _logger.LogInformation("Querying Nutritionix API for: {Query}", query);

                var requestBody = new { query = query };
                var jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_settings.ApiEndpoint, content);

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
