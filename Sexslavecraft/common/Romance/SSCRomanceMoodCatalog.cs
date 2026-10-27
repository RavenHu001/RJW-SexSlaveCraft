// AI 辅助生成：综合心情只读目录与保存契约；不在目录中执行实际互动。
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SexSlaveCraft
{
    /// <summary>一个不可变终端分支；文案身份与数值分离，相同数值不合并分支。</summary>
    public sealed class SSCRomanceMoodBranch
    {
        public string Id { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public SSCRomanceCompositeEvent Event { get; }
        public SSCRomanceControlState Control { get; }
        public int Stage { get; }
        public bool Masochist { get; }
        public SSCRomanceOpinionBand? Opinion { get; }
        public int Value { get; }
        public int LongDays { get; }
        public bool IsContinuous => Control != SSCRomanceControlState.None;

        /// <summary>目录构建入口；稳定 ID 派生两份独立文案键，不使用运行时人物数据。</summary>
        internal SSCRomanceMoodBranch(string id, SSCRomanceCompositeEvent kind, SSCRomanceControlState control,
            int stage, bool masochist, SSCRomanceOpinionBand? opinion, int value, int longDays)
        {
            Id = id; Event = kind; Control = control; Stage = stage; Masochist = masochist;
            Opinion = opinion; Value = value; LongDays = longDays;
            string prefix = "SSC_RomanceMood_" + id.Replace('-', '_');
            NameKey = prefix + "_Name"; DescriptionKey = prefix + "_Description";
        }
    }

    /// <summary>一次纯目录解析结果；零值也是成功解析，但没有可发放的记忆。</summary>
    public sealed class SSCRomanceMoodSelection
    {
        public SSCRomanceMoodBranch Branch { get; }
        public int RawOpinion { get; }
        public SSCRomanceMoodGroup Group { get; }
        /// <summary>零值也保留事件语义，供结清同组及关系阶段清理使用。</summary>
        public SSCRomanceSemanticGroup SemanticGroup => SSCRomanceMoodSemantics.Of(Branch.Event);
        public int DurationTicks { get; }
        public bool HasMemory => Group != SSCRomanceMoodGroup.None;

        /// <summary>先确定最终分支，再用原始好感决定期限；一般档跨零不增加第四个档位。</summary>
        internal SSCRomanceMoodSelection(SSCRomanceMoodBranch branch, int opinion)
        {
            Branch = branch; RawOpinion = opinion;
            if (branch.Value == 0) { Group = SSCRomanceMoodGroup.None; return; }
            bool longTerm = branch.LongDays > 0 && branch.Value < 0 && opinion > 0;
            Group = longTerm ? SSCRomanceMoodGroup.Long : SSCRomanceMoodGroup.Short;
            DurationTicks = (longTerm ? branch.LongDays : 3) * 60000;
        }

        /// <summary>构造独立快照；保留事件确认顺序和冻结关系／主人／规则，不在纯目录查询时签发顺序。</summary>
        public SSCRomanceMoodData CreateSnapshot(SSCRomanceMoodData context = null) => new SSCRomanceMoodData
        {
            DataVersion = SSCRomanceMoodData.CompositeVersion, BranchId = Branch.Id,
            CompositeEvent = Branch.Event, Stage = Branch.Stage, Masochist = Branch.Masochist,
            HasRawOpinion = true, RawOpinion = RawOpinion, OpinionBand = Branch.Opinion.Value,
            Value = Branch.Value, Group = Group, NominalDurationTicks = DurationTicks,
            Relation = context?.Relation ?? SSCRomanceOldRelation.None, Master = context?.Master,
            Rule = context?.Rule ?? default, HasRule = context?.HasRule ?? false,
            OccurrenceOrder = context?.OccurrenceOrder ?? 0L
        };
    }

    /// <summary>完整分支表的 108 个事件与 14 个持续分支；没有加法修正或人物状态查询。</summary>
    public static class SSCRomanceMoodCatalog
    {
        private static readonly Dictionary<string, SSCRomanceMoodBranch> byId = new Dictionary<string, SSCRomanceMoodBranch>(StringComparer.Ordinal);
        private static readonly Dictionary<(SSCRomanceCompositeEvent, int, bool, SSCRomanceOpinionBand), SSCRomanceMoodBranch> events =
            new Dictionary<(SSCRomanceCompositeEvent, int, bool, SSCRomanceOpinionBand), SSCRomanceMoodBranch>();
        private static readonly Dictionary<(SSCRomanceControlState, int, bool), SSCRomanceMoodBranch> controls =
            new Dictionary<(SSCRomanceControlState, int, bool), SSCRomanceMoodBranch>();
        public static ReadOnlyCollection<SSCRomanceMoodBranch> EventBranches { get; }
        public static ReadOnlyCollection<SSCRomanceMoodBranch> ControlBranches { get; }

        /// <summary>按事件→阶段→有效特质→好感的顺序显式登记最终值，不把差异压成统一修正。</summary>
        static SSCRomanceMoodCatalog()
        {
            var eventList = new List<SSCRomanceMoodBranch>();
            AddEvent(eventList, 1, 0, new[] { -6,-2,2, -2,2,5, -1,4,8, 4,8,12 });
            AddEvent(eventList, 2, 0, new[] { -9,-4,3, -5,0,6, -3,3,9, 2,8,14 });
            AddEvent(eventList, 3, 0, new[] { 0,-2,-6, 2,0,-3, 3,1,-2, 7,4,2 });
            AddEvent(eventList, 4, 0, new[] { -1,-4,-9, 2,-1,-6, 3,0,-5, 7,4,-1 });
            AddEvent(eventList, 5, 25, new[] { -1,-7,-15, 2,-4,-12, 3,-3,-11, 7,1,-7 });
            AddEvent(eventList, 6, 0, new[] { -3,3,10, 3,9,15 });
            AddEvent(eventList, 7, 0, new[] { 0,-3,-7, 4,1,-3 });
            AddEvent(eventList, 8, 0, new[] { -5,4,11, 2,10,15 });
            AddEvent(eventList, 9, 25, new[] { -1,-7,-13, 3,-2,-8 });
            AddEvent(eventList, 10, 25, new[] { -1,-9,-15, 3,-4,-11 });
            AddEvent(eventList, 11, 25, new[] { 2,-4,-9, 7,1,-5 });
            AddEvent(eventList, 12, 25, new[] { 2,-6,-12, 7,-1,-7 });
            AddEvent(eventList, 13, 30, new[] { 2,-8,-15, 7,-3,-11 });
            EventBranches = eventList.AsReadOnly();

            var controlList = new List<SSCRomanceMoodBranch>();
            AddControl(controlList, SSCRomanceControlState.Active, 2, -3, -1);
            AddControl(controlList, SSCRomanceControlState.Active, 3, -1, 1);
            AddControl(controlList, SSCRomanceControlState.Active, 4, 1, 3);
            AddControl(controlList, SSCRomanceControlState.Passive, 3, -2, 0);
            AddControl(controlList, SSCRomanceControlState.Passive, 4, 0, 2);
            AddControl(controlList, SSCRomanceControlState.Both, 3, -3, -1);
            AddControl(controlList, SSCRomanceControlState.Both, 4, 2, 4);
            ControlBranches = controlList.AsReadOnly();
        }

        /// <summary>三档好感；原始值仍保留给期限判断，不使用旧版档位或恶堕修正。</summary>
        public static SSCRomanceOpinionBand ClassifyOpinion(int opinion) => opinion <= -10 ? SSCRomanceOpinionBand.Disliked :
            opinion < 60 ? SSCRomanceOpinionBand.General : SSCRomanceOpinionBand.Close;

        /// <summary>无效事件／未解锁阶段返回 false，合法零值返回 true；不接受未知版本降级。</summary>
        public static bool TryResolve(SSCRomanceCompositeEvent kind, int stage, bool effectiveMasochist, int rawOpinion, out SSCRomanceMoodSelection selection)
        {
            selection = null;
            if (!events.TryGetValue((kind, stage, effectiveMasochist, ClassifyOpinion(rawOpinion)), out var branch)) return false;
            selection = new SSCRomanceMoodSelection(branch, rawOpinion); return true;
        }

        /// <summary>持续状态没有对象，不使用好感；零值持续分支仍是有效分支。</summary>
        public static bool TryResolveControl(SSCRomanceControlState state, int stage, bool effectiveMasochist, out SSCRomanceMoodBranch branch)
            => controls.TryGetValue((state, stage, effectiveMasochist), out branch);

        /// <summary>稳定 ID 严格匹配；空值、未知 ID 不映射为普通／一般或零值。</summary>
        public static bool TryGet(string id, out SSCRomanceMoodBranch branch)
        { branch = null; return id != null && byId.TryGetValue(id, out branch); }

        /// <summary>读取已保存身份，不用目录当前数值重算历史；版本／结构不完整时拒绝解释为新分支。</summary>
        public static bool TryReadSnapshot(SSCRomanceMoodData data, out SSCRomanceMoodBranch branch)
        {
            branch = null;
            if (data == null || !data.IsComposite || !data.HasRawOpinion || !TryGet(data.BranchId, out var saved) || saved.IsContinuous ||
                saved.Event != data.CompositeEvent || saved.Stage != data.Stage || saved.Masochist != data.Masochist ||
                saved.Opinion != data.OpinionBand || ClassifyOpinion(data.RawOpinion) != data.OpinionBand || data.Value < -15 || data.Value > 15) return false;
            // 校验保存结构而非重新套用当前数值表；以后平衡更新不能改写既有基准值。
            bool valid = data.Value == 0 ? data.Group == SSCRomanceMoodGroup.None && data.NominalDurationTicks == 0 :
                data.Group == SSCRomanceMoodGroup.Short ? data.NominalDurationTicks == 3 * 60000 && !(saved.LongDays > 0 && data.RawOpinion > 0 && data.Value < 0) :
                data.Group == SSCRomanceMoodGroup.Long && saved.LongDays > 0 && data.RawOpinion > 0 && data.Value < 0 &&
                    data.NominalDurationTicks == saved.LongDays * 60000;
            if (!valid) return false;
            branch = saved; return true;
        }

        /// <summary>按固定顺序登记一行三个好感值；构建时检查目录自身是否重复或超出约定范围。</summary>
        private static void AddEvent(List<SSCRomanceMoodBranch> list, int eventNumber, int longDays, int[] values)
        {
            int offset = 0;
            for (int stage = eventNumber <= 5 ? 3 : 4; stage <= 4; stage++)
            foreach (bool maso in new[] { false, true })
            for (int band = 0; band < 3; band++)
            {
                int value = values[offset++];
                if (value < -15 || value > 15) throw new InvalidOperationException("SSC composite mood value outside contract.");
                string id = "E" + eventNumber.ToString("00", CultureInfo.InvariantCulture) + "-S" + stage.ToString(CultureInfo.InvariantCulture) + "-" + (maso ? "M" : "N") + "-" + "DGC"[band];
                var branch = new SSCRomanceMoodBranch(id, (SSCRomanceCompositeEvent)eventNumber, SSCRomanceControlState.None, stage, maso, (SSCRomanceOpinionBand)band, value, longDays);
                byId.Add(id, branch); events.Add((branch.Event, stage, maso, branch.Opinion.Value), branch); list.Add(branch);
            }
            if (offset != values.Length) throw new InvalidOperationException("SSC composite mood row length mismatch.");
        }

        /// <summary>持续思想一行两个有效特质值；ID 与文案也独立，不按数值建立缓存身份。</summary>
        private static void AddControl(List<SSCRomanceMoodBranch> list, SSCRomanceControlState state, int stage, int ordinary, int maso)
        {
            string stateId = state == SSCRomanceControlState.Active ? "A" : state == SSCRomanceControlState.Passive ? "B" : "AB";
            foreach (bool effective in new[] { false, true })
            {
                string id = "C-" + stateId + "-S" + stage.ToString(CultureInfo.InvariantCulture) + "-" + (effective ? "M" : "N");
                var branch = new SSCRomanceMoodBranch(id, SSCRomanceCompositeEvent.None, state, stage, effective, null, effective ? maso : ordinary, 0);
                byId.Add(id, branch); controls.Add((state, stage, effective), branch); list.Add(branch);
            }
        }
    }
}
