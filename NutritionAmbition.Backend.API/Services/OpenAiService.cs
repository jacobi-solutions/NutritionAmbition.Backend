using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IOpenAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        Task<int> SelectBestFoodMatchAsync(string foodDescription, List<FoodSearchResult> searchResults);
        Task<int> SelectBestBrandedFoodAsync(string userQuery, double quantity, string unit, List<BrandedFoodItem> brandedFoods);
        Task<int> SelectBestGenericFoodAsync(string userQuery, List<CommonFoodItem> commonFoods);
        Task<string> GenerateCoachResponseAsync(string foodDescription, FoodNutrition nutritionData);
        Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems);
        Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt);
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly ILogger<OpenAiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _openAiSettings;
        private const string OpenAiApiEndpoint = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// Initializes a new instance of the OpenAiService class
        /// </summary>
        /// <param name="logger">Logger for logging operations</param>
        /// <param name="httpClient">HttpClient for making API calls</param>
        /// <param name="openAiSettings">Configuration settings for OpenAI</param>
        public OpenAiService(ILogger<OpenAiService> logger, HttpClient httpClient, IOptions<OpenAiSettings> openAiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _openAiSettings = openAiSettings.Value;
            
            // Configure the HTTP client
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Gets a chat response from the OpenAI API
        /// </summary>
        /// <param name="messages">List of messages to send to the API</param>
        /// <param name="temperature">Temperature parameter for response randomness (0.0-1.0)</param>
        /// <param name="maxTokens">Maximum number of tokens in the response</param>
        /// <param name="responseFormat">Format for the response (null or "json_object")</param>
        /// <returns>The content of the AI's response</returns>
        private async Task<string> GetChatResponseAsync(
            List<object> messages, 
            float temperature = 0.7f, 
            int? maxTokens = null,
            string responseFormat = null)
        {
            try
            {
                var requestBody = new
                {
                    model = _openAiSettings.Model ?? "gpt-4",
                    messages,
                    temperature,
                    max_tokens = maxTokens,
                    response_format = responseFormat != null ? new { type = responseFormat } : null
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(OpenAiApiEndpoint, content);
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

                return openAiResponse.Choices[0].Message.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat response from OpenAI");
                throw;
            }
        }

        /// <summary>
        /// Parses a food text description using OpenAI
        /// </summary>
        /// <param name="foodDescription">The food description to parse</param>
        /// <returns>A ParseFoodTextResponse with the result</returns>
        public async Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription)
        {
            try
            {
                _logger.LogInformation("Parsing food text with OpenAI: {FoodDescription}", foodDescription);

                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are a nutrition assistant. Break down the user's food description into individual food items. For each item, extract:\n\nname (string)\n\nquantity (number)\n\nunit (string)\n\nbrand (string, optional — leave empty if no brand mentioned)\n\nisBranded (boolean)\n\nRespond ONLY with a JSON object structured like this:\n\n{\n'foods': [\n{ 'name': 'coffee', 'quantity': 16, 'unit': 'oz', 'brand': '', 'isBranded': false },\n{ 'name': 'cheese pizza', 'quantity': 1, 'unit': 'large slice', 'brand': 'mellow mushroom', 'isBranded': true }\n]\n}\n\nNo extra text, no explanations."
                    },
                    new
                    {
                        role = "user",
                        content = foodDescription
                    }
                };

                // Get the chat response from OpenAI
                var aiContent = await GetChatResponseAsync(messages, 0.1f, 800, "json_object");
                
                // Log the raw response
                _logger.LogDebug("Raw OpenAI response: {RawResponse}", aiContent);
                
                try
                {
                    // Try to deserialize the OpenAI response into a ParseFoodTextResponse object
                    var parsedResponse = JsonSerializer.Deserialize<ParseFoodTextResponse>(aiContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (parsedResponse == null)
                    {
                        _logger.LogError("JSON deserialization failed: OpenAI response deserialized to null. Raw response: {RawResponse}", aiContent);
                        throw new InvalidOperationException("Failed to parse OpenAI food data: The response was empty or invalid");
                    }
                    
                    _logger.LogInformation("Successfully parsed {Count} food items from OpenAI response", 
                        parsedResponse.Foods?.Count ?? 0);
                    return parsedResponse;
                }
                catch (JsonException ex)
                {
                    // Log error with full OpenAI response and throw exception
                    _logger.LogError(ex, "JSON deserialization failed: Error deserializing OpenAI response. Raw response: {RawResponse}", aiContent);
                    throw new InvalidOperationException($"Failed to parse OpenAI food data: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing food text with OpenAI: {FoodDescription}", foodDescription);
                throw; // Re-throw to propagate the exception
            }
        }

        /// <summary>
        /// Selects the best matching food item from a list of search results
        /// </summary>
        /// <param name="foodDescription">The original food description</param>
        /// <param name="searchResults">List of potential food matches</param>
        /// <returns>The FdcId of the best matching food item</returns>
        public async Task<int> SelectBestFoodMatchAsync(string foodDescription, List<FoodSearchResult> searchResults)
        {
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
                        content = @"You are a nutrition assistant that helps select the most appropriate food item from a list of options based on a user's description. 
                        Analyze the options and select the one that best matches the user's food description.
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
                        content = $"User's food description: {foodDescription}\n\nAvailable options:\n{formattedResults}"
                    }
                };

                var aiContent = await GetChatResponseAsync(messages, 0.2f, null, "json_object");
                
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

        /// <summary>
        /// Selects the best matching branded food item from a list of branded foods
        /// </summary>
        /// <param name="userQuery">The user's food description</param>
        /// <param name="quantity">The quantity of the food</param>
        /// <param name="unit">The unit of the food</param>
        /// <param name="brandedFoods">List of potential branded food matches</param>
        /// <returns>The index of the best matching branded food item (0-based) or -1 if no match</returns>
        public async Task<int> SelectBestBrandedFoodAsync(string userQuery, double quantity, string unit, List<BrandedFoodItem> brandedFoods)
        {
            if (brandedFoods == null || !brandedFoods.Any())
            {
                return -1;
            }

            try
            {
                _logger.LogInformation("Selecting best branded food with OpenAI from {Count} options for query: {UserQuery} ({Quantity} {Unit})", 
                    brandedFoods.Count, userQuery, quantity, unit);

                // Format the branded foods list for the prompt
                var formattedOptions = new StringBuilder();
                for (int i = 0; i < brandedFoods.Count; i++)
                {
                    var food = brandedFoods[i];
                    formattedOptions.AppendLine($"{i + 1}. {food.BrandName} {food.FoodName}");
                }

                // Create messages for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are a nutrition assistant helping users match their food descriptions to the best branded food from a list. The user's report includes the food name, quantity, and unit. Choose the food item that most closely matches based on food name, brand name, and portion size. Only respond with the number of the best matching option. If none are a good match, respond with -1."
                    },
                    new
                    {
                        role = "user",
                        content = $"The user reported eating: {quantity} {unit} {userQuery}\n\nAvailable branded food options:\n{formattedOptions}"
                    }
                };

                // Get response from OpenAI
                var aiResponse = await GetChatResponseAsync(messages, 0.2f, 10);
                
                // Log the raw response
                _logger.LogDebug("Raw OpenAI response for branded food selection: {RawResponse}", aiResponse);

                // Parse the response as an integer
                if (int.TryParse(aiResponse.Trim(), out int selectedOption))
                {
                    // Check if the selection is valid
                    if (selectedOption == -1)
                    {
                        _logger.LogInformation("OpenAI determined no good match exists among branded foods");
                        return -1;
                    }
                    else if (selectedOption < 1 || selectedOption > brandedFoods.Count)
                    {
                        _logger.LogWarning("OpenAI returned an out-of-range selection: {Selection}. Valid range is 1-{Count}", 
                            selectedOption, brandedFoods.Count);
                        return -1;
                    }

                    // Convert from 1-based to 0-based index
                    return selectedOption - 1;
                }
                else
                {
                    _logger.LogWarning("Failed to parse OpenAI response as integer: {Response}", aiResponse);
                    return -1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting best branded food with OpenAI: {UserQuery}", userQuery);
                return -1;
            }
        }

        /// <summary>
        /// Selects the best matching generic food item from a list of common foods
        /// </summary>
        /// <param name="userQuery">The user's food description</param>
        /// <param name="commonFoods">List of potential common (generic) food matches</param>
        /// <returns>The index of the best matching generic food item (0-based) or -1 if no match</returns>
        public async Task<int> SelectBestGenericFoodAsync(string userQuery, List<CommonFoodItem> commonFoods)
        {
            if (commonFoods == null || !commonFoods.Any())
            {
                return -1;
            }

            try
            {
                _logger.LogInformation("Selecting best generic food with OpenAI from {Count} options for query: {UserQuery}", 
                    commonFoods.Count, userQuery);

                // Format the common foods list for the prompt
                var formattedOptions = new StringBuilder();
                for (int i = 0; i < commonFoods.Count; i++)
                {
                    var food = commonFoods[i];
                    formattedOptions.AppendLine($"Option {i + 1}:");
                    formattedOptions.AppendLine($"- Food Name: {food.FoodName}");
                    
                    if (!string.IsNullOrEmpty(food.ServingUnit))
                    {
                        formattedOptions.AppendLine($"- Serving Unit: {food.ServingUnit}");
                    }
                    
                    formattedOptions.AppendLine();
                }

                // Create messages for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = "system",
                        content = "You are a nutrition assistant. Given a user's food description and a list of options from a nutrition database, pick the best match."
                    },
                    new
                    {
                        role = "user",
                        content = $"Find the best match for this food description: {userQuery}\n\nOptions:\n{formattedOptions}\n\nRespond with ONLY a JSON object like: {{ \"selectedOption\": 1 }}"
                    }
                };

                // Get response from OpenAI
                var aiResponse = await GetChatResponseAsync(messages, 0.2f, null, "json_object");
                
                // Log the raw response
                _logger.LogDebug("Raw OpenAI response for generic food selection: {RawResponse}", aiResponse);

                // Parse the response
                var selectionResponse = JsonSerializer.Deserialize<FoodSelectionResponse>(aiResponse, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (selectionResponse == null || selectionResponse.SelectedOption < 1 || selectionResponse.SelectedOption > commonFoods.Count)
                {
                    _logger.LogWarning("OpenAI returned an invalid selection: {Response}", aiResponse);
                    return -1;
                }

                // Convert from 1-based to 0-based index
                int selectedIndex = selectionResponse.SelectedOption - 1;
                _logger.LogInformation("Selected generic food at index {Index}: {FoodName}", 
                    selectedIndex, commonFoods[selectedIndex].FoodName);
                
                return selectedIndex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting best generic food with OpenAI: {UserQuery}", userQuery);
                return -1;
            }
        }

        /// <summary>
        /// Generates a coach response for logged food items
        /// </summary>
        /// <param name="foodDescription">The original food description</param>
        /// <param name="nutritionData">Nutrition data for the food</param>
        /// <returns>A friendly coach response about the food</returns>
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

                var messages = new List<object>
                {
                    new { role = "system", content = "You are a friendly and encouraging nutrition coach." },
                    new { role = "user", content = prompt.ToString() }
                };

                var coachResponse = await GetChatResponseAsync(messages, 0.7f, 60);
                _logger.LogInformation("Generated AI coach response: {CoachResponse}", coachResponse);
                return coachResponse.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI coach response for: {FoodDescription}", foodDescription);
                return "Logged!"; // Default response on error
            }
        }

        /// <summary>
        /// Groups food items into logical meal components
        /// </summary>
        /// <param name="originalDescription">The original food description</param>
        /// <param name="foodItems">List of parsed food items</param>
        /// <returns>List of food groups</returns>
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

                var aiContent = await GetChatResponseAsync(messages, 0.3f, null, "json_object");
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

        public async Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt)
        {
            try
            {
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                };
                return await GetChatResponseAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat completion");
                throw;
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

    public class FoodSelectionResponse
    {
        public int SelectedOption { get; set; }
    }

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

