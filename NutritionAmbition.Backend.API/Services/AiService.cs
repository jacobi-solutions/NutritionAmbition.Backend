using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using NutritionAmbition.Backend.API.Settings;
using System;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
    }

    public class AiService : IAiService
    {
        private readonly ILogger<AiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _openAiSettings;

        public AiService(ILogger<AiService> logger, HttpClient httpClient, OpenAiSettings openAiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _openAiSettings = openAiSettings;
        }

        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            try
            {
                _logger.LogInformation("Parsing food text: {FoodDescription}", foodDescription);

                // This is a placeholder for the actual AI API call
                // In a real implementation, this would call an AI service like OpenAI
                
                var aiRequest = new
                {
                    model = _openAiSettings.Model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a nutrition assistant that helps parse food descriptions into structured data. Extract food items with their quantities from the user's input." },
                        new { role = "user", content = foodDescription }
                    },
                    temperature = 0.2,
                    response_format = new { type = "json_object" }
                };

                var content = new StringContent(JsonSerializer.Serialize(aiRequest), Encoding.UTF8, "application/json");
                
                // In a real implementation, we would add the API key to the headers
                // _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_aiApiKey}");
                
                // For now, we'll simulate the AI response
                // var response = await _httpClient.PostAsync(_openAiSettings.ApiEndpoint, content);
                // response.EnsureSuccessStatusCode();
                // var responseContent = await response.Content.ReadAsStringAsync();
                
                // Simulate AI response for development
                var simulatedResponse = SimulateAiResponse(foodDescription);
                
                return simulatedResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing food text: {FoodDescription}", foodDescription);
                var response = new ParseFoodTextResponse();
                response.AddError($"Error parsing food text: {ex.Message}");
                return response;
            }
        }

        private ParseFoodTextResponse SimulateAiResponse(string foodDescription)
        {
            // This is a simple simulation of what the AI would return
            // In a real implementation, this would be replaced with actual AI API calls
            
            // Return a successful response
            // IsSuccess is true by default in the Response base class
            return new ParseFoodTextResponse();
        }
    }
}
