namespace NutritionAmbition.Backend.API.Constants
{
    /// <summary>
    /// Constants for OpenAI model names and related configuration values
    /// </summary>
    public static class OpenAiModelNames
    {

        /// <summary>
        /// Default system prompt role for OpenAI conversations
        /// </summary>
        public const string SystemRole = "system";
        
        /// <summary>
        /// User prompt role for OpenAI conversations
        /// </summary>
        public const string UserRole = "user";
        
        /// <summary>
        /// Assistant prompt role for OpenAI conversations
        /// </summary>
        public const string AssistantRole = "assistant";
        
        /// <summary>
        /// JSON object response format for structured responses
        /// </summary>
        public const string JsonObjectFormat = "json_object";
    }
} 