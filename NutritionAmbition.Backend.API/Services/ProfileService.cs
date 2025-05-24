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
        Task<SaveUserProfileResponse> SaveUserProfileAsync(SaveUserProfileRequest request);
        Task<GetProfileAndGoalsResponse> GetProfileAndGoalsAsync(GetProfileAndGoalsRequest request);
    }
    
    public class ProfileService : IProfileService
    {
        private readonly DailyGoalRepository _dailyGoalRepository;
        private readonly DefaultGoalProfileRepository _defaultGoalProfileRepository;
        private readonly IAccountsService _accountsService;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(
            DailyGoalRepository dailyGoalRepository,
            DefaultGoalProfileRepository defaultGoalProfileRepository,
            IAccountsService accountsService,
            ILogger<ProfileService> logger)
        {
            _dailyGoalRepository = dailyGoalRepository;
            _defaultGoalProfileRepository = defaultGoalProfileRepository;
            _accountsService = accountsService;
            _logger = logger;
        }

        public async Task<SaveUserProfileResponse> SaveUserProfileAsync(SaveUserProfileRequest request)
        {
            var response = new SaveUserProfileResponse();

            try
            {
                _logger.LogInformation("Saving user profile for account {AccountId}", request.AccountId);

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

                if (request.HeightFeet < 0 || request.HeightInches < 0 || request.HeightInches > 11)
                {
                    response.AddError("Height must be valid (feet >= 0, inches between 0-11)");
                    return response;
                }

                if (request.WeightLbs <= 0)
                {
                    response.AddError("Weight must be greater than 0");
                    return response;
                }

                var account = await _accountsService.GetAccountByIdAsync(request.AccountId);
                if (account == null)
                {
                    response.AddError($"Account with ID {request.AccountId} not found");
                    return response;
                }

                if (account.UserProfile == null)
                {
                    account.UserProfile = new UserProfile();
                }

                account.UserProfile.Age = request.Age;
                account.UserProfile.Sex = request.Sex;
                account.UserProfile.HeightFeet = request.HeightFeet;
                account.UserProfile.HeightInches = request.HeightInches;
                account.UserProfile.WeightLbs = request.WeightLbs;
                account.UserProfile.ActivityLevel = request.ActivityLevel;

                await _accountsService.UpdateAccountAsync(account.Id, account);

                response.IsSuccess = true;
                _logger.LogInformation("Successfully saved user profile for account {AccountId}", request.AccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user profile for account {AccountId}", request.AccountId);
                response.AddError("An error occurred while saving the user profile.");
            }

            return response;
        }

        public async Task<GetProfileAndGoalsResponse> GetProfileAndGoalsAsync(GetProfileAndGoalsRequest request)
        {
            var response = new GetProfileAndGoalsResponse();

            try
            {
                _logger.LogInformation("Retrieving profile and goals for account {AccountId}", request.AccountId);

                // Check if a default goal profile exists for the account
                var defaultGoalProfile = await _defaultGoalProfileRepository.GetByAccountIdAsync(request.AccountId);

                // Set HasGoals based on whether a default profile exists
                if (defaultGoalProfile == null)
                {
                    _logger.LogInformation("No default goal profile found for account {AccountId}", request.AccountId);
                    response.HasGoals = false;
                    response.IsSuccess = true;
                    return response;
                }

                _logger.LogInformation("Default goal profile found for account {AccountId}", request.AccountId);
                response.HasGoals = true;

                // Get the latest DailyGoal for the account to extract base calories
                var latestGoal = await _dailyGoalRepository.GetLatestByAccountIdAsync(request.AccountId);
                if (latestGoal != null)
                {
                    response.BaseCalories = latestGoal.BaseCalories;
                }

                response.IsSuccess = true;

                // Get account details - might have some profile data
                var account = await _accountsService.GetAccountByIdAsync(request.AccountId);
                if (account != null && account.UserProfile != null)
                {
                    // Extract profile information if available
                    response.Age = account.UserProfile.Age;
                    response.Sex = account.UserProfile.Sex;
                    response.HeightFeet = account.UserProfile.HeightFeet;
                    response.HeightInches = account.UserProfile.HeightInches;
                    response.WeightLbs = account.UserProfile.WeightLbs;
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

        public double GetActivityFactor(string activityLevel)
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