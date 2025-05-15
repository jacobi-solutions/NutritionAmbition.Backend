namespace NutritionAmbition.Backend.API.Constants
{
    /// <summary>
    /// Constants for OpenAI Assistant tool types
    /// </summary>
    public static class AssistantToolTypes
    {
        public const string LogMealTool = "LogMealTool";
        public const string SaveProfileAndGoalsTool = "SaveProfileAndGoalsTool";
        public const string GetProfileAndGoalsTool = "GetProfileAndGoalsTool";
        
        /// <summary>
        /// Checks if the provided tool name is a valid assistant tool
        /// </summary>
        /// <param name="toolName">Name of the tool to check</param>
        /// <returns>True if the tool name is valid, false otherwise</returns>
        public static bool IsValid(string toolName)
        {
            return toolName == LogMealTool || 
                   toolName == SaveProfileAndGoalsTool || 
                   toolName == GetProfileAndGoalsTool;
        }
    }
} 