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
        Task<string> GenerateCoachResponseAsync(string foodDescription, FoodNutrition nutritionData);
        // 🟢 Add method signature for food grouping
        Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems);
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

        // 🟢 Implement method to group food items using AI
        public async Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems)
        {
            if (foodItems == null || !foodItems.Any())
            {
                return new List<FoodGroup>(); // Return empty list if no items
            }

            // If only one item, create a single group for it
            if (foodItems.Count == 1)
            {
                return new List<FoodGroup>
                {
                    new FoodGroup { GroupName = foodItems[0].Name, Items = foodItems }
                };
            }

            try
            {
                _logger.LogInformation("Grouping food items with OpenAI for: {OriginalDescription}", originalDescription);

                // Format the food items for the prompt
                var formattedItems = new StringBuilder();
                for (int i = 0; i < foodItems.Count; i++)
                {
                    var item = foodItems[i];
                    formattedItems.AppendLine($"Item {i + 1}: {item.Quantity} {item.Unit} {item.Name}");
                }

                // Create the prompt for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = @"You are a nutrition assistant. Given a user's original food description and a list of parsed food items (from Nutritionix), group these items into logical meal components or individual significant items. 
                        Assign a concise, descriptive name to each group (e.g., 'Coffee', 'Protein Shake', 'Chicken', 'Banana').
                        Ensure every parsed item is assigned to exactly one group.
                        Respond with a JSON object containing a list of groups, where each group has a 'groupName' and a list of 'itemIndices' (1-based index from the provided list).
                        Example Input:
                        Original Description: '12 oz of coffee with a tablespoon of Ryze mushroom mix and a half cup of silk organic soy milk. Two small bananas and .46 lb of grilled chicken'
                        Parsed Items:
                        Item 1: 12 oz coffee
                        Item 2: 1 tbsp mushroom powder
                        Item 3: 0.5 cup soy milk
                        Item 4: 2 small banana
                        Item 5: 0.46 lb grilled chicken breast
                        Example JSON Output:
                        {
                          ""groups"": [
                            { ""groupName"": ""Coffee with Soy Milk & Mix"", ""itemIndices"": [1, 2, 3] },
                            { ""groupName"": ""Bananas"", ""itemIndices"": [4] },
                            { ""groupName"": ""Grilled Chicken"", ""itemIndices"": [5] }
                          ]
                        }"
                    },
                    new
                    {
                        role = "user",
                        content = $"Original Description: {originalDescription}\n\nParsed Items:\n{formattedItems}"
                    }
                };

                var requestBody = new
                {
                    model = _openAiSettings.Model, // Use configured model
                    messages,
                    temperature = 0.3, // Keep it relatively deterministic
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
                    throw new Exception("Invalid response from OpenAI during grouping");
                }

                var aiContent = openAiResponse.Choices[0].Message.Content;
                _logger.LogDebug("OpenAI grouping response content: {AIContent}", aiContent);

                var groupingResponse = JsonSerializer.Deserialize<FoodGroupingResponse>(aiContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (groupingResponse?.Groups == null || !groupingResponse.Groups.Any())
                {
                    _logger.LogWarning("OpenAI did not return valid groups, returning items ungrouped.");
                    // Fallback: return each item as its own group
                    return foodItems.Select(item => new FoodGroup { GroupName = item.Name, Items = new List<FoodItem> { item } }).ToList();
                }

                // Map the AI response (indices) back to the actual FoodItem objects
                var resultGroups = new List<FoodGroup>();
                var assignedIndices = new HashSet<int>();

                foreach (var aiGroup in groupingResponse.Groups)
                {
                    var groupItems = new List<FoodItem>();
                    if (aiGroup.ItemIndices != null)
                    {
                        foreach (var index in aiGroup.ItemIndices)
                        {
                            int zeroBasedIndex = index - 1;
                            if (zeroBasedIndex >= 0 && zeroBasedIndex < foodItems.Count)
                            {
                                groupItems.Add(foodItems[zeroBasedIndex]);
                                assignedIndices.Add(zeroBasedIndex);
                            }
                            else
                            {
                                _logger.LogWarning("OpenAI returned invalid item index {Index} during grouping.", index);
                            }
                        }
                    }

                    if (groupItems.Any()) // Only add group if it has items
                    {
                        resultGroups.Add(new FoodGroup
                        {
                            GroupName = aiGroup.GroupName ?? "Unnamed Group",
                            Items = groupItems
                        });
                    }
                }
                
                // Handle any items missed by the AI (shouldn't happen with good prompt, but good fallback)
                for(int i = 0; i < foodItems.Count; i++)
                {
                    if (!assignedIndices.Contains(i))
                    {
                        _logger.LogWarning("Item at index {Index} ({ItemName}) was not assigned to any group by OpenAI. Adding as its own group.", i, foodItems[i].Name);
                        resultGroups.Add(new FoodGroup { GroupName = foodItems[i].Name, Items = new List<FoodItem> { foodItems[i] } });
                    }
                }

                _logger.LogInformation("Successfully grouped {ItemCount} items into {GroupCount} groups.", foodItems.Count, resultGroups.Count);
                return resultGroups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grouping food items with OpenAI for: {OriginalDescription}", originalDescription);
                // Fallback: return each item as its own group in case of error
                return foodItems.Select(item => new FoodGroup { GroupName = item.Name, Items = new List<FoodItem> { item } }).ToList();
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

    // 🟢 New response model for AI grouping
    public class FoodGroupingResponse
    {
        public List<AiGroupInfo> Groups { get; set; } = new List<AiGroupInfo>();
    }

    public class AiGroupInfo
    {
        public string GroupName { get; set; } = string.Empty;
        public List<int> ItemIndices { get; set; } = new List<int>(); // 1-based indices
    }
}

