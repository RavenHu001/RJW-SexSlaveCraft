// AI 辅助生成：1.7 个人恢复默认，复用候选构建与原协调出口，不走批量或克隆清理。
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace SexSlaveCraft
{
    /// <summary>标题按钮的原请求；保存原地可变配置与模板快照，不向存档增加字段。</summary>
    public sealed class SSCRestrictionResetContext
    {
        internal Pawn Pawn, Master;
        internal CompSexSlaveTraining Comp;
        internal Hediff_ChainOfSexSlave Chain;
        internal object Token;
        internal SSCRestrictionConfig Config, Saved, Candidate;
        internal SSCRestrictionRules Template, Defaults;
        internal SexSlaveSpecializationType Specialization;
        internal List<SSCRestrictionDefaultInput> Profiles;
        internal bool Authorized;
    }
    /// <summary>本次默认来源快照；强制层不参与恢复的保存值。</summary>
    internal sealed class SSCRestrictionDefaultInput
    {
        internal SSCRestrictionProfileDef Def;
        internal SexSlaveSpecializationType Specialization;
        internal bool Matches;
        internal SSCRestrictionValue[] Values;
    }

    /// <summary>只读显示结果；按钮禁用不能替代提交入口的上下文检查。</summary>
    public sealed class SSCRestrictionResetView
    {
        public bool CanReset { get; internal set; }
        public string Reason { get; internal set; }
        public SSCRestrictionResetContext Context { get; internal set; }
    }

    public static partial class SSCRestrictionEditor
    {
        /// <summary>构建个人默认候选供标题显示，不初始化角色、不写模板、不改变指派。</summary>
        public static SSCRestrictionResetView GetResetView(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            var chain = SSCBondUtility.GetChain(pawn);
            var template = SSCMod.settings?.restrictionDefaults;
            var context = new SSCRestrictionResetContext { Pawn = pawn, Comp = comp, Chain = chain, Master = chain?.LinkedPawn,
                Token = SSCPersonalityRuntimeUtility.Capture(pawn), Config = comp?.restrictionConfig,
                Saved = comp?.restrictionConfig?.Copy(), Template = template, Defaults = template?.Copy() };
            var view = new SSCRestrictionResetView { Context = context, Reason = "Unavailable" };
            if (!CanResetPersonal(pawn)) return view;
            context.Specialization = comp.specializationType;
            context.Profiles = DefaultInputs(pawn);
            if (!SSCRestrictionConfigurationBuilder.TryCreateInitial(pawn, context.Defaults, out var candidate, out _))
            { view.Reason = "InvalidDefaults"; return view; }
            context.Candidate = candidate;
            if (!DefaultInputsCurrent(pawn, context) || !CanResetPersonal(pawn) || !ResetContextCurrent(pawn, context)) { view.Reason = "Stale"; return view; }
            view.Reason = SameResetConfig(context.Saved, candidate) ? "Default" : null;
            view.CanReset = view.Reason == null;
            context.Authorized = view.CanReset;
            return view;
        }

        /// <summary>用原请求重建并验证当前默认，末次复查后原子替换，仅沿原流程协调指定者。</summary>
        public static bool TryReset(Pawn pawn, SSCRestrictionResetContext context, out string reason)
        {
            reason = "Stale";
            // 不允许禁用视图在条件恢复后自行取得权限，仍须重新取得可用视图。
            if (context?.Authorized != true) return false;
            if (!ResetContextCurrent(pawn, context)) return false;
            if (!CanResetPersonal(pawn)) { reason = "Unavailable"; return false; }
            if (!SSCRestrictionConfigurationBuilder.TryCreateInitial(pawn, context.Defaults, out var candidate, out _))
            { reason = "InvalidDefaults"; return false; }
            // 特化或默认来源在显示后变化也不能使用旧按钮；不将失效请求重新捕获成新请求。
            if (!SameResetConfig(candidate, context.Candidate) || !DefaultInputsCurrent(pawn, context) ||
                !CanResetPersonal(pawn) || !ResetContextCurrent(pawn, context)) return false;
            if (SameResetConfig(context.Saved, candidate)) { reason = "Default"; return false; }
            context.Comp.restrictionConfig = candidate;
            context.Comp.restrictionLifecycleSeen = true;
            SSCRestrictionLifecycle.CoordinateTrainer(pawn);
            reason = null;
            return true;
        }

        /// <summary>保持只允许玩家编辑适用个人；不覆盖未知配置、待迁移输入或人格恢复中的数据。</summary>
        private static bool CanResetPersonal(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return pawn != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && comp != null &&
                Faction.OfPlayer != null && pawn.Faction == Faction.OfPlayer && Scribe.mode == LoadSaveMode.Inactive &&
                !SSCPersonalityRuntimeUtility.IsMutating(pawn) && !SSCMasterProgressionUtility.IsHollow(pawn) &&
                comp.restrictionRestoreDepth == 0 && comp.legacyRestrictionInput == null && IsValid(comp.restrictionConfig) &&
                (SSCRestrictionResolver.IsApplicable(pawn) || SSCSelfTrainingEligibility.IsEligible(pawn));
        }

        /// <summary>引用与完整原始值同时匹配；七项规则、关闭时对象偏好及模板原地修改均使旧请求失效。</summary>
        private static bool ResetContextCurrent(Pawn pawn, SSCRestrictionResetContext c)
            => c != null && pawn != null && pawn == c.Pawn && c.Comp != null && pawn.TryGetComp<CompSexSlaveTraining>() == c.Comp &&
               SSCBondUtility.GetChain(pawn) == c.Chain && c.Comp.restrictionConfig == c.Config && SameResetConfig(c.Config, c.Saved) &&
               c.Comp.specializationType == c.Specialization &&
               c.Chain?.LinkedPawn == c.Master && SSCPersonalityRuntimeUtility.IsCurrent(pawn, c.Token) &&
               SSCMod.settings?.restrictionDefaults == c.Template && SameResetRules(c.Template, c.Defaults);

        /// <summary>记录当前各默认来源及适用条件，防止候选构建的外部回调改特化或默认后提交旧结果。</summary>
        private static List<SSCRestrictionDefaultInput> DefaultInputs(Pawn pawn)
        {
            var inputs = new List<SSCRestrictionDefaultInput>();
            foreach (var profile in DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading)
            {
                var input = new SSCRestrictionDefaultInput { Def = profile, Specialization = profile.specialization,
                    Matches = SSCRestrictionResolver.HasProfile(pawn, profile), Values = new SSCRestrictionValue[SSCRestrictionRules.All.Count] };
                for (int i = 0; i < input.Values.Length; i++) input.Values[i] = profile.defaults?.Get(SSCRestrictionRules.All[i]) ?? SSCRestrictionValue.Unspecified;
                inputs.Add(input);
            }
            return inputs;
        }

        /// <summary>先完成可能有外部包装的适用查询，再直接核对当前定义数据，最后由原上下文检查载体。</summary>
        private static bool DefaultInputsCurrent(Pawn pawn, SSCRestrictionResetContext c)
        {
            var current = DefaultInputs(pawn);
            if (c.Profiles == null || current.Count != c.Profiles.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                var old = c.Profiles[i]; var now = current[i];
                if (old.Def != now.Def || old.Specialization != now.Def.specialization || old.Matches != now.Matches) return false;
                for (int j = 0; j < old.Values.Length; j++)
                    if (old.Values[j] != (now.Def.defaults?.Get(SSCRestrictionRules.All[j]) ?? SSCRestrictionValue.Unspecified)) return false;
            }
            return true;
        }

        /// <summary>比较完整保存配置，不把实时特化／装备强制结果写入个人选择。</summary>
        private static bool SameResetConfig(SSCRestrictionConfig a, SSCRestrictionConfig b)
            => a == null || b == null ? a == b : a.version == b.version && a.busDefaultsApplied == b.busDefaultsApplied && SameResetRules(a.rules, b.rules);

        /// <summary>包含关闭前的对象选择；只比较公开固定规则，不通过序列化或引用相等冒充值相等。</summary>
        private static bool SameResetRules(SSCRestrictionRules a, SSCRestrictionRules b)
        {
            if (a == null || b == null) return a == b;
            if (a.ConsensualTarget != b.ConsensualTarget) return false;
            foreach (var rule in SSCRestrictionRules.All) if (a.Get(rule) != b.Get(rule)) return false;
            return true;
        }
    }
}
