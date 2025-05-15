using System.Text.Json.Serialization;
namespace NutritionAmbition.Backend.API.Models
{
    // Define MealType enum
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MealType
    {
        Unknown,
        Breakfast,
        Lunch,
        Dinner,
        Snack
    }
} 

