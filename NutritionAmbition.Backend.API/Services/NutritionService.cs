using System;
using System.Collections.Generic;
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
        private readonly INutritionixService _nutritionixService;
        private readonly IOpenAiService _openAiService;
        private readonly IOpenAiResponsesService _openAiResponsesService;
        private readonly IFoodEntryService _foodEntryService;
        private readonly ILogger<NutritionService> _logger;
        private readonly NutritionixClient _nutritionixClient;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly IDailySummaryService _dailySummaryService;

        public NutritionService(
            INutritionixService nutritionixService,
            IOpenAiService openAiService,
            IOpenAiResponsesService openAiResponsesService,
            IFoodEntryService foodEntryService,
            ILogger<NutritionService> logger,
            NutritionixClient nutritionixClient,
            INutritionCalculationService nutritionCalculationService,
            IDailySummaryService dailySummaryService)
        {
            _nutritionixService = nutritionixService;
            _openAiService = openAiService;
            _openAiResponsesService = openAiResponsesService;
            _foodEntryService = foodEntryService;
            _logger = logger;
            _nutritionixClient = nutritionixClient;
            _nutritionCalculationService = nutritionCalculationService;
            _dailySummaryService = dailySummaryService;
        }

        private async Task<NutritionixFood> GetNutritionDataByNixItemIdAsync(string nixItemId)
        {
            // Call Nutritionix API to fetch nutrition data by NixItemId
            var response = await _nutritionixClient.GetNutritionByItemIdAsync(nixItemId);
            if (response == null)
            {
                throw new InvalidOperationException($"Nutritionix returned no data for NixItemId {nixItemId}");
            }
            return response;
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

                // 2. Split the parsed foods into branded and generic items
                var brandedItems = parsedFoodsResponse.Foods.Where(f => f.IsBranded).ToList();
                var genericItems = parsedFoodsResponse.Foods.Where(f => !f.IsBranded).ToList();

                _logger.LogInformation("Found {BrandedCount} branded items and {GenericCount} generic items",
                    brandedItems.Count, genericItems.Count);


                var allFoodItems = new List<FoodItem>();
                var missingItems = new List<string>();

                var resolutionTasks = parsedFoodsResponse.Foods.Select(async item =>
                {
                    var (foodItems, success) = await TryResolveFoodItemAsync(item, item.IsBranded);
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

                // 5. Check if we found any nutrition data
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

        private async Task<(List<FoodItem> FoodItems, bool Success)> TryResolveFoodItemAsync(ParsedFoodItem item, bool isBranded)
        {
            var foodItems = new List<FoodItem>();
            try
            {
                string searchQuery = BuildSearchQuery(item);
                _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);

                var searchResults = await _nutritionixService.SearchInstantAsync(searchQuery);
                var candidates = isBranded ? searchResults.Branded : searchResults.Common;

                if (candidates.Count == 0)
                {
                    _logger.LogWarning("No {Type} options found for: {Name}", isBranded ? "branded" : "generic", item.Name);
                    return (foodItems, false);
                }

                _logger.LogInformation("Found {Count} {Type} options for: {Name}", candidates.Count, isBranded ? "branded" : "generic", item.Name);

                var selectedNixItemId = await _openAiService.SelectBestFoodAsync(
                    isBranded ? $"{item.Brand} {item.Name}: {item.Description}" : $"{item.Name}: {item.Description}",
                    item.Quantity, item.Unit, candidates, isBranded);

                if (!string.IsNullOrWhiteSpace(selectedNixItemId))
                {
                    var selected = candidates.FirstOrDefault(x => x.NixFoodId.Equals(selectedNixItemId));
                    if (selected != null)
                    {
                        var resolvedItems = await ResolveAndScaleNutritionixFoodAsync(selected, item);
                        return (resolvedItems, true);
                    }

                    _logger.LogWarning("Failed to resolve selected {Type} food for ID: {Id}", isBranded ? "branded" : "generic", selectedNixItemId);
                }
                else if (!isBranded)
                {
                    _logger.LogWarning("Fallback: no TagId found, retrying as natural language with query: {Query}", searchQuery);
                    var fallbackResponse = await _nutritionixService.GetNutritionDataAsync(searchQuery);
                    if (fallbackResponse?.Foods.Any() == true)
                    {
                        var normalizedFoodItems = MapAndNormalizeNutritionixResponseToFoodItem(fallbackResponse);
                        foreach (var normalizedFoodItem in normalizedFoodItems)
                        {
                            ScaleFoodItemFromUserInput(normalizedFoodItem, item.Quantity, item.Unit);
                        }

                        return (normalizedFoodItems, true);
                    }
                }

                return (foodItems, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {Type} item: {Name}", isBranded ? "branded" : "generic", item.Name);
                return (foodItems, false);
            }
        }

        private List<FoodItem> MapAndNormalizeNutritionixResponseToFoodItem(NutritionixResponse nutritionixResponse)
        {
            var mappedFoods = new List<FoodItem>();

            if (nutritionixResponse?.Foods == null)
            {
                return mappedFoods;
            }

            foreach (var food in nutritionixResponse.Foods)
            {
                if (food != null)
                {
                    // Map the NutritionixFood to a FoodItem without scaling
                    var foodItem = new FoodItem
                    {
                        Name = food.FoodName ?? string.Empty,
                        BrandName = food.BrandName,
                        Quantity = food.ServingQty,
                        Unit = food.ServingUnit ?? string.Empty,
                        // Assign ApiServingKind using our new helper method
                        ApiServingKind = UnitKindHelper.InferUnitKindOrDefault(food.ServingUnit),
                        Micronutrients = new Dictionary<string, double>()
                    };

                    if (food.ServingWeightGrams.HasValue && food.ServingQty > 0)
                    {
                        foodItem.WeightGramsPerUnit = food.ServingWeightGrams.Value / food.ServingQty;
                    }

                    NutritionixNutrientMapper.MapMacronutrients(food, foodItem);
                    NutritionixNutrientMapper.MapMicronutrients(food, foodItem);


                    // Normalize nutrient values to a per-unit baseline
                    if (food.ServingQty != 1 && food.ServingQty > 0)
                    {
                        double normalizationFactor = 1.0 / food.ServingQty;

                        foodItem.Calories *= normalizationFactor;
                        foodItem.Protein *= normalizationFactor;
                        foodItem.Carbohydrates *= normalizationFactor;
                        foodItem.Fat *= normalizationFactor;
                        if (foodItem.WeightGramsPerUnit.HasValue)
                        {
                            foodItem.WeightGramsPerUnit *= normalizationFactor;
                        }

                        foreach (var key in foodItem.Micronutrients.Keys.ToList())
                            {
                                foodItem.Micronutrients[key] *= normalizationFactor;
                            }

                        foodItem.Quantity = 1;
                        foodItem.Unit = food.ServingUnit ?? string.Empty;

                        
                    }
                    
                    mappedFoods.Add(foodItem);
                }
            }
            
            return mappedFoods;
        }

        private List<FoodNutrition> ConvertFoodItemsToFoodNutrition(List<FoodItem> foodItems)
        {
            var foodNutritions = new List<FoodNutrition>();

            foreach (var item in foodItems)
            {

                var foodNutrition = new FoodNutrition
                {
                    Name = item.Name,
                    BrandName = item.BrandName,
                    // Use OriginalScaledQuantity for display to show user the real amount
                    Quantity = item.Quantity.ToString(),
                    Unit = item.Unit,
                    Calories = item.Calories,
                    Macronutrients = new Macronutrients
                    {
                        // Note: Nutrient values are already scaled - do not multiply by Quantity
                        Protein = new NutrientInfo { Amount = item.Protein, Unit = "g" },
                        Carbohydrates = new NutrientInfo { Amount = item.Carbohydrates, Unit = "g" },
                        Fat = new NutrientInfo { Amount = item.Fat, Unit = "g" },
                    },
                    Micronutrients = new Dictionary<string, Micronutrient>()
                };

                // Convert micronutrients
                foreach (var nutrient in item.Micronutrients)
                {
                    var match = NutritionixNutrientMapper.All.FirstOrDefault(kvp => kvp.Value.Name.Equals(nutrient.Key, StringComparison.OrdinalIgnoreCase));

                    string unit = match.Value.Item2 ?? "";

                    // Micronutrient values are already scaled - do not multiply by Quantity
                    foodNutrition.Micronutrients[nutrient.Key] = new Micronutrient
                    {
                        Amount = nutrient.Value,
                        Unit = unit
                    };
                }

                foodNutritions.Add(foodNutrition);
            }

            return foodNutritions;
        }

        // private string BuildSearchQuery(ParsedFoodItem item)
        // {
        //     var parts = new List<string>();

        //     // 2 tbsp peanut butter  →  "2 tbsp peanut butter"
        //     // 2 egg                 →  "2 egg"
        //     if (item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.Unit))
        //     {
        //         // avoid “egg egg”, “avocado avocado” …
        //         if (!item.Unit.Equals(item.Name, StringComparison.OrdinalIgnoreCase))
        //             parts.Add($"{item.Quantity} {item.Unit}");
        //         else
        //             parts.Add($"{item.Quantity} {item.Unit}".Trim());  // still “2 egg”
        //     }

        //     if (!string.IsNullOrWhiteSpace(item.Description))
        //         parts.Add(item.Description);

        //     if (!string.IsNullOrWhiteSpace(item.Brand))
        //         parts.Add(item.Brand);

        //     parts.Add(item.Name);     // always include the name once

        //     return string.Join(' ', parts);
        // }
        private string BuildSearchQuery(ParsedFoodItem item)
        {
            var queryComponents = new List<string>();

            // Add quantity and unit if available
            if (item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.Unit))
            {
                queryComponents.Add($"{item.Quantity} {item.Unit}");
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                queryComponents.Add(item.Description);
            }

            // Add brand if available
            if (!string.IsNullOrWhiteSpace(item.Brand))
            {
                queryComponents.Add(item.Brand);
            }

            // Always add the name
            queryComponents.Add(item.Name);

            // Join everything with spaces
            return string.Join(" ", queryComponents);
        }

        /// <summary>
        /// Centralized helper method for scaling food items based on user input
        /// </summary>
        /// <param name="item">Food item to scale</param>
        /// <param name="scaleToQuantity">User's requested quantity</param>
        /// <param name="scaleToUnit">User's requested unit</param>
        /// <param name="apiServingQty">API-provided serving quantity (from Nutritionix)</param>
        /// <param name="apiServingUnit">API-provided serving unit (from Nutritionix)</param>
        /// <param name="apiServingWeightG">API-provided serving weight in grams (from Nutritionix)</param>
        private void ScaleFoodItemFromUserInput(
            FoodItem item,
            double scaleToQuantity,
            string scaleToUnit)
        {
            try
            {
                // Skip scaling if required values are missing
                if (string.IsNullOrWhiteSpace(item.Unit))
                {
                    _logger.LogWarning("Skipping scaling for {FoodName} - missing API serving unit", item.Name);

                    item.Quantity = scaleToQuantity;
                    item.Unit = scaleToUnit;

                    return;
                }

                // Calculate multiplier using standardized scaling logic
                var multiplier = UnitScalingHelpers.GetMultiplierFromUserInput(
                    scaleToQuantity,
                    scaleToUnit,
                    item.Quantity,
                    item.Unit,
                    item.WeightGramsPerUnit,
                    item.ApiServingKind);

                if (multiplier.HasValue)
                {
                    // Scale the nutrition values using our centralized method
                    UnitScalingHelpers.ScaleNutrition(item, multiplier.Value, _logger);
                    item.Quantity = scaleToQuantity;
                    item.Unit = scaleToUnit;

                }
                else
                {
                    // Fallback: use user-provided quantity and unit without scaling                    
                    item.Quantity = scaleToQuantity;
                    item.Unit = scaleToUnit;

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scaling food item {FoodName}", item.Name);
            }
        }

        private async Task<List<FoodItem>> ResolveAndScaleNutritionixFoodAsync(
            NutritionixFood nutritionixFood,
            ParsedFoodItem originalInput)
        {
            var response = new NutritionixResponse
            {
                Foods = new List<NutritionixFood> { nutritionixFood }
            };

            var normalizedFoodItems = MapAndNormalizeNutritionixResponseToFoodItem(response);


            foreach (var normalizedFoodItem in normalizedFoodItems)
            {
                ScaleFoodItemFromUserInput(
                    normalizedFoodItem,
                    originalInput.Quantity,
                    originalInput.Unit);
            }

            return normalizedFoodItems;
        }
        
        private async Task SaveFoodEntryAsync(string accountId, string description, List<FoodItem> foodItems)
        {
            try
            {
                // todo v2 UX upgrade candidate
                // var groupedItems = await _openAiService.GroupFoodItemsAsync(description, foodItems);
                var groupedItems = new List<FoodGroup>
                {
                    new FoodGroup
                    {
                        GroupName = "Meal",
                        Items = foodItems
                    }
                };
                if (groupedItems == null || !groupedItems.Any())
                {
                    _logger.LogWarning("AI grouping failed, falling back to individual groups");
                    groupedItems = foodItems.Select(x => new FoodGroup
                    {
                        GroupName = x.Name,
                        Items = new List<FoodItem> { x }
                    }).ToList();
                }

                var request = new CreateFoodEntryRequest
                {
                    Description = description,
                    Meal = MealType.Unknown,
                    LoggedDateUtc = DateTime.UtcNow,
                    GroupedItems = groupedItems
                };

                var response = await _foodEntryService.AddFoodEntryAsync(accountId, request);
                if (!response.IsSuccess)
                {
                    _logger.LogWarning("Failed to save food entry for Account {AccountId}: {Errors}",
                        accountId, string.Join(", ", response.Errors));
                }
                else
                {
                    _logger.LogInformation("Successfully saved food entry {FoodEntryId} for Account {AccountId}",
                        response.FoodEntry?.Id, accountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving food entry for Account {AccountId} and description {Description}", accountId, description);
            }
        }



    }
}

