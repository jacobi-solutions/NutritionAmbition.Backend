using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Helpers;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionService
    {
        Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription);
    }

    public class NutritionService : INutritionService
    {
        private readonly IFoodDataApi _foodDataApi;
        private readonly IOpenAiService _openAiService;
        private readonly IOpenAiResponsesService _openAiResponsesService;
        private readonly IFoodEntryService _foodEntryService;
        private readonly ILogger<NutritionService> _logger;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly IDailySummaryService _dailySummaryService;

        public NutritionService(
            IFoodDataApi foodDataApi,
            IOpenAiService openAiService,
            IOpenAiResponsesService openAiResponsesService,
            IFoodEntryService foodEntryService,
            ILogger<NutritionService> logger,
            INutritionCalculationService nutritionCalculationService,
            IDailySummaryService dailySummaryService)
        {
            _foodDataApi = foodDataApi;
            _openAiService = openAiService;
            _openAiResponsesService = openAiResponsesService;
            _foodEntryService = foodEntryService;
            _logger = logger;
            _nutritionCalculationService = nutritionCalculationService;
            _dailySummaryService = dailySummaryService;
        }

        public async Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Smart nutrition lookup for account {AccountId}: {FoodDescription}", accountId, foodDescription);

                // 1. First, parse the food description using OpenAI to identify individual items and whether they're branded
                var parsedFoodsResponse = await _openAiService.ParseFoodTextAsync(foodDescription);

                // Log the number of parsed foods and their details
                _logger.LogInformation("OpenAI parsed {Count} foods from description",
                    parsedFoodsResponse.Foods?.Count ?? 0);

                if (parsedFoodsResponse.Foods != null)
                {
                    foreach (var food in parsedFoodsResponse.Foods)
                    {
                        _logger.LogInformation("Parsed food: Name={Name}, Quantity={Quantity}, Unit={Unit}, IsBranded={IsBranded}",
                            food.Name, food.Quantity, food.Unit, food.IsBranded);
                    }
                }

                if (!parsedFoodsResponse.IsSuccess)
                {
                    _logger.LogWarning("Failed to parse food description: {Errors}",
                        string.Join(", ", parsedFoodsResponse.Errors.Select(e => e.ErrorMessage)));
                    response.AddError("Failed to parse food description. Please try with a clearer description.");
                    return response;
                }

                if (parsedFoodsResponse.Foods == null || !parsedFoodsResponse.Foods.Any())
                {
                    _logger.LogWarning("No foods identified in description: {FoodDescription}", foodDescription);
                    response.AddError("No food items could be identified from the description.");
                    return response;
                }

                var allFoodItems = new List<FoodItem>();
                var missingItems = new List<string>();

                var resolutionTasks = parsedFoodsResponse.Foods.Select(async item =>
                {
                    var (foodItems, success) = await TryResolveFoodItemAsync(item);
                    return (item.Name, foodItems, success);
                }).ToList();

                var resolvedResults = await Task.WhenAll(resolutionTasks);

                foreach (var result in resolvedResults)
                {
                    if (result.success)
                    {
                        allFoodItems.AddRange(result.foodItems);
                    }
                    else
                    {
                        missingItems.Add(result.Name);
                    }
                }

                // Check if we found any nutrition data
                if (allFoodItems.Any())
                {
                    _logger.LogInformation("Converting {Count} food items to FoodNutrition objects", allFoodItems.Count);

                    response.Foods = ConvertFoodItemsToFoodNutrition(allFoodItems);
                    response.IsSuccess = true;

                    if (response.IsSuccess && response.Foods.Any())
                    {
                        await SaveFoodEntryAsync(accountId, foodDescription, allFoodItems);  
                    }

                    return response;
                }
                else
                {
                    // If no nutrition data found, return an error
                    _logger.LogWarning("No nutrition data found for any items in: {FoodDescription}", foodDescription);
                    response.AddError("Could not find nutrition data for the specified food items.");
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in smart nutrition lookup for account {AccountId}: {FoodDescription}",
                    accountId, foodDescription);

                response.AddError($"Error getting nutrition data: {ex.Message}");
                return response;
            }
        }

        private async Task<(List<FoodItem> FoodItems, bool Success)> TryResolveFoodItemAsync(ParsedFoodItem item)
        {
            var foodItems = new List<FoodItem>();
            try
            {
                string searchQuery = BuildSearchQuery(item);
                _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);

                var searchResults = await _foodDataApi.SearchFoodsAsync(searchQuery, CancellationToken.None);
                if (!searchResults.Any())
                {
                    _logger.LogWarning("No options found for: {Name}", item.Name);
                    return (foodItems, false);
                }

                _logger.LogInformation("Found {Count} options for: {Name}", searchResults.Count, item.Name);

                // Use the first result for now - we can enhance this with better matching later
                var selectedFood = searchResults.First();
                var foodDetails = await _foodDataApi.GetFoodDetailsAsync(selectedFood.Id, CancellationToken.None);

                if (foodDetails != null)
                {
                    // Scale the food item based on user input
                    ScaleFoodItemFromUserInput(foodDetails, item.Quantity, item.Unit);
                    foodItems.Add(foodDetails);
                    return (foodItems, true);
                }

                _logger.LogWarning("Failed to get details for food: {Name}", item.Name);
                return (foodItems, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item: {Name}", item.Name);
                return (foodItems, false);
            }
        }

        private string BuildSearchQuery(ParsedFoodItem item)
        {
            var query = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(item.Brand))
            {
                query.Append(item.Brand).Append(" ");
            }

            query.Append(item.Name);

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                query.Append(" ").Append(item.Description);
            }

            return query.ToString().Trim();
        }

        private void ScaleFoodItemFromUserInput(
            FoodItem item,
            double scaleToQuantity,
            string scaleToUnit)
        {
            if (item == null || scaleToQuantity <= 0)
                return;

            try
            {
                // If the units match, just scale the quantity
                if (string.Equals(item.Unit, scaleToUnit, StringComparison.OrdinalIgnoreCase))
                {
                    var scaleFactor = scaleToQuantity / item.Quantity;
                    ScaleFoodItem(item, scaleFactor);
                    return;
                }

                // If we have weight information, use it for conversion
                if (item.WeightGramsPerUnit.HasValue && item.WeightGramsPerUnit.Value > 0)
                {
                    var targetWeightGrams = UnitScalingHelpers.TryConvertToGrams(scaleToQuantity, scaleToUnit);
                    if (targetWeightGrams.HasValue)
                    {
                        var currentWeightGrams = item.WeightGramsPerUnit.Value * item.Quantity;
                        var scaleFactor = targetWeightGrams.Value / currentWeightGrams;
                        ScaleFoodItem(item, scaleFactor);
                        return;
                    }
                }

                // If we can't convert, just update the quantity and unit
                item.Quantity = scaleToQuantity;
                item.Unit = scaleToUnit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scaling food item: {Name}", item.Name);
            }
        }

        private void ScaleFoodItem(FoodItem item, double scaleFactor)
        {
            item.Quantity *= scaleFactor;
            item.Calories *= scaleFactor;
            item.Protein *= scaleFactor;
            item.Fat *= scaleFactor;
            item.Carbohydrates *= scaleFactor;

            if (item.Micronutrients != null)
            {
                foreach (var key in item.Micronutrients.Keys.ToList())
                {
                    item.Micronutrients[key] *= scaleFactor;
                }
            }
        }

        private List<FoodNutrition> ConvertFoodItemsToFoodNutrition(List<FoodItem> foodItems)
        {
            var result = new List<FoodNutrition>();

            foreach (var item in foodItems)
            {
                var foodNutrition = new FoodNutrition
                {
                    Name = item.Name,
                    BrandName = item.BrandName,
                    Quantity = item.Quantity.ToString(),
                    Unit = item.Unit,
                    Calories = item.Calories,
                    Macronutrients = new Macronutrients
                    {
                        Protein = new NutrientInfo { Amount = item.Protein, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = item.Carbohydrates, Unit = "g" },
                        Fat = new NutrientInfo { Amount = item.Fat, Unit = "g" }
                    },
                    Micronutrients = item.Micronutrients.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new Micronutrient { Amount = kvp.Value, Unit = "g" }
                    )
                };

                result.Add(foodNutrition);
            }

            return result;
        }

        private async Task SaveFoodEntryAsync(string accountId, string description, List<FoodItem> foodItems)
        {
            try
            {
                var foodEntry = new FoodEntry
                {
                    AccountId = accountId,
                    Description = description,
                    LoggedDateUtc = DateTime.UtcNow,
                    GroupedItems = new List<FoodGroup>
                    {
                        new FoodGroup
                        {
                            GroupName = "Meal",
                            Items = foodItems
                        }
                    }
                };

                await _foodEntryService.AddFoodEntryAsync(accountId, new CreateFoodEntryRequest
                {
                    Description = description,
                    Meal = MealType.Unknown,
                    LoggedDateUtc = DateTime.UtcNow,
                    GroupedItems = foodEntry.GroupedItems
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving food entry for account {AccountId}", accountId);
            }
        }
    }
}

