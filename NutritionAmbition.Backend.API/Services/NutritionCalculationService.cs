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
            // Log values before calculation for debugging
            if (_logger != null)
            {
                foreach (var item in items)
                {
                    _logger.LogInformation("[DOUBLE_SCALE_CHECK] CalculateTotals item: {Name}, Protein={Protein}, OriginalScaledQuantity={OriginalQty}, OriginalScaledQuantity={Qty}",
                        item.Name, item.Protein, item.Quantity, item.Quantity);
                    
                    // If OriginalScaledQuantity is not 1, log a warning
                    if (item.Quantity != 1)
                    {
                        _logger.LogWarning("[DOUBLE_SCALE_CHECK] Item {Name} has OriginalScaledQuantity={OriginalScaledQuantity}, expected 1 for pre-scaled values",
                            item.Name, item.Quantity);
                    }
                }
            }
            
            // Values are already scaled, do not multiply by item.OriginalScaledQuantity
            var totals = new NutritionTotals
            {
                TotalCalories = items.Sum(i => i.Calories),         // Previously: i.Calories * i.OriginalScaledQuantity
                TotalProtein = items.Sum(i => i.Protein),           // Previously: i.Protein * i.OriginalScaledQuantity
                TotalCarbohydrates = items.Sum(i => i.Carbohydrates), // Previously: i.Carbohydrates * i.OriginalScaledQuantity
                TotalFat = items.Sum(i => i.Fat),                   // Previously: i.Fat * i.OriginalScaledQuantity
                TotalSaturatedFat = items.Sum(i => i.SaturatedFat), // Previously: i.SaturatedFat * i.OriginalScaledQuantity
                TotalMicronutrients = AggregateMicronutrients(items)
            };
            
            // Log the calculated totals
            if (_logger != null)
            {
                _logger.LogInformation("[DOUBLE_SCALE_CHECK] CalculateTotals results: TotalProtein={TotalProtein}, TotalCarbs={TotalCarbs}, TotalFat={TotalFat}",
                    totals.TotalProtein, totals.TotalCarbohydrates, totals.TotalFat);
            }
            
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