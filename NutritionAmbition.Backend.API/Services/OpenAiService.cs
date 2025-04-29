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
using Microsoft.Extensions.Options;
using System.Linq; // 🟢 Added for Any()

namespace NutritionAmbition.Backend.API.Services
{
    public interface IOpenAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        Task<int> SelectBestFoodMatchAsync(string foodDescription, List<FoodSearchResult> searchResults);
        // 🟢 Add method signature for coach response
        Task<string> GenerateCoachResponseAsync(string foodDescription, FoodNutrition nutritionData);
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly ILogger<OpenAiService> _logger;
        private readonly OpenAiClient _openAiClient;
        private readonly OpenAiSettings _openAiSettings;

        public OpenAiService(ILogger<OpenAiService> logger, OpenAiClient openAiClient, IOptions<OpenAiSettings> openAiSettings)
        {
            _logger = logger;
            _openAiClient = openAiClient;
            _openAiSettings = openAiSettings.Value;
        }

        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            // ... existing implementation ...
            try
            {
                _logger.LogInformation("Parsing food text with OpenAI: {FoodDescription}", foodDescription);

                var requestBody = new
                {
                    model = _openAiSettings.Model,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a nutrition assistant..." }, // Keep existing system prompt or refine if needed
                        new { role = "user", content = foodDescription }
                    },
                    temperature = 0.2,
                    response_format = new { type = "json_object" }
                };

                var response = await _openAiClient.PostAsync("", requestBody);
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

        public async Task<int> SelectBestFoodMatchAsync(string foodDescription, List<FoodSearchResult> searchResults)
        {
            // ... existing implementation ...
            try
            {
                _logger.LogInformation("Selecting best food match with OpenAI for: {FoodDescription}", foodDescription);

                if (searchResults == null || searchResults.Count == 0)
                {
                    throw new ArgumentException("No search results provided");
                }

                // If only one result, return it
                if (searchResults.Count == 1)
                {
                    return searchResults[0].FdcId;
                }

                // Format the search results for the prompt
                var formattedResults = new StringBuilder();
                for (int i = 0; i < searchResults.Count; i++)
                {
                    var result = searchResults[i];
                    formattedResults.AppendLine($"Option {i + 1}:");
                    formattedResults.AppendLine($"- Description: {result.Description}");
                    
                    if (!string.IsNullOrEmpty(result.BrandName))
                    {
                        formattedResults.AppendLine($"- Brand: {result.BrandName}");
                    }
                    
                    if (!string.IsNullOrEmpty(result.FoodCategory))
                    {
                        formattedResults.AppendLine($"- Category: {result.FoodCategory}");
                    }
                    
                    if (!string.IsNullOrEmpty(result.Ingredients))
                    {
                        formattedResults.AppendLine($"- Ingredients: {result.Ingredients}");
                    }
                    
                    formattedResults.AppendLine();
                }

                // Create the prompt for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = @"You are a nutrition assistant that helps select the most appropriate food item from a list of options based on a user\s description. 
                        Analyze the options and select the one that best matches the user\s food description.
                        Consider factors like food name, brand, category, and ingredients.
                        Respond with a JSON object containing only the option number (1-based index) of your selection.
                        Format:
                        {
                          ""selectedOption"": 1
                        }"
                    },
                    new
                    {
                        role = "user",
                        content = $"Users food description: {foodDescription}\n\nAvailable options:\n{formattedResults}"
                    }
                };

                var requestBody = new
                {
                    model = "gpt-4", // Or use _openAiSettings.Model if appropriate
                    messages,
                    temperature = 0.2,
                    response_format = new { type = "json_object" }
                };

                var response = await _openAiClient.PostAsync("", requestBody);
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
                var selectionResponse = JsonSerializer.Deserialize<FoodSelectionResponse>(aiContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (selectionResponse == null || selectionResponse.SelectedOption < 1 || selectionResponse.SelectedOption > searchResults.Count)
                {
                    _logger.LogWarning("Invalid selection from OpenAI, defaulting to first option");
                    return searchResults[0].FdcId;
                }

                // Convert from 1-based to 0-based index
                int selectedIndex = selectionResponse.SelectedOption - 1;
                return searchResults[selectedIndex].FdcId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting best food match with OpenAI: {FoodDescription}", foodDescription);
                // Default to first result in case of error
                return searchResults.Any() ? searchResults[0].FdcId : 0; // Return 0 if searchResults is empty
            }
        }

        // 🟢 Implement method to generate coach response
        public async Task<string> GenerateCoachResponseAsync(string foodDescription, FoodNutrition nutritionData)
        {
            try
            {
                _logger.LogInformation("Generating AI coach response for: {FoodDescription}", foodDescription);

                // Construct the prompt
                var prompt = new StringBuilder();
                prompt.AppendLine("You are a friendly and encouraging nutrition coach. A user has just logged the following food item:");
                prompt.AppendLine($"- User Input: {foodDescription}");
                prompt.AppendLine($"- Logged As: {nutritionData.Name} ({nutritionData.Quantity} {nutritionData.Unit})");
                prompt.AppendLine($"- Calories: {nutritionData.Calories:F0}");
                prompt.AppendLine($"- Protein: {nutritionData.Macronutrients.Protein.Amount:F1}g");
                prompt.AppendLine($"- Carbs: {nutritionData.Macronutrients.Carbohydrates.Amount:F1}g");
                prompt.AppendLine($"- Fat: {nutritionData.Macronutrients.Fat.Amount:F1}g");
                prompt.AppendLine();
                prompt.AppendLine("Generate a brief, conversational response (1-2 sentences) acknowledging the logged food. You can optionally offer a simple, positive insight based on the provided nutrition data. Keep it encouraging and natural.");
                prompt.AppendLine("Examples: \"Got it, logged {Food Name}. Looks like a good source of protein!\", \"Okay, {Food Name} has been added to your log.\", \"Logged {Food Name} for you. Nice choice!\"");

                var requestBody = new
                {
                    model = _openAiSettings.Model, // Use configured model
                    messages = new[]
                    {
                        new { role = "system", content = "You are a friendly and encouraging nutrition coach." },
                        new { role = "user", content = prompt.ToString() }
                    },
                    temperature = 0.7, // Allow for a bit more creativity in response
                    max_tokens = 60 // Limit response length
                };

                var response = await _openAiClient.PostAsync("", requestBody);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (openAiResponse?.Choices != null && openAiResponse.Choices.Count > 0)
                {
                    var coachResponse = openAiResponse.Choices[0].Message.Content.Trim();
                    _logger.LogInformation("Generated AI coach response: {CoachResponse}", coachResponse);
                    return coachResponse;
                }
                else
                {
                    throw new Exception("Invalid response from OpenAI when generating coach response");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI coach response for: {FoodDescription}", foodDescription);
                return "Logged!"; // Default response on error
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

    public class FoodSelectionResponse
    {
        public int SelectedOption { get; set; }
    }
}

