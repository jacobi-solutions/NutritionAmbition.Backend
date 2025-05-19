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
                var unescapedJson = toolInput.Trim('"').Replace("\\\"", "\"");
                var mealRequest = JsonSerializer.Deserialize<LogMealToolRequest>(unescapedJson, options);

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
                var unescapedJson = toolInput.Trim('"').Replace("\\\"", "\"");
                var root = JsonSerializer.Deserialize<JsonElement>(unescapedJson, options);

                int age = root.GetProperty("age").GetInt32();
                string sex = root.GetProperty("sex").GetString();
                int heightFeet = root.GetProperty("heightFeet").GetInt32();
                int heightInches = root.GetProperty("heightInches").GetInt32();
                double weightLbs = root.GetProperty("weightLbs").GetDouble();
                string activityLevel = root.GetProperty("activityLevel").GetString();


                var profileRequest = new DataContracts.Profile.SaveUserProfileRequest
                {
                    AccountId = accountId,
                    Age = age,
                    Sex = sex,
                    HeightFeet = heightFeet,
                    HeightInches = heightInches,
                    WeightLbs = weightLbs,
                    ActivityLevel = activityLevel
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
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var unescapedJson = toolInput.Trim('"').Replace("\\\"", "\"");
                var profileRequest = JsonSerializer.Deserialize<SetDefaultGoalProfileRequest>(unescapedJson, options);
                
                if (profileRequest == null)
                {
                    _logger.LogWarning("Failed to deserialize SetDefaultGoalProfileTool input: {ToolInput}", toolInput);
                    return JsonSerializer.Serialize(new { error = "Invalid input format for SetDefaultGoalProfileTool" });
                }
                
                // Set the account ID
                profileRequest.AccountId = accountId;
                
                // Log the number of nutrient goals received
                _logger.LogInformation("Received {Count} nutrient goals for account {AccountId}", 
                    profileRequest.NutrientGoals?.Count ?? 0, accountId);
                
                // Check for missing required fields
                if (profileRequest.BaseCalories <= 0)
                {
                    _logger.LogWarning("Missing required field BaseCalories in SetDefaultGoalProfileTool input");
                    return JsonSerializer.Serialize(new { error = "BaseCalories is required and must be greater than zero" });
                }

                var response = await _assistantToolService.SetDefaultGoalProfileToolAsync(profileRequest);
                return JsonSerializer.Serialize(response);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing SetDefaultGoalProfileTool input: {ToolInput}", toolInput);
                return JsonSerializer.Serialize(new { error = "Invalid input format for SetDefaultGoalProfileTool" });
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
                var unescapedJson = toolInput.Trim('"').Replace("\\\"", "\"");
                var request = JsonSerializer.Deserialize<dynamic>(unescapedJson, options);

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
                var unescapedJson = toolInput.Trim('"').Replace("\\\"", "\"");
                var request = JsonSerializer.Deserialize<GetUserContextRequest>(unescapedJson, options);

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