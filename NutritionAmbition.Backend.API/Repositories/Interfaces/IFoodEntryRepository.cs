using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Repositories
{
    public interface IFoodEntryRepository
    {
        Task<FoodEntry?> GetByIdAsync(string id);
        Task<List<FoodEntry>> GetByAccountIdAsync(string accountId, DateTime? date = null, MealType? mealType = null);
        Task<List<FoodEntry>> GetByDateRangeAsync(string accountId, DateTime startDate, DateTime endDate, MealType? mealType = null);
        Task<List<FoodEntry>> GetFoodEntriesByAccountAndDateAsync(string accountId, DateTime date);
        Task AddAsync(FoodEntry entry);
        Task UpdateAsync(FoodEntry entry);
        Task DeleteAsync(string id);
    }
} 