using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Settings;
using System.Collections.Generic;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionixClient
    {
        private readonly HttpClient _httpClient;
        private readonly NutritionixSettings _settings;

        public NutritionixClient(HttpClient httpClient, NutritionixSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;

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

        public async Task<HttpResponseMessage> GetAsync(string endpoint, Dictionary<string, string> queryParams = null)
        {
            var requestUri = endpoint;
            
            if (queryParams != null && queryParams.Count > 0)
            {
                var queryString = new StringBuilder("?");
                foreach (var param in queryParams)
                {
                    queryString.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}&");
                }
                // Remove the trailing &
                queryString.Length--;
                requestUri += queryString.ToString();
            }
            
            return await _httpClient.GetAsync(requestUri);
        }

        public async Task<SearchInstantResponse> SearchInstantAsync(string query)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "query", query },
                { "branded", "true" },
                { "common", "true" },
                { "detailed", "true" }
            };

            var response = await GetAsync("search/instant", queryParams);
            
            if (!response.IsSuccessStatusCode)
            {
                return new SearchInstantResponse();
            }

            var responseStream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<SearchInstantResponse>(responseStream, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SearchInstantResponse();
        }

        public async Task<NutritionixFood> GetNutritionByItemIdAsync(string nixItemId)
        {
            try
            {
                // Use the item ID to get detailed nutrition information
                var queryParams = new Dictionary<string, string>
                {
                    { "nix_item_id", nixItemId }
                };

                var response = await GetAsync("search/item", queryParams);
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var nutritionixResponse = JsonSerializer.Deserialize<NutritionixResponse>(responseContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (nutritionixResponse?.Foods == null || nutritionixResponse.Foods.Count == 0)
                {
                    return null;
                }

                // Return the first (and typically only) food item
                return nutritionixResponse.Foods[0];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<NutritionixFood> GetNutritionByTagNameAsync(string tagName)
        {
            try
            {
                if (string.IsNullOrEmpty(tagName))
                {
                    return null;
                }

                // Use natural/nutrients endpoint with the tag_name as query
                var data = new { query = tagName };
                var response = await PostAsync("natural/nutrients", data);
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var nutritionixResponse = JsonSerializer.Deserialize<NutritionixResponse>(responseContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (nutritionixResponse?.Foods == null || nutritionixResponse.Foods.Count == 0)
                {
                    return null;
                }

                // Return the first (and typically only) food item
                return nutritionixResponse.Foods[0];
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
} 