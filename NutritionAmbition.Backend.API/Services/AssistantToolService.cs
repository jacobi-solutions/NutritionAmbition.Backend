using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.DataContracts.Tools;
using NutritionAmbition.Backend.API.Models;
using System.Linq;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAssistantToolService
    {
        Task<LogMealToolResponse> LogMealToolAsync(Account account, string meal);
        Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(Account account);
        Task<SetDefaultGoalProfileResponse> SetDefaultGoalProfileToolAsync(Account account, SetDefaultGoalProfileRequest request);
        Task<OverrideDailyGoalsResponse> OverrideDailyGoalsToolAsync(Account account, OverrideDailyGoalsRequest request);
        Task<SaveUserProfileResponse> SaveUserProfileToolAsync(Account account, SaveUserProfileRequest request);
        Task<GetUserContextResponse> GetUserContextToolAsync(Account account, int? timezoneOffsetMinutes);
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

        public async Task<LogMealToolResponse> LogMealToolAsync(Account account, string meal)
        {
            var response = new LogMealToolResponse();

            try
            {
                _logger.LogInformation("Processing assistant meal logging request for account {AccountId}: {Meal}", account.Id, meal);

                // Get nutrition data using the smart nutrition service
                var nutritionResponse = await _nutritionService.GetSmartNutritionDataAsync(account.Id, meal);

                if (!nutritionResponse.IsSuccess)
                {
                    _logger.LogWarning("Failed to process meal for account {AccountId}: {Errors}",
                        account.Id, string.Join(", ", nutritionResponse.Errors.Select(e => e.ErrorMessage)));
                    response.AddError("Failed to process meal. Please try a more specific description.");
                    return response;
                }

                // Check if we got valid nutrition data
                if (nutritionResponse.Foods == null || !nutritionResponse.Foods.Any())
                {
                    _logger.LogWarning("No nutrition data found for account {AccountId} meal: {Meal}", account.Id, meal);
                    response.AddError("No foods could be identified in your meal description.");
                    return response;
                }

                _logger.LogInformation("Meal has been processed and saved via NutritionService for account {AccountId}", account.Id);

                // Calculate total calories
                int totalCalories = (int)Math.Round(nutritionResponse.Foods.Sum(f => f.Calories));

                // Populate structured response
                response.MealName = meal;
                response.Calories = totalCalories;
                response.LoggedAtUtc = DateTime.UtcNow;

                var firstFood = nutritionResponse.Foods.FirstOrDefault();
                if (firstFood != null)
                {
                    response.Nutrients = new NutrientsDto
                    {
                        Protein = (float)(firstFood.Macronutrients?.Protein?.Amount ?? 0),
                        Fat = (float)(firstFood.Macronutrients?.Fat?.Amount ?? 0),
                        Carbs = (float)(firstFood.Macronutrients?.Carbohydrates?.Amount ?? 0)
                    };
                }

                response.IsSuccess = true;

                _logger.LogInformation("Successfully logged meal for account {AccountId}", account.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging meal for account {AccountId}: {Meal}", account.Id, meal);
                response.AddError($"Failed to log your meal: {ex.Message}");
            }

            return response;
        }

        public async Task<GetProfileAndGoalsResponse> GetProfileAndGoalsToolAsync(Account account)
        {
            _logger.LogInformation("AssistantToolService: Getting profile and goals for account {AccountId}", account.Id);
            
            return await _profileService.GetProfileAndGoalsAsync(account);
        }

        public async Task<SetDefaultGoalProfileResponse> SetDefaultGoalProfileToolAsync(Account account, SetDefaultGoalProfileRequest request)
        {
            var response = new SetDefaultGoalProfileResponse();

            try
            {
                _logger.LogInformation("Setting default goal profile for account {AccountId}", account.Id);

                var nutrientGoals = request.NutrientGoals != null && request.NutrientGoals.Any()
                    ? request.NutrientGoals
                    : _goalScaffoldingService.GenerateNutrientGoals(request.BaseCalories);

                var defaultProfile = new DefaultGoalProfile
                {
                    AccountId = account.Id,
                    BaseCalories = request.BaseCalories,
                    NutrientGoals = nutrientGoals
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
                _logger.LogError(ex, "Error in SetDefaultGoalProfileToolAsync for account {AccountId}", account.Id);
                response.AddError("An unexpected error occurred.");
                return response;
            }
        }


        public async Task<OverrideDailyGoalsResponse> OverrideDailyGoalsToolAsync(Account account, OverrideDailyGoalsRequest request)
        {
            var response = new OverrideDailyGoalsResponse();

            try
            {
                var nutrientGoals = _goalScaffoldingService.GenerateNutrientGoals(request.NewBaseCalories);

                var goal = new DailyGoal
                {
                    AccountId = account.Id,
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
                _logger.LogError(ex, "Error overriding daily goals for account {AccountId}", account.Id);
                response.AddError("Failed to override today's goals.");
                return response;
            }
        }

        public async Task<SaveUserProfileResponse> SaveUserProfileToolAsync(Account account, SaveUserProfileRequest request)
        {
            _logger.LogInformation("Processing assistant profile/goals creation request for account {AccountId}", account.Id);
            
            try
            {
                // Delegate to the profile service
                var response = await _profileService.SaveUserProfileAsync(request, account);
                
                if (response.IsSuccess)
                {
                    _logger.LogInformation("Successfully created profile and goals for account {AccountId}", account.Id);
                }
                else
                {
                    _logger.LogWarning("Failed to create profile and goals for account {AccountId}: {Errors}",
                        account.Id, string.Join(", ", response.Errors.Select(e => e.ErrorMessage)));
                }
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile and goals for account {AccountId}", account.Id);
                var errorResponse = new SaveUserProfileResponse();
                errorResponse.AddError($"Failed to create profile and goals: {ex.Message}");
                return errorResponse;
            }
        }

        public async Task<GetUserContextResponse> GetUserContextToolAsync(Account account, int? timezoneOffsetMinutes)
        {
            var response = new GetUserContextResponse();

            try
            {
                _logger.LogInformation("Getting user context for account {AccountId} with timezone offset {TimezoneOffset}", 
                    account.Id, timezoneOffsetMinutes);
                
                if (account == null)
                {
                    response.AddError("Account not found");
                    return response;
                }
                
                // Determine if user has a profile
                bool hasProfile = account.UserProfile != null;
                
                // Determine if user has goals by checking for a default goal profile
                var hasDefaultGoalProfile = await _dailyGoalService.HasDefaultGoalProfileAsync(account.Id);
                
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
                
                _logger.LogInformation("Successfully retrieved user context for account {AccountId}", account.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user context for account {AccountId}", account.Id);
                response.AddError($"Failed to retrieve user context: {ex.Message}");
            }
            
            return response;
        }

    }
} 