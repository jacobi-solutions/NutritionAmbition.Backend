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
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionixClient
    {
        private readonly HttpClient _httpClient;
        private readonly NutritionixSettings _settings;

        /// <summary>
        /// Provides access to the Nutritionix settings
        /// </summary>
        public NutritionixSettings Settings => _settings;

        public NutritionixClient(HttpClient httpClient, NutritionixSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;

            _httpClient.BaseAddress = new Uri(_settings.ApiEndpoint);
            _httpClient.DefaultRequestHeaders.Add(NutritionixConstants.AppIdHeader, _settings.ApplicationId);
            _httpClient.DefaultRequestHeaders.Add(NutritionixConstants.AppKeyHeader, _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<HttpResponseMessage> PostAsync(string endpoint, object data)
        {
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PostAsync(endpoint, content);
        }

        public async Task<HttpResponseMessage> GetAsync(string sendpoint, Dictionary<string, string> queryParams = null)
        {
            var requestUri = sendpoint;
            
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
                { NutritionixConstants.QueryParam, query },
                { NutritionixConstants.BrandedParam, _settings.BrandedDefault },
                { NutritionixConstants.CommonParam, _settings.CommonDefault },
                { NutritionixConstants.DetailedParam, _settings.DetailedDefault }
            };

            var response = await GetAsync(_settings.SearchInstantPath, queryParams);
            
            if (!response.IsSuccessStatusCode)
            {
                return new SearchInstantResponse();
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var nutritionixResponse = JsonSerializer.Deserialize<SearchInstantResponse>(responseContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return nutritionixResponse;
        }

        public async Task<NutritionixFood> GetNutritionByItemIdAsync(string nixItemId)
        {
            try
            {
                // Use the item ID to get detailed nutrition information
                var queryParams = new Dictionary<string, string>
                {
                    { NutritionixConstants.NixItemIdParam, nixItemId }
                };

                var response = await GetAsync(_settings.SearchItemPath, queryParams);
                
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
                var response = await PostAsync(_settings.NaturalNutrientsPath, data);
                
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