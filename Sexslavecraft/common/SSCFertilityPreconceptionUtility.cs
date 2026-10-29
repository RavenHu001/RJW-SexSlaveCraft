// AI 辅助生成：孕前设置只保存于现有子宫身体记录，不接管妊娠或婴儿生成。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>固定序号供身体存档使用；零为旧档缺字段时沿环境自然性别规则。</summary>
    public enum SSCOffspringGender { Natural = 0, Male = 1, Female = 2 }
    /// <summary>只保存物种来源方向；零沿原规则，实际物种与亲代解析留给生成入口。</summary>
    public enum SSCOffspringSpeciesSource { Natural = 0, Self = 1, Partner = 2 }

    /// <summary>一次枚举菜单的原凭据，不存档；身体预设也须由原人格上下文提交。</summary>
    public sealed class SSCFertilityPreconceptionContext
    {
        internal readonly Pawn Pawn;
        internal readonly CompSexSlaveTraining Comp;
        internal readonly object PersonalityToken;
        internal readonly Hediff_SSCUterusDevelopment Owner;
        /// <summary>先固定原身体、组件、人格和子宫实例，后续查询不得重新授权旧菜单。</summary>
        internal SSCFertilityPreconceptionContext(Pawn pawn)
        {
            Pawn = pawn;
            Comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn);
            Owner = SSCUterusDevelopmentUtility.Find(pawn);
        }
    }

    /// <summary>只读的新受孕可用预设，不保存孕次、亲代或遗传快照；后续消费者自行处理真实入口。</summary>
    public readonly struct SSCFertilityPreconceptionSettings
    {
        public readonly SSCOffspringGender Gender;
        public readonly SSCOffspringSpeciesSource SpeciesSource;
        public readonly bool ImprintEnabled;
        /// <summary>封装本次只读查询已按解锁阶段筛过的值，不持有身体、人格或妊娠引用。</summary>
        internal SSCFertilityPreconceptionSettings(SSCOffspringGender gender, SSCOffspringSpeciesSource species, bool imprint)
        { Gender = gender; SpeciesSource = species; ImprintEnabled = imprint; }
    }

    /// <summary>三项身体选择的准入与唯一提交服务；保存值、阶段准入和未孕编辑分别判断。</summary>
    public static class SSCFertilityPreconceptionUtility
    {
        /// <summary>显式识别稳定值，拒绝菜单提交的未知枚举，不用非法值偷偷清除预设。</summary>
        internal static bool IsValid(SSCOffspringGender value) => value is SSCOffspringGender.Natural or SSCOffspringGender.Male or SSCOffspringGender.Female;
        /// <summary>物种只表达来源，不将HAR物种或异种基因写入这个枚举。</summary>
        internal static bool IsValid(SSCOffspringSpeciesSource value) => value is SSCOffspringSpeciesSource.Natural or SSCOffspringSpeciesSource.Self or SSCOffspringSpeciesSource.Partner;
        /// <summary>只捕获原上下文，不补建子宫、训练组件或人格存档字段。</summary>
        public static SSCFertilityPreconceptionContext Capture(Pawn pawn) => new SSCFertilityPreconceptionContext(pawn);
        /// <summary>同Def新实例或同组件新人格均不属于原菜单，离体记录也不再授权。</summary>
        public static bool IsCurrent(SSCFertilityPreconceptionContext context)
            => context?.Pawn != null && context.Owner != null &&
                ReferenceEquals(context.Pawn.TryGetComp<CompSexSlaveTraining>(), context.Comp) &&
                SSCPersonalityRuntimeUtility.IsCurrent(context.Pawn, context.PersonalityToken) &&
                ReferenceEquals(SSCUterusDevelopmentUtility.Find(context.Pawn), context.Owner);

        /// <summary>只读沿用现有隐藏妊娠判定并补原版临产两阶段；不观察胚胎、着床或Tick进程。</summary>
        public static bool HasPregnancyOrLabor(Pawn pawn)
        {
            var set = pawn?.health?.hediffSet;
            if (set == null) return false;
            return rjw.PawnExtensions.IsPregnant(pawn, false) ||
                (HediffDefOf.PregnancyLabor != null && set.HasHediff(HediffDefOf.PregnancyLabor)) ||
                (HediffDefOf.PregnancyLaborPushing != null && set.HasHediff(HediffDefOf.PregnancyLaborPushing));
        }

        /// <summary>当前人格/身体/阶段的通用资格；不附加研究、绑定、玩家编辑或生育开启条件。</summary>
        private static string StageFailure(Pawn pawn, int requiredStage)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                pawn.RaceProps?.Humanlike != true || pawn.TryGetComp<CompSexSlaveTraining>() == null)
                return "SSC_Preconception_Unavailable".Translate();
            if (SSCPersonalityRuntimeUtility.IsMutating(pawn)) return SSCAbilityToggleUtility.Reason("Mutating");
            // Stage已经区分当前普通方向和跨方向终极成果；不能拿历史进度或CanSelect授予设置权限。
            int stage = FertilitySpecializationUtility.Stage(pawn);
            if (stage < 0)
                return !FertilitySpecializationUtility.MeetsConditions(pawn, out string reason) ? reason : "SSC_Preconception_Unavailable".Translate().ToString();
            if (stage >= requiredStage) return null;
            string key = requiredStage == 1 ? "SSC_Preconception_GenderLocked" :
                requiredStage == 2 ? "SSC_Preconception_SpeciesLocked" : "SSC_Preconception_ImprintLocked";
            return key.Translate();
        }

        /// <summary>编辑另查加载、玩家归属及孕中锁；生育关闭仍可预设，失格不能以关闭/自然绕过。</summary>
        private static string EditFailure(Pawn pawn, int requiredStage)
        {
            if (Scribe.mode != LoadSaveMode.Inactive) return SSCAbilityToggleUtility.Reason("Loading");
            string reason = StageFailure(pawn, requiredStage);
            if (reason != null) return reason;
            if (Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer) return SSCAbilityToggleUtility.Reason("PlayerOnly");
            return HasPregnancyOrLabor(pawn) ? "SSC_Preconception_PregnancyBlocked".Translate().ToString() : null;
        }

        /// <summary>在目标值预检之后仍复查原凭据；布尔项使用框架自己的同等上下文。</summary>
        private static bool CanEdit(SSCFertilityPreconceptionContext context, int stage, out string reason)
        {
            if (!IsCurrent(context)) { reason = SSCAbilityToggleUtility.Reason("Changed"); return false; }
            // 阶段、身份及孕态查询之后再查一次原凭据，防止扩展查询使原菜单过期。
            reason = EditFailure(context.Pawn, stage);
            if (reason != null) return false;
            if (!IsCurrent(context)) { reason = SSCAbilityToggleUtility.Reason("Changed"); return false; }
            return true;
        }

        /// <summary>目标值与原上下文共同校验；枚举不注册成多组互斥bool。</summary>
        public static bool CanSetGender(SSCFertilityPreconceptionContext context, SSCOffspringGender value, out string reason)
        {
            if (!IsValid(value)) { reason = "SSC_Preconception_InvalidSelection".Translate(); return false; }
            return CanEdit(context, 1, out reason);
        }
        /// <summary>提交只改原身体预设，不修改怀孕、现有胎儿或角色自身性别。</summary>
        public static bool TrySetGender(SSCFertilityPreconceptionContext context, SSCOffspringGender value, out string reason)
        {
            if (!CanSetGender(context, value, out reason)) return false;
            // 已确认的原owner才是写入对象，不在点击时寻找另一份同Def记录代替它。
            context.Owner.SetOffspringGender(value);
            return true;
        }
        /// <summary>复用同一编辑与owner规则，但使用物种来源的50%门槛。</summary>
        public static bool CanSetSpeciesSource(SSCFertilityPreconceptionContext context, SSCOffspringSpeciesSource value, out string reason)
        {
            if (!IsValid(value)) { reason = "SSC_Preconception_InvalidSelection".Translate(); return false; }
            return CanEdit(context, 2, out reason);
        }
        /// <summary>只保存来源枚举，真正的ThingDef/KindDef选择由后续生成入口负责。</summary>
        public static bool TrySetSpeciesSource(SSCFertilityPreconceptionContext context, SSCOffspringSpeciesSource value, out string reason)
        {
            if (!CanSetSpeciesSource(context, value, out reason)) return false;
            context.Owner.SetOffspringSpeciesSource(value);
            return true;
        }

        /// <summary>供后代印记开关后端查询通用准入；孕中禁改不等同于撤销已采用的选择。</summary>
        internal static string ImprintActivationFailure(Pawn pawn)
            => StageFailure(pawn, 4) ?? (SSCUterusDevelopmentUtility.Find(pawn)?.FertilityEnabled == true ? null : "SSC_Preconception_FertilityDisabled".Translate().ToString());
        /// <summary>开和关都要求完整编辑资格；不复制敏感度的失格可关闭例外。</summary>
        internal static string ImprintChangeFailure(Pawn pawn) => EditFailure(pawn, 4);
        /// <summary>只允许框架捕获的子宫实例接受写入，最终准入复查后不再查找替代owner。</summary>
        internal static bool TrySetImprint(Pawn pawn, Hediff_SSCUterusDevelopment owner, bool enabled, out string reason)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            reason = ImprintChangeFailure(pawn);
            if (reason != null) return false;
            // 后端最终检查也可能进入扩展查询；不能等框架写后报告过期才阻止新人格接受旧请求。
            if (owner == null || !ReferenceEquals(pawn?.TryGetComp<CompSexSlaveTraining>(), comp) ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) ||
                !ReferenceEquals(SSCUterusDevelopmentUtility.Find(pawn), owner))
            { reason = SSCAbilityToggleUtility.Reason("Changed"); return false; }
            owner.SetOffspringImprintEnabled(enabled);
            return true;
        }

        /// <summary>为后续新受孕入口只读解释可用预设；不保存冻结方案，也不能充当既有孕次的历史凭据。</summary>
        public static bool TryGetApplicableSettings(Pawn pawn, out SSCFertilityPreconceptionSettings settings)
        {
            settings = default;
            if (Scribe.mode != LoadSaveMode.Inactive || StageFailure(pawn, 0) != null) return false;
            var owner = SSCUterusDevelopmentUtility.Find(pawn);
            if (owner?.FertilityEnabled != true) return false;
            int stage = FertilitySpecializationUtility.Stage(pawn);
            // 失去某项解锁只让本次结果回自然/关闭，不覆写身体保存值；这里也不执行未孕编辑检查。
            settings = new SSCFertilityPreconceptionSettings(stage >= 1 ? owner.OffspringGender : SSCOffspringGender.Natural,
                stage >= 2 ? owner.OffspringSpeciesSource : SSCOffspringSpeciesSource.Natural,
                stage >= 4 && owner.OffspringImprintEnabled);
            return true;
        }
    }
}
