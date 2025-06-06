using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IFoodEntryService
    {
        Task<CreateFoodEntryResponse> AddFoodEntryAsync(string accountId, CreateFoodEntryRequest request);
        Task<GetFoodEntriesResponse> GetFoodEntriesAsync(string accountId, GetFoodEntriesRequest request);
        Task<UpdateFoodEntryResponse> UpdateFoodEntryAsync(string accountId, UpdateFoodEntryRequest request);
        Task<DeleteFoodEntryResponse> RemoveFoodItemsFromEntryAsync(string accountId, DeleteFoodEntryRequest request);
    }

    public class FoodEntryService : IFoodEntryService
    {
        private readonly FoodEntryRepository _foodEntryRepository;
        private readonly INutritionCalculationService _nutritionCalculationService;
        private readonly ILogger<FoodEntryService> _logger;

        public FoodEntryService(
            FoodEntryRepository foodEntryRepository, 
            INutritionCalculationService nutritionCalculationService,
            ILogger<FoodEntryService> logger)
        {
            _foodEntryRepository = foodEntryRepository;
            _nutritionCalculationService = nutritionCalculationService;
            _logger = logger;
        }

        public async Task<CreateFoodEntryResponse> AddFoodEntryAsync(string accountId, CreateFoodEntryRequest request)
        {
            var response = new CreateFoodEntryResponse();
            try
            {
                var foodEntry = new FoodEntry
                {
                    AccountId = accountId,
                    Description = request.Description,
                    Meal = request.Meal,
                    LoggedDateUtc = request.LoggedDateUtc,
                    // 🟢 Use GroupedItems from request
                    GroupedItems = request.GroupedItems ?? new List<FoodGroup>() 
                };

                await _foodEntryRepository.AddAsync(foodEntry);
                response.FoodEntry = foodEntry;
                response.IsSuccess = true;
                _logger.LogInformation("Successfully added food entry {FoodEntryId} for account {AccountId}", foodEntry.Id, accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding food entry for account {AccountId}", accountId);
                response.AddError($"Failed to add food entry: {ex.Message}");
            }
            return response;
        }

        public async Task<GetFoodEntriesResponse> GetFoodEntriesAsync(string accountId, GetFoodEntriesRequest request)
        {
            var response = new GetFoodEntriesResponse();
            try
            {
                var entries = await _foodEntryRepository.GetByAccountIdAsync(accountId, request.LoggedDateUtc, request.Meal);
                response.FoodEntries = entries;

                // Calculate nutrition totals using NutritionCalculationService
                if (entries.Any())
                {
                    var foodItems = _nutritionCalculationService.FlattenEntries(entries);
                    var nutritionTotals = _nutritionCalculationService.CalculateTotals(foodItems);
                    
                    // Map nutrition totals to response
                    response.TotalCalories = nutritionTotals.TotalCalories;
                    response.TotalProtein = nutritionTotals.TotalProtein;
                    response.TotalCarbs = nutritionTotals.TotalCarbohydrates;
                    response.TotalFat = nutritionTotals.TotalFat;
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting food entries for account {AccountId}", accountId);
                response.AddError($"Failed to get food entries: {ex.Message}");
            }
            return response;
        }

        public async Task<UpdateFoodEntryResponse> UpdateFoodEntryAsync(string accountId, UpdateFoodEntryRequest request)
        {
            var response = new UpdateFoodEntryResponse();
            try
            {
                var entry = await _foodEntryRepository.GetByIdAsync(request.FoodEntryId);
                if (entry == null || entry.AccountId != accountId)
                {
                    response.AddError("Food entry not found or access denied.");
                    return response;
                }

                // Update fields if provided in the request
                entry.Description = request.Description ?? entry.Description;
                entry.Meal = request.Meal ?? entry.Meal;
                entry.LoggedDateUtc = request.LoggedDateUtc ?? entry.LoggedDateUtc;
                // 🟢 Update GroupedItems if provided (assuming UpdateFoodEntryRequest is updated)
                // entry.GroupedItems = request.GroupedItems ?? entry.GroupedItems; 
                // Note: Need to update UpdateFoodEntryRequest and UpdateFoodEntryResponse if GroupedItems should be updatable.
                // For now, we assume only Description, Meal, LoggedDateUtc are updatable via this method.
                // If ParsedItems was previously updatable, we need to decide how to handle GroupedItems updates.

                await _foodEntryRepository.UpdateAsync(entry);
                response.UpdatedEntry = entry;
                response.IsSuccess = true;
                _logger.LogInformation("Successfully updated food entry {FoodEntryId} for account {AccountId}", entry.Id, accountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating food entry {FoodEntryId} for account {AccountId}", request.FoodEntryId, accountId);
                response.AddError($"Failed to update food entry: {ex.Message}");
            }
            return response;
        }

        public async Task<DeleteFoodEntryResponse> RemoveFoodItemsFromEntryAsync(string accountId, DeleteFoodEntryRequest request)
        {
            var response = new DeleteFoodEntryResponse();

            try
            {
                var entries = await _foodEntryRepository.GetByAccountIdAsync(accountId);
                var updatedEntries = new List<FoodEntry>();
                var entriesToDelete = new List<string>();

                foreach (var entry in entries)
                {
                    bool entryModified = false;

                    foreach (var group in entry.GroupedItems.ToList())
                    {
                        var originalCount = group.Items.Count;
                        group.Items = group.Items
                            .Where(item => !request.FoodItemIds.Contains(item.Id.ToString()))
                            .ToList();

                        if (group.Items.Count < originalCount)
                            entryModified = true;
                    }

                    // Remove empty groups
                    entry.GroupedItems = entry.GroupedItems.Where(g => g.Items.Any()).ToList();

                    if (entryModified)
                    {
                        if (!entry.GroupedItems.Any())
                        {
                            entriesToDelete.Add(entry.Id);
                        }
                        else
                        {
                            updatedEntries.Add(entry);
                        }
                    }
                }

                // Apply changes to DB
                foreach (var update in updatedEntries)
                {
                    await _foodEntryRepository.UpdateAsync(update);
                    _logger.LogInformation("Updated FoodEntry {FoodEntryId} after removing items", update.Id);
                }

                foreach (var deleteId in entriesToDelete)
                {
                    await _foodEntryRepository.DeleteAsync(deleteId);
                    _logger.LogInformation("Deleted FoodEntry {FoodEntryId} because all items were removed", deleteId);
                }

                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting food item(s) from entry for account {AccountId}", accountId);
                response.AddError("Failed to delete one or more food items.");
            }

            return response;
        }
    }
}

