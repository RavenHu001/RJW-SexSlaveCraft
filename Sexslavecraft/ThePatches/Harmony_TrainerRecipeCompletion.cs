using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>在原版结算动作的任何消耗与账单计数之前，复查训导官终极化原料。</summary>
    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct))]
    public static class Harmony_TrainerRecipeCompletion
    {
        /// <summary>保留原版最终结算动作，并在其运行前读取当次任务进行只读预检。</summary>
        public static void Postfix(Toil __result)
        {
            // 工厂返回 Toil 时 actor 尚未绑定，不能在这里读取当前账单。
            // 保留原动作，等这个最终步骤真正开始时再读取当次任务与原料；
            // 加工期间修改凝胶数据或读档恢复工作，都必须经过同一轮检查。
            if (__result == null || __result.initAction == null) return;
            Toil completionToil = __result;
            Action completeRecipe = completionToil.initAction;
            completionToil.initAction = () =>
            {
                Pawn actor = completionToil.actor;
                Job job = actor?.CurJob;

                // 本补丁仅守护训导官终极配方。其他配方沿用原动作，
                // 不改写原料消耗、产物搬运、技能经验或账单的完成行为。
                if (job?.RecipeDef is RecipeDef_PSTag recipe
                    && TrainerOfficerRecipeUtility.IsFinalizationRecipe(recipe)
                    && !HasEligibleSource(job))
                {
                    // RimWorld 1.6 的原动作先 CalculateIngredients，再消耗原料，
                    // 最后通知账单完成；其中 CalculateIngredients 还会销毁半成品。
                    // 必须在整个动作之前终止，而不能只在配方工作者中提前返回，
                    // 否则原料已消失且重复次数已经减少。失败时让引擎正常清理任务。
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                // 通过检查后完整执行原版动作。生产工作者继续负责复制人格数据与
                // 替换终极标签，避免此处另做一套产物生成或账单计数逻辑。
                completeRecipe();
            };
        }

        /// <summary>只读访问原版即将使用的原料容器，不调用会分堆或销毁半成品的计算函数。</summary>
        private static bool HasEligibleSource(Job job)
        {
            // 使用半成品的账单从 Target C 取原料；它优先于 placedThings。
            // 虽然当前训导官 XML 不定义半成品，这里保持与原版选择容器的规则一致，
            // 也保护兼容补丁为该配方启用半成品后的材料。
            if (job.GetTarget(TargetIndex.C).Thing is UnfinishedThing unfinished)
            {
                if (unfinished.Destroyed || unfinished.ingredients == null) return false;
                bool foundSource = false;
                foreach (Thing ingredient in unfinished.ingredients)
                {
                    if (ingredient?.TryGetComp<CompPersonalityStore>() == null) continue;
                    // 该配方只转换一个人格。多份凝胶即使各自合格也不能放行，
                    // 否则原版会消耗所有凝胶，工作者却只复制第一份人格。
                    if (foundSource || ingredient.stackCount != 1 || !IsEligibleSource(ingredient)) return false;
                    foundSource = true;
                }
                return foundSource;
            }

            // 普通账单只会结算 placedThings 中计数为正的原料。选择第一个人格组件，
            // 与 Recipe_PSTagWorker 的实际输入选择一致；已经用完的记录不能冒充原料。
            // 找到来源后仍检查余下条目，防止异常集合夹带另一个会被一起消耗的人格。
            if (job.placedThings == null) return false;
            bool foundPlacedSource = false;
            foreach (ThingCountClass ingredient in job.placedThings)
            {
                if (ingredient == null || ingredient.Count <= 0
                    || ingredient.thing?.TryGetComp<CompPersonalityStore>() == null) continue;
                // 人格凝胶本身也必须只有一份：若外部逻辑把它堆叠，原版会先
                // SplitOff，且人格组件没有分堆复制钩子，不能保证新分出的原料带人格。
                if (foundPlacedSource || ingredient.Count != 1 || ingredient.thing.stackCount != 1
                    || !IsEligibleSource(ingredient.thing)) return false;
                foundPlacedSource = true;
            }
            return foundPlacedSource;
        }

        /// <summary>资格有效且能够映射到加工产物时，才允许原版开始不可逆的消耗。</summary>
        private static bool IsEligibleSource(Thing ingredient)
        {
            // 消耗后工作者仍可读取组件，所以销毁检查必须放在这里的消耗前边界。
            // 同时预检产物定义，避免无法转换的外部凝胶被吞掉却没有任何产物。
            return !ingredient.Destroyed
                && TrainerOfficerRecipeUtility.IsEligibleGel(ingredient.TryGetComp<CompPersonalityStore>())
                && PersonalityGelUtility.GetEditedThingDef(ingredient.def) != null;
        }
    }
}
