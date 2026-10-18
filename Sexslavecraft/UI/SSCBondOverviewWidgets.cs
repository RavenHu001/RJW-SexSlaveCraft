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
        private string tooltip;
        // 仅首次悬停时合并完整提示，后续 GUI 事件复用；仍受 64 行缓存和语言／快照失效约束。
        internal string Tooltip => tooltip ?? (tooltip = Name + "\n" + Details);
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
        internal readonly string Title, Count, Trainer, SlaveHeading, OwnedHeading, OwnedDetails, TrainerName, TrainerDetails;
        private readonly string ownedPrefix, ownedNumber;
        private string ownedTitle;
        private float ownedWidth = -1, ownedScale;
        private GameFont ownedFont;
        internal string OwnedTitle(SSCBondOverviewView view, float width)
        {
            // 数量是紧凑卡片的主要信息：完整标题放不下时只省略栏目文字，不截掉末尾数字。
            // 每份展示仅缓存当前宽度／字体／缩放的结果；快照或语言变化由原展示失效路径处理。
            if (ownedTitle != null && ownedWidth == width && ownedFont == Text.Font && ownedScale == Prefs.UIScale)
                return ownedTitle;
            ownedWidth = width; ownedFont = Text.Font; ownedScale = Prefs.UIScale;
            ownedTitle = view.Truncate(OwnedHeading, width);
            if (ownedTitle == OwnedHeading) return ownedTitle;
            float prefixWidth = Math.Max(0, width - Text.CalcSize(ownedNumber).x);
            string prefix = view.Truncate(ownedPrefix, prefixWidth);
            // 原版省略号本身也可能放不下；此时优先只显示数量，完整栏目仍在悬停中。
            if (Text.CalcSize(prefix).x > prefixWidth) prefix = string.Empty;
            return ownedTitle = prefix + ownedNumber;
        }
        private string trainerTooltip;
        internal string TrainerTooltip => trainerTooltip ?? (trainerTooltip = TrainerName + "\n" + TrainerDetails);
        private float gridWidth = -1, gridHeight = -1;
        private SSCBondOverviewLayout.Grid grid;
        internal SSCBondOverviewLayout.Grid Grid(float width, float height)
        {
            if (gridWidth != width || gridHeight != height)
            { gridWidth = width; gridHeight = height; grid = SSCBondOverviewLayout.Fit(Snapshot.TotalSlaveCount, width, height); }
            return grid;
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
            Title = SSCBondOverviewText.Format("TitleSource", snapshot.SourceDead
                ? SSCBondOverviewText.Format("DeadName", snapshot.SourceName) : snapshot.SourceName);
            // 数量始终表示当前缰绳中经实际锁链校验的绑定，不再存在等待全局补查的数量状态。
            Count = SSCBondOverviewText.Format("Counts", snapshot.LivingSlaveCount, snapshot.TotalSlaveCount);
            SlaveHeading = SSCBondOverviewText.Format("ShortCount", snapshot.TotalSlaveCount);
            OwnedHeading = SSCBondOverviewText.Format("OwnedCount", snapshot.TotalSlaveCount);
            ownedPrefix = SSCBondOverviewText.Format("OwnedCount", string.Empty);
            ownedNumber = snapshot.TotalSlaveCount.ToString();
            OwnedDetails = OwnedHeading + "\n" + Count;
            Trainer = SSCBondOverviewText.Format("Trainer", snapshot.TrainerName ?? SSCBondOverviewText.T("None"),
                SSCBondOverviewText.T("Trainer_" + snapshot.AssignedTrainerState));
            TrainerName = snapshot.TrainerName ?? SSCBondOverviewText.T("None");
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
            string name = location.Name;
            return string.IsNullOrEmpty(name) ? kind : Format("LocationName", kind, name);
        }
    }

    /// <summary>完整高度含组头；以算术索引裁剪，不先枚举全名单再跳过离屏行。</summary>
    internal static class SSCBondOverviewLayout
    {
        internal const float RowHeight = 80, HeaderHeight = 28, CellWidth = 80;
        internal static int Columns(float width) => Math.Max(1, (int)(width / CellWidth));
        internal static void VisibleRange(int count, float start, float scroll, float height, out int first, out int end)
        {
            first = Math.Max(0, Math.Min(count, (int)Math.Floor((scroll - start) / RowHeight) - 1));
            end = Math.Max(first, Math.Min(count, (int)Math.Ceiling((scroll + height - start) / RowHeight) + 1));
        }
        internal readonly struct Grid
        {
            internal readonly float Side;
            internal readonly int Columns, Rows;
            internal Grid(float side, int columns, int rows) { Side = side; Columns = columns; Rows = rows; }
        }
        internal static Grid Fit(int count, float width, float height)
        {
            if (count <= 0 || width <= 0 || height <= 0) return default;
            // 与原版一样随人数收缩网格：在有限高度内寻找能容纳全部头像的最大整数边长。
            // 极端数量仍可使用亚像素布局，肖像请求另有固定分辨率，不申请零尺寸纹理。
            for (int side = (int)Math.Min(width, height); side >= 1; side--)
            {
                int columns = Math.Max(1, (int)(width / side)), rows = (int)Math.Ceiling((double)count / columns);
                if (rows * (double)side <= height) return new Grid(side, Math.Min(count, columns), rows);
            }
            int smallColumns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count * (double)width / height)));
            int smallRows = (int)Math.Ceiling((double)count / smallColumns);
            return new Grid(Math.Min(width / smallColumns, height / smallRows), smallColumns, smallRows);
        }
    }

    internal static class SSCBondOverviewWidgets
    {
        /// <summary>原版按钮底框配共享图标；操作仍由宿主延后分派，悬停文字不常驻占位。</summary>
        internal static bool IconButton(Rect rect, Texture2D icon, string tooltip, bool available = true)
        {
            // 本机 1.7 GUI.Button 在按下时捕获、松开时返回 true；必须在调用前记录松开事件，
            // 否则事件被原版 Use 后无法判断，或与 mouseDown 条件互斥而丢弃所有点击。
            bool left = GUI.enabled && available && Event.current.type == EventType.mouseUp && Event.current.button == 0;
            bool clicked = SSCUIWidgets.Button(rect, string.Empty, available);
            using var scope = new GuiScope(false);
            GUI.color = GUI.enabled && available ? Color.white : new Color(.45f, .45f, .45f);
            float inset = Math.Min(4, Math.Min(rect.width, rect.height) * .2f);
            float side = Math.Max(0, Math.Min(rect.width, rect.height) - inset * 2);
            GUI.DrawTexture(new Rect(rect.x + (rect.width - side) * .5f, rect.y + (rect.height - side) * .5f, side, side), icon);
            TooltipHandler.TipRegion(rect, tooltip);
            return left && clicked;
        }

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
            float capture = rect.width > 40 ? 56 : 40;
            GUI.DrawTexture(rect, PortraitsCache.Get(pawn, new Vector2(capture, capture), Rot4.South, cameraZoom: 1.5f));
            if (dead && rect.width >= 24) Widgets.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), SSCBondOverviewText.T("Dead"));
        }

        internal static void Hover(Rect rect, SSCBondOverviewEntry entry, string tooltip)
        {
            if (!Mouse.IsOver(rect)) return;
            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, tooltip);
            SSCBondOverviewNavigation.Highlight(entry.Location);
        }

        internal static bool Avatar(Rect rect, SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry,
            SSCBondOverviewSession session, SSCBondOverviewView view, bool nameBelow = false, Rect? interactionRect = null)
        {
            bool current = SSCBondOverviewUtility.IsCurrent(snapshot, entry);
            if (!current) session.RequestRefresh(view);
            Portrait(rect, entry, current); Badge(rect, entry.Location, current);
            // 缩略图只在总览显示姓名，阶段、特化和具体位置留给悬停；离屏对象不格式化。
            if (nameBelow) Widgets.Label(new Rect(rect.x, rect.yMax, rect.width + 16, 20), view.Truncate(view.Display.Row(entry).Name, rect.width + 16));
            Rect hit = interactionRect ?? rect;
            if (!Mouse.IsOver(hit)) return false;
            if (!current) { TooltipHandler.TipRegion(hit, SSCBondOverviewText.T("RelationChanged")); return false; }
            SSCBondOverviewRowText text = view.Display.Row(entry);
            Hover(hit, entry, text.Tooltip);
            bool available = SSCBondOverviewNavigation.CanPreview(entry.Location, out _);
            // 首屏未建立飞船索引并不代表无法定位；点击沿原导航路径重新解析位置并复查关系。
            if (!available) TooltipHandler.TipRegion(hit, SSCBondOverviewText.T("ResolveOnClick"));
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
            TooltipHandler.TipRegion(rect, view.Display.TrainerTooltip);
            SSCBondOverviewNavigation.Highlight(snapshot.AssignedTrainerLocation);
            bool available = SSCBondOverviewNavigation.CanPreview(snapshot.AssignedTrainerLocation, out _);
            if (!available) TooltipHandler.TipRegion(rect, SSCBondOverviewText.T("ResolveOnClick"));
            return GUI.enabled && Event.current.type == EventType.mouseDown && Event.current.button == 0;
        }

        private static void Badge(Rect rect, SSCBondOverviewLocation location, bool current)
        {
            if (rect.width < 16) return; // 小网格不让固定字号标记溢出并遮挡相邻头像；详情仍在悬停。
            // 正常同图不加说明；只用小标记提示异常、容器或异地，全文放在悬停中。
            string marker = !current ? "!" : location == null || location.Kind == SSCBondOverviewLocationKind.Unknown ? "?"
                : location.Kind == SSCBondOverviewLocationKind.MapContainer ? "□"
                : !ReferenceEquals(location.Map, Find.CurrentMap) ? "↗" : null;
            if (marker != null) Widgets.Label(new Rect(rect.xMax - 12, rect.y, 12, 18), marker);
        }
    }
}
