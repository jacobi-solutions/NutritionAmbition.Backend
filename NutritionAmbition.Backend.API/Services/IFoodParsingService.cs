using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IFoodParsingService
    {
        Task<ParseFoodTextResponse> ParseFoodTextAsync(string foodDescription);
        // Removed GetNutritionDataAsync as it's redundant with NutritionService.ProcessFoodTextAndGetNutritionAsync
    }
}

