using Microsoft.Extensions.DependencyInjection;
using NutritionAmbition.Backend.API.Services;

namespace NutritionAmbition.Backend.API
{
    public static class AiServiceExtensions
    {
        public static IServiceCollection AddAiServices(this IServiceCollection services)
        {
            // Register OpenAI service
            services.AddHttpClient<IOpenAiService, OpenAiService>();
            services.AddScoped<IOpenAiService, OpenAiService>();
            
            // Register Food Parsing service
            services.AddScoped<IFoodParsingService, FoodParsingService>();
            
            return services;
        }
    }
}
