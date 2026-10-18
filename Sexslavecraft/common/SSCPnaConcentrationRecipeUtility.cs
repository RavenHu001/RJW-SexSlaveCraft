// AI 辅助生成
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SSCPnaConcentrationBootstrap
    {
        static SSCPnaConcentrationBootstrap()
        {
            // 等待其他定义和启动期配方注入完成，再读取合并后的工作台配方。
            LongEventHandler.ExecuteWhenFinished(() => SSCPnaConcentrationRecipeUtility.RegisterCookingTables());
        }
    }

    public static class SSCPnaConcentrationRecipeUtility
    {
        public static int RegisterCookingTables()
        {
            return AddToCookingTables(
                DefDatabase<ThingDef>.AllDefsListForReading,
                DefDatabase<RecipeDef>.GetNamedSilentFail("SSC_Make_SuperDrug_Rough"),
                DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimple"),
                DefDatabase<RecipeDef>.GetNamedSilentFail("CookMealSimpleBulk"));
        }

        // 用实际烹饪能力识别模组灶台；不依赖建筑名称、翻译或特定模组包名。
        public static int AddToCookingTables(IEnumerable<ThingDef> tables, RecipeDef rough,
            RecipeDef simpleMeal, RecipeDef bulkSimpleMeal)
        {
            if (tables == null || rough == null || (simpleMeal == null && bulkSimpleMeal == null))
                return 0;

            int added = 0;
            foreach (ThingDef table in tables)
            {
                if (table?.thingClass == null || !typeof(Building_WorkTable).IsAssignableFrom(table.thingClass))
                    continue;

                List<RecipeDef> recipes = table.AllRecipes;
                if (recipes == null || recipes.Contains(rough) ||
                    !((simpleMeal != null && recipes.Contains(simpleMeal)) ||
                      (bulkSimpleMeal != null && recipes.Contains(bulkSimpleMeal))))
                    continue;

                if (rough.recipeUsers == null) rough.recipeUsers = new List<ThingDef>();
                if (!rough.recipeUsers.Contains(table)) rough.recipeUsers.Add(table);

                // AllRecipes 已在读取时生成缓存，追加到同一列表，保留第三方配方及其顺序。
                // recipeUsers 同步保存关联，原版以后重建缓存时仍能包含新配方。
                recipes.Add(rough);
                added++;
            }
            return added;
        }
    }
}
