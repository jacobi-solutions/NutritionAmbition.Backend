using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    public interface INutritionixService
    {
        Task<NutritionixResponse?> GetNutritionDataAsync(string query);
    }
}
