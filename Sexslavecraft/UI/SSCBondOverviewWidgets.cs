// AI 辅助生成。只读展示、固定肖像档位和可见范围；不调用世界候选查询或绑定维护。
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    internal sealed partial class SSCBondOverviewView
    {
        private SSCBondOverviewDisplay display;
        private object displayLanguage;
        private SSCBondOverviewCache<(string, float, GameFont, float), string> truncated;
        partial void ResetPresentation() { display = null; displayLanguage = null; truncated = null; }
        private void CheckLanguage()
        {
            if (ReferenceEquals(displayLanguage, LanguageDatabase.activeLanguage)) return;
            ResetPresentation(); displayLanguage = LanguageDatabase.activeLanguage;
        }
        internal int TruncatedCount => truncated?.Count ?? 0;
        internal string Truncate(string text, float width)
        {
            CheckLanguage();
            // 原版可选字典只按文字索引，不能跨宽度共用；这里同时区分字体与缩放。
            // 缓存属于当前视图，限制条目数，滚动、缩放或窗口尺寸变化不会无限保留旧结果。
            var key = (text, width, Text.Font, Prefs.UIScale);
            if (truncated != null && truncated.TryGet(key, out string result)) return result;
            result = text.Truncate(width);
            (truncated ??= new SSCBondOverviewCache<(string, float, GameFont, float), string>(256)).Add(key, result);
            return result;
        }
        internal SSCBondOverviewDisplay Display
        {
            get
            {
                CheckLanguage();
                if (Snapshot == null || !ReferenceEquals(Snapshot.ViewedMap, Map)) return null;
                if (display == null) display = new SSCBondOverviewDisplay(Snapshot);
                return display;
            }
        }
    }

    /// <summary>小型 FIFO：命中不分配、不更新链表；容量与名单总长度无关。</summary>
    internal sealed class SSCBondOverviewCache<TKey, TValue>
    {
        private readonly int capacity;
        private readonly Dictionary<TKey, TValue> values = new Dictionary<TKey, TValue>();
        private readonly Queue<TKey> order = new Queue<TKey>();
        internal SSCBondOverviewCache(int capacity) { this.capacity = capacity; }
        internal int Count => values.Count;
        internal bool TryGet(TKey key, out TValue value) => values.TryGetValue(key, out value);
        internal void Add(TKey key, TValue value)
        {
            if (values.ContainsKey(key)) return;
            if (values.Count == capacity) values.Remove(order.Dequeue());
            values.Add(key, value); order.Enqueue(key);
        }
    }

    internal sealed class SSCBondOverviewRowText
    {
        internal readonly string Name, Details;
        internal SSCBondOverviewRowText(SSCBondOverviewEntry entry)
        {
            Name = entry.IsDead ? SSCBondOverviewText.Format("DeadName", entry.Name) : entry.Name;
            Details = SSCBondOverviewText.Format("RowDetails", entry.ChainStage,
                SSCBondOverviewText.Specialization(entry.Specialization), SSCBondOverviewText.Location(entry.Location));
        }
    }

    internal sealed class SSCBondOverviewDisplay
    {
        internal readonly SSCBondOverviewSnapshot Snapshot;
        internal readonly string Title, Count, Trainer, CardHeading, WaitingCount, SlaveHeading, TrainerName, TrainerDetails;
        private int pageNumber = -1, pageTotal;
        private string pageLabel;
        internal string PageLabel(int page, int total)
        {
            if (pageNumber != page || pageTotal != total)
            { pageNumber = page; pageTotal = total; pageLabel = (page + 1) + "/" + total; }
            return pageLabel;
        }
        private SSCBondOverviewCache<SSCBondOverviewEntry, SSCBondOverviewRowText> rows;
        internal int RowCount => rows?.Count ?? 0;
        internal SSCBondOverviewRowText Row(SSCBondOverviewEntry entry)
        {
            // 完整候选与排序仍在只读快照中；只有可见行、主人文字或悬停才生成展示文字。
            // 不为全名单分配数组，最多保留 64 行；移出视区后可淘汰，重新进入再生成。
            if (rows != null && rows.TryGet(entry, out SSCBondOverviewRowText text)) return text;
            text = new SSCBondOverviewRowText(entry);
            (rows ??= new SSCBondOverviewCache<SSCBondOverviewEntry, SSCBondOverviewRowText>(64)).Add(entry, text);
            return text;
        }
        internal SSCBondOverviewDisplay(SSCBondOverviewSnapshot snapshot)
        {
            Snapshot = snapshot;
            Title = SSCBondOverviewText.Format("TitleSource", snapshot.Source.Dead
                ? SSCBondOverviewText.Format("DeadName", snapshot.Source.LabelShort) : snapshot.Source.LabelShort);
            Count = snapshot.CoverageComplete ? SSCBondOverviewText.Format("Counts", snapshot.LivingSlaveCount, snapshot.TotalSlaveCount)
                : SSCBondOverviewText.Format("KnownCount", snapshot.TotalSlaveCount);
            SlaveHeading = SSCBondOverviewText.Format(snapshot.CoverageComplete ? "ShortCount" : "KnownCount", snapshot.TotalSlaveCount);
            WaitingCount = Count + " · " + SSCBondOverviewText.T("Waiting");
            CardHeading = snapshot.Master == null ? Count : snapshot.TotalSlaveCount == 0 ? SSCBondOverviewText.T("Master")
                : SSCBondOverviewText.Format("BothCounts", snapshot.LivingSlaveCount, snapshot.TotalSlaveCount);
            Trainer = SSCBondOverviewText.Format("Trainer", snapshot.AssignedTrainer?.LabelShort ?? SSCBondOverviewText.T("None"),
                SSCBondOverviewText.T("Trainer_" + snapshot.AssignedTrainerState));
            TrainerName = snapshot.AssignedTrainer?.LabelShort ?? SSCBondOverviewText.T("None");
            TrainerDetails = Trainer + (snapshot.AssignedTrainerLocation == null ? "" : "\n" + SSCBondOverviewText.Location(snapshot.AssignedTrainerLocation));
        }
    }

    internal static class SSCBondOverviewText
    {
        private static object language;
        private static SSCBondOverviewCache<string, string> translations;
        internal static string T(string key)
        {
            // 仅缓存有限的翻译模板，不保存角色或世界引用；切换语言时整批失效。
            if (translations == null || !ReferenceEquals(language, LanguageDatabase.activeLanguage))
            { translations = new SSCBondOverviewCache<string, string>(128); language = LanguageDatabase.activeLanguage; }
            if (translations.TryGet(key, out string text)) return text;
            text = ("SSC_BondOverview_" + key).Translate(); translations.Add(key, text); return text;
        }
        internal static string Format(string key, params object[] args) => string.Format(T(key), args);
        internal static string Specialization(SexSlaveSpecializationType? type)
        {
            if (!type.HasValue) return T("NoTrainingComponent");
            switch (type.Value)
            {
                case SexSlaveSpecializationType.Bus: return "SSC_ITab_SpecializationBus".Translate();
                case SexSlaveSpecializationType.Cow: return "SSC_ITab_SpecializationCow".Translate();
                case SexSlaveSpecializationType.Combatant: return "SSC_ITab_SpecializationCombatant".Translate();
                case SexSlaveSpecializationType.TrainerOfficer: return "SSC_ITab_SpecializationTrainerOfficer".Translate();
                case SexSlaveSpecializationType.PetCat: return "SSC_ITab_SpecializationPetCat".Translate();
                case SexSlaveSpecializationType.PetDog: return "SSC_ITab_SpecializationPetDog".Translate();
                case SexSlaveSpecializationType.PetRabbit: return "SSC_ITab_SpecializationPetRabbit".Translate();
                default: return "SSC_ITab_SpecializationNone".Translate();
            }
        }
        internal static string Location(SSCBondOverviewLocation location)
        {
            string kind = T("Location_" + location.Kind);
            string name = location.Container?.LabelShort ?? location.Map?.Parent?.LabelCap ?? location.WorldObject?.LabelCap;
            return string.IsNullOrEmpty(name) ? kind : Format("LocationName", kind, name);
        }
    }

    /// <summary>完整高度含组头；以算术索引裁剪，不先枚举全名单再跳过离屏行。</summary>
    internal static class SSCBondOverviewLayout
    {
        internal const float RowHeight = 80, HeaderHeight = 28, CellWidth = 80;
        internal static int Columns(float width) => Math.Max(1, (int)(width / CellWidth));
        internal static int LastPage(int count, int capacity) => capacity <= 0 ? 0 : Math.Max(0, (count - 1) / capacity);
        internal static void VisibleRange(int count, float start, float scroll, float height, out int first, out int end)
        {
            first = Math.Max(0, Math.Min(count, (int)Math.Floor((scroll - start) / RowHeight) - 1));
            end = Math.Max(first, Math.Min(count, (int)Math.Ceiling((scroll + height - start) / RowHeight) + 1));
        }
        internal static int CardCapacity(float width) => Math.Max(0, Math.Min(5, (int)((width - 12) / 44)));
    }

    internal static class SSCBondOverviewWidgets
    {
        /// <summary>保存并恢复调用者 GUI 状态，包括异常退出。</summary>
        internal readonly struct GuiScope : IDisposable
        {
            private readonly Color color;
            private readonly bool enabled, wrap;
            private readonly GameFont font;
            private readonly TextAnchor anchor;
            internal GuiScope(bool initialize)
            {
                color = GUI.color; enabled = GUI.enabled; font = Text.Font; anchor = Text.Anchor; wrap = Text.WordWrap;
                if (initialize) { Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = false; }
            }
            public void Dispose()
            { GUI.color = color; GUI.enabled = enabled; Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap; }
        }

        internal static void Portrait(Rect rect, SSCBondOverviewEntry entry, bool current)
            => Portrait(rect, entry.Pawn, entry.IsDead, current);

        internal static void Portrait(Rect rect, Pawn pawn, bool dead, bool current)
        {
            if (Event.current.type != EventType.repaint || !current) return;
            // 原版缓存拥有纹理；这里只请求两个固定档位，不强制 SetDirty 或自行销毁。
            GUI.DrawTexture(rect, PortraitsCache.Get(pawn, new Vector2(rect.width, rect.height), Rot4.South, cameraZoom: 1.5f));
            if (dead) Widgets.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), SSCBondOverviewText.T("Dead"));
        }

        internal static void Hover(Rect rect, SSCBondOverviewEntry entry, string name, string details)
        {
            if (!Mouse.IsOver(rect)) return;
            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, name + "\n" + details);
            SSCBondOverviewNavigation.Highlight(entry.Location);
        }

        internal static bool Avatar(Rect rect, SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry,
            SSCBondOverviewSession session, SSCBondOverviewView view, bool nameBelow = false)
        {
            bool current = SSCBondOverviewUtility.IsCurrent(snapshot, entry);
            if (!current) session.RequestRefresh(view);
            Portrait(rect, entry, current); Badge(rect, entry.Location, current);
            // 缩略图只在总览显示姓名，阶段、特化和具体位置留给悬停；离屏对象不格式化。
            if (nameBelow) Widgets.Label(new Rect(rect.x, rect.yMax, rect.width + 16, 20), view.Truncate(view.Display.Row(entry).Name, rect.width + 16));
            if (!Mouse.IsOver(rect)) return false;
            if (!current) { TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("RelationChanged")); return false; }
            SSCBondOverviewRowText text = view.Display.Row(entry);
            Hover(rect, entry, text.Name, text.Details);
            bool available = SSCBondOverviewNavigation.CanPreview(entry.Location, out _);
            // 首屏未建立飞船索引并不代表无法定位；点击沿原导航路径重新解析位置并复查关系。
            if (!available) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("ResolveOnClick"));
            return GUI.enabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
        }

        internal static bool TrainerAvatar(Rect rect, SSCBondOverviewSnapshot snapshot, SSCBondOverviewSession session,
            SSCBondOverviewView view, bool nameBelow = false)
        {
            bool current = SSCBondOverviewNavigation.IsTrainerCurrent(snapshot);
            if (!current) session.RequestRefresh(view);
            Portrait(rect, snapshot.AssignedTrainer, snapshot.AssignedTrainer?.Dead == true, current);
            Badge(rect, snapshot.AssignedTrainerLocation, current);
            if (nameBelow) Widgets.Label(new Rect(rect.x, rect.yMax, rect.width + 16, 20), view.Truncate(view.Display.TrainerName, rect.width + 16));
            if (!Mouse.IsOver(rect)) return false;
            if (!current) { TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("RelationChanged")); return false; }
            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, view.Display.TrainerName + "\n" + view.Display.TrainerDetails);
            SSCBondOverviewNavigation.Highlight(snapshot.AssignedTrainerLocation);
            bool available = SSCBondOverviewNavigation.CanPreview(snapshot.AssignedTrainerLocation, out _);
            if (!available) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("ResolveOnClick"));
            return GUI.enabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
        }

        private static void Badge(Rect rect, SSCBondOverviewLocation location, bool current)
        {
            // 正常同图不加说明；只用小标记提示异常、容器或异地，全文放在悬停中。
            string marker = !current ? "!" : location == null || location.Kind == SSCBondOverviewLocationKind.Unknown ? "?"
                : location.Kind == SSCBondOverviewLocationKind.MapContainer ? "□"
                : !ReferenceEquals(location.Map, Find.CurrentMap) ? "↗" : null;
            if (marker != null) Widgets.Label(new Rect(rect.xMax - 12, rect.y, 12, 18), marker);
        }
    }
}
