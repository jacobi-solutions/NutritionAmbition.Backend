using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Services
{
    /// <summary>
    /// Interface for the tool definition registry
    /// </summary>
    public interface IToolDefinitionRegistry
    {
        /// <summary>
        /// Gets a tool definition by its name
        /// </summary>
        /// <param name="name">The name of the tool</param>
        /// <returns>The tool definition or null if not found</returns>
        object? GetToolByName(string name);

        /// <summary>
        /// Gets all registered tool definitions
        /// </summary>
        /// <returns>A read-only list of all tool definitions</returns>
        IReadOnlyList<object> GetAll();
    }

    /// <summary>
    /// Registry for tool definitions used in OpenAI function calls
    /// </summary>
    public class ToolDefinitionRegistry : IToolDefinitionRegistry
    {
        private readonly ILogger<ToolDefinitionRegistry> _logger;
        private readonly Dictionary<string, object> _toolDefinitions;

        public ToolDefinitionRegistry(ILogger<ToolDefinitionRegistry> logger)
        {
            _logger = logger;
            _toolDefinitions = new Dictionary<string, object>
            {
                {
                    "LogMealTool",
                    new {
                        type = "function",
                        name = "LogMealTool",
                        description = "Log a user's meal based on a natural language description.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                meal = new {
                                    type = "string",
                                    description = "A description of the user's meal, such as '2 eggs and toast with orange juice'."
                                }
                            },
                            required = new[] { "meal" }
                        }
                    }
                },
                {
                    "SaveUserProfileTool",
                    new {
                        type = "function",
                        name = "SaveUserProfileTool",
                        description = "Save the user's basic profile information including age, sex, height, weight, and activity level. If the user gives you Height and Weight in metric units, convert them to imperial.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                age = new { type = "integer", description = "User's age in years" },
                                sex = new { type = "string", description = "User's biological sex, either 'male' or 'female'" },
                                heightFeet = new { type = "integer", description = "User's height in feet" },
                                heightInches = new { type = "integer", description = "Additional inches beyond the feet" },
                                weightLbs = new { type = "number", description = "User's weight in pounds" },
                                activityLevel = new { type = "string", description = "User's activity level: sedentary, light, moderate, active, or very active" }
                            },
                            required = new[] { "age", "sex", "heightFeet", "heightInches", "weightLbs", "activityLevel" }
                        }
                    }
                },
                {
                    "GetProfileAndGoalsTool",
                    new {
                        type = "function",
                        name = "GetProfileAndGoalsTool",
                        description = "Fetch the user's current profile and daily nutrient goals. Use this when the user asks about their goals or profile data.",
                        parameters = new {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                },
                {
                    "SetDefaultGoalProfileTool",
                    new {
                        type = "function",
                        name = "SetDefaultGoalProfileTool",
                        description = "Set or update the user's default daily nutrition goals.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                baseCalories = new { type = "number", description = "The user's default daily calorie goal" },
                                nutrientGoals = new {
                                    type = "array",
                                    description = "List of nutrient goals to override the system defaults",
                                    items = new {
                                        type = "object",
                                        properties = new {
                                            nutrientName = new { type = "string", description = "The name of the nutrient" },
                                            unit = new { type = "string", description = "The unit of measurement" },
                                            minValue = new { type = "number", description = "Optional lower bound" },
                                            maxValue = new { type = "number", description = "Optional upper bound" },
                                            percentageOfCalories = new { type = "number", description = "Optional % of total calories" }
                                        },
                                        required = new[] { "nutrientName", "unit" }
                                    }
                                }
                            },
                            required = new string[] { }
                        }
                    }
                },
                {
                    "OverrideDailyGoalsTool",
                    new {
                        type = "function",
                        name = "OverrideDailyGoalsTool",
                        description = "Temporarily override today's nutrition goals.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                newBaseCalories = new { type = "number", description = "Calorie target for today only" },
                                nutrientGoals = new {
                                    type = "array",
                                    items = new {
                                        type = "object",
                                        properties = new {
                                            nutrientName = new { type = "string", description = "Nutrient name" },
                                            unit = new { type = "string", description = "Unit" },
                                            minValue = new { type = "number" },
                                            maxValue = new { type = "number" },
                                            percentageOfCalories = new { type = "number" }
                                        },
                                        required = new[] { "nutrientName", "unit" }
                                    }
                                }
                            },
                            required = new string[] { }
                        }
                    }
                },
                {
                    "GetUserContextTool",
                    new {
                        type = "function",
                        name = "GetUserContextTool",
                        description = "Fetch contextual information about the user. Call this at the beginning of every thread.",
                        parameters = new {
                            type = "object",
                            properties = new { },
                            required = new string[] { }
                        }
                    }
                },
                {
                    "ScoreBrandedFoods",
                    new {
                        type = "function",
                        name = "ScoreBrandedFoods",
                        description = "Score each branded food from 1 (worst) to 10 (best) for how closely it matches the user's description. Return a list of integers.",
                        parameters = new {
                            type = "object",
                            properties = new {
                                scores = new {
                                    type = "array",
                                    items = new { type = "integer", minimum = 1, maximum = 10 },
                                    description = "List of scores corresponding to each branded food item"
                                }
                            },
                            required = new[] { "scores" }
                        }
                    }
                }
            };
        }

        public object? GetToolByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("Attempted to get tool with null or empty name");
                return null;
            }

            if (_toolDefinitions.TryGetValue(name, out var tool))
            {
                _logger.LogDebug("Retrieved tool definition for {ToolName}", name);
                return tool;
            }

            _logger.LogWarning("Tool definition not found for {ToolName}", name);
            return null;
        }

        public IReadOnlyList<object> GetAll()
        {
            _logger.LogDebug("Retrieved all tool definitions, count: {Count}", _toolDefinitions.Count);
            return _toolDefinitions.Values.ToList();
        }
    }
} 