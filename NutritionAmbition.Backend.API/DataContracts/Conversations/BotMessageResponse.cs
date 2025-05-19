using System.Collections.Generic;
using System.Text.Json;

namespace NutritionAmbition.Backend.API.DataContracts
{
    public class BotMessageResponse : Response
    {
        public string Message { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
        public string? ResponseId { get; set; }
    }

    public class ToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ToolFunctionCall Function { get; set; } = new ToolFunctionCall();
    }

    public class ToolFunctionCall
    {
        public string Name { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = "{}";
    }
} 