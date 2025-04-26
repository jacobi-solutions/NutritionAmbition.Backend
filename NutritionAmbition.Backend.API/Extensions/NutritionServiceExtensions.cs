using Microsoft.Extensions.DependencyInjection;
using NutritionAmbition.Backend.API.Services;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API
{
    public static class NutritionServiceExtensions
    {
        public static IServiceCollection AddNutritionServices(this IServiceCollection services)
        {
            // Register USDA FoodData Central API service
            services.AddHttpClient<IUsdaFoodDataService, UsdaFoodDataService>();
            services.AddScoped<IUsdaFoodDataService, UsdaFoodDataService>();
            
            // Register Nutrition service
            services.AddScoped<INutritionService, NutritionService>();
            
            return services;
        }
    }
}
