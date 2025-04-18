using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.DataContracts.FoodEntries
{
    public class GetFoodEntriesResponse : Response
    {
        public List<FoodEntry> FoodEntries { get; set; }
    }
} 