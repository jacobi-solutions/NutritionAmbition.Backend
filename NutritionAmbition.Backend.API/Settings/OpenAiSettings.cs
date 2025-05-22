namespace NutritionAmbition.Backend.API.Settings
{
    /// <summary>
    /// Configuration settings for OpenAI API
    /// </summary>
    public class OpenAiSettings
    {
        /// <summary>
        /// API key for OpenAI authentication
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Base URL for OpenAI API calls
        /// </summary>
        public string ApiBaseUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Model to use for chat completions (e.g., gpt-4, gpt-3.5-turbo)
        /// </summary>
        // public string Model { get; set; } = string.Empty;
        
        /// <summary>
        /// ID of the assistant to use for thread runs
        /// </summary>
        public string AssistantId { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint for chat completions API
        /// </summary>
        public string ChatCompletionsEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Endpoint for the OpenAI Responses API
        /// </summary>
        public string ResponsesEndpoint { get; set; } = "/v1/responses";
        
        /// <summary>
        /// Endpoint for submitting tool outputs
        /// </summary>
        public string SubmitToolOutputsEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Temperature for general-purpose completions (higher = more creative)
        /// </summary>
        public float DefaultTemperature { get; set; } = 0.7f;

        /// <summary>
        /// Temperature for more creative, varied responses (e.g., coach feedback)
        /// </summary>
        public float HighTemperature { get; set; } = 0.9f;

        /// <summary>
        /// Temperature for more deterministic completions (lower = more consistent)
        /// </summary>
        public float ZeroTemperature = 0.0f;

        /// <summary>
        /// Temperature for more deterministic completions (lower = more consistent)
        /// </summary>
        public float LowTemperature { get; set; } = 0.2f;
        
        /// <summary>
        /// Temperature for food parsing (needs to be very deterministic)
        /// </summary>
        public float FoodParsingTemperature { get; set; } = 0.1f;
        
        /// <summary>
        /// Temperature for group food items (slightly more creative but still consistent)
        /// </summary>
        public float GroupingTemperature { get; set; } = 0.3f;

        /// <summary>
        /// Default maximum tokens for chat completions
        /// </summary>
        public int DefaultMaxTokens { get; set; } = 800;
        
        /// <summary>
        /// Maximum tokens for coach responses (should be brief)
        /// </summary>
        public int CoachResponseMaxTokens { get; set; } = 60;
        
        /// <summary>
        /// Maximum attempts to poll for run completion
        /// </summary>
        public int MaxPollAttempts { get; set; } = 20;
        
        /// <summary>
        /// Delay between polling attempts in milliseconds
        /// </summary>
        public int PollDelayMs { get; set; } = 1000;
    }
}
