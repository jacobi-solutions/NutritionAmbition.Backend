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

                var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                response.Message = message;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating initial message for account {AccountId}", accountId);
                response.Message = "Hello! I'm your nutrition assistant. How can I help you today?";
                response.IsSuccess = true;
            }

            return response;
        }

        public async Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId)
        {
            var response = new MergeAnonymousAccountResponse();

            try
            {
                long chatCount = await _chatMessageRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                long foodCount = await _foodEntryRepository.UpdateAccountReferencesAsync(anonymousAccountId, userAccountId);
                await _accountsService.DeleteAccountAsync(anonymousAccountId);

                response.IsSuccess = true;
                response.ChatMessagesMigrated = chatCount;
                response.FoodEntriesMigrated = foodCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging anonymous account {AnonymousAccountId} into user account {UserAccountId}", 
                    anonymousAccountId, userAccountId);
                response.AddError("Failed to merge anonymous account data.");
            }

            return response;
        }

        public async Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal)
        {
            var response = new BotMessageResponse();

            try
            {
                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var userPrompt = "The user has just logged a meal. Provide a helpful hint or suggestion about nutrition or meal tracking.";

                var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                response.Message = message;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating post-log hint for account {AccountId}", accountId);
                response.Message = "Great job logging your meal! Keep up the good work!";
                response.IsSuccess = true;
            }

            return response;
        }

        public async Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal)
        {
            var response = new BotMessageResponse();

            try
            {
                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var userPrompt = "The user is using the app anonymously. Encourage them to create an account to save their progress.";

                var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, userPrompt);
                response.Message = message;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating anonymous warning for account {AccountId}", accountId);
                response.Message = "Consider creating an account to save your progress and access more features!";
                response.IsSuccess = true;
            }

            return response;
        }

        public async Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request)
        {
            var response = new LogChatMessageResponse();

            try
            {
                var chatMessage = new ChatMessage
                {
                    AccountId = accountId,
                    FoodEntryId = request.FoodEntryId,
                    Content = request.Content,
                    Role = request.Role == "assistant" ? MessageRoleTypes.Assistant : MessageRoleTypes.User,
                    LoggedDateUtc = DateTime.UtcNow
                };
                var messageId = await _chatMessageRepository.AddAsync(chatMessage);
                chatMessage.Id = messageId;
                response.Message = chatMessage;
                response.AnonymousAccountId = accountId;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging chat message for account {AccountId}", accountId);
                response.AddError("Failed to log chat message.");
            }

            return response;
        }

        public async Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request)
        {
            var response = new GetChatMessagesResponse();

            try
            {
                var messages = await _chatMessageRepository.GetByDateAsync(accountId, request.LoggedDateUtc);
                response.Messages = messages;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc);
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
                long deletedCount = await _chatMessageRepository.DeleteByDateAsync(accountId, request.LoggedDateUtc);
                response.MessagesDeleted = deletedCount;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing chat messages for account {AccountId}", accountId);
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
                await _openAiService.AppendMessageToThreadAsync(threadId, message);
                
                // Log the user message to the chat history
                var logUserMessageRequest = new LogChatMessageRequest
                {
                    Content = message,
                    Role = "user"
                };
                await LogMessageAsync(accountId, logUserMessageRequest);

                // 3. Start a run with the assistant
                var runId = await _openAiService.StartRunAsync(threadId, _assistantId);
                _logger.LogInformation("Started run {RunId} for thread {ThreadId}", runId, threadId);

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
                    }
                    
                    // Submit tool outputs back to OpenAI
                    if (toolOutputs.Count > 0)
                    {
                        _logger.LogInformation("Submitting {Count} tool outputs for run {RunId}", toolOutputs.Count, runId);
                        runResponse = await _openAiService.SubmitToolOutputsAsync(threadId, runId, toolOutputs);
                        
                        // Poll again for the final response
                        runResponse = await _openAiService.PollRunStatusAsync(threadId, runId);
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
                await LogMessageAsync(accountId, logAssistantMessageRequest);
                
                // 9. Set the response
                response.AssistantMessage = assistantContent;
                response.IsSuccess = true;
                
                _logger.LogInformation("Successfully completed assistant conversation for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in assistant conversation for account {AccountId}", accountId);
                response.AddError($"Error in conversation: {ex.Message}");
            }

            return response;
        }
    }
} 