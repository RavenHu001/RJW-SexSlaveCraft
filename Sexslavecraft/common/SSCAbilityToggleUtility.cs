// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCAbilityToggleGroup { Master, Slave }

    /// <summary>声明一项随人格流转的个人开关；能力效果由各自的实际事件入口处理。</summary>
    public sealed class SSCAbilityToggleDefinition
    {
        public readonly string Id, LabelKey, TipKey, MarkerDefName;
        public readonly SSCAbilityToggleGroup Group;
        public readonly PawnIdentity Identity;
        public readonly bool DefaultEnabled;
        public readonly Func<Pawn, bool> ActivationRule, EditRule;
        public SSCAbilityToggleDefinition(string id, SSCAbilityToggleGroup group, PawnIdentity identity,
            string labelKey, string tipKey, string markerDefName = null, bool defaultEnabled = false,
            Func<Pawn, bool> activationRule = null, Func<Pawn, bool> editRule = null)
        {
            Id = id; Group = group; Identity = identity; LabelKey = labelKey; TipKey = tipKey;
            MarkerDefName = markerDefName; DefaultEnabled = defaultEnabled;
            ActivationRule = activationRule; EditRule = editRule;
        }
    }

    public static class SSCAbilityToggleUtility
    {
        public const string MasterDesireInfestation = "SSC_MasterDesireInfestation";
        public const string InfestationMarker = "SSC_Hediff_MasterDesireInfestation";
        // 注册表只声明能力及可选规则，不保存每个 Pawn 的开启值，也不在这里发放能力收益。
        private static readonly List<SSCAbilityToggleDefinition> definitions = new List<SSCAbilityToggleDefinition>
        {
            new SSCAbilityToggleDefinition(MasterDesireInfestation, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
                "SSC_AbilityToggle_MasterDesireInfestation_Label", "SSC_AbilityToggle_MasterDesireInfestation_Tip", InfestationMarker)
        };
        private static readonly IReadOnlyList<SSCAbilityToggleDefinition> readOnlyDefinitions = definitions.AsReadOnly();
        public static IReadOnlyList<SSCAbilityToggleDefinition> Definitions => readOnlyDefinitions;
        private sealed class SyncState { public bool Running, Pending; }
        private static readonly ConditionalWeakTable<Pawn, SyncState> syncing = new ConditionalWeakTable<Pawn, SyncState>();

        public static void Register(SSCAbilityToggleDefinition definition)
        {
            // 标记与能力一一对应，防止两个定义争抢同一个健康实例；UI 读取只读注册表视图。
            if (definition == null || string.IsNullOrEmpty(definition.Id) ||
                definitions.Any(d => d.Id == definition.Id || (!string.IsNullOrEmpty(definition.MarkerDefName) &&
                    d.MarkerDefName == definition.MarkerDefName)))
                throw new ArgumentException("Ability toggle IDs and marker definitions must be unique.");
            definitions.Add(definition);
        }
        public static SSCAbilityToggleDefinition Find(string id) => definitions.FirstOrDefault(d => d.Id == id);
        public static SSCAbilityToggleDefinition FindByMarker(HediffDef def)
            => def == null ? null : definitions.FirstOrDefault(d => !string.IsNullOrEmpty(d.MarkerDefName) && d.MarkerDefName == def.defName);
        public static bool IsMarkerDef(HediffDef def)
            => def != null && (FindByMarker(def) != null ||
                (def.hediffClass != null && typeof(Hediff_SSCToggleAbilityMarker).IsAssignableFrom(def.hediffClass)));

        // 偏好、实际生效与玩家编辑权限分别查询；退出身份时仍能保存原始偏好。
        public static bool GetEnabled(Pawn pawn, string id)
        {
            SSCAbilityToggleDefinition definition = Find(id);
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return definition != null && comp != null && (comp.abilityToggles
                ?.Get(id, definition.DefaultEnabled) ?? definition.DefaultEnabled);
        }
        private static bool HasStablePersonality(Pawn pawn, SSCAbilityToggleDefinition definition)
        {
            // 人格替换期间全部能力暂停；主人资格、成长记录和缰绳不是通用开关的前置。
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            return definition != null && comp != null && !pawn.Dead && !pawn.Destroyed && !pawn.Discarded &&
                pawn.health?.hediffSet != null && pawn.RaceProps?.Humanlike == true &&
                comp.pawnIdentity == definition.Identity && !SSCMasterProgressionUtility.IsHollow(pawn) &&
                !SSCPersonalityRuntimeUtility.IsMutating(pawn);
        }
        public static bool IsActive(Pawn pawn, string id)
        {
            SSCAbilityToggleDefinition definition = Find(id);
            return HasStablePersonality(pawn, definition) && GetEnabled(pawn, id) &&
                (definition.ActivationRule == null || definition.ActivationRule(pawn));
        }
        public static bool CanEdit(Pawn pawn, string id)
        {
            SSCAbilityToggleDefinition definition = Find(id);
            return HasStablePersonality(pawn, definition) && Faction.OfPlayer != null && pawn.Faction == Faction.OfPlayer &&
                (definition.EditRule == null || definition.EditRule(pawn));
        }
        public static bool TrySetEnabled(Pawn pawn, string id, bool enabled, CompSexSlaveTraining expectedComp, object token)
        {
            // 点击可能晚于选中项或人格改变；写入前同时复查原组件、人格 token 和当前权限。
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.TryGetComp<CompSexSlaveTraining>() != expectedComp ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || !CanEdit(pawn, id)) return false;
            if (expectedComp.abilityToggles == null) expectedComp.abilityToggles = new SSCAbilityToggleState();
            expectedComp.abilityToggles.Set(id, enabled);
            SyncMarkers(pawn);
            return pawn.TryGetComp<CompSexSlaveTraining>() == expectedComp && SSCPersonalityRuntimeUtility.IsCurrent(pawn, token);
        }
        // 清源和植入均替换整个容器；显式关闭必须覆盖宿主开启，不能按 true 或最大值合并。
        public static void ClearPersonalityState(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.abilityToggles = new SSCAbilityToggleState();
            SyncMarkers(pawn);
        }
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
