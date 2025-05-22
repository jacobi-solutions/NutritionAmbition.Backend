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

        private async Task<NutritionixFood> GetBrandedNutritionDataAsync(string nixItemId)
        {
            // Call Nutritionix API to fetch nutrition data by NixItemId
            var response = await _nutritionixClient.GetNutritionByItemIdAsync(nixItemId);
            if (response == null)
            {
                throw new InvalidOperationException($"Nutritionix returned no data for NixItemId {nixItemId}");
            }
            return response;
        }

        private async Task<NutritionixFood> GetGenericNutritionDataAsync(string tagName)
        {
            // Call Nutritionix API to fetch generic food nutrition data by TagName
            var response = await _nutritionixClient.GetNutritionByItemIdAsync(tagName);
            if (response == null)
            {
                throw new InvalidOperationException($"Nutritionix returned no data for TagName {tagName}");
            }
            return response;
        }

        public async Task<NutritionApiResponse> GetSmartNutritionDataAsync(string accountId, string foodDescription)
        {
            var response = new NutritionApiResponse();
            try
            {
                _logger.LogInformation("Smart nutrition lookup for account {AccountId}: {FoodDescription}", accountId, foodDescription);

                // Track items that couldn't be matched
                var missingItems = new List<string>();

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

                // Lists to collect the results
                var allFoodItems = new List<FoodItem>();
                var brandedProcessed = 0;
                var genericProcessed = 0;

                // 3. Process branded items - look them up individually for more precise results
                foreach (var brandedItem in brandedItems)
                {
                    try
                    {
                        _logger.LogInformation("Looking up branded item: {Name} ({Quantity} {Unit})",
                            brandedItem.Name, brandedItem.Quantity, brandedItem.Unit);

                        // Build a more natural search query combining all information
                        string searchQuery = BuildSearchQuery(brandedItem);
                        _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);

                        // Search for the branded item
                        var searchResults = await _nutritionixService.SearchInstantAsync(searchQuery);

                        if (searchResults.Branded.Count > 0)
                        {
                            _logger.LogInformation("Found {Count} branded food options for: {Name}",
                                searchResults.Branded.Count, brandedItem.Name);

                            // Use OpenAI to select the best branded food match
                            // var selectedNixItemId = await _openAiResponsesService.SelectBestBrandedFoodAsync($"{brandedItem.Brand} {brandedItem.Name}", brandedItem.Quantity, brandedItem.Unit, searchResults.Branded);
                            var selectedNixItemId = await _openAiService.SelectBestFoodAsync($"{brandedItem.Brand} {brandedItem.Name}: {brandedItem.Description}", brandedItem.Quantity, brandedItem.Unit, searchResults.Branded, isBranded: true);
                            if (!string.IsNullOrWhiteSpace(selectedNixItemId))
                            {

                                try
                                {
                                    // Get detailed nutrition data using the Nix Item ID
                                    var brandedNutritionData = searchResults.Branded.FirstOrDefault(x => x.NixFoodId.Equals(selectedNixItemId));// await GetdNutritionDataByNixItemIdAsync(selectedNixItemId);

                                    if (brandedNutritionData != null)
                                    {
                                        var foodItems = await ResolveAndScaleNutritionixFoodAsync(brandedNutritionData, brandedItem);

                                        allFoodItems.AddRange(foodItems);
                                        brandedProcessed++;

                                        _logger.LogInformation("Successfully retrieved nutrition data for branded food with NixItemId: {NixItemId}",
                                            selectedNixItemId);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to get nutrition data for selected branded food with NixItemId: {NixItemId}",
                                            selectedNixItemId);
                                        genericItems.Add(brandedItem); // Process as generic if nutrition data retrieval failed
                                        missingItems.Add(brandedItem.Name);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error fetching nutrition data for branded food with NixItemId: {NixItemId}",
                                        selectedNixItemId);
                                    genericItems.Add(brandedItem); // Process as generic if nutrition data retrieval failed
                                    missingItems.Add(brandedItem.Name);
                                }
                            }
                            else
                            {
                                _logger.LogInformation("No confident branded match found by AI for: {Name}", brandedItem.Name);
                                genericItems.Add(brandedItem); // Process as generic if no confident match
                                missingItems.Add(brandedItem.Name);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No branded items found for: {Name}", brandedItem.Name);
                            genericItems.Add(brandedItem); // Process as generic if no search results
                            missingItems.Add(brandedItem.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing branded item: {Name}", brandedItem.Name);
                        // Continue with other items
                    }
                }

                // 4. Process generic items - process each item individually with AI selection
                if (genericItems.Any())
                {
                    _logger.LogInformation("Processing {Count} generic items individually", genericItems.Count);

                    foreach (var genericItem in genericItems)
                    {
                        try
                        {
                            _logger.LogInformation("Looking up generic item: {Name} ({Quantity} {Unit})",
                                 genericItem.Name, genericItem.Quantity, genericItem.Unit);

                            // Build a more natural search query combining all information
                            string searchQuery = BuildSearchQuery(genericItem);
                            _logger.LogInformation("Using search query: {SearchQuery}", searchQuery);

                            // Search for the generic item
                            var searchResults = await _nutritionixService.SearchInstantAsync(searchQuery);

                            if (searchResults.Common.Count > 0)
                            {
                                _logger.LogInformation("Found {Count} generic food options for: {Name}",
                                    searchResults.Common.Count, genericItem.Name);

                                // Use OpenAI to select the best generic food match
                                var selectedNixItemId = await _openAiService.SelectBestFoodAsync($"{genericItem.Name}: {genericItem.Description}", genericItem.Quantity, genericItem.Unit, searchResults.Common, isBranded: false);

                                try
                                {
                                    // Get detailed nutrition data using the TagName
                                    if (!string.IsNullOrEmpty(selectedNixItemId))
                                    {
                                        var genericNutritionData = searchResults.Common.FirstOrDefault(x => x.NixFoodId.Equals(selectedNixItemId));// await GetNutritionDataByNixItemIdAsync(selectedNixItemId);

                                        if (genericNutritionData != null)
                                        {
                                            var foodItems = await ResolveAndScaleNutritionixFoodAsync(genericNutritionData, genericItem);

                                            allFoodItems.AddRange(foodItems);
                                            genericProcessed++;

                                            _logger.LogInformation("Successfully retrieved nutrition data for generic food with TagName: {TagName}",
                                                selectedNixItemId);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Failed to get nutrition data for selected generic food with TagName: {TagName}",
                                                selectedNixItemId);
                                            missingItems.Add(genericItem.Name);
                                        }
                                    }
                                    else
                                    {
                                        // Fallback to using food name if TagName is not available
                                        _logger.LogWarning("No TagName available for selected generic food, falling back to food name: {FoodName}",
                                            selectedNixItemId);

                                        // Create a basic ParsedFoodItem to use with our query builder
                                        var queryItem = new ParsedFoodItem
                                        {
                                            Name = genericItem.Name,
                                            Quantity = genericItem.Quantity,
                                            Unit = genericItem.Unit
                                        };

                                        string fallbackSearchQuery = BuildSearchQuery(queryItem);
                                        _logger.LogInformation("Using fallback search query: {SearchQuery}", fallbackSearchQuery);

                                        var nutritionixResponse = await _nutritionixService.GetNutritionDataAsync(fallbackSearchQuery);

                                        if (nutritionixResponse != null && nutritionixResponse.Foods.Any())
                                        {
                                            var foodItems = MapNutritionixResponseToFoodItem(nutritionixResponse);

                                            foreach (var foodItem in foodItems)
                                            {
                                                ScaleFoodItemFromUserInput(
                                                    foodItem,
                                                    genericItem.Quantity,
                                                    genericItem.Unit,
                                                    nutritionixResponse.Foods.FirstOrDefault()?.ServingQty,
                                                    nutritionixResponse.Foods.FirstOrDefault()?.ServingUnit,
                                                    nutritionixResponse.Foods.FirstOrDefault()?.ServingWeightGrams);
                                            }

                                            allFoodItems.AddRange(foodItems);
                                            genericProcessed++;
                                        }
                                        else
                                        {
                                            _logger.LogWarning("Failed to get nutrition data for selected generic food: {Query}",
                                                fallbackSearchQuery);
                                            missingItems.Add(genericItem.Name);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error fetching nutrition data for generic food: {Name}", genericItem.Name);
                                    missingItems.Add(genericItem.Name);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("No generic options found for: {Name}", genericItem.Name);
                                missingItems.Add(genericItem.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing generic item: {Name}", genericItem.Name);
                            // Continue with other generic items
                        }
                    }

                    if (genericProcessed == 0)
                    {
                        _logger.LogWarning("None of the {Count} generic items were successfully processed", genericItems.Count);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully processed {ProcessedCount} out of {TotalCount} generic items",
                            genericProcessed, genericItems.Count);
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

        private List<FoodItem> MapNutritionixResponseToFoodItem(NutritionixResponse nutritionixResponse)
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

                    NutritionixNutrientMapper.MapMacronutrients(food, foodItem);
                    NutritionixNutrientMapper.MapMicronutrients(food, foodItem);

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
        /// <param name="quantity">User's requested quantity</param>
        /// <param name="unit">User's requested unit</param>
        /// <param name="apiServingQty">API-provided serving quantity (from Nutritionix)</param>
        /// <param name="apiServingUnit">API-provided serving unit (from Nutritionix)</param>
        /// <param name="apiServingWeightG">API-provided serving weight in grams (from Nutritionix)</param>
        private void ScaleFoodItemFromUserInput(
            FoodItem item,
            double quantity,
            string unit,
            double? apiServingQty,
            string? apiServingUnit,
            double? apiServingWeightG)
        {
            try
            {
                // Skip scaling if required values are missing
                if (string.IsNullOrWhiteSpace(apiServingUnit))
                {
                    _logger.LogWarning("Skipping scaling for {FoodName} - missing API serving unit", item.Name);

                    // Even if we skip scaling, normalize Quantity to 1 and store original quantity
                    item.Quantity = quantity;
                    item.Unit = unit;

                    return;
                }

                // Calculate multiplier using standardized scaling logic
                var multiplier = UnitScalingHelpers.ScaleFromUserInput(
                    quantity,
                    unit,
                    apiServingQty ?? 1,
                    apiServingUnit,
                    apiServingWeightG,
                    item.ApiServingKind);

                if (multiplier.HasValue)
                {
                    // Scale the nutrition values using our centralized method
                    UnitScalingHelpers.ScaleNutrition(item, multiplier.Value, _logger);

                    item.Quantity = multiplier.Value;
                    item.Unit = apiServingUnit;

                }
                else
                {
                    // Fallback: use user-provided quantity and unit without scaling                    
                    item.Quantity = quantity;
                    item.Unit = unit;

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

            var foodItems = MapNutritionixResponseToFoodItem(response);

            // Fallback if serving weight is missing
            var servingWeightG = nutritionixFood.ServingWeightGrams
                            ?? UnitScalingHelpers.TryInferMassFromVolume(nutritionixFood.ServingUnit);

            foreach (var item in foodItems)
            {
                ScaleFoodItemFromUserInput(
                    item,
                    originalInput.Quantity,
                    originalInput.Unit,
                    nutritionixFood.ServingQty,
                    nutritionixFood.ServingUnit,
                    servingWeightG);
            }

            return foodItems;
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

