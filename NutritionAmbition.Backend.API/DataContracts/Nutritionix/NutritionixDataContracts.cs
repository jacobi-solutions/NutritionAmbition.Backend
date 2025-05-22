using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NutritionAmbition.Backend.API.DataContracts
{

    // public class BrandedFoodItem
    // {
    //     [JsonPropertyName("food_name")]
    //     public string FoodName { get; set; } = string.Empty;

    //     [JsonPropertyName("brand_name")]
    //     public string BrandName { get; set; } = string.Empty;

    //     [JsonPropertyName("nix_item_id")]
    //     public string NixItemId { get; set; } = string.Empty;

    //     [JsonPropertyName("nf_calories")]
    //     public double? Calories { get; set; }

    //     [JsonPropertyName("photo")]
    //     public NutritionixPhoto? Photo { get; set; }

    //     [JsonPropertyName("nix_brand_id")]
    //     public string? NixBrandId { get; set; }
        
    //     [JsonPropertyName("serving_qty")]
    //     public double ServingQty { get; set; }

    //     [JsonPropertyName("serving_unit")]
    //     public string ServingUnit { get; set; } = string.Empty;
    // }

    // public class CommonFoodItem 
    // {
    //     [JsonPropertyName("food_name")]
    //     public string FoodName { get; set; } = string.Empty;

    //     [JsonPropertyName("tag_id")]
    //     public string? TagId { get; set; }

    //     [JsonPropertyName("photo")]
    //     public NutritionixPhoto? Photo { get; set; }

    //     [JsonPropertyName("serving_unit")]
    //     public string? ServingUnit { get; set; }

    //     [JsonPropertyName("tag_name")]
    //     public string? TagName { get; set; }
    // }

    public class NutritionixFood
    {
        // for branded items
        [JsonPropertyName("nix_item_id")]
        public string? BrandedItemId { get; set; } = string.Empty;

        // for common items
        [JsonPropertyName("tag_id")]
        public string? CommonTagId { get; set; } = string.Empty;

        [JsonIgnore]
        public string? NixFoodId => !string.IsNullOrEmpty(BrandedItemId) ? BrandedItemId : CommonTagId;
    
        [JsonPropertyName("food_name")]
        public string FoodName { get; set; } = string.Empty;

        [JsonPropertyName("brand_name")]
        public string? BrandName { get; set; }

        [JsonPropertyName("nix_brand_id")]
        public string? NixBrandId { get; set; }

        [JsonPropertyName("serving_qty")]
        public double ServingQty { get; set; }

        [JsonPropertyName("serving_unit")]
        public string ServingUnit { get; set; } = string.Empty;

        [JsonPropertyName("serving_weight_grams")]
        public double? ServingWeightGrams { get; set; }

        [JsonPropertyName("nf_calories")]
        public double? Calories { get; set; }

        [JsonPropertyName("nf_total_fat")]
        public double? TotalFat { get; set; }

        [JsonPropertyName("nf_saturated_fat")]
        public double? SaturatedFat { get; set; }

        [JsonPropertyName("nf_cholesterol")]
        public double? Cholesterol { get; set; }

        [JsonPropertyName("nf_sodium")]
        public double? Sodium { get; set; }

        [JsonPropertyName("nf_total_carbohydrate")]
        public double? TotalCarbohydrate { get; set; }

        [JsonPropertyName("nf_dietary_fiber")]
        public double? DietaryFiber { get; set; }

        [JsonPropertyName("nf_sugars")]
        public double? Sugars { get; set; }

        [JsonPropertyName("nf_protein")]
        public double? Protein { get; set; }

        [JsonPropertyName("nf_potassium")]
        public double? Potassium { get; set; }

        [JsonPropertyName("nf_p")]
        public double? Phosphorus { get; set; }

        [JsonPropertyName("full_nutrients")]
        public List<NutritionixNutrient> FullNutrients { get; set; } = new List<NutritionixNutrient>();

        [JsonPropertyName("photo")]
        public NutritionixPhoto? Photo { get; set; }
    }

    public class NutritionixNutrient
    {
        [JsonPropertyName("attr_id")]
        public int AttrId { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    public class NutritionixPhoto
    {
        [JsonPropertyName("thumb")]
        public string? Thumb { get; set; }

        [JsonPropertyName("highres")]
        public string? HighRes { get; set; }

        [JsonPropertyName("is_user_uploaded")]
        public bool IsUserUploaded { get; set; }
    }
}
