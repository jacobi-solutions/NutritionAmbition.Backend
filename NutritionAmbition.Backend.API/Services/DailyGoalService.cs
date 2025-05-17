using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IDailyGoalService
    {
        Task<GetDailyGoalResponse> GetForDateAsync(string accountId, GetDailyGoalRequest request);
        Task<SetDailyGoalResponse> SetGoalsAsync(string accountId, SetDailyGoalRequest request);
        Task<DailyGoal> GetOrGenerateTodayGoalAsync(string accountId);
        Task<DailyGoal?> GetGoalByDateAsync(string accountId, DateTime dateUtc);
        Task<bool> HasDefaultGoalProfileAsync(string accountId);
    }

    public class DailyGoalService : IDailyGoalService
    {
        private readonly DailyGoalRepository _dailyGoalRepository;
        private readonly DefaultGoalProfileRepository _defaultGoalProfileRepository;
        private readonly IGoalScaffoldingService _goalScaffoldingService;
        private readonly ILogger<DailyGoalService> _logger;
        
        public DailyGoalService(
            DailyGoalRepository dailyGoalRepository, 
            DefaultGoalProfileRepository defaultGoalProfileRepository,
            IGoalScaffoldingService goalScaffoldingService,
            ILogger<DailyGoalService> logger)
        {
            _dailyGoalRepository = dailyGoalRepository;
            _defaultGoalProfileRepository = defaultGoalProfileRepository;
            _goalScaffoldingService = goalScaffoldingService;
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

        public async Task<DailyGoal> GetOrGenerateTodayGoalAsync(string accountId)
        {
            var today = DateTime.UtcNow.Date;
            var existingGoal = await _dailyGoalRepository.GetByAccountIdAndDateAsync(accountId, today);
            if (existingGoal != null)
                return existingGoal;

            var defaultProfile = await _defaultGoalProfileRepository.GetByAccountIdAsync(accountId);
            var baseCalories = defaultProfile?.BaseCalories ?? 2000;
            var nutrientGoals = defaultProfile?.NutrientGoals != null && defaultProfile.NutrientGoals.Any()
                ? defaultProfile.NutrientGoals
                : _goalScaffoldingService.GenerateNutrientGoals(baseCalories);

            var todayGoal = new DailyGoal
            {
                AccountId = accountId,
                EffectiveDateUtc = today,
                BaseCalories = baseCalories,
                NutrientGoals = nutrientGoals
            };

            await _dailyGoalRepository.CreateAsync(todayGoal);
            return todayGoal;
        }

        public async Task<DailyGoal?> GetGoalByDateAsync(string accountId, DateTime dateUtc)
        {
            return await _dailyGoalRepository.GetByAccountIdAndDateAsync(accountId, dateUtc);
        }

        private DailyGoal CreateDefaultDailyGoal(string accountId, DateTime effectiveDate)
        {
            const double DEFAULT_CALORIES = 2000;
            
            var defaultGoal = new DailyGoal
            {
                AccountId = accountId,
                EffectiveDateUtc = effectiveDate.Date,
                BaseCalories = DEFAULT_CALORIES,
                NutrientGoals = _goalScaffoldingService.GenerateNutrientGoals(DEFAULT_CALORIES)
            };
            
            return defaultGoal;
        }

        public async Task<bool> HasDefaultGoalProfileAsync(string accountId)
        {
            try
            {
                _logger.LogInformation("Checking if account {AccountId} has a default goal profile", accountId);
                var defaultProfile = await _defaultGoalProfileRepository.GetByAccountIdAsync(accountId);
                bool hasDefaultProfile = defaultProfile != null;
                _logger.LogInformation("Account {AccountId} has default goal profile: {HasProfile}", accountId, hasDefaultProfile);
                return hasDefaultProfile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if account {AccountId} has a default goal profile", accountId);
                return false;
            }
        }
    }
} 