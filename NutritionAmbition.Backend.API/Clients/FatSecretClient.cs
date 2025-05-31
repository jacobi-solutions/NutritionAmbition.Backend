using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Clients
{
    public class FatSecretClient
    {
        private readonly FatSecretSettings _fatSecretSettings;
        private readonly FatSecretTokenProvider _tokenProvider;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FatSecretClient> _logger;
        private readonly string _baseUrl;

        public FatSecretClient(
            FatSecretSettings fatSecretSettings,
            FatSecretTokenProvider tokenProvider,
            IHttpClientFactory httpClientFactory,
            ILogger<FatSecretClient> logger)
        {
            _fatSecretSettings = fatSecretSettings;
            _tokenProvider = tokenProvider;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _baseUrl = fatSecretSettings.BaseUrl;
        }

        public async Task<string> SearchFoodsAsync(string query, CancellationToken cancellationToken)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            var url = $"{_baseUrl}/rest/server.api?method=foods.search&search_expression={Uri.EscapeDataString(query)}&format=json";
            
            return await SendRequestAsync(url, token, cancellationToken);
        }

        public async Task<string> GetFoodByIdAsync(string foodId, CancellationToken cancellationToken)
        {
            var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
            var url = $"{_baseUrl}/rest/server.api?method=food.get&food_id={Uri.EscapeDataString(foodId)}&format=json";
            
            return await SendRequestAsync(url, token, cancellationToken);
        }

        private async Task<string> SendRequestAsync(string url, string token, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.SendAsync(request, cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("FatSecret API request failed with status {StatusCode}. Response: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"FatSecret API request failed with status {response.StatusCode}");
                }

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not HttpRequestException)
            {
                _logger.LogError(ex, "Error making request to FatSecret API");
                throw new HttpRequestException("Failed to communicate with FatSecret API", ex);
            }
        }
    }
} 