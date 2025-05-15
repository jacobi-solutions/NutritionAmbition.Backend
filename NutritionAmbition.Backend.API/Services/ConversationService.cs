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
        Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId);
        Task<BotMessageResponse> GetInitialMessageAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request);
        Task<AssistantRunMessageResponse> RunAssistantConversationAsync(string accountId, string message);
    }

    public class ConversationService : IConversationService
    {
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly AccountsService _accountsService;
        private readonly IOpenAiService _openAiService;
        private readonly IThreadService _threadService;
        private readonly IAssistantToolHandlerService _assistantToolHandlerService;
        private readonly ILogger<ConversationService> _logger;
        private readonly string _assistantId;

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            AccountsService accountsService,
            IOpenAiService openAiService,
            IThreadService threadService,
            IAssistantToolHandlerService assistantToolHandlerService,
            ILogger<ConversationService> logger,
            OpenAiSettings openAiSettings)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _openAiService = openAiService;
            _threadService = threadService;
            _assistantToolHandlerService = assistantToolHandlerService;
            _logger = logger;
            _assistantId = openAiSettings.AssistantId;
        }

        public async Task<BotMessageResponse> GetInitialMessageAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal)
        {
            var response = new BotMessageResponse();

            try
            {
                _logger.LogInformation("Getting initial message for account {AccountId}", accountId);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot get initial message: Account ID is null or empty");
                    response.AddError("Account ID is required.");
                    return response;
                }

                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var userPrompt = hasLoggedFirstMeal 
                    ? "The user has logged their first meal. Welcome them and ask about their goals."
                    : "Welcome the user and ask them to log their first meal.";

                if (lastLoggedDate.HasValue)
                {
                    var daysSinceLastLog = (DateTime.UtcNow - lastLoggedDate.Value).Days;
                    if (daysSinceLastLog > 0)
                    {
                        userPrompt += $" It's been {daysSinceLastLog} days since their last log. Encourage them to continue tracking.";
                    }
                }

                try
                {
                    var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                    _logger.LogInformation("Successfully generated initial message for account {AccountId}", accountId);
                    response.Message = message;
                    response.IsSuccess = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating OpenAI message for account {AccountId}: {ErrorMessage}", 
                        accountId, ex.Message);
                    
                    // Provide a fallback message rather than failing
                    _logger.LogInformation("Using fallback message for account {AccountId}", accountId);
                    response.Message = "Hello! I'm your nutrition assistant. How can I help you today?";
                    response.IsSuccess = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating initial message for account {AccountId}: {ErrorMessage}", 
                    accountId, ex.Message);
                response.AddError("Failed to generate welcome message.");
            }

            return response;
        }

        public async Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId)
        {
            var response = new MergeAnonymousAccountResponse();

            try
            {
                _logger.LogInformation("Merging anonymous account {AnonymousAccountId} to user account {UserAccountId}", 
                    anonymousAccountId, userAccountId);
                    
                if (string.IsNullOrEmpty(anonymousAccountId))
                {
                    _logger.LogWarning("Cannot merge accounts: Anonymous account ID is null or empty");
                    response.AddError("Anonymous account ID is required.");
                    return response;
                }
                
                if (string.IsNullOrEmpty(userAccountId))
                {
                    _logger.LogWarning("Cannot merge accounts: User account ID is null or empty");
                    response.AddError("User account ID is required.");
                    return response;
                }
                
                if (anonymousAccountId == userAccountId)
                {
                    _logger.LogWarning("Cannot merge accounts: Anonymous account ID and user account ID are the same");
                    response.AddError("Anonymous account and user account cannot be the same.");
                    return response;
                }

                long chatCount = await _chatMessageRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                _logger.LogInformation("Migrated {Count} chat messages from anonymous account {AnonymousAccountId} to user account {UserAccountId}",
                    chatCount, anonymousAccountId, userAccountId);
                    
                long foodCount = await _foodEntryRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                _logger.LogInformation("Migrated {Count} food entries from anonymous account {AnonymousAccountId} to user account {UserAccountId}",
                    foodCount, anonymousAccountId, userAccountId);
                    
                await _accountsService.DeleteAccountAsync(anonymousAccountId);
                _logger.LogInformation("Successfully deleted anonymous account {AnonymousAccountId} after migration", anonymousAccountId);

                response.IsSuccess = true;
                response.ChatMessagesMigrated = chatCount;
                response.FoodEntriesMigrated = foodCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging anonymous account {AnonymousAccountId} into user account {UserAccountId}: {ErrorMessage}", 
                    anonymousAccountId, userAccountId, ex.Message);
                response.AddError("Failed to merge anonymous account data.");
            }

            return response;
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

        public async Task<AssistantRunMessageResponse> RunAssistantConversationAsync(string accountId, string message)
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

                // 2. Append the user message to the thread
                try 
                {
                    await _openAiService.AppendMessageToThreadAsync(threadId, message);
                    _logger.LogInformation("Successfully appended user message to thread {ThreadId}", threadId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error appending message to thread {ThreadId} for account {AccountId}", threadId, accountId);
                    response.AddError("Failed to send your message to the assistant.");
                    return response;
                }
                
                // Log the user message to the chat history
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

                // 3. Start a run with the assistant
                string runId;
                try
                {
                    runId = await _openAiService.StartRunAsync(threadId, _assistantId);
                    _logger.LogInformation("Started run {RunId} for thread {ThreadId}", runId, threadId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting assistant run for thread {ThreadId} for account {AccountId}", threadId, accountId);
                    response.AddError("Failed to start the assistant conversation.");
                    return response;
                }

                // 4. Poll for run completion or action required
                var runResponse = await _openAiService.PollRunStatusAsync(threadId, runId);
                
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
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error submitting tool outputs for run {RunId}", runId);
                            response.AddError("Failed to process tools for the assistant.");
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
                response.IsSuccess = true;
                
                _logger.LogInformation("Successfully completed assistant conversation for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in assistant conversation for account {AccountId}: {ErrorMessage}", accountId, ex.Message);
                response.AddError($"Error in conversation: {ex.Message}");
            }

            return response;
        }
    }
} 