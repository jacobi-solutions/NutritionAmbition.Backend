using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Models
{
    public class ThreadMessage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("object")]
        public string Object { get; set; } = string.Empty;
        
        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }
        
        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;
        
        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;
        
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;
        
        [JsonPropertyName("content")]
        public List<MessageContent> Content { get; set; } = new List<MessageContent>();
    }

    public class MessageContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("text")]
        public TextContent Text { get; set; }
    }

    public class TextContent
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public class ToolOutput
    {
        [JsonPropertyName("tool_call_id")]
        public string ToolCallId { get; set; } = string.Empty;
        
        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }

    public class RunRequiredAction
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("submit_tool_outputs")]
        public SubmitToolOutputs SubmitToolOutputs { get; set; }
    }

    public class SubmitToolOutputs
    {
        [JsonPropertyName("tool_calls")]
        public List<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();
    }
} 