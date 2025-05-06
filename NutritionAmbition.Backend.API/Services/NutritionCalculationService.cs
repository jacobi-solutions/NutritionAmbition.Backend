using System.Collections.Generic;
using System.Linq;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Services
{
    public class NutritionTotals
    {
        public double TotalCalories { get; set; }
        public double TotalProtein { get; set; }
        public double TotalCarbohydrates { get; set; }
        public double TotalFat { get; set; }
        public double TotalSaturatedFat { get; set; }
        public Dictionary<string, double> TotalMicronutrients { get; set; } = new Dictionary<string, double>();
    }

    public interface INutritionCalculationService
    {
        IEnumerable<FoodItem> FlattenEntries(IEnumerable<FoodEntry> entries);
        NutritionTotals CalculateTotals(IEnumerable<FoodItem> items);
    }

    public class NutritionCalculationService : INutritionCalculationService
    {
        public IEnumerable<FoodItem> FlattenEntries(IEnumerable<FoodEntry> entries)
        {
            return entries
                .SelectMany(e => e.GroupedItems.SelectMany(g => g.Items))
                .ToList();
        }

        public NutritionTotals CalculateTotals(IEnumerable<FoodItem> items)
        {
            var totals = new NutritionTotals
            {
                TotalCalories = items.Sum(i => i.Calories * i.Quantity),
                TotalProtein = items.Sum(i => i.Protein * i.Quantity),
                TotalCarbohydrates = items.Sum(i => i.Carbohydrates * i.Quantity),
                TotalFat = items.Sum(i => i.Fat * i.Quantity),
                TotalSaturatedFat = items.Sum(i => i.SaturatedFat * i.Quantity),
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
                    if (micronutrients.ContainsKey(kvp.Key))
                        micronutrients[kvp.Key] += kvp.Value * item.Quantity;
                    else
                        micronutrients[kvp.Key] = kvp.Value * item.Quantity;
                }
            }
            
            return micronutrients;
        }
    }
} 