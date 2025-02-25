using NutritionAmbition.Backend.API.DataContracts.FoodEntries;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IFoodEntryService
    {
        Task<CreateFoodEntryResponse> AddFoodEntryAsync(string accountId, CreateFoodEntryRequest request);
        Task<GetFoodEntriesResponse> GetFoodEntriesAsync(string accountId, GetFoodEntriesRequest request);
        Task<UpdateFoodEntryResponse> UpdateFoodEntryAsync(string accountId, UpdateFoodEntryRequest request);
        Task<DeleteFoodEntryResponse> DeleteFoodEntryAsync(string accountId, DeleteFoodEntryRequest request);
    }

    public class FoodEntryService : IFoodEntryService
    {
        private readonly FoodEntryRepository _foodEntryRepository;

        public FoodEntryService(FoodEntryRepository foodEntryRepository)
        {
            _foodEntryRepository = foodEntryRepository;
        }

        public async Task<CreateFoodEntryResponse> AddFoodEntryAsync(string accountId, CreateFoodEntryRequest request)
        {
            var foodEntry = new FoodEntry
            {
                AccountId = accountId,
                Description = request.Description,
                ParsedItems = request.ParsedItems ?? new List<FoodItem>()
            };

            await _foodEntryRepository.AddAsync(foodEntry);

            return new CreateFoodEntryResponse
            {
                FoodEntry = foodEntry
            };
        }

        public async Task<GetFoodEntriesResponse> GetFoodEntriesAsync(string accountId, GetFoodEntriesRequest request)
        {
            var entries = await _foodEntryRepository.GetByAccountIdAsync(accountId, request.LoggedDateUtc);
            return new GetFoodEntriesResponse
            {
                FoodEntries = entries
            };
        }

        public async Task<UpdateFoodEntryResponse> UpdateFoodEntryAsync(string accountId, UpdateFoodEntryRequest request)
        {
            var entry = await _foodEntryRepository.GetByIdAsync(request.FoodEntryId);
            if (entry == null)
            {
                return null;
            }

            entry.Description = request.Description ?? entry.Description;
            entry.ParsedItems = request.ParsedItems ?? entry.ParsedItems;

            await _foodEntryRepository.UpdateAsync(entry);

            return new UpdateFoodEntryResponse
            {
                UpdatedEntry = entry
            };
        }

        public async Task<DeleteFoodEntryResponse> DeleteFoodEntryAsync(string accountId, DeleteFoodEntryRequest request)
        {
            var entry = await _foodEntryRepository.GetByIdAsync(request.FoodEntryId);
            if (entry == null)
            {
                return new DeleteFoodEntryResponse { Success = false };
            }

            await _foodEntryRepository.DeleteAsync(request.FoodEntryId);
            return new DeleteFoodEntryResponse { Success = true };
        }
    }
} 