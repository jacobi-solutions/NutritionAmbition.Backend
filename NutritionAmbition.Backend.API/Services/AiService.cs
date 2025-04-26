using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;

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
                    model = "gpt-4",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a nutrition assistant that helps parse food descriptions into structured data. Extract food items with their quantities from the user's input. Respond with a JSON object containing an array of meal items, each with a name and quantity." },
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
                
                return new ParseFoodTextResponse
                {
                    MealItems = simulatedResponse.MealItems,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing food text: {FoodDescription}", foodDescription);
                return new ParseFoodTextResponse
                {
                    Success = false,
                    ErrorMessage = $"Error parsing food text: {ex.Message}"
                };
            }
        }

        private ParseFoodTextResponse SimulateAiResponse(string foodDescription)
        {
            // This is a simple simulation of what the AI would return
            // In a real implementation, this would be replaced with actual AI API calls
            
            var response = new ParseFoodTextResponse
            {
                MealItems = new List<MealItem>(),
                Success = true
            };

            if (foodDescription.Contains("egg") && foodDescription.Contains("burrito"))
            {
                response.MealItems.Add(new MealItem { Name = "Egg and Avocado Breakfast Burrito", Quantity = "1" });
            }
            
            if (foodDescription.Contains("fruit"))
            {
                response.MealItems.Add(new MealItem { Name = "Mixed Fresh Fruit", Quantity = "1 cup" });
            }
            
            if (foodDescription.Contains("coffee"))
            {
                response.MealItems.Add(new MealItem { Name = "Black Coffee", Quantity = "1 cup" });
            }

            // If no specific items were detected, add a generic entry
            if (response.MealItems.Count == 0)
            {
                response.MealItems.Add(new MealItem { Name = foodDescription, Quantity = "1 serving" });
            }

            return response;
        }
    }
}
