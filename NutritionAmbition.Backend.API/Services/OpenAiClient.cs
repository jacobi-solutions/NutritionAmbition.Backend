using System.Text;
using System.Text.Json;
using NutritionAmbition.Backend.API.Settings;
using Microsoft.Extensions.Options;

namespace NutritionAmbition.Backend.API.Services
{
    public class OpenAiClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;

        public OpenAiClient(HttpClient httpClient, IOptions<OpenAiSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;

            _httpClient.BaseAddress = new Uri(_settings.ApiEndpoint);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
        }

        public async Task<HttpResponseMessage> PostAsync(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(endpoint, content);
        }
    }
}
