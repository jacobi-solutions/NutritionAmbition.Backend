using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IDailyGoalService
    {
        Task<GetDailyGoalResponse> GetForDateAsync(string accountId, GetDailyGoalRequest request);
        Task<SetDailyGoalResponse> SetGoalsAsync(string accountId, SetDailyGoalRequest request);
    }

    public class DailyGoalService : IDailyGoalService
    {
        private readonly DailyGoalRepository _dailyGoalRepository;
        private readonly ILogger<DailyGoalService> _logger;
        
        public DailyGoalService(DailyGoalRepository dailyGoalRepository, ILogger<DailyGoalService> logger)
        {
            _dailyGoalRepository = dailyGoalRepository;
            _logger = logger;
        }

        public async Task<GetDailyGoalResponse> GetForDateAsync(string accountId, GetDailyGoalRequest request)
        {
            var response = new GetDailyGoalResponse();
            try
            {
                DateTime requestDate = request.Date ?? DateTime.UtcNow.Date;
                
                var dailyGoal = await _dailyGoalRepository.GetByAccountIdAndDateAsync(accountId, requestDate);
                
                if (dailyGoal == null)
                {
                    // No existing goal found, create a default one
                    dailyGoal = CreateDefaultDailyGoal(accountId, requestDate);
                    _logger.LogInformation("No existing goal found for account {AccountId} on {Date}, creating default", accountId, requestDate);
                }
                
                response.DailyGoal = dailyGoal;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily goal for account {AccountId}", accountId);
                response.AddError($"Failed to retrieve daily goal: {ex.Message}");
            }
            
            return response;
        }

        public async Task<SetDailyGoalResponse> SetGoalsAsync(string accountId, SetDailyGoalRequest request)
        {
            var response = new SetDailyGoalResponse();
            try
            {
                if (request.DailyGoal == null)
                {
                    response.AddError("Daily goal data is required");
                    return response;
                }
                
                // Ensure the AccountId is set to the current user's account
                request.DailyGoal.AccountId = accountId;
                
                // Attempt to upsert the daily goal
                bool success = await _dailyGoalRepository.UpsertAsync(request.DailyGoal);
                
                if (success)
                {
                    response.DailyGoal = request.DailyGoal;
                    response.IsSuccess = true;
                    _logger.LogInformation("Successfully set daily goal for account {AccountId} on {Date}", 
                        accountId, request.DailyGoal.EffectiveDateUtc);
                }
                else
                {
                    response.AddError("Failed to save daily goal");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting daily goal for account {AccountId}", accountId);
                response.AddError($"Failed to set daily goal: {ex.Message}");
            }
            
            return response;
        }

        private DailyGoal CreateDefaultDailyGoal(string accountId, DateTime effectiveDate)
        {
            var defaultGoal = new DailyGoal
            {
                AccountId = accountId,
                EffectiveDateUtc = effectiveDate.Date,
                BaseCalories = 2000,
                NutrientGoals = new System.Collections.Generic.List<NutrientGoal>
                {
                    new NutrientGoal { NutrientName = "Protein", MinValue = 50, Unit = "g" },
                    new NutrientGoal { NutrientName = "Carbohydrates", PercentageOfCalories = 0.5, Unit = "g" },
                    new NutrientGoal { NutrientName = "Fat", PercentageOfCalories = 0.3, Unit = "g" },
                    new NutrientGoal { NutrientName = "Saturated Fat", MaxValue = 20, Unit = "g" },
                    new NutrientGoal { NutrientName = "Fiber", MinValue = 25, Unit = "g" },
                    new NutrientGoal { NutrientName = "Sugar", MaxValue = 36, Unit = "g" },
                    new NutrientGoal { NutrientName = "Sodium", MaxValue = 2300, Unit = "mg" },
                    new NutrientGoal { NutrientName = "Calcium", MinValue = 1000, Unit = "mg" },
                    new NutrientGoal { NutrientName = "Iron", MinValue = 18, Unit = "mg" },
                    new NutrientGoal { NutrientName = "Vitamin C", MinValue = 75, Unit = "mg" },
                    new NutrientGoal { NutrientName = "Vitamin D", MinValue = 15, Unit = "μg" },
                    new NutrientGoal { NutrientName = "Potassium", MinValue = 3500, Unit = "mg" }
                }
            };
            
            return defaultGoal;
        }
    }
} 