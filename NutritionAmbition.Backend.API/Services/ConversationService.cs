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
        Task<LogChatMessageResponse> LogMessageAsync(Account account, LogChatMessageRequest request, string? responseId = null);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(Account account, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(Account account, ClearChatMessagesRequest request);
        Task<BotMessageResponse> RunResponsesConversationAsync(Account account, string message);
        Task<BotMessageResponse> RunFocusInChatAsync(Account account, FocusInChatRequest request);
        Task<BotMessageResponse> RunLearnMoreAboutAsync(Account account, LearnMoreAboutRequest request);
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
        private readonly ISystemPromptResolver _systemPromptResolver;

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            IAccountsService accountsService,
            IAssistantToolHandlerService assistantToolHandlerService,
            IDailyGoalService dailyGoalService,
            ILogger<ConversationService> logger,
            IOpenAiResponsesService openAiResponsesService,
            IToolDefinitionRegistry toolDefinitionRegistry,
            ISystemPromptResolver systemPromptResolver)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _assistantToolHandlerService = assistantToolHandlerService;
            _dailyGoalService = dailyGoalService;
            _logger = logger;
            _openAiResponsesService = openAiResponsesService;
            _toolDefinitionRegistry = toolDefinitionRegistry;
            _systemPromptResolver = systemPromptResolver;
        }

        


        public async Task<LogChatMessageResponse> LogMessageAsync(Account account, LogChatMessageRequest request, string? responseId = null)
        {
            var response = new LogChatMessageResponse();

            try
            {
                _logger.LogInformation("Logging chat message for account {AccountId}", account);
                
                
                if (string.IsNullOrEmpty(request.Content))
                {
                    _logger.LogWarning("Cannot log message: Message content is empty for account {AccountId}", account.Id);
                    response.AddError("Message content is required.");
                    return response;
                }

                MessageRoleTypes messageRole = request.Role switch 
                { 
                    OpenAiConstants.AssistantRoleLiteral => MessageRoleTypes.Assistant, 
                    OpenAiConstants.ContextNoteLiteral => MessageRoleTypes.ContextNote, 
                    OpenAiConstants.ToolRole => MessageRoleTypes.Tool, 
                    OpenAiConstants.SystemRoleLiteral => MessageRoleTypes.System, 
                    _ => MessageRoleTypes.User 
                };

                var chatMessage = new ChatMessage
                {
                    AccountId = account.Id,
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
                    messageId, account);
                    
                chatMessage.Id = messageId;
                response.Message = chatMessage;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging chat message for account {AccountId}: {ErrorMessage}", 
                    account, ex.Message);
                response.AddError("Failed to log chat message.");
            }

            return response;
        }

        public async Task<GetChatMessagesResponse> GetChatMessagesAsync(Account account, GetChatMessagesRequest request)
        {
            var response = new GetChatMessagesResponse();

            try
            {
                _logger.LogInformation("Getting chat messages for account {AccountId} on date {LoggedDate}", 
                    account, request.LoggedDateUtc);
                    
             
                if (request.LoggedDateUtc == default)
                {
                    _logger.LogWarning("Cannot get messages: LoggedDateUtc is not specified for account {AccountId}", account);
                    response.AddError("Logged date is required.");
                    return response;
                }

                var messages = await _chatMessageRepository.GetByDateAsync(account.Id, request.LoggedDateUtc);
                
                _logger.LogInformation("Retrieved {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, account, request.LoggedDateUtc);
                    
                response.Messages = messages;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for account {AccountId} on date {LoggedDate}: {ErrorMessage}", 
                    account, request.LoggedDateUtc, ex.Message);
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

        /// <summary>
        /// Runs a conversation with OpenAI and handles tool calls if any are present.
        /// </summary>
        /// <param name="account">The account ID for logging and repository calls</param>
        /// <param name="inputMessages">Messages to send to OpenAI</param>
        /// <param name="systemPrompt">The system prompt used in the conversation</param>
        /// <param name="userMessage">Optional original user message for logging context or tool prompt fallback</param>
        /// <returns>A fully populated BotMessageResponse with tool call handling completed</returns>
        private async Task<BotMessageResponse> RunConversationWithToolsAsync(
            Account account,
            List<object> inputMessages,
            string systemPrompt,
            string? userMessage = null)
        {
            var response = new BotMessageResponse();

            try
            {
                // Get today's messages to find the last assistant message (for response continuity)
                var today = DateTime.UtcNow.Date;
                var todayMessages = await _chatMessageRepository.GetByDateAsync(account.Id, today);
                var lastAssistantMessage = todayMessages
                    .Where(m => m.Role == MessageRoleTypes.Assistant && !string.IsNullOrEmpty(m.ResponseId))
                    .OrderByDescending(m => m.LoggedDateUtc)
                    .FirstOrDefault();

                string? previousResponseId = lastAssistantMessage?.ResponseId;

                // Check if we're in a goal-setting context and override the system prompt
                if (userMessage?.ToLowerInvariant().Contains("goal") == true || 
                    (systemPrompt.Contains("wants to discuss") && systemPrompt.Contains("goals")))
                {
                    _logger.LogInformation("Detected goal-setting intent — switching to GoalSettingTemplate for account {AccountId}", account);
                    systemPrompt = SystemPrompts.GoalSettingTemplate;
                    
                    // Update the system message in inputMessages if it exists
                    for (int i = 0; i < inputMessages.Count; i++)
                    {
                        var message = inputMessages[i];
                        if (message is IDictionary<string, object> dict && 
                            dict.ContainsKey("role") && 
                            dict["role"]?.ToString() == "system")
                        {
                            dict["content"] = systemPrompt;
                            break;
                        }
                    }
                    
                    // Add a context note
                    await LogMessageAsync(account, new LogChatMessageRequest
                    {
                        Content = "Entering Goal Setting Mode",
                        Role = OpenAiConstants.ContextNoteLiteral
                    });
                }

                // Run the conversation with the OpenAI Responses API
                response = await _openAiResponsesService.RunConversationRawAsync(
                    inputMessages, 
                    _toolDefinitionRegistry.GetAll().ToList(),
                    previousResponseId: previousResponseId
                );

                if (!response.IsSuccess)
                {
                    _logger.LogWarning("OpenAI Responses API returned error for account {AccountId}: {ErrorMessage}", 
                        account, response.Errors.FirstOrDefault());
                    return response;
                }

                // Check if we have any tool calls to process
                if (response.ToolCalls != null && response.ToolCalls.Count > 0)
                {
                    _logger.LogInformation("Processing {Count} tool calls for account {AccountId}", response.ToolCalls.Count, account);
                    
                    // Dictionary to store tool outputs
                    var toolOutputs = new Dictionary<string, string>();
                    
                    foreach (var toolCall in response.ToolCalls)
                    {
                        _logger.LogDebug("Processing tool call: {ToolName} with ID {ToolCallId}", 
                            toolCall.Function.Name, toolCall.Id);

                        // Log the tool call
                        var toolLogResponse = await LogMessageAsync(
                            account,
                            new LogChatMessageRequest
                            {
                                Content = toolCall.Function.ArgumentsJson,
                                Role = "tool"
                            }
                        );

                        if (!toolLogResponse.IsSuccess)
                        {
                            _logger.LogWarning("Failed to log tool call for account {AccountId}: {ErrorMessage}", 
                                account, toolLogResponse.Errors.FirstOrDefault());
                            response.AddError($"Failed to log tool call: {toolCall.Function.Name}");
                            return response;
                        }

                        _logger.LogInformation("Successfully logged tool call: {ToolName}", toolCall.Function.Name);

                        try
                        {
                            // Execute the tool
                            _logger.LogDebug("Executing tool: {ToolName}", toolCall.Function.Name);
                            var toolOutput = await _assistantToolHandlerService.HandleToolCallAsync(
                                account,
                                toolCall.Function.Name,
                                toolCall.Function.ArgumentsJson
                            );

                            // Log the tool output
                            await LogMessageAsync(account, new LogChatMessageRequest { Content = toolOutput, Role = OpenAiConstants.ToolRole });

                            // Store the tool output
                            toolOutputs.Add(toolCall.Id, toolOutput);
                            _logger.LogInformation("Successfully executed tool: {ToolName}", toolCall.Function.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error executing tool {ToolName} for account {AccountId}", 
                                toolCall.Function.Name, account);
                            response.AddError($"Failed to execute tool: {toolCall.Function.Name}");
                            return response;
                        }
                    }

                    // Submit tool outputs if we have any
                    if (toolOutputs.Count > 0)
                    {
                        _logger.LogInformation("Submitting {Count} tool outputs for account {AccountId}", 
                            toolOutputs.Count, account);

                        try
                        {
                            // Get the final response with tool outputs
                            var finalResponse = await _openAiResponsesService.SubmitToolOutputsAsync(
                                account.Id,
                                response,
                                toolOutputs,
                                _systemPromptResolver.GetPromptForToolContext(response.ToolCalls),
                                userMessage ?? "Continuing conversation after tool execution"
                            );

                            if (!finalResponse.IsSuccess)
                            {
                                _logger.LogWarning("Failed to submit tool outputs for account {AccountId}: {ErrorMessage}", 
                                    account, finalResponse.Errors.FirstOrDefault());
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
                            _logger.LogError(ex, "Error submitting tool outputs for account {AccountId}", account);
                            // Don't return here, continue with the original response
                        }
                    }
                }

                // Log the assistant's message if it exists
                if (!string.IsNullOrEmpty(response.Message))
                {
                    _logger.LogInformation("Logging assistant message for account {AccountId}", account);

                    var assistantLogResponse = await LogMessageAsync(
                        account,
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
                            account, assistantLogResponse.Errors.FirstOrDefault());
                        response.AddError("Failed to log assistant message.");
                        return response;
                    }

                    _logger.LogInformation("Successfully logged assistant message with ResponseId: {ResponseId}", response.ResponseId);
                }
                else if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                {
                    _logger.LogWarning("No message or tool calls to log for account {AccountId}", account);
                    response.AddError("No valid response content to log.");
                    return response;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running conversation with tools for account {AccountId}: {ErrorMessage}", 
                    account, ex.Message);
                response.AddError("Failed to run conversation with tools.");
            }

            return response;
        }

        public async Task<ClearChatMessagesResponse> ClearChatMessagesAsync(Account account, ClearChatMessagesRequest request)
        {
            var response = new ClearChatMessagesResponse();

            try
            {
                _logger.LogInformation("Clearing chat messages for account {AccountId} on date {LoggedDate}", 
                    account, request.LoggedDateUtc);
                    

                long deletedCount = await _chatMessageRepository.DeleteByDateAsync(account.Id, request.LoggedDateUtc);
                
                _logger.LogInformation("Successfully deleted {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    deletedCount, account, request.LoggedDateUtc);
                    
                response.MessagesDeleted = deletedCount;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing chat messages for account {AccountId} on date {LoggedDate}: {ErrorMessage}", 
                    account, request.LoggedDateUtc, ex.Message);
                response.AddError("Failed to clear chat messages.");
            }

            return response;
        }

        public async Task<BotMessageResponse> RunResponsesConversationAsync(Account account, string message)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Running responses conversation for account {AccountId}", account);

                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Cannot run conversation: Message is empty for account {AccountId}", account);
                    response.AddError("Message is required.");
                    return response;
                }

                // Log the user's message first
                var userMessageLogResponse = await LogMessageAsync(
                    account,
                    new LogChatMessageRequest
                    {
                        Content = message,
                        Role = OpenAiConstants.UserRoleLiteral  // This is "user"
                    }
                );

                if (!userMessageLogResponse.IsSuccess)
                {
                    _logger.LogWarning("Failed to log user message for account {AccountId}: {ErrorMessage}", 
                        account, userMessageLogResponse.Errors.FirstOrDefault());
                    response.AddError("Failed to log user message.");
                    return response;
                }

                _logger.LogInformation("Successfully logged user message for account {AccountId}", account);

                // Get system prompt from default initially
                string systemPrompt = SystemPrompts.DefaultNutritionAssistant;

                // Build the input messages
                var inputMessages = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = message }
                };

                // Run the conversation with the OpenAI Responses API
                response = await RunConversationWithToolsAsync(account, inputMessages, systemPrompt, message);

                if (!response.IsSuccess)
                {
                    _logger.LogWarning("OpenAI Responses API returned error for account {AccountId}: {ErrorMessage}", 
                        account, response.Errors.FirstOrDefault());
                    return response;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running responses conversation for account {AccountId}: {ErrorMessage}", 
                    account, ex.Message);
                response.AddError("Failed to run conversation.");
            }

            return response;
        }

        public async Task<BotMessageResponse> RunFocusInChatAsync(Account account, FocusInChatRequest request)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Running focus-in-chat for account {AccountId} with focus: {FocusText}", 
                    account, request.FocusText);

                if (string.IsNullOrEmpty(request.FocusText))
                {
                    _logger.LogWarning("Cannot run focus-in-chat: Focus text is empty for account {AccountId}", account);
                    response.AddError("Focus text is required.");
                    return response;
                }

                // Create the specialized system prompt using the template from SystemPrompts
                string systemPrompt = string.Format(SystemPrompts.FocusInChatTemplate, request.FocusText);

                // Log the system message first
                await LogMessageAsync(
                    account,
                    new LogChatMessageRequest
                    {
                        Content = systemPrompt,
                        Role = OpenAiConstants.SystemRoleLiteral
                    }
                );

                // Create a user message based on the focus text
                string userMessage = $"I'd like to learn more about {request.FocusText}";
                string contextNote = $"Focusing on: {request.FocusText}";

                await LogMessageAsync(
                    account,
                    new LogChatMessageRequest
                    {
                        Content = contextNote,
                        Role = OpenAiConstants.ContextNoteLiteral
                    }
                );

                // Build the input messages
                var inputMessages = new List<object>
                {
                    new { role = OpenAiConstants.SystemRoleLiteral, content = systemPrompt },
                    new { role = OpenAiConstants.UserRoleLiteral, content = userMessage }
                };

                // Run the conversation with the OpenAI Responses API
                response = await RunConversationWithToolsAsync(account, inputMessages, systemPrompt, userMessage);

                if (!response.IsSuccess)
                {
                    _logger.LogWarning("OpenAI Responses API returned error for focus-in-chat for account {AccountId}: {ErrorMessage}", 
                        account, response.Errors.FirstOrDefault());
                    return response;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running focus-in-chat for account {AccountId}: {ErrorMessage}", 
                    account, ex.Message);
                response.AddError("Failed to run focus-in-chat conversation.");
            }

            return response;
        }

        public async Task<BotMessageResponse> RunLearnMoreAboutAsync(Account account, LearnMoreAboutRequest request)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Running learn-more-about for account {AccountId} with topic: {Topic}", 
                    account, request.Topic);

                if (string.IsNullOrEmpty(request.Topic))
                {
                    _logger.LogWarning("Cannot run learn-more-about: Topic is empty for account {AccountId}", account);
                    response.AddError("Topic is required.");
                    return response;
                }

                // Create the specialized system prompt using the template from SystemPrompts
                string systemPrompt = string.Format(SystemPrompts.LearnMoreAboutTemplate, request.Topic);

                // Log the system message first
                await LogMessageAsync(
                    account,
                    new LogChatMessageRequest
                    {
                        Content = systemPrompt,
                        Role = OpenAiConstants.SystemRoleLiteral
                    }
                );

                // Create a user message based on the topic
                string userMessage = $"I'd like to learn more about {request.Topic}";
                string contextNote = $"Learning more about: {request.Topic}";

                await LogMessageAsync(
                    account,
                    new LogChatMessageRequest
                    {
                        Content = contextNote,
                        Role = OpenAiConstants.ContextNoteLiteral
                    }
                );

                // Build the input messages
                var inputMessages = new List<object>
                {
                    new { role = OpenAiConstants.SystemRoleLiteral, content = systemPrompt },
                    new { role = OpenAiConstants.UserRoleLiteral, content = userMessage }
                };

                // Run the conversation with the OpenAI Responses API
                response = await RunConversationWithToolsAsync(account, inputMessages, systemPrompt, userMessage);

                if (!response.IsSuccess)
                {
                    _logger.LogWarning("OpenAI Responses API returned error for learn-more-about for account {AccountId}: {ErrorMessage}", 
                        account, response.Errors.FirstOrDefault());
                    return response;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running learn-more-about for account {AccountId}: {ErrorMessage}", 
                    account, ex.Message);
                response.AddError("Failed to run learn-more-about conversation.");
            }

            return response;
        }
    }
} 