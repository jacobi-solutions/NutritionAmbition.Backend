using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IChatMessageService
    {
        Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request);
        Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request);
        Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request);
    }

    public class ChatMessageService : IChatMessageService
    {
        private readonly ChatMessageRepository _chatMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly ILogger<ChatMessageService> _logger;

        public ChatMessageService(
            ChatMessageRepository chatMessageRepository,
            FoodEntryRepository foodEntryRepository,
            ILogger<ChatMessageService> logger)
        {
            _chatMessageRepository = chatMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _logger = logger;
        }

        public async Task<LogChatMessageResponse> LogMessageAsync(string accountId, LogChatMessageRequest request)
        {
            var response = new LogChatMessageResponse();
            
            try
            {
                // Validate that the FoodEntryId exists and belongs to the account if provided
                if (!string.IsNullOrEmpty(request.FoodEntryId))
                {
                    var foodEntry = await _foodEntryRepository.GetByIdAsync(request.FoodEntryId);
                    
                    if (foodEntry == null)
                    {
                        response.AddError($"Food entry with ID {request.FoodEntryId} not found");
                        _logger.LogWarning("Food entry with ID {FoodEntryId} not found", request.FoodEntryId);
                        return response;
                    }

                    if (foodEntry.AccountId != accountId)
                    {
                        response.AddError("Food entry does not belong to the specified account");
                        _logger.LogWarning("Food entry with ID {FoodEntryId} does not belong to account {AccountId}", 
                            request.FoodEntryId, accountId);
                        return response;
                    }
                }

                // Create and save the chat message
                var chatMessage = new ChatMessage
                {
                    AccountId = accountId,
                    FoodEntryId = request.FoodEntryId,
                    Content = request.Content,
                    Role = request.Role == "assistant" ? MessageRole.Assistant : MessageRole.User,
                    LoggedDateUtc = DateTime.UtcNow
                };

                var messageId = await _chatMessageRepository.AddAsync(chatMessage);
                chatMessage.Id = messageId;
                
                // Create data contract for the response
                response.Message = chatMessage;
                response.IsSuccess = true;
                
                _logger.LogInformation("Chat message logged successfully for account {AccountId} with role {Role}", 
                    accountId, chatMessage.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging chat message for account {AccountId}", accountId);
                response.CaptureException(ex);
            }
            
            return response;
        }
        
        public async Task<GetChatMessagesResponse> GetChatMessagesAsync(string accountId, GetChatMessagesRequest request)
        {
            var response = new GetChatMessagesResponse();
            
            try
            {
                _logger.LogInformation("Getting chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc.Date);
                
                // Get all chat messages for this account on the specified date
                var messages = await _chatMessageRepository.GetByDateAsync(accountId, request.LoggedDateUtc);
                
                response.Messages = messages;
                response.IsSuccess = true;
                
                _logger.LogInformation("Retrieved {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, accountId, request.LoggedDateUtc.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc.Date);
                
                response.CaptureException(ex);
            }
            
            return response;
        }
        
        public async Task<ClearChatMessagesResponse> ClearChatMessagesAsync(string accountId, ClearChatMessagesRequest request)
        {
            var response = new ClearChatMessagesResponse();
            
            try
            {
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogInformation("Clearing chat messages for account {AccountId} on date {LoggedDate}", 
                        accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogInformation("Clearing ALL chat messages for account {AccountId}", accountId);
                }
                
                // Delete chat messages based on the provided criteria
                var deletedCount = await _chatMessageRepository.DeleteByDateAsync(accountId, request.LoggedDateUtc);
                
                response.Success = true;
                response.MessagesDeleted = deletedCount;
                response.IsSuccess = true;
                
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogInformation("Successfully cleared {Count} chat messages for account {AccountId} on date {LoggedDate}", 
                        deletedCount, accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogInformation("Successfully cleared {Count} chat messages for account {AccountId}", 
                        deletedCount, accountId);
                }
            }
            catch (Exception ex)
            {
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogError(ex, "Error clearing chat messages for account {AccountId} on date {LoggedDate}", 
                        accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogError(ex, "Error clearing ALL chat messages for account {AccountId}", accountId);
                }
                
                response.CaptureException(ex);
            }
            
            return response;
        }
    }
} 