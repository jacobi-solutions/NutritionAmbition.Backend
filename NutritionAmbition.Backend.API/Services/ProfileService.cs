using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts.Profile;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IProfileService
    {
        Task<SaveProfileAndGoalsResponse> SaveProfileAndGoalsAsync(SaveProfileAndGoalsRequest request);
        Task<GetProfileAndGoalsResponse> GetProfileAndGoalsAsync(GetProfileAndGoalsRequest request);
    }
    
    public class ProfileService : IProfileService
    {
        private readonly DailyGoalRepository _dailyGoalRepository;
        private readonly AccountsService _accountsService;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            DailyGoalRepository dailyGoalRepository,
            AccountsService accountsService,
            ILogger<ProfileService> logger)
        {
            _dailyGoalRepository = dailyGoalRepository;
            _accountsService = accountsService;
            _logger = logger;
        }

        public async Task<SaveProfileAndGoalsResponse> SaveProfileAndGoalsAsync(SaveProfileAndGoalsRequest request)
        {
            var response = new SaveProfileAndGoalsResponse();

            try
            {
                _logger.LogInformation("Creating profile and goals for account {AccountId}", request.AccountId);

                // Validate the request
                if (string.IsNullOrEmpty(request.AccountId))
                {
                    response.AddError("AccountId is required");
                    return response;
                }

                if (request.Age <= 0 || request.Age > 120)
                {
                    response.AddError("Age must be between 1 and 120");
                    return response;
                }

                if (string.IsNullOrEmpty(request.Sex) || (request.Sex.ToLower() != "male" && request.Sex.ToLower() != "female"))
                {
                    response.AddError("Sex must be either 'male' or 'female'");
                    return response;
                }

                if (request.HeightCm <= 0)
                {
                    response.AddError("Height must be greater than 0");
                    return response;
                }

                if (request.WeightKg <= 0)
                {
                    response.AddError("Weight must be greater than 0");
                    return response;
                }

                // Verify the account exists
                var account = await _accountsService.GetAccountByIdAsync(request.AccountId);
                if (account == null)
                {
                    response.AddError($"Account with ID {request.AccountId} not found");
                    return response;
                }

                // Calculate BMR using Mifflin-St Jeor formula
                double bmr = 0;
                if (request.Sex.ToLower() == "male")
                {
                    bmr = (10 * request.WeightKg) + (6.25 * request.HeightCm) - (5 * request.Age) + 5;
                }
                else
                {
                    bmr = (10 * request.WeightKg) + (6.25 * request.HeightCm) - (5 * request.Age) - 161;
                }

                // Adjust BMR for activity level
                double activityFactor = GetActivityFactor(request.ActivityLevel);
                double adjustedCalories = Math.Round(bmr * activityFactor);

                // Create nutrient goals based on calculated calories
                var proteinGrams = Math.Round((adjustedCalories * 0.25) / 4); // 25% of calories from protein (4 calories per gram)
                var fatGrams = Math.Round((adjustedCalories * 0.30) / 9);     // 30% of calories from fat (9 calories per gram)
                var carbGrams = Math.Round((adjustedCalories * 0.45) / 4);    // 45% of calories from carbs (4 calories per gram)

                // Create DailyGoal
                var dailyGoal = new DailyGoal
                {
                    AccountId = request.AccountId,
                    BaseCalories = adjustedCalories,
                    NutrientGoals = new List<NutrientGoal>
                    {
                        new NutrientGoal { NutrientName = "Protein", MinValue = proteinGrams, Unit = "g" },
                        new NutrientGoal { NutrientName = "Fat", MinValue = fatGrams, Unit = "g" },
                        new NutrientGoal { NutrientName = "Carbohydrates", MinValue = carbGrams, Unit = "g" },
                        new NutrientGoal { NutrientName = "Fiber", MinValue = 25, Unit = "g" }
                    }
                };

                // Save the DailyGoal
                var savedGoal = await _dailyGoalRepository.CreateAsync(dailyGoal);

                // Also save the profile data to the Account
                if (account != null)
                {
                    // Initialize UserProfile if it doesn't exist
                    if (account.UserProfile == null)
                    {
                        account.UserProfile = new UserProfile();
                    }

                    // Update profile data
                    account.UserProfile.Age = request.Age;
                    account.UserProfile.Sex = request.Sex;
                    account.UserProfile.HeightCm = request.HeightCm;
                    account.UserProfile.WeightKg = request.WeightKg;
                    account.UserProfile.ActivityLevel = request.ActivityLevel;

                    // Save the updated account
                    await _accountsService.UpdateAccountAsync(account.Id, account);
                    _logger.LogInformation("Updated account profile for {AccountId}", request.AccountId);
                }

                response.IsCreated = true;
                response.DailyGoal = savedGoal;
                response.IsSuccess = true;
                _logger.LogInformation("Successfully created profile and goals for account {AccountId}", request.AccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating profile and goals for account {AccountId}", request.AccountId);
                response.AddError("An error occurred while creating the profile and goals.");
            }

            return response;
        }

        public async Task<GetProfileAndGoalsResponse> GetProfileAndGoalsAsync(GetProfileAndGoalsRequest request)
        {
            var response = new GetProfileAndGoalsResponse();

            try
            {
                _logger.LogInformation("Retrieving profile and goals for account {AccountId}", request.AccountId);

                // Get the latest DailyGoal for the account
                var latestGoal = await _dailyGoalRepository.GetLatestByAccountIdAsync(request.AccountId);

                // If no goal exists, return a response indicating no goals
                if (latestGoal == null)
                {
                    _logger.LogInformation("No daily goals found for account {AccountId}", request.AccountId);
                    response.HasGoals = false;
                    response.IsSuccess = true;
                    return response;
                }

                // Extract profile data from daily goal
                // Profile data is not explicitly stored, but we can infer from what we have in goals
                response.BaseCalories = latestGoal.BaseCalories;
                response.HasGoals = true;
                response.IsSuccess = true;

                // Get account details - might have some profile data
                var account = await _accountsService.GetAccountByIdAsync(request.AccountId);
                if (account != null && account.UserProfile != null)
                {
                    // Extract profile information if available
                    response.Age = account.UserProfile.Age;
                    response.Sex = account.UserProfile.Sex;
                    response.HeightCm = account.UserProfile.HeightCm;
                    response.WeightKg = account.UserProfile.WeightKg;
                    response.ActivityLevel = account.UserProfile.ActivityLevel;
                }

                _logger.LogInformation("Successfully retrieved profile and goals for account {AccountId}", request.AccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving profile and goals for account {AccountId}", request.AccountId);
                response.AddError("An error occurred while retrieving the profile and goals.");
            }

            return response;
        }

        private double GetActivityFactor(string activityLevel)
        {
            return activityLevel.ToLower() switch
            {
                "sedentary" => 1.2,      // Little or no exercise
                "light" => 1.375,         // Light exercise/sports 1-3 days/week
                "moderate" => 1.55,       // Moderate exercise/sports 3-5 days/week
                "active" => 1.725,        // Hard exercise/sports 6-7 days/week
                "very active" => 1.9,     // Very hard daily exercise/sports & physical job
                _ => 1.55                 // Default to moderate activity
            };
        }
    }
} 