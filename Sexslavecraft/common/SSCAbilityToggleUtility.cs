// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // 分组只决定展示区域，不改变各后端的保存归属，也不自动附加身份或研究门槛。
    public enum SSCAbilityToggleGroup { Master, Slave, Body, Appearance }

    /// <summary>声明个人开关；默认沿用人格存储，1.7 可适配其他原载体。</summary>
    public sealed class SSCAbilityToggleDefinition
    {
        public readonly string Id, LabelKey, TipKey, MarkerDefName;
        public readonly SSCAbilityToggleGroup Group;
        public readonly PawnIdentity Identity;
        public readonly bool DefaultEnabled;
        public readonly Func<Pawn, bool> ActivationRule, EditRule;
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
        // 后端决定保存源与准入；排序、显示和提示只控制呈现，不改变人格／关系归属。
        public readonly SSCAbilityToggleBackend Backend;
        public readonly int Order;
        public readonly Func<Pawn, bool> VisibleRule;
        public readonly Func<Pawn, bool, string> ChangeRule;
        public readonly Func<Pawn, string> TipRule;
        // 只读从属文字，例如原泌乳充盈度；不承担开关保存、生产结算或新增状态展示。
        public readonly Func<Pawn, string> DetailRule;
#endif
        /// <summary>声明稳定 ID、默认值和规则；未指定后端时沿用人格字典，定义对象不保存某个角色的开关。</summary>
        public SSCAbilityToggleDefinition(string id, SSCAbilityToggleGroup group, PawnIdentity identity,
            string labelKey, string tipKey, string markerDefName = null, bool defaultEnabled = false,
            Func<Pawn, bool> activationRule = null, Func<Pawn, bool> editRule = null
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            , SSCAbilityToggleBackend backend = null, int order = 0, Func<Pawn, bool> visibleRule = null,
            Func<Pawn, bool, string> changeRule = null, Func<Pawn, string> tipRule = null,
            Func<Pawn, string> detailRule = null
#endif
            )
        {
            Id = id; Group = group; Identity = identity; LabelKey = labelKey; TipKey = tipKey;
            MarkerDefName = markerDefName; DefaultEnabled = defaultEnabled;
            ActivationRule = activationRule; EditRule = editRule;
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            Backend = backend ?? SSCPersonalityAbilityToggleBackend.Instance;
            Order = order; VisibleRule = visibleRule; ChangeRule = changeRule; TipRule = tipRule;
            DetailRule = detailRule;
#endif
        }
    }

    public static partial class SSCAbilityToggleUtility
    {
        public const string MasterDesireInfestation = "SSC_MasterDesireInfestation";
        public const string InfestationMarker = "SSC_Hediff_MasterDesireInfestation";
        // 注册表只声明能力及可选规则，不保存每个 Pawn 的开启值，也不在这里发放能力收益。
        // 模型宿主可仅链接某类载体；实际 1.7 不定义 SCOPED_TEST，始终注册全部五项。
        private static readonly List<SSCAbilityToggleDefinition> definitions = new List<SSCAbilityToggleDefinition>
        {
            new SSCAbilityToggleDefinition(MasterDesireInfestation, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
                "SSC_AbilityToggle_MasterDesireInfestation_Label", "SSC_AbilityToggle_MasterDesireInfestation_Tip", InfestationMarker)
#if (SSC_RW17 && !SSC_ABILITY_TOGGLE_SCOPED_TEST) || SSC_ABILITY_TOGGLE_MASTER_SHARE
            // 分伤沿原缰绳保存，排序保持在恩赐之前；百分比提示仍使用原业务常量。
            , new SSCAbilityToggleDefinition(MasterDamageSharing, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
                "SSC_MasterShareDamage_Label", "SSC_MasterShareDamage_Desc", defaultEnabled: true,
                backend: SSCBridleAbilityToggleBackend.Instance, order: -100,
                tipRule: pawn => "SSC_MasterShareDamage_Desc".Translate(SSCMasterBondUtility.DamageShareRatio.ToStringPercent()).ToString())
#endif
#if (SSC_RW17 && !SSC_ABILITY_TOGGLE_SCOPED_TEST) || SSC_ABILITY_TOGGLE_BODY
            // 两项同处身体栏但保存源不同：泌乳沿 Comp，生育沿身体 Hediff，均不迁入人格字典。
            , new SSCAbilityToggleDefinition(MilkProduction, SSCAbilityToggleGroup.Body, PawnIdentity.Unset,
                "SSC_ITab_MilkProductionToggle", "SSC_BodyManagement_MilkTip", defaultEnabled: true,
                backend: SSCMilkAbilityToggleBackend.Instance,
                visibleRule: pawn => pawn?.TryGetComp<CompSexSlaveTraining>() != null && SSCMilkAbilityToggleBackend.Find(pawn) != null,
                detailRule: pawn => SSCMilkAbilityToggleBackend.Find(pawn)?.TryGetComp<HediffComp_PermanentLactating>()?.CompLabelInBracketsExtra)
            , new SSCAbilityToggleDefinition(UterusFertility, SSCAbilityToggleGroup.Body, PawnIdentity.Unset,
                "SSC_BodyManagement_FertilityToggle", "SSC_BodyManagement_FertilityTip",
                backend: SSCUterusAbilityToggleBackend.Instance, order: 10,
                visibleRule: pawn => pawn?.TryGetComp<CompSexSlaveTraining>() != null && SSCUterusDevelopmentUtility.Find(pawn) != null)
#endif
#if (SSC_RW17 && !SSC_ABILITY_TOGGLE_SCOPED_TEST) || SSC_ABILITY_TOGGLE_GEL
            // 外观只接入个人渲染选择；颜色、透明度、快照和高频渲染查询仍由原外观模块负责。
            , new SSCAbilityToggleDefinition(GelRendering, SSCAbilityToggleGroup.Appearance, PawnIdentity.Unset,
                "SSC_GelAppearance_RenderEnabled", "SSC_GelAppearance_RenderTip", defaultEnabled: true,
                backend: SSCGelRenderingAbilityToggleBackend.Instance,
                visibleRule: pawn => FullGelAppearanceUtility.GetOwner(pawn) != null)
#endif
        };
        private static readonly IReadOnlyList<SSCAbilityToggleDefinition> readOnlyDefinitions = definitions.AsReadOnly();
        public static IReadOnlyList<SSCAbilityToggleDefinition> Definitions => readOnlyDefinitions;
        private sealed class SyncState { public bool Running, Pending; }
        private static readonly ConditionalWeakTable<Pawn, SyncState> syncing = new ConditionalWeakTable<Pawn, SyncState>();

        /// <summary>追加只读定义；拒绝重复 ID 与标记，避免保存解释冲突或两个能力争抢同一健康标记。</summary>
        public static void Register(SSCAbilityToggleDefinition definition)
        {
            // 标记与能力一一对应，防止两个定义争抢同一个健康实例；UI 读取只读注册表视图。
            if (definition == null || string.IsNullOrEmpty(definition.Id) ||
                definitions.Any(d => d.Id == definition.Id || (!string.IsNullOrEmpty(definition.MarkerDefName) &&
                    d.MarkerDefName == definition.MarkerDefName)))
                throw new ArgumentException("Ability toggle IDs and marker definitions must be unique.");
            definitions.Add(definition);
        }
        /// <summary>按稳定保存／展示 ID 查询定义，未知 ID 返回 null，不创建占位能力。</summary>
        public static SSCAbilityToggleDefinition Find(string id) => definitions.FirstOrDefault(d => d.Id == id);
        /// <summary>按标记 Def 查所属能力，未声明标记或缺少 Def 时返回 null。</summary>
        public static SSCAbilityToggleDefinition FindByMarker(HediffDef def)
            => def == null ? null : definitions.FirstOrDefault(d => !string.IsNullOrEmpty(d.MarkerDefName) && d.MarkerDefName == def.defName);
        /// <summary>识别已注册或属于通用标记类型的 Def，供复制过滤与孤儿标记清理使用。</summary>
        public static bool IsMarkerDef(HediffDef def)
            => def != null && (FindByMarker(def) != null ||
                (def.hediffClass != null && typeof(Hediff_SSCToggleAbilityMarker).IsAssignableFrom(def.hediffClass)));

        // 偏好、实际生效与玩家编辑权限分别查询；退出身份时仍能保存原始偏好。
        /// <summary>读取能力自己的唯一保存源；不以当前资格或健康标记反推玩家选择。</summary>
        public static bool GetEnabled(Pawn pawn, string id)
        {
            SSCAbilityToggleDefinition definition = Find(id);
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            return definition != null && definition.Backend.GetEnabled(pawn, definition);
#else
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return definition != null && comp != null && (comp.abilityToggles
                ?.Get(id, definition.DefaultEnabled) ?? definition.DefaultEnabled);
#endif
        }
#if !(SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK)
        /// <summary>冻结版本保留原统一人格条件；1.7 已将这些条件移入默认人格后端。</summary>
        private static bool HasStablePersonality(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            // 人格替换期间全部能力暂停；主人资格、成长记录和缰绳不是通用开关的前置。
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return definition != null && comp != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
                pawn.health?.hediffSet != null && pawn.RaceProps?.Humanlike == true &&
                comp.pawnIdentity == definition.Identity && !SSCMasterProgressionUtility.IsHollow(pawn) &&
                !SSCPersonalityRuntimeUtility.IsMutating(pawn);
        }
#endif
        /// <summary>组合保存开启与能力当前通用准入；具体目标、伤害或收益条件仍在所属业务入口判断。</summary>
        public static bool IsActive(Pawn pawn, string id)
        {
            SSCAbilityToggleDefinition definition = Find(id);
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            return definition != null && GetEnabled(pawn, id) && ActivationFailure(pawn, definition) == null;
#else
            return HasStablePersonality(pawn, definition) && GetEnabled(pawn, id) &&
                (definition.ActivationRule == null || definition.ActivationRule(pawn));
#endif
        }
        /// <summary>兼容旧编辑查询；1.7 按复选框下一次切换的目标值调用公共校验。</summary>
        public static bool CanEdit(Pawn pawn, string id)
        {
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            return CanSet(pawn, id, !GetEnabled(pawn, id), out _);
#else
            SSCAbilityToggleDefinition definition = Find(id);
            return HasStablePersonality(pawn, definition) && Faction.OfPlayer != null && pawn.Faction == Faction.OfPlayer &&
                (definition.EditRule == null || definition.EditRule(pawn));
#endif
        }
        /// <summary>保留旧人格提交签名；只有原组件及 token 一致的人格后端可用，关系项须提供完整 owner 上下文。</summary>
        public static bool TrySetEnabled(Pawn pawn, string id, bool enabled, CompSexSlaveTraining expectedComp, object token)
        {
#if SSC_RW17 || SSC_ABILITY_TOGGLE_FRAMEWORK
            // 旧接口只有人格凭据，不能据此提交需要原身体／关系 owner 的请求。
            var definition = Find(id);
            if (definition == null || definition.Backend != SSCPersonalityAbilityToggleBackend.Instance) return false;
            var context = new SSCAbilityToggleContext(pawn, definition);
            if (!ReferenceEquals(context.Comp, expectedComp) || !ReferenceEquals(context.PersonalityToken, token)) return false;
            return TrySet(pawn, id, enabled, context, out _);
#else
            // 点击可能晚于选中项或人格改变；写入前同时复查原组件、人格 token 和当前权限。
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.TryGetComp<CompSexSlaveTraining>() != expectedComp ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || !CanEdit(pawn, id)) return false;
            if (expectedComp.abilityToggles == null) expectedComp.abilityToggles = new SSCAbilityToggleState();
            expectedComp.abilityToggles.Set(id, enabled);
            SyncMarkers(pawn);
            return pawn.TryGetComp<CompSexSlaveTraining>() == expectedComp && SSCPersonalityRuntimeUtility.IsCurrent(pawn, token);
#endif
        }
        // 清源和植入均替换整个容器；显式关闭必须覆盖宿主开启，不能按 true 或最大值合并。
        /// <summary>只清空通用人格偏好并协调标记；缰绳、身体状态及旧泌乳字段不随此入口重置。</summary>
        public static void ClearPersonalityState(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.abilityToggles = new SSCAbilityToggleState();
            SyncMarkers(pawn);
        }
        /// <summary>用独立副本精确覆盖人格字典，保留显式关闭和未知键，不修改其他后端的保存源。</summary>
        public static void RestorePreference(Pawn pawn, SSCAbilityToggleState snapshot)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.abilityToggles = snapshot?.Copy() ?? new SSCAbilityToggleState();
            SyncMarkers(pawn);
        }

        /// <summary>只在生命周期写入入口调用；自身健康通知合并为有界重评估，不增加 Tick 扫描。</summary>
        public static void SyncMarkers(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null) return;
            // Add/RemoveHediff 会同步回调本入口；将重入合并为待处理标志，限制最多四轮。
            SyncState state = syncing.GetOrCreateValue(pawn);
            state.Pending = true;
            if (state.Running) return;
            state.Running = true;
            try
            {
                for (int attempt = 0; attempt < 4 && state.Pending; attempt++)
                {
                    state.Pending = false;
                    // 未注册标记不可作为旧存档的开启依据；先清孤儿，再按最新偏好修复每项标记。
                    foreach (Hediff orphan in pawn.health.hediffSet.hediffs
                        .Where(h => h is Hediff_SSCToggleAbilityMarker && FindByMarker(h.def) == null).ToList())
                        pawn.health.RemoveHediff(orphan);
                    foreach (SSCAbilityToggleDefinition definition in definitions.ToArray())
                    {
                        // 原缰绳等无标记后端不参与健康状态修复，不能在此创建或搬运其保存值。
                        if (string.IsNullOrEmpty(definition.MarkerDefName)) continue;
                        bool active = IsActive(pawn, definition.Id);
                        Hediff keep = null;
                        foreach (Hediff marker in pawn.health.hediffSet.hediffs
                            .Where(h => h?.def?.defName == definition.MarkerDefName).ToList())
                        {
                            // 每次写入后都重新读最终状态；回调关闭能力时不能留下刚添加的标记。
                            if (active && keep == null && marker is Hediff_SSCToggleAbilityMarker) keep = marker;
                            else if (pawn.health.hediffSet.hediffs.Contains(marker)) pawn.health.RemoveHediff(marker);
                            active = IsActive(pawn, definition.Id);
                        }
                        // 有效能力最多留一个正确类型的实例；缺 Def 时不创建替代数据或更改偏好。
                        if (active && keep == null)
                        {
                            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(definition.MarkerDefName);
                            if (def != null && typeof(Hediff_SSCToggleAbilityMarker).IsAssignableFrom(def.hediffClass))
                            {
                                Hediff marker = HediffMaker.MakeHediff(def, pawn);
                                marker.Severity = 1f;
                                pawn.health.AddHediff(marker);
                                state.Pending = true;
                            }
                        }
                        else if (!active && keep != null && pawn.health.hediffSet.hediffs.Contains(keep))
                        {
                            pawn.health.RemoveHediff(keep);
                            state.Pending = true;
                        }
                    }
                }
            }
            finally { state.Running = false; }
        }
    }
}
