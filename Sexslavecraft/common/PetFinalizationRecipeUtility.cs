using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>宠物终极化的只读资格；筛料、消耗前守卫和工作者共用。</summary>
    internal static class PetFinalizationRecipeUtility
    {
        // 互斥组只包括猫、狗、兔。遍历整个组才能阻止跨种终极冲突，
        // 奶牛等组外成果不会参与此检查，也不会在产物清理时被移除。
        private static readonly SexSlaveSpecializationType[] PetTypes =
        {
            SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog,
            SexSlaveSpecializationType.PetRabbit
        };

        /// <summary>按输出 Hediff 的名称识别宠物终极目标，供官方及同目标的外部配方使用。</summary>
        private static SexSlaveSpecializationType GetTargetType(RecipeDef_PSTag recipe)
        {
            // 此处只识别应受保护的类别；实际加工还须在 IsEligibleGel 中
            // 确认目标就是数据库里的真实 Def，不能用同名临时对象代替。
            foreach (SexSlaveSpecializationType type in PetTypes)
                if (recipe?.hediffToAdd?.defName == "SSC_Hediff_" + type + "_Final") return type;
            return SexSlaveSpecializationType.None;
        }

        /// <summary>通过官方配方名称识别目标，即使输出 Def 缺失也能进入保护分支。</summary>
        private static SexSlaveSpecializationType GetNamedRecipeType(RecipeDef_PSTag recipe)
        {
            foreach (SexSlaveSpecializationType type in PetTypes)
                if (recipe?.defName == "SSC_PSEdit_" + type + "_Final") return type;
            return SexSlaveSpecializationType.None;
        }

        /// <summary>判断是否适用宠物终极化保护；此步骤仅分类，不代表允许加工。</summary>
        internal static bool IsFinalizationRecipe(RecipeDef_PSTag recipe)
        {
            // 已知配方在目标 Def 缺失时仍受保护，不能回落到通用的放行分支。
            return GetTargetType(recipe) != SexSlaveSpecializationType.None ||
                GetNamedRecipeType(recipe) != SexSlaveSpecializationType.None;
        }

        /// <summary>只读检查配方契约及凝胶的当前普通培养资格，三个加工入口共用同一结果。</summary>
        internal static bool IsEligibleGel(RecipeDef_PSTag recipe, CompPersonalityStore gel)
        {
            // 配方声明的方向、输出目标、完成要求及终极严重度必须一致。
            // 官方配方缺字段或被改成其他目标时直接拒绝，避免回退到通用加工。
            SexSlaveSpecializationType type = GetTargetType(recipe);
            SexSlaveSpecializationType namedType = GetNamedRecipeType(recipe);
            if (gel == null || !PetSpecializationRules.IsPetSpecialization(type) ||
                (namedType != SexSlaveSpecializationType.None && namedType != type) ||
                recipe.requireSpecializationType != type || !recipe.requireSpecializationComplete ||
                recipe.severity != 1f) return false;

            // 组内六个 Def 必须齐全，才能识别全部冲突并清理产物普通标签。
            // 缺定义不视作“没有终极”，而是保守拒绝；此检查不修改源凝胶。
            foreach (SexSlaveSpecializationType petType in PetTypes)
                if (PetSpecializationUtility.GetBaseHediffDef(petType) == null ||
                    PetSpecializationUtility.GetFinalHediffDef(petType) == null) return false;
            HediffDef baseDef = PetSpecializationUtility.GetBaseHediffDef(type);
            if (recipe.hediffToAdd != PetSpecializationUtility.GetFinalHediffDef(type)) return false;

            // 只认当前方向及其当前进度；历史字典不能授予本次加工资格。
            // 0.999 是既有完成容差，有限值与上限检查阻止异常数据绕过门槛。
            // 普通标签证明尚处普通阶段，CanTrain 同时排除组内任意终极。
            // 不追加玩家选择的开放／研究门槛，以保留合法旧猫、兔人格的加工。
            // 不检查 parent.Destroyed：正常结算先消耗原料，工作者随后读取组件；
            // 物品存活与数量由筛料、消耗前守卫及工作者各自按执行时机检查。
            float progress = gel.specializationProgress;
            return !float.IsNaN(progress) && !float.IsInfinity(progress) &&
                progress >= CompSexSlaveTraining.SpecializationCompletionProgress && progress <= 1f &&
                gel.HasTag(baseDef) && PetSpecializationRules.CanTrain(type, gel.specializationType,
                    gel.HasTag(PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetCat)),
                    gel.HasTag(PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetDog)),
                    gel.HasTag(PetSpecializationUtility.GetFinalHediffDef(SexSlaveSpecializationType.PetRabbit)));
        }

        /// <summary>预检既有凝胶等级映射及产物人格组件声明，避免消耗后无法复制人格。</summary>
        internal static bool CanCreateProduct(ThingDef sourceDef)
        {
            // 仅接受既有八种凝胶，沿用四种已编辑等级映射；外部未知基底不放行。
            if (sourceDef == null || !PersonalityGelUtility.IsSupportedPersonalityGel(sourceDef)) return false;
            ThingDef productDef = PersonalityGelUtility.GetEditedThingDef(sourceDef);
            // 目标 Def 存在还不够：必须声明人格组件或其派生类。
            // 这里只检查定义，实际实例仍由工作者在 ThingMaker 创建后确认。
            return productDef?.comps?.Any(props => props?.compClass != null &&
                typeof(CompPersonalityStore).IsAssignableFrom(props.compClass)) == true;
        }

        /// <summary>从已复制的产物清除三种宠物普通标签，交由调用方写入一个目标终极。</summary>
        internal static void RemoveOrdinaryTags(CompPersonalityStore product)
        {
            // 调用前已经验证完整组定义及来源资格。只清理产物的状态标签，
            // 不修改源凝胶、方向历史、当前进度或组外成果；也不承担旧多终极迁移。
            foreach (SexSlaveSpecializationType type in PetTypes)
                product.RemoveTag(PetSpecializationUtility.GetBaseHediffDef(type));
        }
    }
}
