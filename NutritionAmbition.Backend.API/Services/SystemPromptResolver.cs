using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NutritionAmbition.Backend.API.Constants;
using NutritionAmbition.Backend.API.DataContracts;

namespace NutritionAmbition.Backend.API.Services
{
    /// <summary>
    /// Interface for resolving system prompts based on context
    /// </summary>
    public interface ISystemPromptResolver
    {
        /// <summary>
        /// Gets a context-aware system prompt based on which tools were used in the conversation
        /// </summary>
        /// <param name="toolCalls">The list of tool calls from the conversation</param>
        /// <returns>A context-specific system prompt</returns>
        string GetPromptForToolContext(List<ToolCall>? toolCalls);
    }

    /// <summary>
    /// Service that returns context-aware system prompts for OpenAI conversations
    /// </summary>
    public class SystemPromptResolver : ISystemPromptResolver
    {
        private readonly ILogger<SystemPromptResolver> _logger;

        public SystemPromptResolver(ILogger<SystemPromptResolver> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gets a context-aware system prompt based on which tools were used in the conversation
        /// </summary>
        /// <param name="toolCalls">The list of tool calls from the conversation</param>
        /// <returns>A context-specific system prompt</returns>
        public string GetPromptForToolContext(List<ToolCall>? toolCalls)
        {
            try
            {
                _logger.LogInformation("Resolving system prompt based on tool context");
                
                if (toolCalls == null || !toolCalls.Any())
                {
                    _logger.LogInformation("No tool calls found, using default system prompt");
                    return SystemPrompts.DefaultNutritionAssistant;
                }

                // Examine tool calls to determine context
                foreach (var toolCall in toolCalls)
                {
                    if (string.IsNullOrEmpty(toolCall.Function?.Name))
                    {
                        continue;
                    }

                    string toolName = toolCall.Function.Name;
                    
                    if (toolName == AssistantToolTypes.LogMealTool)
                    {
                        _logger.LogInformation("LogMealTool was used, providing meal logging context prompt");
                        return "The user just logged a meal. Don't repeat macros. Help them reflect positively on their choices.";
                    }
                    
                    if (toolName == AssistantToolTypes.GetSummaryTool)
                    {
                        _logger.LogInformation("GetSummaryTool was used, providing summary context prompt");
                        return "The user just got a summary. Offer helpful advice without repeating exact numbers.";
                    }
                }

                // No specific context found, use default
                _logger.LogInformation("No specific context found in tool calls, using default system prompt");
                return SystemPrompts.DefaultNutritionAssistant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving system prompt for tool context");
                return SystemPrompts.DefaultNutritionAssistant;
            }
        }
    }
} 