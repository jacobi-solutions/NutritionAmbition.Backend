using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IConversationService
    {
        Task<BotMessageResponse> GenerateInitialBotReplyAsync(string messageContent);
        Task<MergeAnonymousAccountResponse> MergeAnonymousAccountAsync(string anonymousAccountId, string userAccountId);
        Task<BotMessageResponse> GetInitialMessageAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetPostLogHintAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<BotMessageResponse> GetAnonymousWarningAsync(string accountId, DateTime? lastLoggedDate, bool hasLoggedFirstMeal);
        Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request);
    }

    public class ConversationService : IConversationService
    {
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly AccountsService _accountsService;
        private readonly IOpenAiService _openAiService;
        private readonly ILogger<ConversationService> _logger;

        public ConversationService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            AccountsService accountsService,
            IOpenAiService openAiService,
            ILogger<ConversationService> logger)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _accountsService = accountsService;
            _openAiService = openAiService;
            _logger = logger;
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

        public async Task<BotMessageResponse> GenerateInitialBotReplyAsync(string messageContent)
        {
            var response = new BotMessageResponse();

            try
            {
                var systemPrompt = "You are a friendly and helpful nutrition assistant. Your goal is to help users track their meals and maintain a healthy diet. Be encouraging and supportive.";
                var message = await _openAiService.CreateChatCompletionAsync(systemPrompt, messageContent);
                response.Message = message;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bot reply for message: {MessageContent}", messageContent);
                response.Message = "I'm here to help you with your nutrition goals. What would you like to know?";
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
                var messages = await _chatMessageRepository.GetByAccountIdAsync(accountId);
                response.Messages = messages;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for account {AccountId}", accountId);
                response.AddError("Failed to retrieve chat messages.");
            }

            return response;
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
    }
} 