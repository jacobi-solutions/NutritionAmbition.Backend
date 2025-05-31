using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Clients
{
    public class FatSecretTokenProvider
    {
        private readonly FatSecretSettings _fatSecretSettings;
        private readonly ILogger<FatSecretTokenProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private string? _accessToken;
        private DateTime _expiresAt;

        public FatSecretTokenProvider(
            FatSecretSettings fatSecretSettings,
            ILogger<FatSecretTokenProvider> logger,
            IHttpClientFactory httpClientFactory)
        {
            _fatSecretSettings = fatSecretSettings;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiresAt)
            {
                return _accessToken;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_fatSecretSettings.BaseUrl}/connect/token");
                request.Content = new StringContent("grant_type=client_credentials&scope=basic", Encoding.UTF8, "application/x-www-form-urlencoded");

                var authHeader = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_fatSecretSettings.ClientId}:{_fatSecretSettings.ClientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                var response = await client.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var tokenResponse = JsonSerializer.Deserialize<FatSecretTokenResponse>(content);

                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
                {
                    throw new InvalidOperationException("Failed to deserialize token response");
                }

                _accessToken = tokenResponse.AccessToken;
                _expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

                return _accessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain FatSecret access token");
                throw new InvalidOperationException("Failed to obtain FatSecret access token", ex);
            }
        }

        private class FatSecretTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }
        }
    }
} 