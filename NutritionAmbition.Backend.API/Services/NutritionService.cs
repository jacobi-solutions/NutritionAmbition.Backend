using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionService
    {
        Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string foodDescription);
        Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string foodDescription);
    }

    public class NutritionService : INutritionService
    {
        private readonly INutritionixService _nutritionixService;
        private readonly IOpenAiService _openAiService; // Still needed for potential future parsing or validation
        private readonly ILogger<NutritionService> _logger;

        public NutritionService(
            INutritionixService nutritionixService, 
            IOpenAiService openAiService, // Keep for potential future use
            ILogger<NutritionService> logger)
        {
            _nutritionixService = nutritionixService;
            _openAiService = openAiService;
            _logger = logger;
        }

        // Simplified method to directly query Nutritionix
        public async Task<NutritionApiResponse> GetNutritionDataForFoodItemAsync(string foodDescription)
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

                response.Foods = MapNutritionixResponse(nutritionixResponse);
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

        // Main method using Nutritionix natural language endpoint
        public async Task<NutritionApiResponse> ProcessFoodTextAndGetNutritionAsync(string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Processing food text and getting nutrition data via Nutritionix: {FoodDescription}", foodDescription);
                
                // Directly query Nutritionix using its natural language processing
                var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(foodDescription);

                if (nutritionixResponse == null || !nutritionixResponse.Foods.Any())
                {
                    _logger.LogWarning("Nutritionix returned no data for: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food description.");
                    return response;
                }

                // Map the response from Nutritionix format to our internal format
                response.Foods = MapNutritionixResponse(nutritionixResponse);
                response.IsSuccess = true;

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing food text and getting nutrition data: {FoodDescription}", foodDescription);
                response.AddError($"Error processing food text: {ex.Message}");
                return response;
            }
        }

        // Helper method to map Nutritionix response to our internal FoodNutrition structure
        private List<FoodNutrition> MapNutritionixResponse(NutritionixResponse nutritionixResponse)
        {
            var mappedFoods = new List<FoodNutrition>();

            foreach (var food in nutritionixResponse.Foods)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = food.FoodName,
                    Quantity = food.ServingQty.ToString(), // Use serving quantity from Nutritionix
                    Unit = food.ServingUnit, // Use serving unit from Nutritionix
                    Calories = food.Calories ?? 0,
                    Macronutrients = new Macronutrients // Use the existing Macronutrients class
                    {
                        // Use the existing NutrientInfo class instead of the non-existent Macronutrient class
                        Protein = new NutrientInfo { Amount = food.Protein ?? 0, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = food.TotalCarbohydrate ?? 0, Unit = "g" },
                        Fat = new NutrientInfo { Amount = food.TotalFat ?? 0, Unit = "g" },
                        Fiber = new NutrientInfo { Amount = food.DietaryFiber ?? 0, Unit = "g" },
                        Sugar = new NutrientInfo { Amount = food.Sugars ?? 0, Unit = "g" },
                        SaturatedFat = new NutrientInfo { Amount = food.SaturatedFat ?? 0, Unit = "g" }
                        // UnsaturatedFat and TransFat are not directly available in the base Nutritionix response, 
                        // they might be in FullNutrients if needed.
                    },
                    Micronutrients = new Dictionary<string, Micronutrient>() // Populate if needed from FullNutrients
                };

                // Optionally map micronutrients from food.FullNutrients if required
                // Example: Map Potassium (attr_id 306)
                var potassium = food.FullNutrients.FirstOrDefault(n => n.AttrId == 306);
                if (potassium != null)
                {
                    // Use the existing Micronutrient class
                    foodNutrition.Micronutrients["Potassium"] = new Micronutrient { Amount = potassium.Value, Unit = "mg" }; // Assuming unit is mg
                }
                // Add mappings for other relevant micronutrients based on AttrId

                mappedFoods.Add(foodNutrition);
            }

            return mappedFoods;
        }
    }
}
