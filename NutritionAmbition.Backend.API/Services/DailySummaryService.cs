using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IDailySummaryService
    {
        Task<DailySummaryResponse> GetDailySummaryAsync(string accountId);
        Task<NutritionSummaryResponse> GetWeeklySummaryAsync(string accountId, DateTime startDateUtc);
        Task<NutritionSummaryResponse> GetMonthlySummaryAsync(string accountId, DateTime startDateUtc);
    }
    
    public class DailySummaryService : IDailySummaryService
    {
        private readonly FoodEntryRepository _repo;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly ILogger<DailySummaryService> _logger;

        public DailySummaryService(
            FoodEntryRepository repo,
            INutritionCalculationService nutritionCalculationService,
            ILogger<DailySummaryService> logger)
        {
            _repo = repo;
            _nutritionCalculationService = nutritionCalculationService;
            _logger = logger;
        }

        public async Task<DailySummaryResponse> GetDailySummaryAsync(string accountId)
        {
            var response = new DailySummaryResponse();
            try
            {
                var entries = await _repo.GetFoodEntriesByAccountAndDateAsync(accountId, DateTime.UtcNow.Date);
                if (!entries.Any())
                {
                    _logger.LogInformation("No food entries found for account {AccountId} on {Date}", accountId, DateTime.UtcNow.Date);
                    response.IsSuccess = true;
                    return response;
                }

                var foodItems = _nutritionCalculationService.FlattenEntries(entries);
                var nutritionTotals = _nutritionCalculationService.CalculateTotals(foodItems);

                // Map nutrition totals to the response
                response.TotalCalories = nutritionTotals.TotalCalories;
                response.TotalProtein = nutritionTotals.TotalProtein;
                response.TotalCarbohydrates = nutritionTotals.TotalCarbohydrates;
                response.TotalFat = nutritionTotals.TotalFat;
                response.TotalMicronutrients = nutritionTotals.TotalMicronutrients;

                response.IsSuccess = true;
                _logger.LogInformation("Successfully generated daily summary for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily summary for {AccountId}", accountId);
                response.AddError(ex.Message);
            }
            return response;
        }

        

        /// <summary>
        /// Gets a weekly nutrition summary starting from a specific date
        /// </summary>
        /// <param name="accountId">The account ID</param>
        /// <param name="startDateUtc">The start date of the week (in UTC)</param>
        /// <returns>A nutrition summary response with calculated totals for the week</returns>
        public async Task<NutritionSummaryResponse> GetWeeklySummaryAsync(string accountId, DateTime startDateUtc)
        {
            var startDate = startDateUtc.Date;
            var endDate = startDate.AddDays(7).AddTicks(-1); // One week (7 days)
            
            var response = new NutritionSummaryResponse
            {
                PeriodStart = startDate,
                PeriodEnd = endDate
            };

            try
            {
                _logger.LogInformation("Generating weekly nutrition summary for account {AccountId} from {StartDate} to {EndDate}", 
                    accountId, startDate, endDate);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot generate weekly summary with empty account ID");
                    return response;
                }

                // Get all food entries for the week
                var entries = await _repo.GetByDateRangeAsync(accountId, startDate, endDate);
                
                if (entries == null || !entries.Any())
                {
                    _logger.LogInformation("No food entries found for account {AccountId} from {StartDate} to {EndDate}", 
                        accountId, startDate, endDate);
                    return response;
                }

                // Use the nutrition calculation service to compute the totals
                var foodItems = _nutritionCalculationService.FlattenEntries(entries);
                var nutritionTotals = _nutritionCalculationService.CalculateTotals(foodItems);

                // Map the values to the response
                response.TotalCalories = nutritionTotals.TotalCalories;
                
                response.Macronutrients = new MacronutrientsSummary
                {
                    Calories = nutritionTotals.TotalCalories,
                    Protein = nutritionTotals.TotalProtein,
                    Carbohydrates = nutritionTotals.TotalCarbohydrates,
                    Fat = nutritionTotals.TotalFat,
                };
                
                // Add all micronutrients from the calculation
                response.Micronutrients = nutritionTotals.TotalMicronutrients;
                
                _logger.LogInformation("Successfully generated weekly nutrition summary for account {AccountId} - {Calories} calories", 
                    accountId, response.TotalCalories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly nutrition summary for account {AccountId} from {StartDate} to {EndDate}", 
                    accountId, startDate, endDate);
            }
            
            return response;
        }

        /// <summary>
        /// Gets a monthly nutrition summary starting from a specific date
        /// </summary>
        /// <param name="accountId">The account ID</param>
        /// <param name="startDateUtc">The start date of the month (in UTC)</param>
        /// <returns>A nutrition summary response with calculated totals for the month</returns>
        public async Task<NutritionSummaryResponse> GetMonthlySummaryAsync(string accountId, DateTime startDateUtc)
        {
            var startDate = startDateUtc.Date;
            var endDate = startDate.AddMonths(1).AddTicks(-1); // One month from start date
            
            var response = new NutritionSummaryResponse
            {
                PeriodStart = startDate,
                PeriodEnd = endDate
            };

            try
            {
                _logger.LogInformation("Generating monthly nutrition summary for account {AccountId} from {StartDate} to {EndDate}", 
                    accountId, startDate, endDate);
                
                if (string.IsNullOrEmpty(accountId))
                {
                    _logger.LogWarning("Cannot generate monthly summary with empty account ID");
                    return response;
                }

                // Get all food entries for the month
                var entries = await _repo.GetByDateRangeAsync(accountId, startDate, endDate);
                
                if (entries == null || !entries.Any())
                {
                    _logger.LogInformation("No food entries found for account {AccountId} from {StartDate} to {EndDate}", 
                        accountId, startDate, endDate);
                    return response;
                }

                // Use the nutrition calculation service to compute the totals
                var foodItems = _nutritionCalculationService.FlattenEntries(entries);
                var nutritionTotals = _nutritionCalculationService.CalculateTotals(foodItems);

                // Map the values to the response
                response.TotalCalories = nutritionTotals.TotalCalories;
                
                response.Macronutrients = new MacronutrientsSummary
                {
                    Calories = nutritionTotals.TotalCalories,
                    Protein = nutritionTotals.TotalProtein,
                    Carbohydrates = nutritionTotals.TotalCarbohydrates,
                    Fat = nutritionTotals.TotalFat,
                };
                
                // Add all micronutrients from the calculation
                response.Micronutrients = nutritionTotals.TotalMicronutrients;
                
                _logger.LogInformation("Successfully generated monthly nutrition summary for account {AccountId} - {Calories} calories", 
                    accountId, response.TotalCalories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating monthly nutrition summary for account {AccountId} from {StartDate} to {EndDate}", 
                    accountId, startDate, endDate);
            }
            
            return response;
        }
    }
} 