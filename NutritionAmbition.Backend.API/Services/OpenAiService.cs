using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IOpenAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly ILogger<OpenAiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _openAiSettings;
        private readonly string _model = "gpt-4o";

        public OpenAiService(ILogger<OpenAiService> logger, HttpClient httpClient, OpenAiSettings openAiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _openAiSettings = openAiSettings;
        }

        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            try
            {
                _logger.LogInformation("Parsing food text with OpenAI: {FoodDescription}", foodDescription);

                // Create the prompt for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = @"You are a nutrition assistant that helps parse food descriptions into structured data. 
                        Extract food items with their quantities from the user's input. 
                        For each food item, identify the name and quantity (with unit if provided).
                        Respond with a JSON object containing an array of meal items, each with a name and quantity.
                        Format:
                        {
                          ""mealItems"": [
                            {
                              ""name"": ""Food Name"",
                              ""quantity"": ""Quantity with unit if available""
                            }
                          ]
                        }"
                    },
                    new
                    {
                        role = "user",
                        content = foodDescription
                    }
                };

                var requestBody = new
                {
                    model = _model,
                    messages,
                    temperature = 0.2,
                    response_format = new { type = "json_object" }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiSettings.ApiKey}");

                var response = await _httpClient.PostAsync(_openAiSettings.ApiEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (openAiResponse?.Choices == null || openAiResponse.Choices.Count == 0)
                {
                    throw new Exception("Invalid response from OpenAI");
                }

                var aiContent = openAiResponse.Choices[0].Message.Content;
                var parsedResponse = JsonSerializer.Deserialize<MealItemsResponse>(aiContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsedResponse?.MealItems == null)
                {
                    throw new Exception("Failed to parse OpenAI response");
                }

                return new ParseFoodTextResponse
                {
                    MealItems = parsedResponse.MealItems,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing food text with OpenAI: {FoodDescription}", foodDescription);
                return new ParseFoodTextResponse
                {
                    Success = false,
                    ErrorMessage = $"Error parsing food text: {ex.Message}"
                };
            }
        }
    }

    // OpenAI API response models
    public class OpenAiResponse
    {
        public List<Choice> Choices { get; set; } = new List<Choice>();
    }

    public class Choice
    {
        public Message Message { get; set; } = new Message();
    }

    public class Message
    {
        public string Content { get; set; } = string.Empty;
    }

    public class MealItemsResponse
    {
        public List<MealItem> MealItems { get; set; } = new List<MealItem>();
    }
}
