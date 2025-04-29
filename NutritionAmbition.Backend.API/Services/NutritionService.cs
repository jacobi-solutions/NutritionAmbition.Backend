using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models; // Added for FoodEntry, FoodItem, MealType

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionService
    {
        // Modified to accept accountId
        Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string accountId, string foodDescription);
        // Modified to accept accountId
        Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string accountId, string foodDescription);
    }

    public class NutritionService : INutritionService
    {
        private readonly INutritionixService _nutritionixService;
        private readonly IOpenAiService _openAiService;
        private readonly IFoodEntryService _foodEntryService; // 🟢 Inject IFoodEntryService
        private readonly ILogger<NutritionService> _logger;

        public NutritionService(INutritionixService nutritionixService, IOpenAiService openAiService, IFoodEntryService foodEntryService, ILogger<NutritionService> logger)
        {
            _nutritionixService = nutritionixService;
            _openAiService = openAiService;
            _foodEntryService = foodEntryService; // 🟢 Inject IFoodEntryService
            _logger = logger;
        }

        // Simplified method - Modified to accept accountId (though not used here currently)
        public async Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Getting nutrition data for food item via Nutritionix: {FoodDescription}", foodDescription);
                
                var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(foodDescription);

                if (nutritionixResponse == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix returned no data for: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food.");
                    return response;
                }

                response.Foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                response.IsSuccess = true;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting nutrition data for food item: {FoodDescription}", foodDescription);
                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        // Main method - Modified to accept accountId and save FoodEntry
        public async Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Processing food text and getting nutrition data via Nutritionix for Account {AccountId}: {FoodDescription}", accountId, foodDescription);
                
                // 1. Get nutrition data from Nutritionix
                var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(foodDescription);

                if (nutritionixResponse == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix returned no data for: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food description.");
                    // Still generate a coach response even if nutrition data fails
                    response.AiCoachResponse = await GenerateFallbackCoachResponseAsync(foodDescription);
                    return response;
                }

                // 2. Map Nutritionix response to our internal FoodNutrition structure
                response.Foods = MapNutritionixResponseToFoodNutrition(nutritionixResponse);
                response.IsSuccess = true;

                // 3. 🟢 Save the FoodEntry to the database
                if (response.IsSuccess && response.Foods.Any())
                {
                    try
                    {
                        // Map FoodNutrition to FoodItem for saving
                        var parsedItems = MapFoodNutritionToFoodItem(response.Foods);

                        var createFoodEntryRequest = new CreateFoodEntryRequest
                        {
                            Description = foodDescription,
                            Meal = MealType.Unknown, // Default for now
                            LoggedDateUtc = DateTime.UtcNow,
                            ParsedItems = parsedItems
                        };
                        // Call the service to add the entry
                        var saveResponse = await _foodEntryService.AddFoodEntryAsync(accountId, createFoodEntryRequest);
                        if (!saveResponse.IsSuccess)
                        {
                            _logger.LogWarning("Failed to save food entry for Account {AccountId}: {Errors}", accountId, string.Join(", ", saveResponse.Errors));
                            // Don't block the response to the user if saving fails, just log it
                        }
                        else
                        {
                            _logger.LogInformation("Successfully saved food entry {FoodEntryId} for Account {AccountId}", saveResponse.FoodEntry?.Id, accountId);
                        }
                    }
                    catch (Exception saveEx)
                    {
                        _logger.LogError(saveEx, "Error saving food entry for Account {AccountId} and description {FoodDescription}", accountId, foodDescription);
                        // Don't block the response to the user if saving fails
                    }
                }

                // 4. 🟢 Generate AI coach response after getting nutrition data
                if (response.IsSuccess && response.Foods.Any())
                {
                    try
                    {
                        response.AiCoachResponse = await _openAiService.GenerateCoachResponseAsync(foodDescription, response.Foods[0]);
                    }
                    catch (Exception coachEx)
                    {
                        _logger.LogWarning(coachEx, "Failed to generate AI coach response for: {FoodDescription}", foodDescription);
                        response.AiCoachResponse = "Logged!"; // Default response on error
                    }
                }
                else // Handle case where nutrition data retrieval failed but we still want a response
                {
                    response.AiCoachResponse = await GenerateFallbackCoachResponseAsync(foodDescription);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing food text and getting nutrition data for Account {AccountId}: {FoodDescription}", accountId, foodDescription);
                response.AddError($"Error processing food text: {ex.Message}");
                response.AiCoachResponse = "Sorry, an error occurred while processing your request."; // Error response
                return response;
            }
        }

        // Helper method to map Nutritionix response to our internal FoodNutrition structure
        private List<FoodNutrition> MapNutritionixResponseToFoodNutrition(NutritionixResponse nutritionixResponse)
        {
            var mappedFoods = new List<FoodNutrition>();

            foreach (var food in nutritionixResponse.Foods)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = food.FoodName,
                    Quantity = food.ServingQty.ToString(),
                    Unit = food.ServingUnit,
                    Calories = food.Calories ?? 0,
                    Macronutrients = new Macronutrients
                    {
                        Protein = new NutrientInfo { Amount = food.Protein ?? 0, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = food.TotalCarbohydrate ?? 0, Unit = "g" },
                        Fat = new NutrientInfo { Amount = food.TotalFat ?? 0, Unit = "g" },
                        Fiber = new NutrientInfo { Amount = food.DietaryFiber ?? 0, Unit = "g" },
                        Sugar = new NutrientInfo { Amount = food.Sugars ?? 0, Unit = "g" },
                        SaturatedFat = new NutrientInfo { Amount = food.SaturatedFat ?? 0, Unit = "g" }
                    },
                    Micronutrients = new Dictionary<string, Micronutrient>()
                };

                // Map micronutrients from FullNutrients
                if (food.FullNutrients != null)
                {
                    foreach (var nutrient in food.FullNutrients)
                    {
                        // Basic mapping based on common attr_ids (can be expanded)
                        string? nutrientName = nutrient.AttrId switch
                        {
                            301 => "Calcium",
                            303 => "Iron",
                            304 => "Magnesium",
                            305 => "Phosphorus",
                            306 => "Potassium",
                            307 => "Sodium",
                            309 => "Zinc",
                            312 => "Copper",
                            315 => "Manganese",
                            317 => "Selenium",
                            401 => "Vitamin C",
                            404 => "Thiamin", // B1
                            405 => "Riboflavin", // B2
                            406 => "Niacin", // B3
                            410 => "Pantothenic Acid", // B5
                            415 => "Vitamin B6",
                            417 => "Folate", // B9
                            418 => "Vitamin B12",
                            320 => "Vitamin A", // RAE
                            323 => "Vitamin E",
                            328 => "Vitamin D", // D2 + D3
                            430 => "Vitamin K",
                            _ => null
                        };

                        if (nutrientName != null)
                        {
                            // Determine unit (most are mg, some mcg or IU)
                            string unit = nutrient.AttrId switch
                            {
                                328 => "IU", // Vitamin D often in IU
                                417 => "mcg", // Folate often in mcg (DFE)
                                418 => "mcg", // B12 often in mcg
                                430 => "mcg", // Vitamin K often in mcg
                                320 => "mcg", // Vitamin A often in mcg (RAE)
                                _ => "mg"
                            };
                            foodNutrition.Micronutrients[nutrientName] = new Micronutrient { Amount = nutrient.Value, Unit = unit };
                        }
                    }
                }
                mappedFoods.Add(foodNutrition);
            }
            return mappedFoods;
        }

        // 🟢 Helper method to map FoodNutrition (API contract) to FoodItem (DB model)
        private List<FoodItem> MapFoodNutritionToFoodItem(List<FoodNutrition> foodNutritions)
        {
            var foodItems = new List<FoodItem>();
            foreach (var fn in foodNutritions)
            {
                var fi = new FoodItem
                {
                    Name = fn.Name,
                    // Attempt to parse quantity, default to 1 if invalid
                    Quantity = double.TryParse(fn.Quantity, out double qty) ? qty : 1.0,
                    Unit = fn.Unit ?? string.Empty,
                    Calories = fn.Calories,
                    Protein = fn.Macronutrients.Protein.Amount,
                    Carbohydrates = fn.Macronutrients.Carbohydrates.Amount,
                    Fat = fn.Macronutrients.Fat.Amount,
                    // Map micronutrients (simplified: store name and amount)
                    Micronutrients = fn.Micronutrients.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Amount)
                };
                foodItems.Add(fi);
            }
            return foodItems;
        }

        // 🟢 Helper method to generate a fallback coach response when nutrition data fails
        private async Task<string> GenerateFallbackCoachResponseAsync(string foodDescription)
        {
            try
            {
                // Use a simpler prompt for OpenAI just acknowledging the input
                return await _openAiService.GenerateCoachResponseAsync(foodDescription, null); // Pass null for nutrition data
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate fallback AI coach response for: {FoodDescription}", foodDescription);
                return "Sorry, I couldn't find nutrition data, but I've noted the description."; // Default fallback
            }
        }
    }
}

