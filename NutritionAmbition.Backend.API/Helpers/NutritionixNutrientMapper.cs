using NutritionAmbition.Backend.API.DataContracts;
using NutritionAmbition.Backend.API.Models;
using System.Collections.Generic;

namespace NutritionAmbition.Backend.API.Helpers
{
    /// <summary>
    /// Helper class for mapping nutrients from Nutritionix API responses to FoodItem objects.
    /// </summary>
    public static class NutritionixNutrientMapper
    {
        public static IReadOnlyDictionary<int, (string Name, string Unit)> All => MicroNutrientMap;
        /// <summary>
        /// Attempts to map a Nutritionix nutrient attribute ID to a nutrient name and unit.
        /// Returns null if the attribute ID is not recognized.
        /// </summary>
        /// <param name="attrId">The Nutritionix attribute ID.</param>
        /// <returns>A tuple containing the nutrient name and unit, or null if not recognized.</returns>
        
        private static readonly Dictionary<int, (string Name, string Unit)> MacroNutrientMap = new()
        {
            { 203, ("Protein", "g") },
            { 204, ("Fat", "g") },
            { 205, ("Carbohydrate", "g") },
            { 208, ("Calories", "g") },
        };
        private static readonly Dictionary<int, (string Name, string Unit)> MicroNutrientMap = new()
        {
            { 207, ("Ash", "g") },
            { 209, ("Starch", "g") },
            { 210, ("Sucrose", "g") },
            { 211, ("Glucose (dextrose)", "g") },
            { 212, ("Fructose", "g") },
            { 213, ("Lactose", "g") },
            { 214, ("Maltose", "g") },
            { 221, ("Alcohol, ethyl", "g") },
            { 255, ("Water", "g") },
            { 260, ("Mannitol", "g") },
            { 261, ("Sorbitol", "g") },
            { 262, ("Caffeine", "mg") },
            { 263, ("Theobromine", "mg") },
            { 269, ("Sugars", "g") },
            { 287, ("Galactose", "g") },
            { 290, ("Xylitol", "g") },
            { 291, ("Fiber", "g") },
            { 299, ("Sugar Alcohol", "g") },
            { 301, ("Calcium", "mg") },
            { 303, ("Iron", "mg") },
            { 304, ("Magnesium", "mg") },
            { 305, ("Phosphorus", "mg") },
            { 306, ("Potassium", "mg") },
            { 307, ("Sodium", "mg") },
            { 309, ("Zinc", "mg") },
            { 312, ("Copper", "mg") },
            { 313, ("Fluoride", "mcg") },
            { 315, ("Manganese", "mg") },
            { 317, ("Selenium", "mcg") },
            { 318, ("Vitamin A", "IU") },
            { 319, ("Retinol", "mcg") },
            { 320, ("Vitamin A (RAE)", "mcg") },
            { 321, ("Carotene, beta", "mcg") },
            { 322, ("Carotene, alpha", "mcg") },
            { 323, ("Vitamin E (alpha-tocopherol)", "mg") },
            { 324, ("Vitamin D", "IU") },
            { 325, ("Vitamin D2 (ergocalciferol)", "mcg") },
            { 326, ("Vitamin D3 (cholecalciferol)", "mcg") },
            { 328, ("Vitamin D (D2 + D3)", "mcg") },
            { 334, ("Cryptoxanthin, beta", "mcg") },
            { 337, ("Lycopene", "mcg") },
            { 338, ("Lutein + zeaxanthin", "mcg") },
            { 341, ("Tocopherol, beta", "mg") },
            { 342, ("Tocopherol, gamma", "mg") },
            { 343, ("Tocopherol, delta", "mg") },
            { 344, ("Tocotrienol, alpha", "mg") },
            { 345, ("Tocotrienol, beta", "mg") },
            { 346, ("Tocotrienol, gamma", "mg") },
            { 347, ("Tocotrienol,delta", "mg") },
            { 401, ("Vitamin C", "mg") },
            { 404, ("Vitamin B1", "mg") },
            { 405, ("Vitamin B2", "mg") },
            { 406, ("Vitamin B3", "mg") },
            { 410, ("Vitamin B5", "mg") },
            { 415, ("Vitamin B6", "mg") },
            { 417, ("Vitamin B9", "mcg") },
            { 418, ("Vitamin B12", "mcg") },
            { 421, ("Choline", "mg") },
            { 428, ("Menaquinone-4", "mcg") },
            { 429, ("Dihydrophylloquinone", "mcg") },
            { 430, ("Vitamin K", "mcg") },
            { 431, ("Vitamin B9 (folic acid)", "mcg") },
            { 432, ("Vitamin B9 (food folate)", "mcg") },
            { 435, ("Vitamin B9 (DFE)", "mcg") },
            { 454, ("Betaine", "mg") },
            { 501, ("Tryptophan", "g") },
            { 502, ("Threonine", "g") },
            { 503, ("Isoleucine", "g") },
            { 504, ("Leucine", "g") },
            { 505, ("Lysine", "g") },
            { 506, ("Methionine", "g") },
            { 507, ("Cystine", "g") },
            { 508, ("Phenylalanine", "g") },
            { 509, ("Tyrosine", "g") },
            { 510, ("Valine", "g") },
            { 511, ("Arginine", "g") },
            { 512, ("Histidine", "g") },
            { 513, ("Alanine", "g") },
            { 514, ("Aspartic acid", "g") },
            { 515, ("Glutamic acid", "g") },
            { 516, ("Glycine", "g") },
            { 517, ("Proline", "g") },
            { 518, ("Serine", "g") },
            { 521, ("Hydroxyproline", "g") },
            { 539, ("Added Sugars", "g") },
            { 573, ("Vitamin E (added)", "mg") },
            { 578, ("Vitamin B12 (added)", "mcg") },
            { 601, ("Cholesterol", "mg") },
            { 605, ("Trans Fat", "g") },
            { 606, ("Saturated Fat", "g") },
            { 636, ("Phytosterols", "mg") },
            { 638, ("Stigmasterol", "mg") },
            { 639, ("Campesterol", "mg") },
            { 641, ("Beta-sitosterol", "mg") },
            { 645, ("Monounsaturated Fat", "g") },
            { 646, ("Polyunsaturated Fat", "g") },
            { 693, ("Trans Monoenoic Fat", "g") },
            { 695, ("Trans Polyenoic Fat", "g") },
            { 1001, ("Erythritol", "g") },
            { 1002, ("Glycerin", "g") },
            { 1003, ("Maltitol", "g") },
            { 1004, ("Isomalt", "g") },
            { 1005, ("Lactitol", "g") },
            { 1006, ("Allulose", "g") }
        };

        public static (string Name, string Unit)? TryMapMicroNutrient(int attrId)
        {
            return MicroNutrientMap.TryGetValue(attrId, out var mapped)
                ? mapped
                : null;
        }

        public static string? GetMicroNutrientUnit(string name)
        {
            foreach (var entry in MicroNutrientMap.Values)
            {
                if (string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                    return entry.Unit;
            }

            return null;
        }


        /// <summary>
        /// Maps micronutrients from the Nutritionix food object to the target FoodItem object.
        /// Stores all nutrients in in the Micronutrients dictionary.
        /// </summary>
        /// <param name="source">The source Nutritionix food object.</param>
        /// <param name="target">The target FoodItem object.</param>
        public static void MapMicronutrients(NutritionixFood source, FoodItem target)
        {
            if (source.FullNutrients == null) return;

            foreach (var nutrient in source.FullNutrients)
            {
                var mapped = TryMapMicroNutrient(nutrient.AttrId);

                if (mapped != null)
                {
                    target.Micronutrients[mapped.Value.Name] = nutrient.Value;
                }
            }
        }

       public static void MapMacronutrients(NutritionixFood source, FoodItem target)
        {
            target.Calories = GetMacroValue(source, 208);
            target.Protein = GetMacroValue(source, 203);
            target.Carbohydrates = GetMacroValue(source, 205);
            target.Fat = GetMacroValue(source, 204);
        }

        private static double GetMacroValue(NutritionixFood source, int attrId)
        {
            return source.FullNutrients?.FirstOrDefault(n => n.AttrId == attrId)?.Value ?? 0;
        }
    }
} 