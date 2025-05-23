namespace NutritionAmbition.Backend.API.Constants
{
    /// <summary>
    /// Constants for OpenAI model names, response keys, and related configuration values
    /// </summary>
    public static class OpenAiConstants
    {
        #region Model Names and Roles

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

        #endregion

        #region LiteralTypes
        
        public const string FunctionCallType = "function_call";
        public const string FunctionCallOutputType = "function_call_output";
        public const string ToolRole = "tool";
        public const string AssistantRoleLiteral = "assistant";
        public const string UserRoleLiteral = "user";
        public const string SystemRoleLiteral = "system";
        public const string ContextNoteLiteral = "context";
        
        #endregion

        #region Model Names

        /// <summary>
        /// Default GPT-4o model name
        /// </summary>
        public const string ModelGpt4o = "gpt-4o";

        /// <summary>
        /// GPT-4o-mini model name for lighter tasks
        /// </summary>
        public const string ModelGpt4oMini = "gpt-4o-mini";

        #endregion

        #region Response Keys

        /// <summary>
        /// Output key in OpenAI responses
        /// </summary>
        public const string Output = "output";

        /// <summary>
        /// ID key in OpenAI responses
        /// </summary>
        public const string Id = "id";

        /// <summary>
        /// Type key in OpenAI responses
        /// </summary>
        public const string Type = "type";

        /// <summary>
        /// Message key in OpenAI responses
        /// </summary>
        public const string Message = "message";

        /// <summary>
        /// Content key in OpenAI responses
        /// </summary>
        public const string Content = "content";

        /// <summary>
        /// Text key in OpenAI responses
        /// </summary>
        public const string Text = "text";

        /// <summary>
        /// Function call key in OpenAI responses
        /// </summary>
        public const string FunctionCall = "function_call";

        /// <summary>
        /// Arguments key in OpenAI responses
        /// </summary>
        public const string Arguments = "arguments";

        /// <summary>
        /// Name key in OpenAI responses
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// Call ID key in OpenAI responses
        /// </summary>
        public const string CallId = "call_id";

        #endregion
    }
} 