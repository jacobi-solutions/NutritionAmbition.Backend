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
        private readonly IToolDefinitionRegistry _toolDefinitionRegistry;

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            IAccountsService accountsService,
            IAssistantToolHandlerService assistantToolHandlerService,
            IDailyGoalService dailyGoalService,
            ILogger<ConversationService> logger,
            IOpenAiResponsesService openAiResponsesService,
            IToolDefinitionRegistry toolDefinitionRegistry)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _assistantToolHandlerService = assistantToolHandlerService;
            _dailyGoalService = dailyGoalService;
            _logger = logger;
            _openAiResponsesService = openAiResponsesService;
            _toolDefinitionRegistry = toolDefinitionRegistry;
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
                    response = await _openAiResponsesService.RunChatAsync(accountId, userPrompt, systemPrompt);
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
                    response = await _openAiResponsesService.RunChatAsync(accountId, userPrompt, systemPrompt);
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

                MessageRoleTypes messageRole = request.Role switch 
                { 
                    OpenAiConstants.AssistantRoleLiteral => MessageRoleTypes.Assistant, 
                    OpenAiConstants.ToolRole => MessageRoleTypes.Tool, 
                    OpenAiConstants.SystemRoleLiteral => MessageRoleTypes.System, 
                    _ => MessageRoleTypes.User 
                };

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
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Running responses conversation for account {AccountId}", accountId);

                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot run conversation: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Cannot run conversation: Message is empty for account {AccountId}", accountId);
                    response.AddError("Message is required.");
                    return response;
                }

                // Get today's messages to find the last assistant message
                var today = DateTime.UtcNow.Date;
                var todayMessages = await _chatMessageRepository.GetByDateAsync(accountId, today);
                var lastAssistantMessage = todayMessages
                    .Where(m => m.Role == MessageRoleTypes.Assistant && !string.IsNullOrEmpty(m.ResponseId))
                    .OrderByDescending(m => m.LoggedDateUtc)
                    .FirstOrDefault();

                string? previousResponseId = lastAssistantMessage?.ResponseId;

                // Build the input messages
                var inputMessages = new List<object>
                {
                    new { role = "system", content = SystemPrompts.DefaultNutritionAssistant },
                    new { role = "user", content = message }
                };

                // Run the conversation with the OpenAI Responses API
                response = await _openAiResponsesService.RunConversationRawAsync(
                    inputMessages, 
                    _toolDefinitionRegistry.GetAll().ToList(),
                    previousResponseId: previousResponseId
                );

                if (!response.IsSuccess)
                {
                    _logger.LogWarning("OpenAI Responses API returned error for account {AccountId}: {ErrorMessage}", 
                        accountId, response.Errors.FirstOrDefault());
                    return response;
                }

                // Check if we have any tool calls to process
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    _logger.LogInformation("Processing {Count} tool calls for account {AccountId}", response.ToolCalls.Count, accountId);
                    
                    // Dictionary to store tool outputs
                    var toolOutputs = new Dictionary<string, string>();
                    
                    foreach (var toolCall in response.ToolCalls)
                    {
                        _logger.LogDebug("Processing tool call: {ToolName} with ID {ToolCallId}", 
                            toolCall.Function.Name, toolCall.Id);

                        // Log the tool call
                        var toolLogResponse = await LogMessageAsync(
                            accountId,
                            new LogChatMessageRequest
                            {
                                Content = toolCall.Function.ArgumentsJson,
                                Role = "tool"
                            }
                        );

                        if (!toolLogResponse.IsSuccess)
                        {
                            _logger.LogWarning("Failed to log tool call for account {AccountId}: {ErrorMessage}", 
                                accountId, toolLogResponse.Errors.FirstOrDefault());
                            response.AddError($"Failed to log tool call: {toolCall.Function.Name}");
                            return response;
                        }

                        _logger.LogInformation("Successfully logged tool call: {ToolName}", toolCall.Function.Name);

                        try
                        {
                            // Execute the tool
                            _logger.LogDebug("Executing tool: {ToolName}", toolCall.Function.Name);
                            var toolOutput = await _assistantToolHandlerService.HandleToolCallAsync(
                                accountId,
                                toolCall.Function.Name,
                                toolCall.Function.ArgumentsJson
                            );

                            // Log the tool output
                            await LogMessageAsync(accountId, new LogChatMessageRequest { Content = toolOutput, Role = OpenAiConstants.ToolRole });

                            // Store the tool output
                            toolOutputs.Add(toolCall.Id, toolOutput);
                            _logger.LogInformation("Successfully executed tool: {ToolName}", toolCall.Function.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing tool {ToolName} for account {AccountId}", 
                                toolCall.Function.Name, accountId);
                            response.AddError($"Failed to execute tool: {toolCall.Function.Name}");
                            return response;
                        }
                    }

                    // Submit tool outputs if we have any
                    if (toolOutputs.Count > 0)
                    {
                        _logger.LogInformation("Submitting {Count} tool outputs for account {AccountId}", 
                            toolOutputs.Count, accountId);

                        try
                        {
                            // Get the final response with tool outputs
                            var finalResponse = await _openAiResponsesService.SubmitToolOutputsAsync(
                                accountId,
                                response,
                                toolOutputs,
                                SystemPrompts.DefaultNutritionAssistant,
                                message
                            );

                            if (!finalResponse.IsSuccess)
                            {
                                _logger.LogWarning("Failed to submit tool outputs for account {AccountId}: {ErrorMessage}", 
                                    accountId, finalResponse.Errors.FirstOrDefault());
                                // Don't return here, continue with the original response
                            }
                            else
                            {
                                _logger.LogInformation("Successfully received final response after tool outputs");
                                response = finalResponse;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error submitting tool outputs for account {AccountId}", accountId);
                            // Don't return here, continue with the original response
                        }
                    }
                }

                // Log the assistant's message if it exists
                if (!string.IsNullOrEmpty(response.Message))
                {
                    _logger.LogInformation("Logging assistant message for account {AccountId}", accountId);

                    var assistantLogResponse = await LogMessageAsync(
                        accountId,
                        new LogChatMessageRequest
                        {
                            Content = response.Message,
                            Role = "assistant"
                        },
                        response.ResponseId
                    );

                    if (!assistantLogResponse.IsSuccess)
                    {
                        _logger.LogWarning("Failed to log assistant message for account {AccountId}: {ErrorMessage}", 
                            accountId, assistantLogResponse.Errors.FirstOrDefault());
                        response.AddError("Failed to log assistant message.");
                        return response;
                    }

                    _logger.LogInformation("Successfully logged assistant message with ResponseId: {ResponseId}", response.ResponseId);
                }
                else if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                {
                    _logger.LogWarning("No message or tool calls to log for account {AccountId}", accountId);
                    response.AddError("No valid response content to log.");
                    return response;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running responses conversation for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("Failed to run conversation.");
            }

            return response;
        }
    }
} 