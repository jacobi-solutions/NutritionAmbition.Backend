using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.DataContracts.Profile;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IAssistantToolHandlerService
    {
        Task<string> HandleToolCallAsync(string accountId, string toolName, string toolInput);
    }

    public class AssistantToolHandlerService : IAssistantToolHandlerService
    {
        private readonly IAssistantToolService _assistantToolService;
        private readonly ILogger<AssistantToolHandlerService> _logger;

        public AssistantToolHandlerService(
            IAssistantToolService assistantToolService,
            ILogger<AssistantToolHandlerService> logger)
        {
            _assistantToolService = assistantToolService;
            _logger = logger;
        }

        public async Task<string> HandleToolCallAsync(string accountId, string toolName, string toolInput)
        {
            try
            {
                _logger.LogInformation("Handling tool call {ToolName} for account {AccountId}", toolName, accountId);

                switch (toolName)
                {
                    case AssistantToolTypes.LogMealTool:
                        return await HandleLogMealToolAsync(accountId, toolInput);
                    case AssistantToolTypes.SaveUserProfileTool:
                        return await HandleSaveUserProfileToolAsync(accountId, toolInput);
                    case AssistantToolTypes.GetProfileAndGoalsTool:
                        return await HandleGetProfileAndGoalsToolAsync(accountId, toolInput);
                    case AssistantToolTypes.SetDefaultGoalProfileTool:
                        return await HandleSetDefaultGoalProfileToolAsync(accountId, toolInput);
                    case AssistantToolTypes.OverrideDailyGoalsTool:
                        return await HandleOverrideDailyGoalsToolAsync(accountId, toolInput);
                    case AssistantToolTypes.GetUserContextTool:
                        return await HandleGetUserContextToolAsync(accountId, toolInput);
                    default:
                        _logger.LogWarning("Unknown tool call: {ToolName}", toolName);
                        return JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tool call {ToolName} for account {AccountId}", toolName, accountId);
                return JsonSerializer.Serialize(new { error = $"Error processing tool call: {ex.Message}" });
            }
        }

        private async Task<string> HandleLogMealToolAsync(string accountId, string toolInput)
        {
            try
            {
                // Parse the input JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var mealRequest = JsonSerializer.Deserialize<LogMealToolRequest>(toolInput, options);

                if (string.IsNullOrEmpty(mealRequest?.Meal))
                {
                    _logger.LogWarning("Missing meal description in LogMealTool request");
                    return JsonSerializer.Serialize(new { error = "Missing meal description" });
                }

                // Call the service to process the meal
                var response = await _assistantToolService.LogMealToolAsync(accountId, mealRequest.Meal);

                // Return the serialized response
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing LogMealTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LogMealTool request");
                return JsonSerializer.Serialize(new { error = $"Error processing meal: {ex.Message}" });
            }
        }

        private async Task<string> HandleSaveUserProfileToolAsync(string accountId, string toolInput)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<dynamic>(toolInput, options);

                var profileRequest = new DataContracts.Profile.SaveUserProfileRequest
                {
                    AccountId = accountId,
                    Age = Convert.ToInt32(request.GetProperty("age")),
                    Sex = request.GetProperty("sex").GetString(),
                    HeightCm = Convert.ToDouble(request.GetProperty("heightCm")),
                    WeightKg = Convert.ToDouble(request.GetProperty("weightKg")),
                    ActivityLevel = request.GetProperty("activityLevel").GetString()
                };

                var response = await _assistantToolService.SaveUserProfileToolAsync(profileRequest);
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing SaveUserProfileTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SaveUserProfileTool request");
                return JsonSerializer.Serialize(new { error = $"Error processing profile data: {ex.Message}" });
            }
        }

        private async Task<string> HandleGetProfileAndGoalsToolAsync(string accountId, string toolInput)
        {
            try
            {
                // Call the service to get the profile and goals
                var response = await _assistantToolService.GetProfileAndGoalsToolAsync(accountId);

                // Return the serialized response
                return JsonSerializer.Serialize(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetProfileAndGoalsTool request");
                return JsonSerializer.Serialize(new { error = $"Error retrieving profile data: {ex.Message}" });
            }
        }
        
        private async Task<string> HandleSetDefaultGoalProfileToolAsync(string accountId, string toolInput)
        {
            try
            {
                // Parse the input JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<dynamic>(toolInput, options);

                // Create a request object from the dynamic data
                var profileRequest = new SetDefaultGoalProfileRequest
                {
                    AccountId = accountId,
                    BaseCalories = Convert.ToDouble(request.GetProperty("baseCalories"))
                };

                // Call the service to process the default goal profile
                var response = await _assistantToolService.SetDefaultGoalProfileToolAsync(profileRequest);

                // Return the serialized response
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing SetDefaultGoalProfileTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SetDefaultGoalProfileTool request");
                return JsonSerializer.Serialize(new { error = $"Error setting default goal profile: {ex.Message}" });
            }
        }

        private async Task<string> HandleOverrideDailyGoalsToolAsync(string accountId, string toolInput)
        {
            try
            {
                // Parse the input JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<dynamic>(toolInput, options);

                // Create a request object from the dynamic data
                var profileRequest = new OverrideDailyGoalsRequest
                {
                    AccountId = accountId,
                    NewBaseCalories = Convert.ToDouble(request.GetProperty("newBaseCalories"))
                };

                // Call the service to process the daily goals override
                var response = await _assistantToolService.OverrideDailyGoalsToolAsync(profileRequest);

                // Return the serialized response
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing OverrideDailyGoalsTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OverrideDailyGoalsTool request");
                return JsonSerializer.Serialize(new { error = $"Error overriding daily goals: {ex.Message}" });
            }
        }

        private async Task<string> HandleGetUserContextToolAsync(string accountId, string toolInput)
        {
            try
            {
                // Parse the input JSON
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var request = JsonSerializer.Deserialize<GetUserContextRequest>(toolInput, options);

                // Call the service to get the user context
                var response = await _assistantToolService.GetUserContextToolAsync(accountId, request?.TimezoneOffsetMinutes);

                // Return the serialized response
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing GetUserContextTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GetUserContextTool request");
                return JsonSerializer.Serialize(new { error = $"Error retrieving user context: {ex.Message}" });
            }
        }
    }
} 