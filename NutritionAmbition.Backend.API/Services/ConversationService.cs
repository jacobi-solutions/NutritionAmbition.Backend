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

namespace NutritionAmbition.Backend.API.Services
{
    public interface IConversationService
    {
        Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request);
        Task<AssistantRunMessageResponse> RunAssistantConversationAsync(string accountId, string message, int? timezoneOffsetMinutes = null);
    }

    public class ConversationService : IConversationService
    {
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly IAccountsService _accountsService;
        private readonly IOpenAiService _openAiService;
        private readonly IThreadService _threadService;
        private readonly IAssistantToolHandlerService _assistantToolHandlerService;
        private readonly IDailyGoalService _dailyGoalService;
        private readonly AssistantRunRepository _assistantRunRepository;
        private readonly ILogger<ConversationService> _logger;
        private readonly string _assistantId;

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            IAccountsService accountsService,
            IOpenAiService openAiService,
            IThreadService threadService,
            IAssistantToolHandlerService assistantToolHandlerService,
            IDailyGoalService dailyGoalService,
            AssistantRunRepository assistantRunRepository,
            ILogger<ConversationService> logger,
            OpenAiSettings openAiSettings)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _openAiService = openAiService;
            _threadService = threadService;
            _assistantToolHandlerService = assistantToolHandlerService;
            _dailyGoalService = dailyGoalService;
            _assistantRunRepository = assistantRunRepository;
            _logger = logger;
            _assistantId = openAiSettings.AssistantId;
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

                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var userPrompt = "The user has just logged a meal. Provide a helpful hint or suggestion about nutrition or meal tracking.";

                try
                {
                    var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                    _logger.LogInformation("Successfully generated post-log hint for account {AccountId}", accountId);
                    response.Message = message;
                    response.IsSuccess = true;
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

                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var userPrompt = "The user is using the app anonymously. Encourage them to create an account to save their progress.";

                try
                {
                    var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                    _logger.LogInformation("Successfully generated anonymous warning for account {AccountId}", accountId);
                    response.Message = message;
                    response.IsSuccess = true;
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

        public async Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request)
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

                var chatMessage = new ChatMessage
                {
                    AccountId = accountId,
                    FoodEntryId = request.FoodEntryId,
                    Content = request.Content,
                    Role = request.Role == "assistant" ? MessageRoleTypes.Assistant : MessageRoleTypes.User,
                    LoggedDateUtc = DateTime.UtcNow
                };
                
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

        public async Task<AssistantRunMessageResponse> RunAssistantConversationAsync(string accountId, string message, int? timezoneOffsetMinutes = null)
        {
            var response = new AssistantRunMessageResponse();
            
            // Set the AccountId in the response
            response.AccountId = accountId;

            try
            {
                _logger.LogInformation("Running assistant conversation for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot run assistant conversation: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }
                
                if (string.IsNullOrEmpty(message))
                {
                    _logger.LogWarning("Cannot run assistant conversation: Message is empty for account {AccountId}", accountId);
                    response.AddError("Message content is required.");
                    return response;
                }

                // Check if there's an active run for this account
                await _assistantRunRepository.ExpireStaleRunsAsync(accountId, timeoutMinutes: 5);

                var hasActiveRun = await _assistantRunRepository.HasActiveRunAsync(accountId);
                if (hasActiveRun)
                {
                    _logger.LogWarning("Account {AccountId} has an active run in progress. Cannot start a new run.", accountId);
                    response.RunStatus = "in_progress";
                    response.AssistantMessage = "Hold on one moment — I'm still working on your last request!";
                    response.IsSuccess = true;
                    return response;
                }

                // Check if this is a daily check-in request
                bool isDailyCheckIn = string.Equals(message, ConversationConstants.DAILY_CHECKIN, StringComparison.OrdinalIgnoreCase);
                
                // 1. Get or create today's thread for the user
                var threadResponse = await _threadService.GetTodayThreadAsync(accountId);
                
                if (!threadResponse.IsSuccess || string.IsNullOrEmpty(threadResponse.ThreadId))
                {
                    _logger.LogError("Failed to get thread for account {AccountId}", accountId);
                    response.AddError("Failed to initialize conversation thread.");
                    return response;
                }
                
                string threadId = threadResponse.ThreadId;
                _logger.LogInformation("Using thread {ThreadId} for account {AccountId}", threadId, accountId);

                // 2. Append the user message to the thread or system message for daily check-in
                try 
                {
                    if (isDailyCheckIn)
                    {
                        // For daily check-in, append a system message with metadata
                        await _openAiService.AppendSystemDailyCheckInAsync(accountId, threadId, timezoneOffsetMinutes);
                        _logger.LogInformation("Successfully appended system daily check-in message to thread {ThreadId}", threadId);
                    }
                    else
                    {
                        // For regular user message, append user message and log it
                        await _openAiService.AppendMessageToThreadAsync(threadId, message);
                        _logger.LogInformation("Successfully appended user message to thread {ThreadId}", threadId);

                        // Explicitly log user message since frontend no longer does it.
                        var logUserMessageRequest = new LogChatMessageRequest
                        {
                            Content = message,
                            Role = "user"
                        };

                        var logUserResult = await LogMessageAsync(accountId, logUserMessageRequest);
                        if (!logUserResult.IsSuccess)
                        {
                            _logger.LogWarning("Failed to log user message for account {AccountId}, but continuing conversation", accountId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error appending message to thread {ThreadId} for account {AccountId}", threadId, accountId);
                    response.AddError("Failed to send your message to the assistant.");
                    return response;
                }

                // 3. Start a run with the assistant
                string runId;
                try
                {
                    runId = await _openAiService.StartRunAsync(_assistantId, threadId);
                    _logger.LogInformation("Started run {RunId} for thread {ThreadId}", runId, threadId);
                    
                    // Record the run in our database
                    await _assistantRunRepository.InsertRunAsync(new AssistantRun
                    {
                        AccountId = accountId,
                        ThreadId = threadId,
                        RunId = runId,
                        Status = "in_progress",
                        StartedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting assistant run for thread {ThreadId} for account {AccountId}", threadId, accountId);
                    response.AddError("Failed to start the assistant conversation.");
                    return response;
                }

                // 4. Poll for run completion or action required
                var runResponse = await _openAiService.PollRunStatusAsync(threadId, runId);
                
                // Update the run status in our database
                await _assistantRunRepository.UpdateRunStatusAsync(runId, runResponse.Status);
                
                // 5. Process tool calls if needed
                if (runResponse.Status == "requires_action" && 
                    runResponse.RequiredAction?.SubmitToolOutputs?.ToolCalls?.Any() == true)
                {
                    _logger.LogInformation("Run {RunId} requires tool outputs", runId);
                    
                    var toolOutputs = new List<ToolOutput>();
                    
                    foreach (var toolCall in runResponse.RequiredAction.SubmitToolOutputs.ToolCalls)
                    {
                        if (toolCall.Type == "function" && AssistantToolTypes.IsValid(toolCall.Function.Name))
                        {
                            _logger.LogInformation("Processing {ToolName} call for run {RunId}", toolCall.Function.Name, runId);
                            
                            try
                            {
                                // Process the tool call request
                                var toolOutput = await _assistantToolHandlerService.HandleToolCallAsync(accountId, 
                                    toolCall.Function.Name, 
                                    toolCall.Function.Arguments);
                                
                                toolOutputs.Add(new ToolOutput
                                {
                                    ToolCallId = toolCall.Id,
                                    Output = toolOutput
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error processing tool call {ToolName} for run {RunId}", toolCall.Function.Name, runId);
                                toolOutputs.Add(new ToolOutput
                                {
                                    ToolCallId = toolCall.Id,
                                    Output = $"{{\"error\": \"Failed to process tool: {ex.Message}\"}}"
                                });
                            }
                        }
                    }
                    
                    // Submit tool outputs back to OpenAI
                    if (toolOutputs.Count > 0)
                    {
                        _logger.LogInformation("Submitting {Count} tool outputs for run {RunId}", toolOutputs.Count, runId);
                        try
                        {
                            runResponse = await _openAiService.SubmitToolOutputsAsync(threadId, runId, toolOutputs);
                            
                            // Poll again for the final response
                            runResponse = await _openAiService.PollRunStatusAsync(threadId, runId);
                            
                            // Update the final run status
                            await _assistantRunRepository.UpdateRunStatusAsync(runId, runResponse.Status);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error submitting tool outputs for run {RunId}", runId);
                            response.AddError("Failed to process tools for the assistant.");
                            
                            // Mark the run as failed
                            await _assistantRunRepository.UpdateRunStatusAsync(runId, "failed");
                            return response;
                        }
                    }
                }
                
                // 6. Check if the run completed successfully
                if (runResponse.Status != "completed")
                {
                    _logger.LogWarning("Run {RunId} did not complete successfully. Status: {Status}", runId, runResponse.Status);
                    response.AddError($"Conversation did not complete: {runResponse.Status}");
                    return response;
                }
                
                // 7. Get the assistant's response message
                var messages = await _openAiService.GetRunMessagesAsync(threadId, runId);
                var assistantMessage = messages.LastOrDefault(m => m.Role.Equals(OpenAI.Assistants.MessageRole.Assistant.ToString(), StringComparison.OrdinalIgnoreCase));
                
                if (assistantMessage == null || assistantMessage.Content == null || !assistantMessage.Content.Any())
                {
                    _logger.LogWarning("No assistant message found for run {RunId}", runId);
                    response.AddError("No response from assistant.");
                    return response;
                }
                
                // Get the text content from the message
                string assistantContent = string.Empty;
                foreach (var content in assistantMessage.Content)
                {
                    if (content.Type == "text" && content.Text != null)
                    {
                        assistantContent = content.Text.Value;
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(assistantContent))
                {
                    _logger.LogWarning("No text content found in assistant message for run {RunId}", runId);
                    response.AddError("No readable content in assistant response.");
                    return response;
                }
                
                // 8. Log the assistant's message to the chat history
                var logAssistantMessageRequest = new LogChatMessageRequest
                {
                    Content = assistantContent,
                    Role = "assistant"
                };
                
                var logAssistantResult = await LogMessageAsync(accountId, logAssistantMessageRequest);
                if (!logAssistantResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to log assistant message for account {AccountId}, but continuing with response", accountId);
                }
                
                // 9. Set the response
                response.AssistantMessage = assistantContent;
                response.RunStatus = "completed";
                response.IsSuccess = true;
                
                _logger.LogInformation("Successfully completed assistant conversation for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in assistant conversation for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("An error occurred while processing your conversation with the assistant.");
            }

            return response;
        }
    }
} 