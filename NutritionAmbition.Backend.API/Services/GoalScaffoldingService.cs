using System;
using System.Collections.Generic;
using NutritionAmbition.Backend.API.Models;

namespace NutritionAmbition.Backend.API.Services
{
    public interface IGoalScaffoldingService
    {
        List<NutrientGoal> GenerateNutrientGoals(double calories);
    }


    public class GoalScaffoldingService : IGoalScaffoldingService
    {
        public List<NutrientGoal> GenerateNutrientGoals(double calories)
        {
            return new List<NutrientGoal>
            {
                new NutrientGoal { NutrientName = "Protein", MinValue = Math.Round(calories * 0.32 / 4), PercentageOfCalories = 0.32, Unit = "g" },
                new NutrientGoal { NutrientName = "Fat", MinValue = Math.Round(calories * 0.30 / 9), PercentageOfCalories = 0.30, Unit = "g" },
                new NutrientGoal { NutrientName = "Carbohydrates", MinValue = Math.Round(calories * 0.35 / 4), PercentageOfCalories = 0.35, Unit = "g" },
                new NutrientGoal { NutrientName = "Saturated Fat", MaxValue = Math.Round(calories * 0.06 / 9), PercentageOfCalories = 0.06, Unit = "g" },
                new NutrientGoal { NutrientName = "Cholesterol", MaxValue = 300, Unit = "mg" },
                new NutrientGoal { NutrientName = "Total Fiber", MinValue = 25, Unit = "g" },
                new NutrientGoal { NutrientName = "Soluble Fiber", MinValue = 10, Unit = "g" },
                new NutrientGoal { NutrientName = "Added Sugar", MaxValue = Math.Round(calories * 0.10 / 4), PercentageOfCalories = 0.10, Unit = "g" },
                new NutrientGoal { NutrientName = "Magnesium", MinValue = 420, Unit = "mg" },
                new NutrientGoal { NutrientName = "Iron", MinValue = 18, Unit = "mg" },
                new NutrientGoal { NutrientName = "Zinc", MinValue = 11, Unit = "mg" },
                new NutrientGoal { NutrientName = "Vitamin D", MinValue = 1000, Unit = "IU" },
                new NutrientGoal { NutrientName = "Vitamin B12", MinValue = 2.4, Unit = "mcg" },
                new NutrientGoal { NutrientName = "Choline", MinValue = 550, Unit = "mg" }
            };
        }
    }
} 