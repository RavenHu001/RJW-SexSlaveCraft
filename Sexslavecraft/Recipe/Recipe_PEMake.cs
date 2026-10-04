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

    /// <summary>战斗员终极化要求当前普通培养完成；不接受其他方向历史或已有终极记录。</summary>
    internal static class CombatantRecipeUtility
    {
        internal static bool IsFinalizationRecipe(RecipeDef_PSTag recipe)
        {
            return recipe?.hediffToAdd != null && SSCDefOf.SSC_Hediff_Combatant_Final != null &&
                recipe.hediffToAdd == SSCDefOf.SSC_Hediff_Combatant_Final;
        }

        internal static bool IsEligibleGel(CompPersonalityStore gel)
        {
            if (gel == null || SSCDefOf.SSC_Hediff_Combatant == null ||
                SSCDefOf.SSC_Hediff_Combatant_Final == null) return false;
            float progress = gel.specializationProgress;
            return gel.specializationType == SexSlaveSpecializationType.Combatant &&
                !float.IsNaN(progress) && !float.IsInfinity(progress) &&
                progress >= CompSexSlaveTraining.SpecializationCompletionProgress && progress <= 1f &&
                gel.HasTag(SSCDefOf.SSC_Hediff_Combatant) && !gel.HasTag(SSCDefOf.SSC_Hediff_Combatant_Final);
        }
    }

    /// <summary>筛料、消耗前检查和产物复查共用资格分派；其他配方保留原流程。</summary>
    internal static class SpecializationFinalizationRecipeUtility
    {
        internal static bool IsProtectedRecipe(RecipeDef_PSTag recipe)
        {
            // 复用现有最终结算守卫，把宠物终极化纳入其保护范围，
            // 无需再安装第二个结算补丁；其他配方仍执行原版完成动作。
            return TrainerOfficerRecipeUtility.IsFinalizationRecipe(recipe) || CombatantRecipeUtility.IsFinalizationRecipe(recipe)
                || PetFinalizationRecipeUtility.IsFinalizationRecipe(recipe);
        }

        internal static bool IsEligibleGel(RecipeDef_PSTag recipe, CompPersonalityStore gel)
        {
            // 宠物先分派到专用契约检查。即使官方配方缺失输出 Def，
            // 也会在此拒绝，不能落到末尾通用配方的放行结果。
            if (PetFinalizationRecipeUtility.IsFinalizationRecipe(recipe)) return PetFinalizationRecipeUtility.IsEligibleGel(recipe, gel);
            if (TrainerOfficerRecipeUtility.IsFinalizationRecipe(recipe)) return TrainerOfficerRecipeUtility.IsEligibleGel(gel);
            if (CombatantRecipeUtility.IsFinalizationRecipe(recipe)) return CombatantRecipeUtility.IsEligibleGel(gel);
            return true;
        }
    }

    // =========================================================
    // 2. 配方工作者：负责搬运数据，并动态注入 XML 里定义的 Tag
    // =========================================================
    public class Recipe_PSTagWorker : RecipeWorker
    {
        /// <summary>复查来源并复制人格到对应等级的产物，随后替换状态标签和放置凝胶。</summary>
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            // 原版账单及外部调用都从实际传入集合读取来源；空集合或空条目
            // 不应触发组件访问异常。此处不自行计算或扣减账单完成次数。
            if (ingredients == null) return;
            Thing sourceItem = ingredients.FirstOrDefault(x => x?.TryGetComp<CompPersonalityStore>() != null);
            if (sourceItem == null) return;

            CompPersonalityStore sourceComp = sourceItem.TryGetComp<CompPersonalityStore>();

            // 这里只负责拒绝生成不合法的产物，不能用来保护已被原版消耗的材料。
            // 正常工作台账单会先经过 Harmony_TrainerRecipeCompletion 的消耗前检查；
            // 这里保留复查，保护其他模组直接调用工作者时的输出资格。
            if (recipe is RecipeDef_PSTag finalRecipe
                && !SpecializationFinalizationRecipeUtility.IsEligibleGel(finalRecipe, sourceComp)) return;

            bool petFinalization = PetFinalizationRecipeUtility.IsFinalizationRecipe(recipe as RecipeDef_PSTag);
            // 宠物加工只复制一个人格，拒绝多来源及无法生成存储组件的基底。
            // 原版 Destroy 会把非 Pawn 来源的 stackCount 清零，因此只对存活
            // 来源复查实体数量；正常消耗后的来源已由最终 Toil 检查为一份。
            // 若在这里要求已销毁来源仍为一份，合法账单会扣次数却没有产物。
            if (petFinalization && ((!sourceItem.Destroyed && sourceItem.stackCount != 1) ||
                ingredients.Count(x => x?.TryGetComp<CompPersonalityStore>() != null) != 1 ||
                !PetFinalizationRecipeUtility.CanCreateProduct(sourceItem.def))) return;

            ThingDef targetDef = PersonalityGelUtility.GetEditedThingDef(sourceItem.def);

            if (targetDef == null) return;

            Thing newItem = ThingMaker.MakeThing(targetDef);
            CompPersonalityStore targetComp = newItem.TryGetComp<CompPersonalityStore>();

            if (petFinalization && targetComp == null)
            {
                // 定义预检之外再确认真实实例，防止外部修改生成空人格产物。
                // 失败只销毁刚创建的无效产物，不在这里销毁尚存的来源凝胶；
                // 已由原版消耗的来源无法在此补偿，保料依赖消耗前守卫。
                newItem.Destroy(DestroyMode.Vanish);
                return;
            }

            if (targetComp != null)
            {
                // 先完整复制人格字段、特质、记忆、方向历史及标签，再改产物标签。
                // CopyFrom 隔离可变快照，后面的移除和写入不会修改源人格数据。
                targetComp.CopyFrom(sourceComp);

                // 输出状态由 XML 指定；各方向先清理其普通或互斥标签，再写终极。
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
                    // exclusiveTags 也用于筛料，普通标签只能在合格人格复制后单独移除。
                    if (CombatantRecipeUtility.IsFinalizationRecipe(smartRecipe))
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_Combatant);
                    if (petFinalization)
                    {
                        // 清除整个宠物组的普通标记，避免旧普通标签随终极产物叠加。
                        // 当前方向和各方向历史继续保留，组外标签不在清理范围内。
                        PetFinalizationRecipeUtility.RemoveOrdinaryTags(targetComp);
                    }
                    else if (petBaseTag != null)
                    {
                        targetComp.RemoveTag(petBaseTag);
                    }

                    if (smartRecipe.exclusiveTags != null)
                    {
                        // 此列表只修改产物副本。宠物来源若已有任意终极，前面的
                        // 共用资格已拒绝；不能靠这里移除冲突来把旧成果改成另一种。
                        foreach (HediffDef exclusiveTag in smartRecipe.exclusiveTags)
                        {
                            if (exclusiveTag != null)
                            {
                                targetComp.RemoveTag(exclusiveTag);
                            }
                        }
                    }

                    // 宠物配方已确认严重度为 1；写入一个目标终极供正常植入恢复。
                    targetComp.SetTag(smartRecipe.hediffToAdd, smartRecipe.severity);
                }
            }

            // 原版账单已消耗来源时不重复销毁；外部直接调用成功时才消费尚存来源。
            // 产物沿用原来的近处放置流程，账单计数仍由引擎负责。
            if (!sourceItem.Destroyed) sourceItem.Destroy(DestroyMode.Vanish);
            GenPlace.TryPlaceThing(newItem, billDoer.Position, billDoer.Map, ThingPlaceMode.Near);
        }
    }
}
