using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Settings;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionixClient
    {
        private readonly HttpClient _httpClient;
        private readonly NutritionixSettings _settings;

        public NutritionixClient(HttpClient httpClient, IOptions<NutritionixSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;

            _httpClient.BaseAddress = new Uri(_settings.ApiEndpoint);
            _httpClient.DefaultRequestHeaders.Add("x-app-id", _settings.ApplicationId);
            _httpClient.DefaultRequestHeaders.Add("x-app-key", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<HttpResponseMessage> PostAsync(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(endpoint, content);
        }
    }
} 