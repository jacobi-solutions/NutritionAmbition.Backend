using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Repositories;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Services
{
    public interface ICoachMessageService
    {
        Task<LogCoachMessageResponse> LogMessageAsync(string accountId, LogCoachMessageRequest request);
        Task<GetCoachMessagesResponse> GetCoachMessagesAsync(string accountId, GetCoachMessagesRequest request);
        Task<ClearCoachMessagesResponse> ClearCoachMessagesAsync(string accountId, ClearCoachMessagesRequest request);
    }

    public class CoachMessageService : ICoachMessageService
    {
        private readonly CoachMessageRepository _coachMessageRepository;
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly ILogger<CoachMessageService> _logger;

        public CoachMessageService(
            CoachMessageRepository coachMessageRepository,
            FoodEntryRepository foodEntryRepository,
            ILogger<CoachMessageService> logger)
        {
            _coachMessageRepository = coachMessageRepository;
            _foodEntryRepository = foodEntryRepository;
            _logger = logger;
        }

        public async Task<LogCoachMessageResponse> LogMessageAsync(string accountId, LogCoachMessageRequest request)
        {
            var response = new LogCoachMessageResponse();
            
            try
            {
                // Validate that the FoodEntryId exists and belongs to the account
                if (string.IsNullOrEmpty(request.FoodEntryId))
                {
                    response.AddError("FoodEntryId is required");
                    _logger.LogWarning("FoodEntryId is required but was not provided for account {AccountId}", accountId);
                    return response;
                }

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

                // Create and save the coach message
                var modelCoachMessage = new NutritionAmbition.Backend.API.Models.CoachMessage
                {
                    AccountId = accountId,
                    FoodEntryId = request.FoodEntryId,
                    Message = request.Message,
                    Role = request.Role ?? "coach", // Default to "coach" if not specified
                    TimestampUtc = DateTime.UtcNow,
                    LoggedDateUtc = foodEntry.LoggedDateUtc // Use the food entry's date
                };

                var messageId = await _coachMessageRepository.AddAsync(modelCoachMessage);
                modelCoachMessage.Id = messageId;
                
                // Create data contract coach message for the response
                response.Message = modelCoachMessage;
                response.IsSuccess = true;
                
                _logger.LogInformation("Coach message logged successfully for account {AccountId} and food entry {FoodEntryId}", 
                    accountId, request.FoodEntryId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging coach message for account {AccountId}", accountId);
                response.CaptureException(ex);
            }
            
            return response;
        }
        
        public async Task<GetCoachMessagesResponse> GetCoachMessagesAsync(string accountId, GetCoachMessagesRequest request)
        {
            var response = new GetCoachMessagesResponse();
            
            try
            {
                _logger.LogInformation("Getting coach messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc.Date);
                
                // Get all coach messages for this account on the specified date
                var messages = await _coachMessageRepository.GetByDateAsync(accountId, request.LoggedDateUtc);
                
                response.Messages = messages;
                response.IsSuccess = true;
                
                _logger.LogInformation("Retrieved {Count} coach messages for account {AccountId} on date {LoggedDate}", 
                    messages.Count, accountId, request.LoggedDateUtc.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting coach messages for account {AccountId} on date {LoggedDate}", 
                    accountId, request.LoggedDateUtc.Date);
                
                response.CaptureException(ex);
            }
            
            return response;
        }
        
        public async Task<ClearCoachMessagesResponse> ClearCoachMessagesAsync(string accountId, ClearCoachMessagesRequest request)
        {
            var response = new ClearCoachMessagesResponse();
            
            try
            {
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogInformation("Clearing coach messages for account {AccountId} on date {LoggedDate}", 
                        accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogInformation("Clearing ALL coach messages for account {AccountId}", accountId);
                }
                
                // Delete coach messages based on the provided criteria
                var deletedCount = await _coachMessageRepository.DeleteByDateAsync(accountId, request.LoggedDateUtc);
                
                response.Success = true;
                response.MessagesDeleted = deletedCount;
                response.IsSuccess = true;
                
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogInformation("Successfully cleared {Count} coach messages for account {AccountId} on date {LoggedDate}", 
                        deletedCount, accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogInformation("Successfully cleared {Count} coach messages for account {AccountId}", 
                        deletedCount, accountId);
                }
            }
            catch (Exception ex)
            {
                if (request.LoggedDateUtc.HasValue)
                {
                    _logger.LogError(ex, "Error clearing coach messages for account {AccountId} on date {LoggedDate}", 
                        accountId, request.LoggedDateUtc.Value.Date);
                }
                else
                {
                    _logger.LogError(ex, "Error clearing ALL coach messages for account {AccountId}", accountId);
                }
                
                response.CaptureException(ex);
            }
            
            return response;
        }
    }
} 