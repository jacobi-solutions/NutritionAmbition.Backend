using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface ICoachMessageService
    {
        Task<LogCoachMessageResponse> LogMessageAsync(string accountId, LogCoachMessageRequest request);
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
                    TimestampUtc = DateTime.UtcNow
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
    }
} 