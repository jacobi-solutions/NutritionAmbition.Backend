using System.Collections.Generic;
using NutritionAmbition.Backend.API.Constants;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class SubmitToolOutputsRequest : Request
    {
        public BotMessageResponse InitialResponse { get; set; }
        public Dictionary<string, string> ToolOutputs { get; set; }
        public string SystemPrompt { get; set; } = SystemPrompts.DefaultNutritionAssistant;
        public string UserMessage { get; set; } = string.Empty;
    }
} 