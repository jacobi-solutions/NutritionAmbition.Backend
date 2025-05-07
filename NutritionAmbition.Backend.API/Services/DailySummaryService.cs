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
                response.TotalSaturatedFat = nutritionTotals.TotalSaturatedFat;
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
    }
} 