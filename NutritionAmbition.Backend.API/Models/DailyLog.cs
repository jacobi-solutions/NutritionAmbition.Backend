using System;
using System.Collections.Generic;
using System.Linq;

namespace NutritionAmbition.Backend.API.Models
{
    public class DailyLog : Model
    {
        public string AccountId { get; set; } // Required
        public DateTime LoggedDateUtc { get; set; } = DateTime.UtcNow.Date;
        public List<FoodEntry> Entries { get; set; } = new List<FoodEntry>();

        public double TotalCalories => Entries.Sum(entry => entry.GroupedItems.Sum(item => item.Items.Sum(i => i.Calories)));
        public double TotalProtein => Entries.Sum(entry => entry.GroupedItems.Sum(item => item.Items.Sum(i => i.Protein)));
        public double TotalCarbs => Entries.Sum(entry => entry.GroupedItems.Sum(item => item.Items.Sum(i => i.Carbohydrates)));
        public double TotalFat => Entries.Sum(entry => entry.GroupedItems.Sum(item => item.Items.Sum(i => i.Fat)));
    }
} 