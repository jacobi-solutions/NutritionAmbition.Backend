using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Models;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAssistantToolService
    {
        Task<LogMealToolResponse> LogMealToolAsync(string accountId, string meal);
        Task<SaveProfileAndGoalsResponse> SaveProfileAndGoalsToolAsync(SaveProfileAndGoalsRequest request);
        Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(string accountId);
    }

    public class AssistantToolService : IAssistantToolService
    {
        private readonly INutritionService _nutritionService;
        private readonly IProfileService _profileService;
        private readonly ILogger<AssistantToolService> _logger;

        public AssistantToolService(
            INutritionService nutritionService,
            IProfileService profileService,
            ILogger<AssistantToolService> logger)
        {
            _nutritionService = nutritionService;
            _profileService = profileService;
            _logger = logger;
        }

        public async Task<LogMealToolResponse> LogMealToolAsync(string accountId, string meal)
        {
            var response = new LogMealToolResponse();

            try
            {
                _logger.LogInformation("Processing assistant meal logging request for account {AccountId}: {Meal}", accountId, meal);

                // Get nutrition data using the smart nutrition service
                var nutritionResponse = await _nutritionService.GetSmartNutritionDataAsync(accountId, meal);

                if (!nutritionResponse.IsSuccess)
                {
                    _logger.LogWarning("Failed to process meal for account {AccountId}: {Errors}", 
                        accountId, string.Join(", ", nutritionResponse.Errors.Select(e => e.ErrorMessage)));
                    response.AddError("Failed to process meal. Please try a more specific description.");
                    return response;
                }

                // Check if we got valid nutrition data
                if (nutritionResponse.Foods == null || !nutritionResponse.Foods.Any())
                {
                    _logger.LogWarning("No nutrition data found for account {AccountId} meal: {Meal}", accountId, meal);
                    response.AddError("No foods could be identified in your meal description.");
                    return response;
                }

                // Log that the meal was already processed by the nutrition service
                _logger.LogInformation("Meal has been processed and saved via NutritionService for account {AccountId}", accountId);

                // Calculate total calories with proper casting and rounding
                int totalCalories = (int)Math.Round(nutritionResponse.Foods.Sum(f => f.Calories));

                // Generate a summary response
                response.Summary = $"Logged your meal: {meal} ({totalCalories} kcal).";
                response.IsSuccess = true;

                _logger.LogInformation("Successfully logged meal for account {AccountId}: {Summary}", accountId, response.Summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging meal for account {AccountId}: {Meal}", accountId, meal);
                response.AddError($"Failed to log your meal: {ex.Message}");
            }

            return response;
        }

        public async Task<SaveProfileAndGoalsResponse> SaveProfileAndGoalsToolAsync(SaveProfileAndGoalsRequest request)
        {
            _logger.LogInformation("Processing assistant profile/goals creation request for account {AccountId}", request.AccountId);
            
            try
            {
                // Delegate to the profile service
                var response = await _profileService.SaveProfileAndGoalsAsync(request);
                
                if (response.IsSuccess)
                {
                    _logger.LogInformation("Successfully created profile and goals for account {AccountId}", request.AccountId);
                }
                else
                {
                    _logger.LogWarning("Failed to create profile and goals for account {AccountId}: {Errors}",
                        request.AccountId, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile and goals for account {AccountId}", request.AccountId);
                var errorResponse = new SaveProfileAndGoalsResponse();
                errorResponse.AddError($"Failed to create profile and goals: {ex.Message}");
                return errorResponse;
            }
        }

        public async Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(string accountId)
        {
            _logger.LogInformation("AssistantToolService: Getting profile and goals for account {AccountId}", accountId);
            
            var request = new GetProfileAndGoalsRequest
            {
                AccountId = accountId
            };
            
            return await _profileService.GetProfileAndGoalsAsync(request);
        }

        private MealType DetermineMealType(string mealDescription)
        {
            mealDescription = mealDescription.ToLower();

            if (mealDescription.Contains("breakfast") || 
                mealDescription.Contains("morning") ||
                mealDescription.Contains("cereal") ||
                mealDescription.Contains("toast"))
            {
                return MealType.Breakfast;
            }
            else if (mealDescription.Contains("lunch") ||
                     mealDescription.Contains("noon") ||
                     mealDescription.Contains("sandwich") ||
                     mealDescription.Contains("wrap"))
            {
                return MealType.Lunch;
            }
            else if (mealDescription.Contains("dinner") ||
                     mealDescription.Contains("supper") ||
                     mealDescription.Contains("evening meal"))
            {
                return MealType.Dinner;
            }
            else if (mealDescription.Contains("snack") ||
                     mealDescription.Contains("treat") ||
                     mealDescription.Contains("between meals"))
            {
                return MealType.Snack;
            }

            // Default to most recent meal type based on current time
            var hour = DateTime.Now.Hour;
            if (hour < 11)
                return MealType.Breakfast;
            else if (hour < 15)
                return MealType.Lunch;
            else if (hour < 20)
                return MealType.Dinner;
            else
                return MealType.Snack;
        }
    }
} 