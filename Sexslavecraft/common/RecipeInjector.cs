using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

// EN: This bootstrap injects SSC surgeries into every humanlike race, RJW-style.
// EN: It expands the recipeUsers list at startup so personality excretion, gelatinization, and related surgeries are available on all humanlike pawns.
// CN: 这个启动器会用 RJW 风格把 SSC 手术注入到所有人形种族上。
// CN: 它会在启动时扩展 recipeUsers 列表，让人格排泄、胶化等手术可以出现在所有人形 Pawn 身上。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class RecipeInjector
    {
        static RecipeInjector()
        {
            SSCLog.Important("[SSC] 开始执行 RJW 风格的手术注入...");

            // EN: Step 1: collect every SSC surgery that should be exposed to all humanlike races.
            // CN: 步骤 1：整理出所有应该对全部人形种族开放的 SSC 手术。
            List<RecipeDef> myRecipes = new List<RecipeDef>
            {
                SSCDefOf.SSC_InducePersonalityExcretion,
                SSCDefOf.SSC_Surgery_GenderChange_MtF,
                SSCDefOf.SSC_ApplyGelatinization,
                SSCDefOf.SSC_ApplyFullGelatinization
            };

            // EN: Step 2: visit each SSC surgery and expand its recipeUsers white list.
            // CN: 步骤 2：遍历每一个 SSC 手术，并扩展它的 recipeUsers 白名单。
            foreach (RecipeDef recipe in myRecipes)
            {
                if (recipe == null)
                {
                    SSCLog.Error("[SSC] 无法找到手术 Def，跳过。");
                    continue;
                }

                // EN: Step 3: make sure recipeUsers exists before injection, even if an XML edit removed it.
                // CN: 步骤 3：在注入前确保 recipeUsers 已经初始化，哪怕 XML 被改坏了也能兜住。
                if (recipe.recipeUsers == null)
                {
                    recipe.recipeUsers = new List<ThingDef>();
                }

                // EN: Step 4: scan all ThingDefs and add every humanlike race to the surgery white list.
                // CN: 步骤 4：扫描全部 ThingDef，把所有人形种族都加入这台手术的白名单。
                foreach (ThingDef raceDef in DefDatabase<ThingDef>.AllDefs)
                {
                    // EN: Only humanlike races are valid surgery users for these SSC procedures.
                    // CN: 这些 SSC 手术只应该对白名单中的人形种族开放。
                    if (raceDef.race != null && raceDef.race.Humanlike)
                    {
                        // EN: The RJW-style difference is to add races into the surgery, not surgeries into the race.
                        // CN: 这里的 RJW 风格关键点，是把“种族加进手术”，而不是“把手术塞进种族”。
                        if (!recipe.recipeUsers.Contains(raceDef))
                        {
                            recipe.recipeUsers.Add(raceDef);
                            // Log.Message($"[SSC] 手术 {recipe.defName} 现在支持种族: {raceDef.defName}");
                        }
                    }
                }
            }

            SSCLog.Important("[SSC] 注入完成 (RJW 模式)。");
        }
    }
}
