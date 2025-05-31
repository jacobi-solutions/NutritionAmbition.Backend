using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Clients;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Services
{

    public interface IFoodDataApi
    {
        Task<List<FoodItem>> SearchFoodsAsync(string query, CancellationToken ct);
        Task<FoodItem?> GetFoodDetailsAsync(string foodId, CancellationToken ct);
    }

    public class FatSecretService : IFoodDataApi
    {
        private readonly FatSecretClient _fatSecretClient;
        private readonly ILogger<FatSecretService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public FatSecretService(
            FatSecretClient fatSecretClient,
            ILogger<FatSecretService> logger)
        {
            _fatSecretClient = fatSecretClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<FoodItem>> SearchFoodsAsync(string query, CancellationToken ct)
        {
            try
            {
                var jsonResponse = await _fatSecretClient.SearchFoodsAsync(query, ct);
                var response = JsonSerializer.Deserialize<FatSecretSearchResponse>(jsonResponse, _jsonOptions);

                if (response?.Foods?.Food == null)
                {
                    _logger.LogWarning("No foods found in FatSecret response for query: {Query}", query);
                    return new List<FoodItem>();
                }

                return response.Foods.Food.Select(food => new FoodItem
                {
                    Name = food.FoodName,
                    BrandName = food.BrandName,
                    Id = food.FoodId // Using Id instead of ApiFoodId
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching foods in FatSecret API for query: {Query}", query);
                return new List<FoodItem>();
            }
        }

        public async Task<FoodItem?> GetFoodDetailsAsync(string foodId, CancellationToken ct)
        {
            try
            {
                var jsonResponse = await _fatSecretClient.GetFoodByIdAsync(foodId, ct);
                var response = JsonSerializer.Deserialize<FatSecretFoodResponse>(jsonResponse, _jsonOptions);

                if (response?.Food == null)
                {
                    _logger.LogWarning("No food details found in FatSecret response for foodId: {FoodId}", foodId);
                    return null;
                }

                var food = response.Food;
                var serving = GetFirstServing(food.Servings);

                if (serving == null)
                {
                    _logger.LogWarning("No serving information found for foodId: {FoodId}", foodId);
                    return null;
                }

                var (unit, weightGrams) = ParseServingDescription(serving.ServingDescription);

                return new FoodItem
                {
                    Name = food.FoodName,
                    BrandName = food.BrandName,
                    Id = food.FoodId,
                    Calories = ParseDouble(serving.Calories),
                    Protein = ParseDouble(serving.Protein),
                    Fat = ParseDouble(serving.Fat),
                    Carbohydrates = ParseDouble(serving.Carbohydrate),
                    Unit = unit,
                    WeightGramsPerUnit = weightGrams,
                    Quantity = 1,
                    ApiServingKind = UnitKind.Weight // Default to Weight since we're parsing grams
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting food details from FatSecret API for foodId: {FoodId}", foodId);
                return null;
            }
        }

        private static FatSecretServing? GetFirstServing(FatSecretServings servings)
        {
            if (servings?.Serving == null)
                return null;

            // Handle both array and single object cases
            return servings.Serving is JsonElement element && element.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<List<FatSecretServing>>(element.GetRawText())?.FirstOrDefault()
                : JsonSerializer.Deserialize<FatSecretServing>(servings.Serving.ToString());
        }

        private static (string Unit, double WeightGrams) ParseServingDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return (string.Empty, 0);

            var match = Regex.Match(description, @"\((\d+)\s*g\)");
            var weightGrams = match.Success ? double.Parse(match.Groups[1].Value) : 0;

            // Extract the unit part (e.g., "1 cup" from "1 cup (240 g)")
            var unitMatch = Regex.Match(description, @"^[\d./]+\s+(.+?)(?:\s*\(|$)");
            var unit = unitMatch.Success ? unitMatch.Groups[1].Value.Trim() : description;

            return (unit, weightGrams);
        }

        private static double ParseDouble(string value)
        {
            return double.TryParse(value, out var result) ? result : 0;
        }

        private class FatSecretSearchResponse
        {
            public FatSecretFoods Foods { get; set; }
        }

        private class FatSecretFoods
        {
            public List<FatSecretFood> Food { get; set; }
        }

        private class FatSecretFood
        {
            public string FoodId { get; set; }
            public string FoodName { get; set; }
            public string BrandName { get; set; }
        }

        private class FatSecretFoodResponse
        {
            public FatSecretFoodDetails Food { get; set; }
        }

        private class FatSecretFoodDetails : FatSecretFood
        {
            public FatSecretServings Servings { get; set; }
        }

        private class FatSecretServings
        {
            public object Serving { get; set; }
        }

        private class FatSecretServing
        {
            public string ServingDescription { get; set; }
            public string Calories { get; set; }
            public string Protein { get; set; }
            public string Fat { get; set; }
            public string Carbohydrate { get; set; }
        }
    }
} 