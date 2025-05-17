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
using System.Text.Json.Serialization;
using OpenAI;
using OpenAI.Assistants;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IOpenAiService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        Task<int> SelectBestBrandedFoodAsync(string userQuery, double quantity, string unit, List<BrandedFoodItem> brandedFoods);
        Task<int> SelectBestGenericFoodAsync(string userQuery, List<CommonFoodItem> commonFoods);
        Task<string> GenerateCoachResponseAsync(string foodDescription, FoodNutrition nutritionData);
        Task<List<FoodGroup>> GroupFoodItemsAsync(string originalDescription, List<FoodItem> foodItems);
        Task<string> CreateChatCompletionAsync(string systemPrompt, string userPrompt);
        Task<string> CreateNewThreadAsync();
        Task<string> AppendMessageToThreadAsync(string threadId, string message, string role = "user");
        Task<string> StartRunAsync(string threadId, string assistantId);
        Task<RunResponse> PollRunStatusAsync(string threadId, string runId, int maxAttempts = 0, int delayMs = 0);
        Task<RunResponse> SubmitToolOutputsAsync(string threadId, string runId, List<Models.ToolOutput> toolOutputs);
        Task<List<Models.ThreadMessage>> GetRunMessagesAsync(string threadId, string runId);
        Task<string> GetInitialMessageAsync(string accountId, bool hasUserProfile, bool hasGoals, string localDate, int timezoneOffsetMinutes);
        Task<string> AppendSystemDailyCheckInAsync(string accountId, string threadId, int? timezoneOffsetMinutes);
    }

    public class OpenAiService : IOpenAiService
    {
        private readonly ILogger<OpenAiService> _logger;
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _openAiSettings;
        private readonly OpenAIClient _openAiClient;
        private readonly IAccountsService _accountsService;
        private readonly IDailyGoalService _dailyGoalService;

        /// <summary>
        /// Initializes a new instance of the OpenAiService class
        /// </summary>
        /// <param name="logger">Logger for logging operations</param>
        /// <param name="httpClient">HttpClient for making API calls</param>
        /// <param name="openAiSettings">Configuration settings for OpenAI</param>
        /// <param name="openAiClient">The OpenAI SDK client</param>
        /// <param name="accountsService">Service for account operations</param>
        /// <param name="dailyGoalService">Service for daily goals</param>
        public OpenAiService(
            ILogger<OpenAiService> logger, 
            HttpClient httpClient, 
            OpenAiSettings openAiSettings, 
            OpenAIClient openAiClient,
            IAccountsService accountsService,
            IDailyGoalService dailyGoalService)
        {
            _logger = logger;
            _httpClient = httpClient;
            _openAiSettings = openAiSettings;
            _openAiClient = openAiClient;
            _accountsService = accountsService;
            _dailyGoalService = dailyGoalService;
            
            // Configure the HTTP client with the API key from configuration
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
            float? temperature = null,
            int? maxTokens = null,
            string responseFormat = null)
        {
            try
            {
                // Use configured temperature or default from settings
                float tempValue = temperature ?? _openAiSettings.DefaultTemperature;
                
                // Use the model configured in settings rather than hardcoding
                var requestBody = new
                {
                    model = _openAiSettings.Model,
                    messages,
                    temperature = tempValue,
                    max_tokens = maxTokens,
                    response_format = responseFormat != null ? new { type = responseFormat } : null
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                // Use API base URL and endpoints from configuration
                string chatCompletionsUrl = $"{_openAiSettings.ApiBaseUrl.TrimEnd('/')}{_openAiSettings.ChatCompletionsEndpoint}";
                var response = await _httpClient.PostAsync(chatCompletionsUrl, content);
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
                        role = OpenAiModelNames.SystemRole,
                        content = "You are a nutrition assistant. Break down the user's food description into individual food items. For each item, extract:\n\nname (string)\n\nquantity (number)\n\nunit (string)\n\nbrand (string, optional — leave empty if no brand mentioned)\n\nisBranded (boolean)\n\nRespond ONLY with a JSON object structured like this:\n\n{\n'foods': [\n{ 'name': 'coffee', 'quantity': 16, 'unit': 'oz', 'brand': '', 'isBranded': false },\n{ 'name': 'cheese pizza', 'quantity': 1, 'unit': 'large slice', 'brand': 'mellow mushroom', 'isBranded': true }\n]\n}\n\nNo extra text, no explanations."
                    },
                    new
                    {
                        role = OpenAiModelNames.UserRole,
                        content = foodDescription
                    }
                };

                // Get the chat response from OpenAI
                var aiContent = await GetChatResponseAsync(
                    messages, 
                    _openAiSettings.FoodParsingTemperature, 
                    _openAiSettings.DefaultMaxTokens, 
                    OpenAiModelNames.JsonObjectFormat);
                
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
                        role = OpenAiModelNames.SystemRole,
                        content = "You are a nutrition assistant helping users match their food descriptions to the best branded food from a list. The user's report includes the food name, quantity, and unit. Choose the food item that most closely matches based on food name, brand name, and portion size. Only respond with the number of the best matching option. If none are a good match, respond with -1."
                    },
                    new
                    {
                        role = OpenAiModelNames.UserRole,
                        content = $"The user reported eating: {quantity} {unit} {userQuery}\n\nAvailable branded food options:\n{formattedOptions}"
                    }
                };

                // Get response from OpenAI
                var aiResponse = await GetChatResponseAsync(
                    messages, 
                    _openAiSettings.LowTemperature, 
                    10);
                
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
                        role = OpenAiModelNames.SystemRole,
                        content = "You are a nutrition assistant. Given a user's food description and a list of options from a nutrition database, pick the best match."
                    },
                    new
                    {
                        role = OpenAiModelNames.UserRole,
                        content = $"Find the best match for this food description: {userQuery}\n\nOptions:\n{formattedOptions}\n\nRespond with ONLY a JSON object like: {{ \"selectedOption\": 1 }}"
                    }
                };

                // Get response from OpenAI
                var aiResponse = await GetChatResponseAsync(
                    messages, 
                    _openAiSettings.LowTemperature, 
                    null, 
                    OpenAiModelNames.JsonObjectFormat);
                
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
                    new { role = OpenAiModelNames.SystemRole, content = "You are a friendly and encouraging nutrition coach." },
                    new { role = OpenAiModelNames.UserRole, content = prompt.ToString() }
                };

                var coachResponse = await GetChatResponseAsync(
                    messages, 
                    _openAiSettings.DefaultTemperature, 
                    _openAiSettings.CoachResponseMaxTokens);
                    
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
                        role = OpenAiModelNames.SystemRole,
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
                        role = OpenAiModelNames.UserRole,
                        content = $"Original Description: {originalDescription}\n\nParsed Items:\n{formattedItems}"
                    }
                };

                var aiContent = await GetChatResponseAsync(
                    messages, 
                    _openAiSettings.GroupingTemperature, 
                    null, 
                    OpenAiModelNames.JsonObjectFormat);
                    
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
                    new { role = OpenAiModelNames.SystemRole, content = systemPrompt },
                    new { role = OpenAiModelNames.UserRole, content = userPrompt }
                };
                return await GetChatResponseAsync(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating chat completion");
                throw;
            }
        }

        public async Task<string> CreateNewThreadAsync()
        {
            try
            {
                _logger.LogInformation("Creating new OpenAI thread via SDK");

                var assistantClient = _openAiClient.GetAssistantClient();
                var thread = await assistantClient.CreateThreadAsync();
                
                if (thread == null || string.IsNullOrEmpty(thread.Value.Id))
                {
                    throw new Exception("OpenAI SDK returned a thread with no ID");
                }

                _logger.LogInformation("Successfully created new thread: {ThreadId}", thread.Value.Id);
                return thread.Value.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new thread");
                throw;
            }
        }

        public async Task<string> AppendMessageToThreadAsync(string threadId, string message, string role = "user")
        {
            try
            {
                _logger.LogInformation("Appending message to thread {ThreadId}", threadId);

                var assistantClient = _openAiClient.GetAssistantClient();
                

                var content = new List<OpenAI.Assistants.MessageContent>
                {
                    OpenAI.Assistants.MessageContent.FromText(message)
                };

                
                if (!Enum.TryParse<MessageRole>(role, ignoreCase: true, out var messageRole))
                {
                    throw new ArgumentException($"Invalid message role: {role}", nameof(role));
                }

                var result = await assistantClient.CreateMessageAsync(
                    threadId,
                    messageRole,
                    content);
                
                if (result == null || string.IsNullOrEmpty(result.Value.Id))
                {
                    throw new Exception("Invalid response from OpenAI: Message ID is missing");
                }

                _logger.LogInformation("Successfully appended message to thread {ThreadId}, message ID: {MessageId}", threadId, result.Value.Id);
                return result.Value.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending message to thread {ThreadId}", threadId);
                throw;
            }
        }

        public async Task<string> StartRunAsync(string threadId, string assistantId)
        {
            try
            {
                _logger.LogInformation("Starting run for thread {ThreadId} with assistant {AssistantId}", threadId, assistantId);

                var assistantClient = _openAiClient.GetAssistantClient();
                var runOptions = new RunCreationOptions();
                var run = await assistantClient.CreateRunAsync(threadId, assistantId, runOptions);

                if (run == null || string.IsNullOrEmpty(run.Value.Id))
                {
                    throw new Exception("Invalid response from OpenAI: Run ID is missing");
                }

                _logger.LogInformation("Successfully started run {RunId} for thread {ThreadId}", run.Value.Id, threadId);
                return run.Value.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting run for thread {ThreadId}", threadId);
                throw;
            }
        }

        public async Task<RunResponse> PollRunStatusAsync(string threadId, string runId, int maxAttempts = 0, int delayMs = 0)
        {
            try
            {
                // Use parameter values if provided, otherwise use settings
                int attemptsToUse = maxAttempts > 0 ? maxAttempts : _openAiSettings.MaxPollAttempts;
                int delayToUse = delayMs > 0 ? delayMs : _openAiSettings.PollDelayMs;
                
                _logger.LogInformation("Polling run status for run {RunId} on thread {ThreadId} (maxAttempts: {MaxAttempts}, delay: {DelayMs}ms)", 
                    runId, threadId, attemptsToUse, delayToUse);

                var assistantClient = _openAiClient.GetAssistantClient();
                RunResponse runResponse = null;
                int attempts = 0;

                while (attempts < attemptsToUse)
                {
                    attempts++;

                    var run = await assistantClient.GetRunAsync(threadId, runId);

                    if (run?.Value == null)
                    {
                        throw new Exception("Invalid response from OpenAI: Run not found");
                    }

                    var runValue = run.Value;

                    runResponse = new RunResponse
                    {
                        Id = runValue.Id,
                        Status = runValue.Status.ToString()
                    };

                    if (runValue.Status == RunStatus.RequiresAction && runValue.RequiredActions != null)
                    {
                        var mappedCalls = new List<Models.ToolCall>();

                        foreach (var action in runValue.RequiredActions)
                        {
                            // This assumes the SDK is returning function tool calls directly
                            var id = action.GetType().GetProperty("Id")?.GetValue(action)?.ToString();
                            var type = "function";
                            var name = action.GetType().GetProperty("FunctionName")?.GetValue(action)?.ToString();
                            var arguments = action.GetType().GetProperty("FunctionArguments")?.GetValue(action)?.ToString();

                            mappedCalls.Add(new Models.ToolCall
                            {
                                Id = id,
                                Type = type,
                                Function = new Models.FunctionCall
                                {
                                    Name = name,
                                    Arguments = arguments
                                }
                            });
                        }

                        runResponse.RequiredAction = new RunRequiredAction
                        {
                            SubmitToolOutputs = new SubmitToolOutputs
                            {
                                ToolCalls = mappedCalls
                            }
                        };
                    }

                    if (runValue.Status == RunStatus.Completed ||
                        runValue.Status == RunStatus.RequiresAction ||
                        runValue.Status == RunStatus.Failed ||
                        runValue.Status == RunStatus.Cancelled ||
                        runValue.Status == RunStatus.Expired)
                    {
                        _logger.LogInformation("Run {RunId} ended with status {Status} after {Attempts} attempts", runId, runValue.Status, attempts);
                        return runResponse;
                    }

                    await Task.Delay(delayToUse);
                }

                _logger.LogWarning("Reached max attempts polling run {RunId}", runId);
                return runResponse ?? new RunResponse { Id = runId, Status = "timeout" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling run status for run {RunId}", runId);
                throw;
            }
        }

        public async Task<RunResponse> SubmitToolOutputsAsync(string threadId, string runId, List<Models.ToolOutput> toolOutputs)
        {
            try
            {
                _logger.LogInformation("Submitting tool outputs for run {RunId} on thread {ThreadId}", runId, threadId);

                var assistantClient = _openAiClient.GetAssistantClient();
                
                // Convert our ToolOutput models to SDK ToolOutput objects
                
                var sdkToolOutputs = toolOutputs.Select(to =>
                new OpenAI.Assistants.ToolOutput(to.ToolCallId, to.Output)).ToList();

                var run = await assistantClient.SubmitToolOutputsToRunAsync(
                    threadId,
                    runId,
                    sdkToolOutputs);
                
                if (run == null)
                {
                    throw new Exception("Invalid response from OpenAI: Run not found");
                }

                // Convert the SDK response to our internal RunResponse model
                var runResponse = new RunResponse
                {
                    Id = run.Value.Id,
                    Status = run.Value.Status.ToString()
                };

                _logger.LogInformation("Successfully submitted tool outputs for run {RunId}, new status: {Status}", runId, runResponse.Status);
                return runResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting tool outputs for run {RunId}", runId);
                throw;
            }
        }

        public async Task<List<Models.ThreadMessage>> GetRunMessagesAsync(string threadId, string runId)
        {
            try
            {
                _logger.LogInformation("Getting messages for run {RunId} on thread {ThreadId}", runId, threadId);

                var assistantClient = _openAiClient.GetAssistantClient();

                var sdkMessages = new List<OpenAI.Assistants.ThreadMessage>();

                await foreach (var message in assistantClient.GetMessagesAsync(
                    threadId,
                    new MessageCollectionOptions { Order = MessageCollectionOrder.Ascending }))
                {
                    sdkMessages.Add(message);
                }

                var allMessages = sdkMessages.Select(m => new Models.ThreadMessage
                {
                    Id = m.Id,
                    ThreadId = m.ThreadId,
                    RunId = m.RunId ?? string.Empty,
                    Role = m.Role.ToString(),
                    CreatedAt = m.CreatedAt.ToUnixTimeSeconds(),
                    Content = m.Content
                        .Select(c => new Models.MessageContent
                        {
                            Type = "text",
                            Text = new Models.TextContent
                            {
                                Value = (c as OpenAI.Assistants.MessageContent)?.Text // or `c.Text` if accessible directly
                            }
                        }).Where(mc => mc.Text?.Value != null).ToList()

                }).ToList();

                var runMessages = allMessages
                    .Where(m => string.IsNullOrEmpty(m.RunId) || m.RunId == runId)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();
                
                // var infoString = $"Retrieved {runMessages.Count} messages for run {runId}";
                // _logger.LogInformation(infoString);
                return runMessages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for run {RunId} on thread {ThreadId}", runId, threadId);
                throw;
            }
        }

        public async Task<string> GetInitialMessageAsync(string accountId, bool hasUserProfile, bool hasGoals, string localDate, int timezoneOffsetMinutes)
        {
            try
            {
                _logger.LogInformation("Getting initial message via Assistants API for account {AccountId}", accountId);

                // 1. Check if thread exists for today; if not, create a new one
                var threadId = await CreateNewThreadAsync();

                // 2. Append system message with context
                var metadata = new
                {
                    hasUserProfile,
                    hasGoals,
                    localDate,
                    timezoneOffsetMinutes
                };

                var systemMessage = "dailyCheckIn";
                var metadataString = JsonSerializer.Serialize(metadata);
                _logger.LogInformation("Sending system message with metadata: {Metadata}", metadataString);
                
                await AppendMessageToThreadAsync(threadId, systemMessage, "system");

                // 3. Start the run with the assistant
                var runId = await StartRunAsync(threadId, _openAiSettings.AssistantId);

                // 4. Poll until the run completes
                var runResponse = await PollRunStatusAsync(threadId, runId);

                if (runResponse.Status != "completed")
                {
                    _logger.LogWarning("Run did not complete successfully. Status: {Status}", runResponse.Status);
                    return "Hello! I'm your nutrition assistant. How can I help you today?";
                }

                // 5. Get the assistant's reply
                var messages = await GetRunMessagesAsync(threadId, runId);
                var assistantMessages = messages
                    .Where(m => m.Role.ToLower() == "assistant")
                    .OrderByDescending(m => m.CreatedAt)
                    .ToList();

                if (assistantMessages.Count == 0 || !assistantMessages[0].Content.Any())
                {
                    _logger.LogWarning("No assistant messages found in the run");
                    return "Hello! I'm your nutrition assistant. How can I help you today?";
                }

                var assistantMessage = assistantMessages[0].Content
                    .FirstOrDefault(c => c.Type == "text")?.Text?.Value ?? 
                    "Hello! I'm your nutrition assistant. How can I help you today?";

                _logger.LogInformation("Successfully retrieved initial message from assistant for account {AccountId}", accountId);
                return assistantMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting initial message from assistant for account {AccountId}", accountId);
                return "Hello! I'm your nutrition assistant. How can I help you today?";
            }
        }

        public async Task<string> AppendSystemDailyCheckInAsync(string accountId, string threadId, int? timezoneOffsetMinutes)
        {
            try
            {
                _logger.LogInformation("Appending system daily check-in message for account {AccountId} to thread {ThreadId}", 
                    accountId, threadId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    throw new ArgumentException("Account ID is required", nameof(accountId));
                }
                
                if (string.IsNullOrEmpty(threadId))
                {
                    throw new ArgumentException("Thread ID is required", nameof(threadId));
                }
                
                // Fetch account info to determine if user has profile
                bool hasUserProfile = false;
                bool hasGoals = false;
                
                // Get account information from AccountsService to check if user has profile
                try
                {
                    var account = await _accountsService.GetAccountByIdAsync(accountId);
                    hasUserProfile = account?.UserProfile != null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking user profile for account {AccountId}, continuing with hasUserProfile=false", accountId);
                    // Continue with hasUserProfile = false
                }

                // Check if user has goals from DailyGoalService
                try
                {
                    var dailyGoal = await _dailyGoalService.GetOrGenerateTodayGoalAsync(accountId);
                    hasGoals = dailyGoal != null;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking goals for account {AccountId}, continuing with hasGoals=false", accountId);
                    // Continue with hasGoals = false
                }

                // Calculate local date using timezone offset
                int tzOffset = timezoneOffsetMinutes ?? 0; // Default to UTC if no timezone provided
                DateTime localDateTime = DateTime.UtcNow.AddMinutes(tzOffset);
                string localDate = localDateTime.ToString("yyyy-MM-dd");

                // Create metadata JSON
                var metadata = new
                {
                    accountId,
                    hasUserProfile,
                    hasGoals,
                    localDate,
                    timezoneOffsetMinutes = tzOffset
                };

                // Serialize the metadata
                var metadataJson = JsonSerializer.Serialize(metadata);
                _logger.LogInformation("Daily check-in metadata: {Metadata}", metadataJson);

                // Send the system message with metadata
                var messageId = await AppendMessageToThreadAsync(threadId, metadataJson, "system");
                _logger.LogInformation("Successfully appended system daily check-in message (ID: {MessageId}) for account {AccountId}", 
                    messageId, accountId);
                
                return messageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending system daily check-in message for account {AccountId} to thread {ThreadId}", 
                    accountId, threadId);
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

    public class ThreadResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
    }

    public class RunResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("required_action")]
        public RunRequiredAction RequiredAction { get; set; }
    }
}

