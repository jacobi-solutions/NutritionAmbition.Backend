using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using NutritionAmbition.Backend.API.Repositories;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IThreadService
    {
        Task<GetTodayThreadResponse> GetTodayThreadAsync(string accountId);
    }

    public class ThreadService : IThreadService
    {
        private readonly IOpenAiService _openAiService;
        private readonly OpenAiThreadRepository _threadRepository;
        private readonly ILogger<ThreadService> _logger;

        public ThreadService(
            IOpenAiService openAiService,
            OpenAiThreadRepository threadRepository,
            ILogger<ThreadService> logger)
        {
            _openAiService = openAiService;
            _threadRepository = threadRepository;
            _logger = logger;
        }

        public async Task<GetTodayThreadResponse> GetTodayThreadAsync(string accountId)
        {
            var response = new GetTodayThreadResponse();

            try
            {
                var today = DateTime.UtcNow.Date;
                var existingThread = await _threadRepository.GetThreadByAccountIdAndDateAsync(accountId, today);

                if (existingThread != null)
                {
                    _logger.LogInformation("Found existing thread {ThreadId} for account {AccountId}", 
                        existingThread.ThreadId, accountId);
                    response.ThreadId = existingThread.ThreadId;
                    response.IsSuccess = true;
                    return response;
                }

                // Create new thread
                var threadId = await _openAiService.CreateNewThreadAsync();
                
                var threadRecord = new OpenAiThreadRecord
                {
                    AccountId = accountId,
                    Date = today,
                    ThreadId = threadId
                };

                await _threadRepository.InsertThreadAsync(threadRecord);

                _logger.LogInformation("Created new thread {ThreadId} for account {AccountId}", 
                    threadId, accountId);

                response.ThreadId = threadId;
                response.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's thread for account {AccountId}", accountId);
                response.AddError("Failed to get or create thread.");
            }

            return response;
        }
    }
} 