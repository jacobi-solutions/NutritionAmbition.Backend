using System.Text.Json;
using System.Web;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IUsdaFoodDataService
    {
        Task<List<FoodSearchResult>> SearchFoodsAsync(string query, int pageSize = 25);
        Task<FoodDetails> GetFoodDetailsAsync(int fdcId);
    }

    public class UsdaFoodDataService : IUsdaFoodDataService
    {
        private readonly ILogger<UsdaFoodDataService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.nal.usda.gov/fdc/v1";

        public UsdaFoodDataService(ILogger<UsdaFoodDataService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiKey = "6ocOPNlbNk5Eno0mtnJwCJcMq2qowmK0iquoyqvW"; // In production, this should be stored in a secure configuration
        }

        public async Task<List<FoodSearchResult>> SearchFoodsAsync(string query, int pageSize = 25)
        {
            try
            {
                _logger.LogInformation("Searching USDA foods with query: {Query}", query);

                var encodedQuery = HttpUtility.UrlEncode(query);
                var url = $"{_baseUrl}/foods/search?api_key={_apiKey}&query={encodedQuery}&pageSize={pageSize}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<UsdaSearchResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (searchResponse?.Foods == null)
                {
                    return new List<FoodSearchResult>();
                }

                var results = new List<FoodSearchResult>();
                foreach (var food in searchResponse.Foods)
                {
                    results.Add(new FoodSearchResult
                    {
                        FdcId = food.FdcId,
                        Description = food.Description,
                        BrandName = food.BrandName,
                        Ingredients = food.Ingredients,
                        FoodCategory = food.FoodCategory
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching USDA foods with query: {Query}", query);
                throw;
            }
        }

        public async Task<FoodDetails> GetFoodDetailsAsync(int fdcId)
        {
            try
            {
                _logger.LogInformation("Getting USDA food details for FDC ID: {FdcId}", fdcId);

                var url = $"{_baseUrl}/food/{fdcId}?api_key={_apiKey}";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var usdaFood = JsonSerializer.Deserialize<UsdaFoodDetails>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (usdaFood == null)
                {
                    throw new Exception($"Failed to deserialize food details for FDC ID: {fdcId}");
                }

                var foodDetails = new FoodDetails
                {
                    FdcId = usdaFood.FdcId,
                    Description = usdaFood.Description,
                    BrandName = usdaFood.BrandName,
                    Ingredients = usdaFood.Ingredients,
                    ServingSize = usdaFood.ServingSize,
                    ServingSizeUnit = usdaFood.ServingSizeUnit,
                    FoodCategory = usdaFood.FoodCategory,
                    Nutrients = new List<Nutrient>()
                };

                if (usdaFood.FoodNutrients != null)
                {
                    foreach (var nutrient in usdaFood.FoodNutrients)
                    {
                        if (nutrient.Nutrient != null)
                        {
                            foodDetails.Nutrients.Add(new Nutrient
                            {
                                Id = nutrient.Nutrient.Id,
                                Name = nutrient.Nutrient.Name,
                                UnitName = nutrient.Nutrient.UnitName,
                                Amount = nutrient.Amount,
                                Number = nutrient.Nutrient.Number
                            });
                        }
                    }
                }

                return foodDetails;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting USDA food details for FDC ID: {FdcId}", fdcId);
                throw;
            }
        }
    }

    // USDA API response models
    public class UsdaSearchResponse
    {
        public List<UsdaFoodSearchResult> Foods { get; set; } = new List<UsdaFoodSearchResult>();
        public int TotalHits { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }

    public class UsdaFoodSearchResult
    {
        public int FdcId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;

        [JsonConverter(typeof(SafeStringConverter))]
        public string FoodCategory { get; set; } = string.Empty;
    }

    public class UsdaFoodDetails
    {
        public int FdcId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public double? ServingSize { get; set; }
        public string ServingSizeUnit { get; set; } = string.Empty;
        [JsonConverter(typeof(SafeStringConverter))]
        public string FoodCategory { get; set; } = string.Empty;
        public List<UsdaFoodNutrient> FoodNutrients { get; set; } = new List<UsdaFoodNutrient>();
    }

    public class UsdaFoodNutrient
    {
        public UsdaNutrient Nutrient { get; set; } = new UsdaNutrient();
        public double Amount { get; set; }
    }

    public class UsdaNutrient
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
    }

    // Our application models
    public class FoodSearchResult
    {
        public int FdcId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        [JsonConverter(typeof(SafeStringConverter))]
        public string FoodCategory { get; set; } = string.Empty;
    }

    public class FoodDetails
    {
        public int FdcId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string BrandName { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public double? ServingSize { get; set; }
        public string ServingSizeUnit { get; set; } = string.Empty;
        [JsonConverter(typeof(SafeStringConverter))]
        public string FoodCategory { get; set; } = string.Empty;
        public List<Nutrient> Nutrients { get; set; } = new List<Nutrient>();
    }

    public class Nutrient
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public double Amount { get; set; }
    }
}
