using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Models;
using System.Linq;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAssistantToolService
    {
        Task<LogMealToolResponse> LogMealToolAsync(string accountId, string meal);
        Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(string accountId);
        Task<SetDefaultGoalProfileResponse> SetDefaultGoalProfileToolAsync(SetDefaultGoalProfileRequest request);
        Task<OverrideDailyGoalsResponse> OverrideDailyGoalsToolAsync(OverrideDailyGoalsRequest request);
        Task<SaveUserProfileResponse> SaveUserProfileToolAsync(SaveUserProfileRequest request);
        Task<GetUserContextResponse> GetUserContextToolAsync(string accountId, int? timezoneOffsetMinutes);
    }

    public class AssistantToolService : IAssistantToolService
    {
        private readonly INutritionService _nutritionService;
        private readonly IProfileService _profileService;
        private readonly DefaultGoalProfileRepository _defaultGoalProfileRepository;
        private readonly DailyGoalRepository _dailyGoalRepository;
        private readonly IGoalScaffoldingService _goalScaffoldingService;
        private readonly ILogger<AssistantToolService> _logger;
        private readonly IAccountsService _accountsService;
        private readonly IDailyGoalService _dailyGoalService;

        public AssistantToolService(
            INutritionService nutritionService,
            IProfileService profileService,
            DefaultGoalProfileRepository defaultGoalProfileRepository,
            DailyGoalRepository dailyGoalRepository,
            IGoalScaffoldingService goalScaffoldingService,
            ILogger<AssistantToolService> logger,
            IAccountsService accountsService,
            IDailyGoalService dailyGoalService)
        {
            _nutritionService = nutritionService;
            _profileService = profileService;
            _defaultGoalProfileRepository = defaultGoalProfileRepository;
            _dailyGoalRepository = dailyGoalRepository;
            _goalScaffoldingService = goalScaffoldingService;
            _logger = logger;
            _accountsService = accountsService;
            _dailyGoalService = dailyGoalService;
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

        public async Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(string accountId)
        {
            _logger.LogInformation("AssistantToolService: Getting profile and goals for account {AccountId}", accountId);
            
            var request = new GetProfileAndGoalsRequest
            {
                AccountId = accountId
            };
            
            return await _profileService.GetProfileAndGoalsAsync(request);
        }

        public async Task<SetDefaultGoalProfileResponse> SetDefaultGoalProfileToolAsync(SetDefaultGoalProfileRequest request)
        {
            var response = new SetDefaultGoalProfileResponse();

            try
            {
                var defaultProfile = new DefaultGoalProfile
                {
                    AccountId = request.AccountId,
                    BaseCalories = request.BaseCalories,
                    NutrientGoals = _goalScaffoldingService.GenerateNutrientGoals(request.BaseCalories)
                };

                var success = await _defaultGoalProfileRepository.UpsertAsync(defaultProfile);

                if (!success)
                {
                    response.AddError("Failed to save default profile.");
                    return response;
                }

                response.IsSuccess = true;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SetDefaultGoalProfileToolAsync for account {AccountId}", request.AccountId);
                response.AddError("An unexpected error occurred.");
                return response;
            }
        }

        public async Task<OverrideDailyGoalsResponse> OverrideDailyGoalsToolAsync(OverrideDailyGoalsRequest request)
        {
            var response = new OverrideDailyGoalsResponse();

            try
            {
                var nutrientGoals = _goalScaffoldingService.GenerateNutrientGoals(request.NewBaseCalories);

                var goal = new DailyGoal
                {
                    AccountId = request.AccountId,
                    EffectiveDateUtc = DateTime.UtcNow.Date,
                    BaseCalories = request.NewBaseCalories,
                    NutrientGoals = nutrientGoals
                };

                await _dailyGoalRepository.UpsertAsync(goal);
                response.IsSuccess = true;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error overriding daily goals for account {AccountId}", request.AccountId);
                response.AddError("Failed to override today's goals.");
                return response;
            }
        }

        public async Task<SaveUserProfileResponse> SaveUserProfileToolAsync(SaveUserProfileRequest request)
        {
            _logger.LogInformation("Processing assistant profile/goals creation request for account {AccountId}", request.AccountId);
            
            try
            {
                // Delegate to the profile service
                var response = await _profileService.SaveUserProfileAsync(request);
                
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
                var errorResponse = new SaveUserProfileResponse();
                errorResponse.AddError($"Failed to create profile and goals: {ex.Message}");
                return errorResponse;
            }
        }

        public async Task<GetUserContextResponse> GetUserContextToolAsync(string accountId, int? timezoneOffsetMinutes)
        {
            var response = new GetUserContextResponse();

            try
            {
                _logger.LogInformation("Getting user context for account {AccountId} with timezone offset {TimezoneOffset}", 
                    accountId, timezoneOffsetMinutes);
                
                // Retrieve the account
                var account = await _accountsService.GetAccountByIdAsync(accountId);
                if (account == null)
                {
                    response.AddError("Account not found");
                    return response;
                }
                
                // Determine if user has a profile
                bool hasProfile = account.UserProfile != null;
                
                // Determine if user has goals by checking for a default goal profile
                var hasDefaultGoalProfile = await _dailyGoalService.HasDefaultGoalProfileAsync(accountId);
                
                // Set HasGoals based on presence of default goal profile
                bool hasGoals = hasDefaultGoalProfile;
                
                // Set timezone offset, using 0 as default if not provided
                int offset = timezoneOffsetMinutes ?? 0;
                
                // Compute local date based on timezone offset
                DateTime localDate = DateTime.UtcNow.AddMinutes(offset);
                
                // Populate response
                response.IsAnonymousUser = account.IsAnonymousUser;
                response.HasProfile = hasProfile;
                response.HasGoals = hasGoals;
                response.LocalDate = localDate;
                response.IsSuccess = true;
                
                _logger.LogInformation("Successfully retrieved user context for account {AccountId}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user context for account {AccountId}", accountId);
                response.AddError($"Failed to retrieve user context: {ex.Message}");
            }
            
            return response;
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