using System.Threading.Tasks;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IDailySummaryService
    {
        Task<DailySummaryResponse> GetDailySummaryAsync(string accountId);
    }
} 