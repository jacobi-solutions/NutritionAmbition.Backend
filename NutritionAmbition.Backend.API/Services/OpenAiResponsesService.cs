using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Settings;
using OpenAI;
using OpenAI.Chat;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.Services
{
    /// <summary>
    /// Interface for OpenAI Responses API service
    /// </summary>
    public interface IOpenAiResponsesService
    {
        /// <summary>
        /// Runs a conversation using the OpenAI Responses API
        /// </summary>
        /// <param name="accountId">Account ID for tracking</param>
        /// <param name="userMessage">Message from the user</param>
        /// <param name="systemPrompt">System prompt to guide the conversation</param>
        /// <param name="model">Model name (default: gpt-4o)</param>
        /// <param name="tools">Optional list of tools to include in the request</param>
        /// <returns>A response containing the assistant's message</returns>
        Task<BotMessageResponse> RunConversationAsync(string accountId, string userMessage, string systemPrompt, string model = "gpt-4o", List<object>? tools = null);
        
        /// <summary>
        /// Submits tool outputs to get a final response from the model
        /// </summary>
        /// <param name="accountId">Account ID for tracking</param>
        /// <param name="initialResponse">The initial response containing tool calls</param>
        /// <param name="toolOutputs">Dictionary mapping tool call IDs to their JSON outputs</param>
        /// <param name="systemPrompt">System prompt to guide the conversation</param>
        /// <param name="userMessage">Original user message</param>
        /// <param name="model">Model name (default: gpt-4o)</param>
        /// <returns>A response containing the assistant's final message</returns>
        Task<BotMessageResponse> SubmitToolOutputsAsync(string accountId, BotMessageResponse initialResponse, Dictionary<string, string> toolOutputs, string systemPrompt, string userMessage, string model = "gpt-4o");
    }

    /// <summary>
    /// Implementation of the OpenAI Responses API service
    /// </summary>
    public class OpenAiResponsesService : IOpenAiResponsesService
    {
        private readonly ILogger<OpenAiResponsesService> _logger;
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _openAiSettings;
        private readonly OpenAIClient _chatClient;

        /// <summary>
        /// Initializes a new instance of the OpenAiResponsesService class
        /// </summary>
        /// <param name="logger">Logger for logging operations</param>
        /// <param name="httpClient">HTTP client for API requests</param>
        /// <param name="openAiSettings">OpenAI configuration settings</param>
        /// <param name="chatClient">OpenAI client for chat completions</param>
        public OpenAiResponsesService(
            ILogger<OpenAiResponsesService> logger,
            HttpClient httpClient,
            IOptions<OpenAiSettings> openAiSettings,
            OpenAIClient chatClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _openAiSettings = openAiSettings.Value;
            _chatClient = chatClient;
        }

        /// <summary>
        /// Runs a conversation using the OpenAI Responses API
        /// </summary>
        /// <param name="accountId">Account ID for tracking</param>
        /// <param name="userMessage">Message from the user</param>
        /// <param name="systemPrompt">System prompt to guide the conversation</param>
        /// <param name="model">Model name (default: gpt-4o)</param>
        /// <param name="tools">Optional list of tools to include in the request</param>
        /// <returns>A response containing the assistant's message</returns>
        public async Task<BotMessageResponse> RunConversationAsync(
            string accountId, 
            string userMessage, 
            string systemPrompt, 
            string model = "gpt-4o",
            List<object>? tools = null)
        {
            var response = new BotMessageResponse
            {
                AccountId = accountId,
                IsSuccess = true
            };

            try
            {
                _logger.LogInformation("Running conversation with OpenAI Responses API for accountId: {AccountId}", accountId);

                // Validate tools parameter if provided
                if (tools != null && tools.Count > 0)
                {
                    _logger.LogInformation("Including {ToolCount} tools in the request", tools.Count);
                    
                    // Validate tool definitions
                    foreach (var tool in tools)
                    {
                        if (tool is JsonElement jsonElement)
                        {
                            if (!jsonElement.TryGetProperty(OpenAiResponseKeys.Type, out var typeElement) || 
                                typeElement.GetString() != "function")
                            {
                                _logger.LogWarning("Tool definition missing or invalid 'type' property. Expected 'function'.");
                            }
                            
                            if (!jsonElement.TryGetProperty("function", out var functionElement))
                            {
                                _logger.LogWarning("Tool definition missing required 'function' property.");
                                continue;
                            }
                            
                            if (!functionElement.TryGetProperty(OpenAiResponseKeys.Name, out var nameElement) || 
                                string.IsNullOrEmpty(nameElement.GetString()))
                            {
                                _logger.LogWarning("Tool function missing required 'name' property.");
                            }
                            
                            if (!functionElement.TryGetProperty("parameters", out var parametersElement))
                            {
                                _logger.LogWarning("Tool function missing required 'parameters' property.");
                            }
                            else
                            {
                                if (!parametersElement.TryGetProperty(OpenAiResponseKeys.Type, out var paramTypeElement) || 
                                    paramTypeElement.GetString() != "object")
                                {
                                    _logger.LogWarning("Tool function parameters missing or invalid 'type' property. Expected 'object'.");
                                }
                                
                                if (!parametersElement.TryGetProperty("properties", out _))
                                {
                                    _logger.LogWarning("Tool function parameters missing required 'properties' object.");
                                }
                            }
                        }
                        else
                        {
                            // For non-JsonElement objects, we can't do much validation
                            // without knowing the specific schema/type
                            _logger.LogInformation("Unable to validate tool definition format for non-JsonElement object.");
                        }
                    }
                }

                // Build the request payload
                object requestData;
                
                if (tools != null && tools.Count > 0)
                {
                    requestData = new
                    {
                        model = model,
                        input = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userMessage }
                        },
                        tools = tools
                    };
                }
                else
                {
                    requestData = new
                    {
                        model = model,
                        input = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userMessage }
                        }
                    };
                }

                // Serialize the payload
                var jsonRequest = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Configure the HTTP client
                var apiUrl = $"{_openAiSettings.ApiBaseUrl.TrimEnd('/')}/{_openAiSettings.ResponsesEndpoint.TrimStart('/')}";

                // Create the request message
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);
                request.Content = content;

                // Send the request and get the response
                var httpResponse = await _httpClient.SendAsync(request);
                
                // Read the response content as string
                var responseContent = await httpResponse.Content.ReadAsStringAsync();

                // Log the response status code and content for debugging
                _logger.LogDebug("OpenAI Responses API response status: {StatusCode}", httpResponse.StatusCode);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Responses API error: {StatusCode}, Details: {ErrorDetails}", 
                        httpResponse.StatusCode, responseContent);
                    response.AddError($"OpenAI API error: {httpResponse.StatusCode}");
                    return response;
                }

                // Parse the response
                try
                {
                    var jsonDoc = JsonDocument.Parse(responseContent);
                    var root = jsonDoc.RootElement;

                    // Get response ID
                    if (root.TryGetProperty(OpenAiResponseKeys.Id, out var idElement))
                    {
                        response.ResponseId = idElement.GetString();
                    }

                    // Parse output array
                    if (root.TryGetProperty(OpenAiResponseKeys.Output, out var outputArray) && outputArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var outputItem in outputArray.EnumerateArray())
                        {
                            if (outputItem.TryGetProperty(OpenAiResponseKeys.Type, out var typeProp))
                            {
                                var type = typeProp.GetString();
                                switch (type)
                                {
                                    case OpenAiResponseKeys.Message:
                                        var contentArray = outputItem.GetProperty(OpenAiResponseKeys.Content);
                                        var textElement = contentArray.EnumerateArray()
                                            .FirstOrDefault(c => c.TryGetProperty(OpenAiResponseKeys.Text, out _));
                                        if (textElement.ValueKind == JsonValueKind.Object &&
                                            textElement.TryGetProperty(OpenAiResponseKeys.Text, out var textContent))
                                        {
                                            response.Message = textContent.GetString();
                                        }
                                        break;

                                    case OpenAiResponseKeys.FunctionCall:
                                        var functionName = outputItem.GetProperty(OpenAiResponseKeys.Name).GetString();
                                        var argumentsRaw = outputItem.GetProperty(OpenAiResponseKeys.Arguments).GetString();
                                        var callId = outputItem.TryGetProperty(OpenAiResponseKeys.CallId, out var callIdProp) ? callIdProp.GetString() : "";

                                        response.ToolCalls.Add(new ToolCall
                                        {
                                            Id = callId ?? "",
                                            Type = "function",
                                            Function = new ToolFunctionCall
                                            {
                                                Name = functionName ?? "",
                                                ArgumentsJson = argumentsRaw ?? "{}"
                                            }
                                        });
                                        break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(response.Message) && response.ToolCalls.Count == 0)
                    {
                        _logger.LogWarning("No valid content found in OpenAI Responses API response: {ResponseContent}", responseContent);
                        response.AddError("No valid content found in OpenAI response");
                    }
                    else
                    {
                        _logger.LogInformation("Parsed response: message={Message}, toolCalls={ToolCallCount}", response.Message, response.ToolCalls.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing OpenAI Responses API response: {ResponseContent}", responseContent);
                    response.AddError("Failed to parse OpenAI response");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI Responses API");
                response.AddError("An error occurred while communicating with OpenAI");
                response.CaptureException(ex);
            }

            return response;
        }

        /// <summary>
        /// Submits tool outputs to get a final response from the model
        /// </summary>
        /// <param name="accountId">Account ID for tracking</param>
        /// <param name="initialResponse">The initial response containing tool calls</param>
        /// <param name="toolOutputs">Dictionary mapping tool call IDs to their JSON outputs</param>
        /// <param name="systemPrompt">System prompt to guide the conversation</param>
        /// <param name="userMessage">Original user message</param>
        /// <param name="model">Model name (default: gpt-4o)</param>
        /// <returns>A response containing the assistant's final message</returns>
        public async Task<BotMessageResponse> SubmitToolOutputsAsync(
            string accountId, 
            BotMessageResponse initialResponse, 
            Dictionary<string, string> toolOutputs, 
            string systemPrompt, 
            string userMessage, 
            string model = "gpt-4o")
        {
            var response = new BotMessageResponse
            {
                AccountId = accountId,
                IsSuccess = true,
                // Clone any other properties needed from the initial response
                Message = initialResponse.Message
            };

            try
            {
                _logger.LogInformation("Submitting tool outputs to OpenAI for account {AccountId}", accountId);
                
                // Check if there are any tool calls to process
                if (initialResponse.ToolCalls == null || initialResponse.ToolCalls.Count == 0)
                {
                    _logger.LogInformation("No tool calls to submit, returning original response");
                    return initialResponse;
                }
                
                // Check if there are any tool outputs to process
                if (toolOutputs == null || toolOutputs.Count == 0)
                {
                    _logger.LogWarning("No tool outputs provided, returning original response");
                    return initialResponse;
                }

                // Create a new input messages list that will contain all messages including tool calls and outputs
                var inputMessages = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage },
                    new { role = "assistant", content = initialResponse.Message ?? string.Empty }
                };
                
                // Add each tool call as a message object with type=function_call
                foreach (var toolCall in initialResponse.ToolCalls)
                {
                    // Add the tool call
                    inputMessages.Add(new
                    {
                        type = "function_call",
                        call_id = toolCall.Id,
                        name = toolCall.Function.Name,
                        arguments = toolCall.Function.ArgumentsJson
                    });
                    
                    // Add the corresponding tool output
                    if (toolOutputs.TryGetValue(toolCall.Id, out var toolOutput))
                    {
                        inputMessages.Add(new
                        {
                            type = "function_call_output",
                            call_id = toolCall.Id,
                            output = toolOutput
                        });
                    }
                    else
                    {
                        _logger.LogWarning("No output provided for tool call {ToolCallId}", toolCall.Id);
                        inputMessages.Add(new
                        {
                            type = "function_call_output",
                            call_id = toolCall.Id,
                            output = "{}"
                        });
                    }
                }
                
                // Construct the raw JSON payload for the Responses API with tool outputs embedded in the input list
                Dictionary<string, object?> requestData = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["input"] = inputMessages
                };
                
                if (!string.IsNullOrEmpty(initialResponse.ResponseId))
                {
                    _logger.LogInformation("Including previous_response_id: {ResponseId} in the request", initialResponse.ResponseId);
                    requestData["previous_response_id"] = initialResponse.ResponseId;
                }
                
                // Serialize the payload
                var jsonRequest = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                // Configure the HTTP client
                var apiUrl = $"{_openAiSettings.ApiBaseUrl.TrimEnd('/')}/{_openAiSettings.ResponsesEndpoint.TrimStart('/')}";
                
                // Create the request message
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);
                request.Content = content;
                
                _logger.LogInformation("Sending tool outputs submission request to OpenAI Responses API");
                
                // Send the request and get the response
                var httpResponse = await _httpClient.SendAsync(request);
                
                // Read the response content as string
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                
                // Log the response status code
                _logger.LogDebug("OpenAI Responses API response status: {StatusCode}", httpResponse.StatusCode);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Responses API error: {StatusCode}, Details: {ErrorDetails}", 
                        httpResponse.StatusCode, responseContent);
                    response.AddError($"OpenAI API error: {httpResponse.StatusCode}");
                    return response;
                }
                
                // Parse the response
                try
                {
                    var responseObject = JsonSerializer.Deserialize<OpenAiResponsesApiResponse>(responseContent);
                    if (responseObject?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text != null)
                    {
                        _logger.LogInformation("Received final response after tool output submission with ID: {ResponseId}", responseObject.Id);
                        response.Message = responseObject.Output.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? string.Empty;
                        response.ResponseId = responseObject.Id;
                    }
                    else
                    {
                        _logger.LogWarning("No valid content found in OpenAI Responses API response after tool submission");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing OpenAI Responses API response: {ResponseContent}", responseContent);
                    response.AddError("Failed to parse OpenAI response after tool output submission");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting tool outputs to OpenAI");
                response.AddError("An error occurred while submitting tool outputs to OpenAI");
                response.CaptureException(ex);
            }

            return response;
        }

        /// <summary>
        /// Submits tool outputs back to the model to get a final response
        /// </summary>
        /// <param name="response">The original response containing tool calls</param>
        /// <param name="systemPrompt">The system prompt used in the original request</param>
        /// <param name="userMessage">The user message from the original request</param>
        /// <param name="model">The model to use for the follow-up request</param>
        /// <returns>A task representing the asynchronous operation</returns>
        private async Task SubmitToolOutputsAsync(
            BotMessageResponse response, 
            string systemPrompt, 
            string userMessage, 
            string model)
        {
            try
            {
                _logger.LogInformation("Submitting tool outputs to OpenAI for final response");
                
                // Check if there are any tool calls to process
                if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                {
                    _logger.LogInformation("No tool calls to submit, skipping submission");
                    return;
                }
                
                // Create the input list that will contain all messages including tool calls and outputs
                var inputMessages = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage },
                    new { role = "assistant", content = response.Message ?? string.Empty }
                };
                
                // Add tool calls and outputs to the input list
                foreach (var toolCall in response.ToolCalls)
                {
                    // Add the tool call
                    inputMessages.Add(new
                    {
                        type = "function_call",
                        call_id = toolCall.Id,
                        name = toolCall.Function.Name,
                        arguments = toolCall.Function.ArgumentsJson
                    });
                    
                    // Tool outputs would be provided by the caller after executing the tools
                    // For now, we use a placeholder
                    string toolOutput = "{}"; // Default empty JSON object as placeholder
                    
                    // In a real implementation, this would come from executing the tool
                    // toolOutput = ExecuteTool(toolCall.Function.Name, toolCall.Function.Arguments);
                    
                    // Add the tool output to the input list
                    inputMessages.Add(new
                    {
                        type = "function_call_output",
                        call_id = toolCall.Id,
                        output = toolOutput
                    });
                }
                
                // Construct the raw JSON payload for the Responses API
                Dictionary<string, object?> requestData = new Dictionary<string, object?>
                {
                    ["model"] = model,
                    ["input"] = inputMessages
                };
                
                if (!string.IsNullOrEmpty(response.ResponseId))
                {
                    _logger.LogInformation("Including previous_response_id: {ResponseId} in the tool outputs submission", response.ResponseId);
                    requestData["previous_response_id"] = response.ResponseId;
                }
                
                // Serialize the payload
                var jsonRequest = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                // Configure the HTTP client
                var apiUrl = string.IsNullOrEmpty(_openAiSettings.ApiBaseUrl) 
                    ? $"https://api.openai.com{_openAiSettings.ResponsesEndpoint}" 
                    : $"{_openAiSettings.ApiBaseUrl.TrimEnd('/')}/{_openAiSettings.ResponsesEndpoint.TrimStart('/')}";
                
                // Create the request message
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiSettings.ApiKey);
                request.Content = content;
                
                _logger.LogInformation("Sending follow-up request to simulate multi-turn tool call cycle under the Responses API");
                
                // Send the request and get the response
                var httpResponse = await _httpClient.SendAsync(request);
                
                // Read the response content as string
                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                
                // Log the response status code
                _logger.LogDebug("OpenAI Responses API response status: {StatusCode}", httpResponse.StatusCode);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("OpenAI Responses API error: {StatusCode}, Details: {ErrorDetails}", 
                        httpResponse.StatusCode, responseContent);
                    response.AddError($"OpenAI API error: {httpResponse.StatusCode}");
                    return;
                }
                
                // Parse the response
                try
                {
                    var responseObject = JsonSerializer.Deserialize<OpenAiResponsesApiResponse>(responseContent);
                    if (responseObject?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text != null)
                    {
                        _logger.LogInformation("Received final response after tool output submission with ID: {ResponseId}", responseObject.Id);
                        response.Message = responseObject.Output.FirstOrDefault()?.Content?.FirstOrDefault()?.Text ?? string.Empty;
                        response.ResponseId = responseObject.Id;
                    }
                    else
                    {
                        _logger.LogWarning("No valid content found in OpenAI Responses API response after tool submission");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing OpenAI Responses API response: {ResponseContent}", responseContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting tool outputs to OpenAI");
                response.AddError("An error occurred while submitting tool outputs to OpenAI");
            }
        }

        #region Response Models

        /// <summary>
        /// Response model for OpenAI Responses API
        /// </summary>
        private class OpenAiResponsesApiResponse
        {
            /// <summary>
            /// Response ID from OpenAI
            /// </summary>
            [JsonPropertyName(OpenAiResponseKeys.Id)]
            public string Id { get; set; } = string.Empty;
            
            /// <summary>
            /// Output from the model
            /// </summary>
            [JsonPropertyName(OpenAiResponseKeys.Output)]
            public List<OpenAiResponseOutput> Output { get; set; } = new();

            /// <summary>
            /// Usage information for the request
            /// </summary>
            public OpenAiResponseUsage Usage { get; set; } = new OpenAiResponseUsage();
        }

        /// <summary>
        /// Output part of the OpenAI Response 
        /// </summary>
        private class OpenAiResponseOutput
        {
            /// <summary>
            /// Role of the response (typically "assistant")
            /// </summary>
            public string Role { get; set; } = string.Empty;

            /// <summary>
            /// Content array containing the response
            /// </summary>
            [JsonPropertyName(OpenAiResponseKeys.Content)]
            public OpenAiResponseContent[] Content { get; set; } = Array.Empty<OpenAiResponseContent>();
            
            /// <summary>
            /// Tool calls returned by the model
            /// </summary>
            public List<ToolCall>? ToolCalls { get; set; }
        }

        /// <summary>
        /// Content structure for OpenAI responses
        /// </summary>
        private class OpenAiResponseContent
        {
            /// <summary>
            /// Type of content (typically "text")
            /// </summary>
            [JsonPropertyName(OpenAiResponseKeys.Type)]
            public string Type { get; set; } = string.Empty;

            /// <summary>
            /// The actual text response
            /// </summary>
            [JsonPropertyName(OpenAiResponseKeys.Text)]
            public string Text { get; set; } = string.Empty;
        }

        /// <summary>
        /// Usage information for the OpenAI Response
        /// </summary>
        private class OpenAiResponseUsage
        {
            /// <summary>
            /// Number of input tokens used
            /// </summary>
            public int InputTokens { get; set; }

            /// <summary>
            /// Number of output tokens used
            /// </summary>
            public int OutputTokens { get; set; }

            /// <summary>
            /// Total tokens used (input + output)
            /// </summary>
            public int TotalTokens { get; set; }
        }

        #endregion
    }
} 