using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCRestrictionSource { Saved, Specialization, Equipment, SystemDisabled }

    public sealed class SSCRestrictionResolution
    {
        public SSCRestrictionRule Rule { get; internal set; }
        public SSCRestrictionValue SavedValue { get; internal set; }
        public SSCRestrictionValue Value { get; internal set; }
        public SSCRestrictionSource Source { get; internal set; }
        public string SourceDef { get; internal set; }
        public bool Valid { get; internal set; }
    }

    /// <summary>纯读取条目来源。绝不协调身份、绑定、特化或创建 Pawn 配置。</summary>
    public static class SSCRestrictionResolver
    {
        /// <summary>以性奴身份或已有绑定关系判断是否受系统管理；仅持有训练组件不代表适用。</summary>
        public static bool IsApplicable(Pawn pawn)
        {
            return pawn != null && (pawn.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity == PawnIdentity.Slave ||
                SSCBondUtility.GetBoundMaster(pawn) != null);
        }

        /// <summary>检查当前特化是否匹配配置；巴士也认可已有巴士健康状态，查询不修正角色状态。</summary>
        public static bool HasProfile(Pawn pawn, SSCRestrictionProfileDef profile)
        {
            if (pawn == null || profile == null || profile.specialization == SexSlaveSpecializationType.None) return false;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            return comp?.specializationType == profile.specialization ||
                (profile.specialization == SexSlaveSpecializationType.Bus && BusSpecializationUtility.HasAnyBusState(pawn));
        }

        /// <summary>只创建独立值，不写回 Pawn。阶段 2 的生命周期入口负责决定何时调用并保存。</summary>
        public static SSCRestrictionConfig CreateInitialConfiguration(Pawn pawn, SSCRestrictionRules template)
        {
            var config = new SSCRestrictionConfig { rules = (template ?? new SSCRestrictionRules()).Copy() };
            var profiles = DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Where(p => HasProfile(pawn, p)).ToList();
            foreach (SSCRestrictionRule rule in Enum.GetValues(typeof(SSCRestrictionRule)))
            {
                SSCRestrictionResolution value = FromSaved(config.rules, rule);
                ApplyLayer(value, profiles.Select(p => new Entry(p.defName, p.defaults)), SSCRestrictionSource.Specialization);
                if (!value.Valid) throw new InvalidOperationException("Invalid restriction defaults: " + value.SourceDef + "/" + rule);
                config.rules.Set(rule, value.Value);
            }
            config.busDefaultsApplied = profiles.Any(p => p.specialization == SexSlaveSpecializationType.Bus);
            return config;
        }

        /// <summary>界面和许可入口共用条目解析：装备 > 特化 > 保存值；系统停用时不执行强制。</summary>
        public static SSCRestrictionResolution Resolve(Pawn pawn, SSCRestrictionRules saved, SSCRestrictionRule rule)
        {
            SSCRestrictionResolution result = FromSaved(saved, rule);
            if (SSCMod.settings != null && !SSCMod.settings.enableSexSlaveProtectionRules)
            {
                result.Value = SSCRestrictionValue.Allow;
                result.Source = SSCRestrictionSource.SystemDisabled;
                result.Valid = true;
                return result;
            }
            if (!result.Valid) return result;
            if (SSCMod.settings?.enableSpecializationRestrictionOverrides ?? true)
                ApplyLayer(result, DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading
                    .Where(p => HasProfile(pawn, p)).Select(p => new Entry(p.defName, p.forced)), SSCRestrictionSource.Specialization);

            if (pawn?.apparel != null)
                ApplyLayer(result, pawn.apparel.WornApparel.Select(a => new Entry(a.def.defName,
                    a.def.GetModExtension<SSCRestrictionEquipmentExtension>()?.forced)), SSCRestrictionSource.Equipment);
            return result;
        }

        /// <summary>以保存值创建解析结果并校验合法性；缺失规则标记无效，不生成或回写默认配置。</summary>
        private static SSCRestrictionResolution FromSaved(SSCRestrictionRules saved, SSCRestrictionRule rule)
        {
            SSCRestrictionValue value = saved?.Get(rule) ?? SSCRestrictionValue.Unspecified;
            return new SSCRestrictionResolution
            {
                Rule = rule, SavedValue = value, Value = value, Source = SSCRestrictionSource.Saved,
                Valid = SSCRestrictionRules.IsValid(rule, value)
            };
        }

        private sealed class Entry
        {
            public readonly string DefName;
            public readonly SSCRestrictionOverrides Values;
            /// <summary>关联覆盖条目与来源定义名，供同层排序及诊断显示使用。</summary>
            public Entry(string defName, SSCRestrictionOverrides values) { DefName = defName; Values = values; }
        }

        /// <summary>应用一层稀疏覆盖：已声明项替换下层值，同层取最严格值，平局按定义名排序。</summary>
        /// <remarks>覆盖可放宽下层限制；非法条目标记结果无效并记录来源，未声明项不改变结果。</remarks>
        private static void ApplyLayer(SSCRestrictionResolution result, IEnumerable<Entry> entries, SSCRestrictionSource source)
        {
            Entry winner = null;
            SSCRestrictionValue chosen = SSCRestrictionValue.Unspecified;
            foreach (Entry entry in entries.OrderBy(e => e.DefName, StringComparer.Ordinal))
            {
                SSCRestrictionValue value = entry.Values?.Get(result.Rule) ?? SSCRestrictionValue.Unspecified;
                if (value == SSCRestrictionValue.Unspecified) continue;
                if (!SSCRestrictionRules.IsValid(result.Rule, value))
                {
                    result.Valid = false;
                    result.Source = source;
                    result.SourceDef = entry.DefName;
                    return;
                }
                if (winner == null || value < chosen) { winner = entry; chosen = value; }
            }
            if (winner == null) return;
            result.Value = chosen;
            result.Source = source;
            result.SourceDef = winner.DefName;
        }
    }
}
