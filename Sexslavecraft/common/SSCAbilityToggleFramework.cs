// AI 辅助生成：个人开关的只读状态与载体适配；不改变原存档归属。
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>每种载体负责自己的资格、引用凭据和唯一写入口，展示分组不授予资格。</summary>
    public abstract class SSCAbilityToggleBackend
    {
        /// <summary>只读取得唯一载体中的保存选择；缺字段沿该载体既有默认规则解释。</summary>
        public abstract bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition);
        /// <summary>返回能力当前通用准入的失败原因；null 表示放行，不替代具体事件的目标／伤害检查。</summary>
        public abstract string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition);
        /// <summary>按目标值检查载体编辑资格；返回 null 表示允许，拒绝时返回可供界面显示的原因。</summary>
        public abstract string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled);
        /// <summary>捕获原保存载体引用，不补建缺失的组件、身体记录或关系。</summary>
        public abstract object CaptureOwner(Pawn pawn);
        /// <summary>确认当前保存载体仍是捕获的同一实例，防止旧请求落到替代实例。</summary>
        public abstract bool IsOwnerCurrent(Pawn pawn, object owner);
        /// <summary>在公共入口完成校验后执行唯一写入；同步标记／图形等副作用由对应后端负责。</summary>
        public abstract void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled);
        /// <summary>兼容第一批无失败返回的写入；有权威拒绝结果的载体覆盖此入口并原样回传原因。</summary>
        public virtual bool TrySetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled, out string reason)
        {
            // 第一批人格／缰绳后端沿用无返回值入口；能够拒绝提交的身体后端必须覆盖本方法。
            SetEnabled(pawn, definition, enabled);
            reason = null;
            return true;
        }
    }

    /// <summary>一次 UI 事件捕获的提交凭据；不能从新载体反推旧请求的授权。</summary>
    public sealed class SSCAbilityToggleContext
    {
        internal readonly Pawn Pawn;
        internal readonly SSCAbilityToggleDefinition Definition;
        internal readonly CompSexSlaveTraining Comp;
        internal readonly object PersonalityToken, Owner;
        /// <summary>先捕获角色、人格和原载体凭据，再交给可扩展规则查询；上下文只在当前进程使用。</summary>
        internal SSCAbilityToggleContext(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            Pawn = pawn; Definition = definition;
            Comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            PersonalityToken = pawn == null ? null : SSCPersonalityRuntimeUtility.Capture(pawn);
            Owner = definition?.Backend.CaptureOwner(pawn);
        }
    }

    /// <summary>单次查询的显示快照，不存档；Active 只描述通用准入，不保证具体事件发生效果。</summary>
    public sealed class SSCAbilityToggleView
    {
        public bool Visible { get; internal set; }
        public bool Enabled { get; internal set; }
        public bool Active { get; internal set; }
        // CanChange 对应下一次反向切换；编辑被禁与保存开启但暂不生效分别提供原因。
        public bool CanChange { get; internal set; }
        public string ChangeReason { get; internal set; }
        public string InactiveReason { get; internal set; }
        public SSCAbilityToggleContext Context { get; internal set; }
    }

    /// <summary>现有欢愉恩赐及同类能力沿用人格字典、身份门槛与可选规则。</summary>
    internal sealed class SSCPersonalityAbilityToggleBackend : SSCAbilityToggleBackend
    {
        internal static readonly SSCPersonalityAbilityToggleBackend Instance = new SSCPersonalityAbilityToggleBackend();
        /// <summary>读取旧人格字典，缺键按定义默认值返回；查询不补写字典或创建训练组件。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && (comp.abilityToggles?.Get(definition.Id, definition.DefaultEnabled) ?? definition.DefaultEnabled);
        }
        /// <summary>保留欢愉恩赐原有的有效人格门槛，不要求缰绳、成长记录或永久主人资格。</summary>
        private static string PersonalityFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            // 先排除无效身体／组件；偏好本身仍留在原字典，资格失效不等于清除选择。
            if (comp == null || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                pawn.health?.hediffSet == null || pawn.RaceProps?.Humanlike != true)
                return SSCAbilityToggleUtility.Reason("Unavailable");
            if (comp.pawnIdentity != definition.Identity) return SSCAbilityToggleUtility.Reason("Identity");
            // 空壳与替换中的人格暂停能力，避免半恢复状态参与实际收益结算。
            if (SSCMasterProgressionUtility.IsHollow(pawn)) return SSCAbilityToggleUtility.Reason("Hollow");
            if (SSCPersonalityRuntimeUtility.IsMutating(pawn)) return SSCAbilityToggleUtility.Reason("Mutating");
            return null;
        }
        /// <summary>返回人格准入原因；定义额外激活规则由公共入口统一追加。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => PersonalityFailure(pawn, definition);
        /// <summary>有效人格且属于玩家时允许编辑；非玩家仍可按自己的保存选择实际生效。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            string reason = PersonalityFailure(pawn, definition);
            if (reason != null) return reason;
            if (Faction.OfPlayer == null || pawn.Faction != Faction.OfPlayer) return SSCAbilityToggleUtility.Reason("PlayerOnly");
            return null;
        }
        /// <summary>人格字典由原训练组件持有，组件引用作为本后端的载体凭据。</summary>
        public override object CaptureOwner(Pawn pawn) => pawn?.TryGetComp<CompSexSlaveTraining>();
        /// <summary>组件被替换时拒绝旧请求；同组件换人格还须通过公共 token 检查。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner)
            => owner != null && ReferenceEquals(pawn?.TryGetComp<CompSexSlaveTraining>(), owner);
        /// <summary>保存明确的 true／false 并协调派生标记，不将实际生效状态另存一份。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        {
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp.abilityToggles == null) comp.abilityToggles = new SSCAbilityToggleState();
            // 显式关闭也写入稳定键；标记由最终偏好派生，重复同步按既有幂等规则处理。
            comp.abilityToggles.Set(definition.Id, enabled);
            SSCAbilityToggleUtility.SyncMarkers(pawn);
        }
    }

    public static partial class SSCAbilityToggleUtility
    {
        /// <summary>统一解析通用拒绝原因的翻译键，不在策略内硬编码玩家文本。</summary>
        internal static string Reason(string suffix) => ("SSC_AbilityToggle_" + suffix).Translate().ToString();
        /// <summary>先保留载体失败原因，再检查定义附加激活规则，供视图与实际生效查询共用。</summary>
        internal static string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition)
            => definition.Backend.ActivationFailure(pawn, definition) ??
                (definition.ActivationRule != null && !definition.ActivationRule(pawn) ? Reason("Unavailable") : null);

        /// <summary>形成一次只读状态快照，同时保留保存选择、当前准入和下一次切换的编辑原因。</summary>
        public static SSCAbilityToggleView GetView(Pawn pawn, string id)
        {
            var definition = Find(id);
            // 先绑定来源，再执行可扩展查询；查询回调换入的新人格不能继承旧显示值的提交凭据。
            var context = new SSCAbilityToggleContext(pawn, definition);
            bool enabled = GetEnabled(pawn, id);
            string inactive = definition == null ? Reason("Unavailable") : ActivationFailure(pawn, definition);
            // 复选框默认请求当前选择的反向值；界面禁用状态不能取代提交时的目标值复查。
            bool canChange = CanSet(pawn, id, !enabled, out string reason);
            return new SSCAbilityToggleView
            {
                Visible = definition != null && (definition.VisibleRule == null || definition.VisibleRule(pawn)),
                Enabled = enabled, Active = enabled && definition != null && inactive == null,
                CanChange = canChange, ChangeReason = reason, InactiveReason = inactive,
                Context = context
            };
        }

        /// <summary>只读校验指定目标值：载入期拒绝，之后按载体、通用编辑规则和方向性规则依次检查。</summary>
        public static bool CanSet(Pawn pawn, string id, bool enabled, out string reason)
        {
            var definition = Find(id);
            if (definition == null) { reason = Reason("Unavailable"); return false; }
            if (Scribe.mode != LoadSaveMode.Inactive) { reason = Reason("Loading"); return false; }
            // 首个拒绝原因决定反馈；方向规则可表达“能关不能开”等行为，不改写保存值。
            reason = definition.Backend.ChangeFailure(pawn, definition, enabled) ??
                (definition.EditRule != null && !definition.EditRule(pawn) ? Reason("Unavailable") : null) ??
                definition.ChangeRule?.Invoke(pawn, enabled);
            return reason == null;
        }

        /// <summary>用原上下文执行授权写入；前后核对人格／载体，失败结果不声称提供整套事务回滚。</summary>
        public static bool TrySet(Pawn pawn, string id, bool enabled, SSCAbilityToggleContext context, out string reason)
        {
            var definition = Find(id);
            // 第一轮拒绝已过期或跨角色／跨能力的凭据，再按当前条件检查目标操作。
            if (!IsContextCurrent(pawn, definition, context))
            { reason = Reason("Changed"); return false; }
            if (!CanSet(pawn, id, enabled, out reason)) return false;
            // 自定义规则可能触发外部回调；校验结束后仍须确认原载体才能写入。
            if (!IsContextCurrent(pawn, definition, context))
            { reason = Reason("Changed"); return false; }
            // 预检不替代载体最终检查：例如预检后才检出妊娠时，保留原 setter 的 false 和原因。
            if (!definition.Backend.TrySetEnabled(pawn, definition, enabled, out reason)) return false;
            // 健康回调可能替换人格或原载体，不能将原请求的结果授权给新对象。
            bool current = IsContextCurrent(pawn, definition, context);
            reason = current ? null : Reason("Changed");
            return current;
        }
        /// <summary>将角色、定义、组件、人格和后端 owner 作为同一归属检查，不按 ID 或身份相同放宽引用要求。</summary>
        private static bool IsContextCurrent(Pawn pawn, SSCAbilityToggleDefinition definition, SSCAbilityToggleContext context)
            => context != null && pawn != null && context.Pawn == pawn && definition != null &&
                ReferenceEquals(context.Definition, definition) &&
                ReferenceEquals(pawn.TryGetComp<CompSexSlaveTraining>(), context.Comp) &&
                SSCPersonalityRuntimeUtility.IsCurrent(pawn, context.PersonalityToken) &&
                definition.Backend.IsOwnerCurrent(pawn, context.Owner);

        /// <summary>供身体／外观绘制器只读检查快照；复用同一套人格与 owner 凭据，不重拍上下文或写入载体。</summary>
        public static bool IsCurrent(Pawn pawn, string id, SSCAbilityToggleContext context)
            => IsContextCurrent(pawn, Find(id), context);
    }
}
