using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using NutritionAmbition.Backend.API.Services;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Settings;
using System.Text.Json;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IConversationService
    {
        Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request, string? responseId = null);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request);
        Task<BotMessageResponse> RunResponsesConversationAsync(string accountId, string message);
    }

    public class ConversationService : IConversationService
    {
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly IAccountsService _accountsService;
        private readonly IAssistantToolHandlerService _assistantToolHandlerService;
        private readonly IDailyGoalService _dailyGoalService;
        private readonly ILogger<ConversationService> _logger;
        private readonly IOpenAiResponsesService _openAiResponsesService;
        
        private readonly List<object> _responseTools = new List<object>
        {
            new {
                type = "function",
                name = "LogMealTool",
                description = "Log a user's meal based on a natural language description.",
                parameters = new {
                    type = "object",
                    properties = new {
                        meal = new {
                            type = "string",
                            description = "A description of the user's meal, such as '2 eggs and toast with orange juice'."
                        }
                    },
                    required = new[] { "meal" }
                }
            },
            new {
                type = "function",
                name = "SaveUserProfileTool",
                description = "Save the user's basic profile information including age, sex, height, weight, and activity level. If the user gives you Height and Weight in metric units, convert them to imperial.",
                parameters = new {
                    type = "object",
                    properties = new {
                        age = new { type = "integer", description = "User's age in years" },
                        sex = new { type = "string", description = "User's biological sex, either 'male' or 'female'" },
                        heightFeet = new { type = "integer", description = "User's height in feet" },
                        heightInches = new { type = "integer", description = "Additional inches beyond the feet" },
                        weightLbs = new { type = "number", description = "User's weight in pounds" },
                        activityLevel = new { type = "string", description = "User's activity level: sedentary, light, moderate, active, or very active" }
                    },
                    required = new[] { "age", "sex", "heightFeet", "heightInches", "weightLbs", "activityLevel" }
                }
            },
            new {
                type = "function",
                name = "GetProfileAndGoalsTool",
                description = "Fetch the user's current profile and daily nutrient goals. Use this when the user asks about their goals or profile data.",
                parameters = new {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            },
            new {
                type = "function",
                name = "SetDefaultGoalProfileTool",
                description = "Set or update the user's default daily nutrition goals.",
                parameters = new {
                    type = "object",
                    properties = new {
                        baseCalories = new { type = "number", description = "The user's default daily calorie goal" },
                        nutrientGoals = new {
                            type = "array",
                            description = "List of nutrient goals to override the system defaults",
                            items = new {
                                type = "object",
                                properties = new {
                                    nutrientName = new { type = "string", description = "The name of the nutrient" },
                                    unit = new { type = "string", description = "The unit of measurement" },
                                    minValue = new { type = "number", description = "Optional lower bound" },
                                    maxValue = new { type = "number", description = "Optional upper bound" },
                                    percentageOfCalories = new { type = "number", description = "Optional % of total calories" }
                                },
                                required = new[] { "nutrientName", "unit" }
                            }
                        }
                    },
                    required = new string[] { }
                }
            },
            new {
                type = "function",
                name = "OverrideDailyGoalsTool",
                description = "Temporarily override today's nutrition goals.",
                parameters = new {
                    type = "object",
                    properties = new {
                        newBaseCalories = new { type = "number", description = "Calorie target for today only" },
                        nutrientGoals = new {
                            type = "array",
                            items = new {
                                type = "object",
                                properties = new {
                                    nutrientName = new { type = "string", description = "Nutrient name" },
                                    unit = new { type = "string", description = "Unit" },
                                    minValue = new { type = "number" },
                                    maxValue = new { type = "number" },
                                    percentageOfCalories = new { type = "number" }
                                },
                                required = new[] { "nutrientName", "unit" }
                            }
                        }
                    },
                    required = new string[] { }
                }
            },
            new {
                type = "function",
                name = "GetUserContextTool",
                description = "Fetch contextual information about the user. Call this at the beginning of every thread.",
                parameters = new {
                    type = "object",
                    properties = new { },
                    required = new string[] { }
                }
            }
        };

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            IAccountsService accountsService,
            IAssistantToolHandlerService assistantToolHandlerService,
            IDailyGoalService dailyGoalService,
            ILogger<ConversationService> logger,
            IOpenAiResponsesService openAiResponsesService)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _assistantToolHandlerService = assistantToolHandlerService;
            _dailyGoalService = dailyGoalService;
            _logger = logger;
            _openAiResponsesService = openAiResponsesService;
        }

        public async Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Getting post-log hint for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot get post-log hint: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                var systemPrompt = SystemPrompts.DefaultNutritionAssistant;
                var userPrompt = "The user has just logged a meal. Provide a helpful hint or suggestion about nutrition or meal tracking.";

                try
                {
                    // Use the OpenAI Responses API for generating the hint
                    response = await _openAiResponsesService.RunConversationAsync(accountId, userPrompt, systemPrompt);
                    _logger.LogInformation("Successfully generated post-log hint for account {AccountId}", accountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating OpenAI message for post-log hint for account {AccountId}: {ErrorMessage}", 
                        accountId, ex.Message);
                    
                    // Provide a fallback message rather than failing
                    _logger.LogInformation("Using fallback post-log hint for account {AccountId}", accountId);
                    response.Message = "Great job logging your meal! Keep up the good work!";
                    response.IsSuccess = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating post-log hint for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("Failed to generate post-log hint.");
            }

            return response;
        }

        public async Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Getting anonymous warning for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot get anonymous warning: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                var systemPrompt = SystemPrompts.DefaultNutritionAssistant;
                var userPrompt = "The user is using the app anonymously. Encourage them to create an account to save their progress.";

                try
                {
                    // Use the OpenAI Responses API for generating the warning
                    response = await _openAiResponsesService.RunConversationAsync(accountId, userPrompt, systemPrompt);
                    _logger.LogInformation("Successfully generated anonymous warning for account {AccountId}", accountId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating OpenAI message for anonymous warning for account {AccountId}: {ErrorMessage}", 
                        accountId, ex.Message);
                    
                    // Provide a fallback message rather than failing
                    _logger.LogInformation("Using fallback anonymous warning for account {AccountId}", accountId);
                    response.Message = "Consider creating an account to save your progress and access more features!";
                    response.IsSuccess = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating anonymous warning for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("Failed to generate anonymous warning message.");
            }

            return response;
        }

        public async Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request, string? responseId = null)
        {
            var response = new LogChatMessageResponse();

            try
            {
                _logger.LogInformation("Logging chat message for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot log message: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }
                
                if (string.IsNullOrEmpty(request.Content))
                {
                    _logger.LogWarning("Cannot log message: Message content is empty for account {AccountId}", accountId);
                    response.AddError("Message content is required.");
                    return response;
                }

                var messageRole = request.Role == "assistant" ? MessageRoleTypes.Assistant : 
                                  request.Role == "tool" ? MessageRoleTypes.Tool : MessageRoleTypes.User;

                var chatMessage = new ChatMessage
                {
                    AccountId = accountId,
                    FoodEntryId = request.FoodEntryId,
                    Content = request.Content,
                    Role = messageRole,
                    LoggedDateUtc = DateTime.UtcNow
                };
                
                // Set ResponseId if it's an assistant message and responseId is provided
                if (messageRole == MessageRoleTypes.Assistant && !string.IsNullOrEmpty(responseId))
                {
                    chatMessage.ResponseId = responseId;
                    _logger.LogInformation("Setting ResponseId {ResponseId} on assistant message", responseId);
                }
                
                var messageId = await _chatMessageRepository.AddAsync(chatMessage);
                _logger.LogInformation("Successfully logged chat message with ID {MessageId} for account {AccountId}", 
                    messageId, accountId);
                    
                chatMessage.Id = messageId;
                response.Message = chatMessage;
                response.AnonymousAccountId = accountId;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging chat message for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("Failed to log chat message.");
            }

            return response;
        }

        public async Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request)
        {
            var response = new GetChatMessagesResponse();

            try
            {
                _logger.LogInformation("Getting chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc);
                    
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot get messages: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                if (request.LoggedDateUtc == default)
                {
                    _logger.LogWarning("Cannot get messages: LoggedDateUtc is not specified for account {AccountId}", accountId);
                    response.AddError("Logged date is required.");
                    return response;
                }

                var messages = await _chatMessageRepository.GetByDateAsync(accountId, request.LoggedDateUtc);
                
                _logger.LogInformation("Retrieved {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, accountId, request.LoggedDateUtc);
                    
                response.Messages = messages;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for account {AccountId} on date {LoggedDate}: {ErrorMessage}", 
                    accountId, request.LoggedDateUtc, ex.Message);
                response.AddError("Failed to retrieve chat messages.");
            }

            return response;
        }

        /// <summary>
        /// Helper method to get the start and end of a UTC day
        /// </summary>
        /// <param name="dateUtc">The UTC date</param>
        /// <returns>A tuple containing the start and end of the UTC day</returns>
        private (DateTime start, DateTime end) GetStartAndEndOfUtcDay(DateTime dateUtc)
        {
            DateTime start = dateUtc.Date; // Set to start of day (00:00:00)
            DateTime end = start.AddDays(1); // Set to start of next day (00:00:00)
            return (start, end);
        }

        public async Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request)
        {
            var response = new ClearChatMessagesResponse();

            try
            {
                _logger.LogInformation("Clearing chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc);
                    
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot clear messages: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                long deletedCount = await _chatMessageRepository.DeleteByDateAsync(accountId, request.LoggedDateUtc);
                
                _logger.LogInformation("Successfully deleted {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    deletedCount, accountId, request.LoggedDateUtc);
                    
                response.MessagesDeleted = deletedCount;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing chat messages for account {AccountId} on date {LoggedDate}: {ErrorMessage}", 
                    accountId, request.LoggedDateUtc, ex.Message);
                response.AddError("Failed to clear chat messages.");
            }

            return response;
        }

        public async Task<BotMessageResponse> RunResponsesConversationAsync(string accountId, string message)
        {
            try
            {
                var response = new BotMessageResponse();
                _logger.LogInformation("Running responses conversation for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot run responses conversation: Account ID is null or empty");
                    
                    response.AddError("Account ID is required.");
                    return response;
                }
                
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Cannot run responses conversation: Message is empty for account {AccountId}", accountId);
                    response.AddError("Message content is required.");
                    return response;
                }

                response = await _openAiResponsesService.RunConversationAsync(
                    accountId, 
                    message, 
                    SystemPrompts.DefaultNutritionAssistant,
                    tools: _responseTools);
                
                // Process tool calls if present
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    _logger.LogInformation("Processing {Count} tool calls for account {AccountId}", response.ToolCalls.Count, accountId);
                    
                    // Create a dictionary to store tool outputs
                    var toolOutputs = new Dictionary<string, string>();
                    
                    foreach (var toolCall in response.ToolCalls)
                    {
                        if (toolCall.Type == "function")
                        {
                            _logger.LogInformation("Processing tool call {ToolName} with ID {ToolCallId}", 
                                toolCall.Function.Name, toolCall.Id);
                            
                            try
                            {
                                // Convert JsonElement to string for the tool handler
                                string argumentsJson = toolCall.Function.ArgumentsJson;
                                
                                // Call the tool handler
                                string toolOutput = await _assistantToolHandlerService.HandleToolCallAsync(
                                    accountId, 
                                    toolCall.Function.Name, 
                                    argumentsJson);
                                
                                _logger.LogInformation("Successfully executed tool {ToolName}, output: {ToolOutput}", 
                                    toolCall.Function.Name, toolOutput);
                                
                                // Store the tool output for submission
                                toolOutputs.Add(toolCall.Id, toolOutput);
                                
                                // Log the tool output as a chat message
                                try
                                {
                                    var logToolMessageRequest = new LogChatMessageRequest
                                    {
                                        Content = $"Tool: {toolCall.Function.Name}\nID: {toolCall.Id}\nOutput: {toolOutput}",
                                        Role = "tool"
                                    };
                                    
                                    await LogMessageAsync(accountId, logToolMessageRequest);
                                    _logger.LogInformation("Successfully logged tool output as chat message for {ToolName}", 
                                        toolCall.Function.Name);
                                }
                                catch (Exception logEx)
                                {
                                    _logger.LogWarning(logEx, "Failed to log tool output as chat message for {ToolName}, but continuing with response", 
                                        toolCall.Function.Name);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing tool call {ToolName}", toolCall.Function.Name);
                                // Add an error message as the tool output
                                toolOutputs.Add(toolCall.Id, JsonSerializer.Serialize(new { error = $"Error executing tool: {ex.Message}" }));
                                
                                // Log the error output as a chat message
                                try
                                {
                                    var logToolErrorRequest = new LogChatMessageRequest
                                    {
                                        Content = $"Tool: {toolCall.Function.Name}\nID: {toolCall.Id}\nError: {ex.Message}",
                                        Role = "tool"
                                    };
                                    
                                    await LogMessageAsync(accountId, logToolErrorRequest);
                                    _logger.LogInformation("Successfully logged tool error as chat message for {ToolName}", 
                                        toolCall.Function.Name);
                                }
                                catch (Exception logEx)
                                {
                                    _logger.LogWarning(logEx, "Failed to log tool error as chat message for {ToolName}, but continuing with response", 
                                        toolCall.Function.Name);
                                }
                            }
                        }
                    }
                    
                    // Submit tool outputs to get a final response
                    if (toolOutputs.Count > 0)
                    {
                        _logger.LogInformation("Submitting {Count} tool outputs for account {AccountId}", toolOutputs.Count, accountId);
                        
                        try
                        {
                            var finalResponse = await _openAiResponsesService.SubmitToolOutputsAsync(
                                accountId,
                                response,
                                toolOutputs,
                                SystemPrompts.DefaultNutritionAssistant,
                                message);
                                
                            if (finalResponse.IsSuccess && !string.IsNullOrEmpty(finalResponse.Message))
                            {
                                _logger.LogInformation("Received final response after submitting tool outputs");
                                response = finalResponse;
                            }
                            else
                            {
                                _logger.LogWarning("Failed to get valid response after submitting tool outputs");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error submitting tool outputs for account {AccountId}", accountId);
                        }
                    }
                }
                
                // Log the conversation if successful
                if (response.IsSuccess && !string.IsNullOrEmpty(response.Message))
                {
                    try
                    {
                        // Log user message
                        var logUserMessageRequest = new LogChatMessageRequest
                        {
                            Content = message,
                            Role = "user"
                        };
                        
                        await LogMessageAsync(accountId, logUserMessageRequest);
                        
                        // Log assistant message
                        var logAssistantMessageRequest = new LogChatMessageRequest
                        {
                            Content = response.Message,
                            Role = "assistant"
                        };
                        
                        await LogMessageAsync(accountId, logAssistantMessageRequest, response.ResponseId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to log conversation messages for account {AccountId}, but continuing with response", accountId);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RunResponsesConversationAsync for account {AccountId}", accountId);
                var response = new BotMessageResponse();
                response.AddError("Failed to run conversation using the Responses API.");
                return response;
            }
        }
    }
} 