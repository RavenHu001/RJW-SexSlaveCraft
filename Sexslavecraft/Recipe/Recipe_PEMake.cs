using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // =========================================================
    // 1. 配方定义类：你的 XML 控制中心
    // =========================================================
    public class RecipeDef_PSTag : RecipeDef
    {
        // 恢复这三个字段，彻底交由 XML 控制
        public HediffDef hediffToAdd;
        public float severity = 1f;
        public bool filterIfTagExists = true;
        public bool requireBusSpecializationComplete = false;
        public SexSlaveSpecializationType requireSpecializationType = SexSlaveSpecializationType.None;
        public bool requireSpecializationComplete = false;
        public bool requirePersonalityExcretionCompleted = false;
        public List<HediffDef> exclusiveTags;
    }

    /// <summary>训导官终极配方共用的原料判定，供筛料和实际结算同时使用。</summary>
    internal static class TrainerOfficerRecipeUtility
    {
        /// <summary>确认当前配方确实要写入训导官的有效终极标记。</summary>
        internal static bool IsFinalizationRecipe(RecipeDef_PSTag recipe)
        {
            // 缺失的 Def 不能被当作匹配的 null 标签；启动时的 Def 错误另由加载流程报告。
            return recipe?.hediffToAdd != null
                && SSCDefOf.SSC_Hediff_TrainerOfficer_Final != null
                && recipe.hediffToAdd == SSCDefOf.SSC_Hediff_TrainerOfficer_Final;
        }

        /// <summary>仅普通训导官进度完成、且尚无任何终极记录的凝胶可以终极化。</summary>
        internal static bool IsEligibleGel(CompPersonalityStore gel)
        {
            // 普通、有效终极和禁用终极定义均须存在。缺失定义时保守拒绝，
            // 避免制作出无法通过人格恢复正确识别的半成品。
            if (gel == null || SSCDefOf.SSC_Hediff_TrainerOfficer == null
                || SSCDefOf.SSC_Hediff_TrainerOfficer_Final == null
                || SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled == null)
                return false;

            // 当前方向及当前进度才决定能否加工，不能拿其他方向的历史进度代替。
            // 异常浮点值也必须拒绝：NaN 在普通“小于门槛”比较中会漏过。
            float progress = gel.specializationProgress;
            if (gel.specializationType != SexSlaveSpecializationType.TrainerOfficer
                || float.IsNaN(progress) || float.IsInfinity(progress)
                || progress < CompSexSlaveTraining.SpecializationCompletionProgress || progress > 1f)
                return false;

            // 基础标签证明凝胶仍处于普通培养状态；两种终极标签都证明已经加工过。
            // 禁用终极只代表暂时失格，不允许通过再次加工清除既有完成记录。
            return gel.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer)
                && !gel.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final)
                && !gel.HasTag(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
        }
    }

    // =========================================================
    // 2. 配方工作者：负责搬运数据，并动态注入 XML 里定义的 Tag
    // =========================================================
    public class Recipe_PSTagWorker : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            Thing sourceItem = ingredients.FirstOrDefault(x => x.TryGetComp<CompPersonalityStore>() != null);
            if (sourceItem == null) return;

            CompPersonalityStore sourceComp = sourceItem.TryGetComp<CompPersonalityStore>();

            // 这里只负责拒绝生成不合法的产物，不能用来保护已被原版消耗的材料。
            // 正常工作台账单会先经过 Harmony_TrainerRecipeCompletion 的消耗前检查；
            // 这里保留复查，保护其他模组直接调用工作者时的输出资格。
            if (recipe is RecipeDef_PSTag finalRecipe
                && TrainerOfficerRecipeUtility.IsFinalizationRecipe(finalRecipe)
                && !TrainerOfficerRecipeUtility.IsEligibleGel(sourceComp)) return;

            ThingDef targetDef = PersonalityGelUtility.GetEditedThingDef(sourceItem.def);

            if (targetDef == null) return;

            Thing newItem = ThingMaker.MakeThing(targetDef);
            CompPersonalityStore targetComp = newItem.TryGetComp<CompPersonalityStore>();

            if (targetComp != null)
            {
                // 继承原数据
                targetComp.CopyFrom(sourceComp);

                // 🔥 【核心注入逻辑】读取 XML 里的 hediffToAdd，动态打入雕像体内！
                if (recipe is RecipeDef_PSTag smartRecipe && smartRecipe.hediffToAdd != null)
                {
                    if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Bus_Final)
                    {
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_Bus);
                    }

                    if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Cow_Final)
                    {
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_Cow);
                    }

                    if (TrainerOfficerRecipeUtility.IsFinalizationRecipe(smartRecipe))
                    {
                        // CopyFrom 已深拷贝当前方向、进度、各方向历史及全部标签。
                        // 只替换本方向的状态标签；普通与两种终极标记不得共存，
                        // 其他方向的培养历史和标签则保持原样。
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer);
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
                    }

                    HediffDef petBaseTag = PetSpecializationUtility.GetBaseHediffForFinal(smartRecipe.hediffToAdd);
                    if (petBaseTag != null)
                    {
                        targetComp.RemoveTag(petBaseTag);
                    }

                    if (smartRecipe.exclusiveTags != null)
                    {
                        foreach (HediffDef exclusiveTag in smartRecipe.exclusiveTags)
                        {
                            if (exclusiveTag != null)
                            {
                                targetComp.RemoveTag(exclusiveTag);
                            }
                        }
                    }

                    // 使用你之前写好的 SetTag 方法
                    targetComp.SetTag(smartRecipe.hediffToAdd, smartRecipe.severity);
                }
            }

            if (!sourceItem.Destroyed) sourceItem.Destroy(DestroyMode.Vanish);
            GenPlace.TryPlaceThing(newItem, billDoer.Position, billDoer.Map, ThingPlaceMode.Near);
        }
    }
}
