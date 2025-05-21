using System.Collections.Generic;
using System.Linq;
using NutritionAmbition.Backend.API.Models;
using Microsoft.Extensions.Logging;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionTotals
    {
        public double TotalCalories { get; set; }
        public double TotalProtein { get; set; }
        public double TotalCarbohydrates { get; set; }
        public double TotalFat { get; set; }
        public Dictionary<string, double> TotalMicronutrients { get; set; } = new Dictionary<string, double>();
    }

    public interface INutritionCalculationService
    {
        IEnumerable<FoodItem> FlattenEntries(IEnumerable<FoodEntry> entries);
        NutritionTotals CalculateTotals(IEnumerable<FoodItem> items);
    }

    public class NutritionCalculationService : INutritionCalculationService
    {
        private readonly ILogger<NutritionCalculationService> _logger;

        public NutritionCalculationService(ILogger<NutritionCalculationService> logger = null)
        {
            _logger = logger;
        }
        
        public IEnumerable<FoodItem> FlattenEntries(IEnumerable<FoodEntry> entries)
        {
            return entries
                .SelectMany(e => e.GroupedItems.SelectMany(g => g.Items))
                .ToList();
        }

        public NutritionTotals CalculateTotals(IEnumerable<FoodItem> items)
        {
            
            // Values are already scaled, do not multiply by item.OriginalScaledQuantity
            var totals = new NutritionTotals
            {
                TotalCalories = items.Sum(i => i.Calories),         // Previously: i.Calories * i.OriginalScaledQuantity
                TotalProtein = items.Sum(i => i.Protein),           // Previously: i.Protein * i.OriginalScaledQuantity
                TotalCarbohydrates = items.Sum(i => i.Carbohydrates), // Previously: i.Carbohydrates * i.OriginalScaledQuantity
                TotalFat = items.Sum(i => i.Fat),                   // Previously: i.Fat * i.OriginalScaledQuantity
                TotalMicronutrients = AggregateMicronutrients(items)
            };
            
            
            return totals;
        }

        private Dictionary<string, double> AggregateMicronutrients(IEnumerable<FoodItem> items)
        {
            var micronutrients = new Dictionary<string, double>();
            
            foreach (var item in items)
            {
                foreach (var kvp in item.Micronutrients)
                {
                    // Values are already scaled, do not multiply by item.OriginalScaledQuantity
                    if (micronutrients.ContainsKey(kvp.Key))
                        micronutrients[kvp.Key] += kvp.Value; // Previously: kvp.Value * item.OriginalScaledQuantity
                    else
                        micronutrients[kvp.Key] = kvp.Value;  // Previously: kvp.Value * item.OriginalScaledQuantity
                }
            }
            
            return micronutrients;
        }
    }
} 