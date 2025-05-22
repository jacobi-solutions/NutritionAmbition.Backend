using System.Text;
using System.Text.Json;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Settings;
using System.Text.Json.Serialization;
using NutritionAmbition.Backend.API.Constants;
using System.Net.Http.Headers;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IOpenAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        Task<string> SelectBestGenericFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> commonFoods);
        Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems);
        Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt);
        Task<string> GetChatResponseAsync(List<object> messages, int? maxTokens = null, double? temperature = null, string responseFormat = null);
        Task<string> SelectBestBrandedFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> brandedFoods);
        Task<string> SelectBestFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> foodOptions, bool isBranded);    
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly ILogger<OpenAiService> _logger;
        private readonly OpenAiSettings _openAiSettings;
        private readonly HttpClient _httpClient;
        private readonly IAccountsService _accountsService;
        private readonly IDailyGoalService _dailyGoalService;
        private readonly IOpenAiResponsesService _openAiResponsesService;

        /// <summary>
        /// Initializes a new instance of the OpenAiService class
        /// </summary>
        /// <param name="logger">Logger for logging operations</param>
        /// <param name="openAiSettings">Configuration settings for OpenAI</param>
        /// <param name="httpClient">HttpClient for making API requests</param>
        /// <param name="accountsService">Service for account operations</param>
        /// <param name="dailyGoalService">Service for daily goals</param>
        /// <param name="openAiResponsesService">Service for OpenAI responses</param>
        public OpenAiService(
            ILogger<OpenAiService> logger, 
            OpenAiSettings openAiSettings, 
            HttpClient httpClient,
            IAccountsService accountsService,
            IDailyGoalService dailyGoalService,
            IOpenAiResponsesService openAiResponsesService)
        {
            _logger = logger;
            _openAiSettings = openAiSettings;
            _httpClient = httpClient;
            _accountsService = accountsService;
            _dailyGoalService = dailyGoalService;
            _openAiResponsesService = openAiResponsesService;
        }

        /// <summary>
        /// Gets a chat response from the OpenAI API using direct HTTP requests
        /// </summary>
        /// <param name="messages">List of messages to send to the API</param>
        /// <param name="maxTokens">Maximum number of tokens in the response</param>
        /// <param name="temperature">Temperature parameter for response randomness (0.0-1.0)</param>
        /// <param name="responseFormat">Format for the response (null or "json_object")</param>
        /// <returns>The content of the AI's response</returns>
        public async Task<string> GetChatResponseAsync(
            List<object> messages, 
            int? maxTokens = null, 
            double? temperature = null,
            string responseFormat = null)
        {
            try
            {
                _logger.LogInformation("Calling OpenAI chat completion API");

                // Construct the request payload as a Dictionary
                var requestBody = new Dictionary<string, object?>
                {
                    ["model"] = _openAiSettings.Model,
                    ["messages"] = messages,
                    ["temperature"] = temperature ?? _openAiSettings.DefaultTemperature
                };
                
                // Add optional parameters only if they have values
                if (maxTokens.HasValue)
                {
                    requestBody["max_tokens"] = maxTokens.Value;
                }
                
                // Handle response format
                if (responseFormat == OpenAiConstants.JsonObjectFormat)
                {
                    requestBody["response_format"] = new Dictionary<string, string> { ["type"] = "json_object" };
                }

                // Serialize the request to JSON
                var jsonContent = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                
                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                // Send the request
                var response = await _httpClient.SendAsync(request);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                // Log the raw response for debugging
                _logger.LogDebug("OpenAI API response status: {StatusCode}", response.StatusCode);
                _logger.LogDebug("OpenAI API raw response: {Response}", responseContent);
                
                // Ensure the response was successful
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Error calling OpenAI API: Status {StatusCode}, Response: {Response}", 
                        response.StatusCode, responseContent);
                    throw new Exception($"OpenAI API error: {response.StatusCode}");
                }
                
                // Parse the response
                using var responseJson = JsonDocument.Parse(responseContent);
                var choices = responseJson.RootElement.GetProperty("choices");
                
                if (choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    var message = choice.GetProperty("message");
                    var content = message.GetProperty("content").GetString();
                    
                    if (string.IsNullOrEmpty(content))
                    {
                        throw new Exception("No content returned from OpenAI API");
                    }
                    
                    return content;
                }
                
                throw new Exception("No choices returned from OpenAI API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI chat API");
                throw;
            }
        }
        
        public async Task<string> SelectBestBrandedFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> brandedFoods)
        {
            if (brandedFoods == null || !brandedFoods.Any())
            {
                return null;
            }

            try
            {
                _logger.LogInformation("Selecting best branded food with prompt for query: {Query}", userQuery);

                var formattedOptions = new StringBuilder();
                for (int i = 0; i < brandedFoods.Count; i++)
                {
                    var food = brandedFoods[i];
                    formattedOptions.AppendLine($"{i + 1}. {food.BrandName} {food.FoodName}, {food.ServingQty} {food.ServingUnit} per serving [id: {food.NixFoodId}]");
                }

                var messages = new List<object>
                {
                    new {
                        role = OpenAiConstants.SystemRole,
                        content = @"You are a nutrition assistant. Your job is to help choose the best branded food item that matches the user's input. You will be given a user query and a numbered list of branded food options with IDs.

Choose the SINGLE best match and return only its ID in JSON format:

{ ""bestId"": ""abc123"" }

Do NOT include any explanation or extra text. Only return the JSON object."
                    },
                    new {
                        role = OpenAiConstants.UserRole,
                        content = $"User query: {quantity} {unit} of {userQuery}\n\nOptions:\n{formattedOptions}"
                    }
                };

                var aiResponse = await GetChatResponseAsync(
                    messages,
                    null,
                    _openAiSettings.ZeroTemperature,
                    OpenAiConstants.JsonObjectFormat
                );

                _logger.LogDebug("Raw response for branded selection: {Raw}", aiResponse);

                try
                {
                    using var doc = JsonDocument.Parse(aiResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("bestId", out var bestIdProp))
                    {
                        return bestIdProp.GetString();
                    }

                    _logger.LogWarning("No 'bestId' found in branded selection response");
                    return null;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON for branded food prompt response");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SelectBestBrandedFoodWithPromptAsync");
                return null;
            }
        }

        public async Task<string> SelectBestFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> foodOptions, bool isBranded)
        {
            if (foodOptions == null || !foodOptions.Any())
            {
                return null;
            }

            try
            {
                _logger.LogInformation("Selecting best {Type} food with OpenAI from {Count} options for: {Quantity} {Unit} {FoodName}",
                    isBranded ? "branded" : "generic", foodOptions.Count, quantity, unit, userQuery);

                var formattedOptions = new StringBuilder();
                for (int i = 0; i < foodOptions.Count; i++)
                {
                    var food = foodOptions[i];
                    var brandPrefix = isBranded && !string.IsNullOrWhiteSpace(food.BrandName) ? $"{food.BrandName} " : "";
                    formattedOptions.AppendLine($"{i + 1}. {brandPrefix}{food.FoodName}, {food.ServingQty} {food.ServingUnit} per serving [id: {food.NixFoodId}]");
                }

                var systemPrompt = $@"You are a nutrition assistant helping users match their food descriptions to the best {(isBranded ? "branded" : "generic")} food from a list.

You will be given a user query (e.g., '100 g of {(isBranded ? "Chobani " : "")}nonfat greek yogurt') and a numbered list of food options with IDs. Choose the SINGLE best match and return only its ID in JSON format:

{{ ""bestId"": ""abc123"" }}

Do NOT include any explanation or extra text. Only return the JSON object.";

                var messages = new List<object>
                {
                    new
                    {
                        role = OpenAiConstants.SystemRole,
                        content = systemPrompt
                    },
                    new
                    {
                        role = OpenAiConstants.UserRole,
                        content = $"User query: {quantity} {unit} of {userQuery}\n\nOptions:\n{formattedOptions}"
                    }
                };

                var aiResponse = await GetChatResponseAsync(
                    messages,
                    null,
                    _openAiSettings.ZeroTemperature,
                    OpenAiConstants.JsonObjectFormat
                );

                _logger.LogDebug("Raw OpenAI response for best food selection: {Response}", aiResponse);

                using var doc = JsonDocument.Parse(aiResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("bestId", out var bestIdProp))
                {
                    return bestIdProp.GetString();
                }

                _logger.LogWarning("No 'bestId' found in food selection response");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON for best food prompt response");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting best food for: {FoodQuery}", $"{quantity} {unit} of {userQuery}");
                return null;
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
                        role = OpenAiConstants.SystemRole,
                        content = "You are a nutrition assistant. Break down the user's food description into individual food items. For each item, extract:\n\nname (string, short and generic like \"greek yogurt\")\n\ndescription (string, optional — include any modifiers like \"non-fat\", \"vanilla\", \"plain\", \"grass-fed\", etc. Use comma-separated phrases if there are multiple.)\n\nquantity (number)\n\nunit (string)\n\nbrand (string, optional — leave empty if no brand mentioned)\n\nisBranded (boolean)\n\nRespond ONLY with a JSON object structured like this:\n\n{\n  \"foods\": [\n    { \"name\": \"coffee\", \"description\": \"non-fat, vanilla, plain\", \"quantity\": 16, \"unit\": \"oz\", \"brand\": \"\", \"isBranded\": false },\n    { \"name\": \"cheese pizza\", \"description\": \"thin crust, extra cheese\", \"quantity\": 1, \"unit\": \"large slice\", \"brand\": \"mellow mushroom\", \"isBranded\": true }\n  ]\n}\n\nNo extra text. No explanations."
                    },
                    new
                    {
                        role = OpenAiConstants.UserRole,
                        content = foodDescription
                    }
                };


                // Get a response from OpenAI to determine the best match
                var aiResponse = await GetChatResponseAsync(
                    messages,
                    null,
                    _openAiSettings.ZeroTemperature,
                    OpenAiConstants.JsonObjectFormat);

                // Log the raw response
                _logger.LogDebug("Raw OpenAI response: {RawResponse}", aiResponse);

                try
                {
                    // Try to deserialize the OpenAI response into a ParseFoodTextResponse object
                    var parsedResponse = JsonSerializer.Deserialize<ParseFoodTextResponse>(aiResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsedResponse == null)
                    {
                        _logger.LogError("JSON deserialization failed: OpenAI response deserialized to null. Raw response: {RawResponse}", aiResponse);
                        throw new InvalidOperationException("Failed to parse OpenAI food data: The response was empty or invalid");
                    }

                    _logger.LogInformation("Successfully parsed {Count} food items from OpenAI response",
                        parsedResponse.Foods?.Count ?? 0);
                    return parsedResponse;
                }
                catch (JsonException ex)
                {
                    // Log error with full OpenAI response and throw exception
                    _logger.LogError(ex, "JSON deserialization failed: Error deserializing OpenAI response. Raw response: {RawResponse}", aiResponse);
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
        /// Selects the best matching generic food item from a list of common foods
        /// </summary>
        /// <param name="userQuery">The user's food description</param>
        /// <param name="commonFoods">List of potential common food matches</param>
        /// <returns>The index of the best matching common food item (0-based) or -1 if no match</returns>
        public async Task<string> SelectBestGenericFoodAsync(string userQuery, double quantity, string unit, List<NutritionixFood> commonFoods)
        {
            if (commonFoods == null || !commonFoods.Any())
        {
            return null;
        }

        try
            {
                _logger.LogInformation("Selecting best generic food with OpenAI from {Count} options for: {Quantity} {Unit} {FoodName}",
                    commonFoods.Count, quantity, unit, userQuery);

                var formattedOptions = new StringBuilder();
                for (int i = 0; i < commonFoods.Count; i++)
                {
                    var food = commonFoods[i];
                    formattedOptions.AppendLine($"{i + 1}. {food.FoodName}, {food.FoodName} {food.ServingUnit} per serving [id: {food.NixFoodId}]");
                }

                var messages = new List<object>
                {
                    new
                    {
                        role = OpenAiConstants.SystemRole,
                        content = @"You are a nutrition assistant helping users match their food descriptions to the best generic food from a list.

You will be given a user query (e.g., '100 g of nonfat greek yogurt') numbered list of food options with IDs. Choose the SINGLE best match and return only its ID in JSON format:

{ ""bestId"": ""abc123"" }

Do NOT include any explanation or extra text. Only return the JSON object."
                    },
                    new
                    {
                        role = OpenAiConstants.UserRole,
                        content = $"User query: {quantity} {unit} of {userQuery}\n\nOptions:\n{formattedOptions}"
                    }
                };

                var aiResponse = await GetChatResponseAsync(messages, null, _openAiSettings.LowTemperature, OpenAiConstants.JsonObjectFormat);

                _logger.LogDebug("Raw OpenAI response for generic selection: {Response}", aiResponse);

                try
                {
                    using var doc = JsonDocument.Parse(aiResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("bestId", out var bestIdProp))
                    {
                        return bestIdProp.GetString();
                    }

                    _logger.LogWarning("No 'bestId' found in branded selection response");
                    return null;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse JSON for branded food prompt response");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting best generic food for: {FoodQuery}", $"{quantity} {unit} of {userQuery}");
                return null;
            }
        }


        /// <summary>
        /// Groups food items into logical meal groups using AI
        /// </summary>
        /// <param name="originalDescription">The original food description</param>
        /// <param name="foodItems">The list of individual food items</param>
        /// <returns>A list of food groups</returns>
        public async Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems)
        {
            try
            {
                if (foodItems == null || !foodItems.Any())
                {
                    return new List<FoodGroup>();
                }
                
                // If there's only one item, no need to group
                if (foodItems.Count == 1)
                {
                    return new List<FoodGroup>
                    {
                        new FoodGroup
                        {
                            GroupName = foodItems[0].Name,
                            Items = new List<FoodItem> { foodItems[0] }
                        }
                    };
                }

                _logger.LogInformation("Grouping {Count} food items with OpenAI", foodItems.Count);

                // Format the food items for the prompt
                var formattedItems = new StringBuilder();
                for (int i = 0; i < foodItems.Count; i++)
                {
                    var food = foodItems[i];
                    formattedItems.AppendLine($"{i + 1}. {food.Quantity} {food.Unit} {food.Name}");
                }

                // Create messages for OpenAI
                var messages = new List<object>
                {
                    new
                    {
                        role = OpenAiConstants.SystemRole,
                        content = "You are a nutrition assistant helping to organize logged food items into logical groups for a meal. Group foods that belong together into logical categories (e.g., a sandwich, a complete dish, or a drink). Respond ONLY with a JSON array of objects structured like this: [{\"groupName\": \"Sandwich\", \"itemIndices\": [1, 2, 3]}, {\"groupName\": \"Side Dish\", \"itemIndices\": [4, 5]}, {\"groupName\": \"Beverage\", \"itemIndices\": [6]}]. Use 1-based indices to refer to the food items. Create descriptive but concise group names."
                    },
                    new
                    {
                        role = OpenAiConstants.UserRole,
                        content = $"Original description: {originalDescription}\n\nFood items:\n{formattedItems}"
                    }
                };

                // Get a response from OpenAI
                var aiResponse = await GetChatResponseAsync(
                    messages, 
                    null, 
                    _openAiSettings.LowTemperature,
                    OpenAiConstants.JsonObjectFormat);
                
                // Try to parse the response to get the food groups
                try
                {
                    var groupingResponse = JsonSerializer.Deserialize<FoodGroupingResponse>(aiResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (groupingResponse == null || groupingResponse.Groups == null || !groupingResponse.Groups.Any())
                    {
                        _logger.LogWarning("Empty or invalid grouping response from OpenAI: {Response}", aiResponse);
                        return CreateDefaultGrouping(foodItems);
                    }
                    
                    // Convert the AI response to our domain model
                    var foodGroups = new List<FoodGroup>();
                    
                    foreach (var group in groupingResponse.Groups)
                    {
                        if (group.ItemIndices == null || !group.ItemIndices.Any())
                        {
                            continue;
                        }
                        
                        var foodGroup = new FoodGroup
                        {
                            GroupName = group.GroupName,
                            Items = new List<FoodItem>()
                        };
                        
                        foreach (var index in group.ItemIndices)
                        {
                            // Convert from 1-based to 0-based index
                            int zeroBasedIndex = index - 1;
                            
                            if (zeroBasedIndex >= 0 && zeroBasedIndex < foodItems.Count)
                            {
                                foodGroup.Items.Add(foodItems[zeroBasedIndex]);
                            }
                        }
                        
                        if (foodGroup.Items.Any())
                        {
                            foodGroups.Add(foodGroup);
                        }
                    }
                    
                    // Ensure all food items are included in a group
                    var includedIndices = new HashSet<int>();
                    foreach (var group in foodGroups)
                    {
                        foreach (var item in group.Items)
                        {
                            includedIndices.Add(foodItems.IndexOf(item));
                        }
                    }
                    
                    for (int i = 0; i < foodItems.Count; i++)
                    {
                        if (!includedIndices.Contains(i))
                        {
                            foodGroups.Add(new FoodGroup
                            {
                                GroupName = foodItems[i].Name,
                                Items = new List<FoodItem> { foodItems[i] }
                            });
                        }
                    }
                    
                    _logger.LogInformation("Successfully created {Count} food groups", foodGroups.Count);
                    return foodGroups;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing food grouping response from OpenAI: {Response}", aiResponse);
                    return CreateDefaultGrouping(foodItems);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error grouping food items with OpenAI");
                return CreateDefaultGrouping(foodItems);
            }
        }
        
        /// <summary>
        /// Creates a simple default grouping when AI grouping fails
        /// </summary>
        private List<FoodGroup> CreateDefaultGrouping(List<FoodItem> foodItems)
        {
            return new List<FoodGroup>
            {
                new FoodGroup
                {
                    GroupName = "Meal",
                    Items = foodItems
                }
            };
        }

        /// <summary>
        /// Creates a chat completion with a system and user prompt
        /// </summary>
        /// <param name="systemPrompt">The system prompt</param>
        /// <param name="userPrompt">The user prompt</param>
        /// <returns>The AI's response</returns>
        public async Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt)
        {
            try
            {
                _logger.LogInformation("Creating chat completion");

                var messages = new List<object>
                {
                    new
                    {
                        role = OpenAiConstants.SystemRole,
                        content = systemPrompt
                    },
                    new
                    {
                        role = OpenAiConstants.UserRole,
                        content = userPrompt
                    }
                };

                return await GetChatResponseAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat completion");
                throw;
            }
        }

        #region Response Models
        private class FoodGroupingResponse
        {
            [JsonPropertyName("groups")]
            public List<AiGroupInfo> Groups { get; set; } = new List<AiGroupInfo>();
        }

        private class AiGroupInfo
        {
            [JsonPropertyName("groupName")]
            public string GroupName { get; set; } = string.Empty;
            [JsonPropertyName("itemIndices")]
            public List<int> ItemIndices { get; set; } = new List<int>(); // 1-based indices
        }
        #endregion
    }
}

