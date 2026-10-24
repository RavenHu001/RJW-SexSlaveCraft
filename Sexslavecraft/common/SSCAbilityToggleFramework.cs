// AI 辅助生成：个人开关的只读状态与载体适配；不改变原存档归属。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>每种载体负责自己的资格、引用凭据和唯一写入口，展示分组不授予资格。</summary>
    public abstract class SSCAbilityToggleBackend
    {
        public abstract bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition);
        public abstract string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition);
        public abstract string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled);
        public abstract object CaptureOwner(Pawn pawn);
        public abstract bool IsOwnerCurrent(Pawn pawn, object owner);
        public abstract void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled);
    }

    /// <summary>一次 UI 事件捕获的提交凭据；不能从新载体反推旧请求的授权。</summary>
    public sealed class SSCAbilityToggleContext
    {
        internal readonly Pawn Pawn;
        internal readonly SSCAbilityToggleDefinition Definition;
        internal readonly CompSexSlaveTraining Comp;
        internal readonly object PersonalityToken, Owner;
        internal SSCAbilityToggleContext(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            Pawn = pawn; Definition = definition;
            Comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            PersonalityToken = pawn == null ? null : SSCPersonalityRuntimeUtility.Capture(pawn);
            Owner = definition?.Backend.CaptureOwner(pawn);
        }
    }

    public sealed class SSCAbilityToggleView
    {
        public bool Visible { get; internal set; }
        public bool Enabled { get; internal set; }
        public bool Active { get; internal set; }
        public bool CanChange { get; internal set; }
        public string ChangeReason { get; internal set; }
        public string InactiveReason { get; internal set; }
        public SSCAbilityToggleContext Context { get; internal set; }
    }

    /// <summary>现有欢愉恩赐及同类能力沿用人格字典、身份门槛与可选规则。</summary>
    internal sealed class SSCPersonalityAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCPersonalityAbilityToggleBackend Instance = new SSCPersonalityAbilityToggleBackend();
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && (comp.abilityToggles?.Get(definition.Id, definition.DefaultEnabled) ?? definition.DefaultEnabled);
        }
        private static string PersonalityFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                pawn.health?.hediffSet == null || pawn.RaceProps?.Humanlike != true)
                return SSCAbilityToggleUtility.Reason("Unavailable");
            if (comp.pawnIdentity != definition.Identity) return SSCAbilityToggleUtility.Reason("Identity");
            if (SSCMasterProgressionUtility.IsHollow(pawn)) return SSCAbilityToggleUtility.Reason("Hollow");
            if (SSCPersonalityRuntimeUtility.IsMutating(pawn)) return SSCAbilityToggleUtility.Reason("Mutating");
            return null;
        }
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => PersonalityFailure(pawn, definition);
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            string reason = PersonalityFailure(pawn, definition);
            if (reason != null) return reason;
            if (Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer) return SSCAbilityToggleUtility.Reason("PlayerOnly");
            return null;
        }
        public override object CaptureOwner(Pawn pawn) => pawn?.TryGetComp<CompSexSlaveTraining>();
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(pawn?.TryGetComp<CompSexSlaveTraining>(), owner);
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp.abilityToggles == null) comp.abilityToggles = new SSCAbilityToggleState();
            comp.abilityToggles.Set(definition.Id, enabled);
            SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }

    public static partial class SSCAbilityToggleUtility
    {
        internal static string Reason(string suffix) => ("SSC_AbilityToggle_" + suffix).Translate().ToString();
        internal static string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => definition.Backend.ActivationFailure(pawn, definition) ??
                (definition.ActivationRule != null && !definition.ActivationRule(pawn) ? Reason("Unavailable") : null);

        public static SSCAbilityToggleView GetView(Pawn pawn, string id)
        {
            var definition = Find(id);
            // 先绑定来源，再执行可扩展查询；查询回调换入的新人格不能继承旧显示值的提交凭据。
            var context = new SSCAbilityToggleContext(pawn, definition);
            bool enabled = GetEnabled(pawn, id);
            string inactive = definition == null ? Reason("Unavailable") : ActivationFailure(pawn, definition);
            bool canChange = CanSet(pawn, id, !enabled, out string reason);
            return new SSCAbilityToggleView
            {
                Visible = definition != null && (definition.VisibleRule == null || definition.VisibleRule(pawn)),
                Enabled = enabled, Active = enabled && definition != null && inactive == null,
                CanChange = canChange, ChangeReason = reason, InactiveReason = inactive,
                Context = context
            };
        }

        public static bool CanSet(Pawn pawn, string id, bool enabled, out string reason)
        {
            var definition = Find(id);
            if (definition == null) { reason = Reason("Unavailable"); return false; }
            if (Scribe.mode != LoadSaveMode.Inactive) { reason = Reason("Loading"); return false; }
            reason = definition.Backend.ChangeFailure(pawn, definition, enabled) ??
                (definition.EditRule != null && !definition.EditRule(pawn) ? Reason("Unavailable") : null) ??
                definition.ChangeRule?.Invoke(pawn, enabled);
            return reason == null;
        }

        public static bool TrySet(Pawn pawn, string id, bool enabled, SSCAbilityToggleContext context, out string reason)
        {
            var definition = Find(id);
            if (!IsContextCurrent(pawn, definition, context))
            { reason = Reason("Changed"); return false; }
            if (!CanSet(pawn, id, enabled, out reason)) return false;
            // 自定义规则可能触发外部回调；校验结束后仍须确认原载体才能写入。
            if (!IsContextCurrent(pawn, definition, context))
            { reason = Reason("Changed"); return false; }
            definition.Backend.SetEnabled(pawn, definition, enabled);
            // 健康回调可能替换人格或原载体，不能将原请求的结果授权给新对象。
            bool current = IsContextCurrent(pawn, definition, context);
            reason = current ? null : Reason("Changed");
            return current;
        }
        private static bool IsContextCurrent(Pawn pawn, SSCAbilityToggleDefinition definition, SSCAbilityToggleContext context)
            => context != null && pawn != null && context.Pawn == pawn && definition != null &&
                ReferenceEquals(context.Definition, definition) &&
                ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), context.Comp) &&
                SSCPersonalityRuntimeUtility.IsCurrent(pawn, context.PersonalityToken) &&
                definition.Backend.IsOwnerCurrent(pawn, context.Owner);
    }
}
