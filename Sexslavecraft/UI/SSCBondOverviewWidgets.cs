// AI 辅助生成。只读展示、固定肖像档位和可见范围；不调用世界候选查询或绑定维护。
using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    internal sealed partial class SSCBondOverviewView
    {
        private SSCBondOverviewDisplay display;
        private object displayLanguage;
        partial void ResetPresentation() { display = null; displayLanguage = null; }
        internal SSCBondOverviewDisplay Display
        {
            get
            {
                if (Snapshot == null) return null;
                if (display == null || !ReferenceEquals(displayLanguage, LanguageDatabase.activeLanguage))
                { display = new SSCBondOverviewDisplay(Snapshot); displayLanguage = LanguageDatabase.activeLanguage; }
                return display;
            }
        }
    }

    internal sealed class SSCBondOverviewDisplay
    {
        internal readonly SSCBondOverviewSnapshot Snapshot;
        internal readonly string Title, Count, Trainer;
        internal readonly string[] Names, Details;
        internal readonly string MasterName, MasterDetails;
        internal SSCBondOverviewDisplay(SSCBondOverviewSnapshot snapshot)
        {
            Snapshot = snapshot;
            Title = SSCBondOverviewText.Format("TitleSource", snapshot.Source.Dead
                ? SSCBondOverviewText.Format("DeadName", snapshot.Source.LabelShort) : snapshot.Source.LabelShort);
            Count = SSCBondOverviewText.Format("Counts", snapshot.LivingSlaveCount, snapshot.TotalSlaveCount);
            Trainer = SSCBondOverviewText.Format("Trainer", snapshot.AssignedTrainer?.LabelShort ?? SSCBondOverviewText.T("None"),
                SSCBondOverviewText.T("Trainer_" + snapshot.AssignedTrainerState));
            if (snapshot.Master != null)
            { MasterName = Name(snapshot.Master); MasterDetails = Detail(snapshot.Master); }
            Names = new string[snapshot.TotalSlaveCount]; Details = new string[snapshot.TotalSlaveCount];
            // 仅换快照或换语言时生成文字；GUI 不再对全名单排序、复制或格式化。
            for (int i = 0; i < Names.Length; i++)
            { Names[i] = Name(snapshot.Slaves[i]); Details[i] = Detail(snapshot.Slaves[i]); }
        }
        private static string Name(SSCBondOverviewEntry entry)
            => entry.IsDead ? SSCBondOverviewText.Format("DeadName", entry.Name) : entry.Name;
        private static string Detail(SSCBondOverviewEntry entry)
            => SSCBondOverviewText.Format("RowDetails", entry.ChainStage, SSCBondOverviewText.Specialization(entry.Specialization),
                SSCBondOverviewText.Location(entry.Location));
    }

    internal static class SSCBondOverviewText
    {
        internal static string T(string key) => ("SSC_BondOverview_" + key).Translate();
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
        internal const float RowHeight = 64, HeaderHeight = 28;
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
        {
            if (Event.current.type != EventType.repaint || !current) return;
            // 原版缓存拥有纹理；这里只请求两个固定档位，不强制 SetDirty 或自行销毁。
            GUI.DrawTexture(rect, PortraitsCache.Get(entry.Pawn, new Vector2(rect.width, rect.height), Rot4.South));
            if (entry.IsDead) Widgets.Label(new Rect(rect.x, rect.yMax - 18, rect.width, 18), SSCBondOverviewText.T("Dead"));
        }

        internal static void Hover(Rect rect, SSCBondOverviewEntry entry, string name, string details)
        {
            if (!Mouse.IsOver(rect)) return;
            Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, name + "\n" + details);
            SSCBondOverviewNavigation.Highlight(entry.Location);
        }

        internal static bool Row(Rect rect, SSCBondOverviewSnapshot snapshot, SSCBondOverviewEntry entry,
            string name, string details, SSCBondOverviewSession session, SSCBondOverviewView view)
        {
            bool current = SSCBondOverviewUtility.IsCurrent(snapshot, entry);
            if (!current) session.RequestRefresh(view);
            Portrait(new Rect(rect.x, rect.y + 8, 48, 48), entry, current);
            float buttonWidth = Math.Min(110, Math.Max(46, rect.width * .23f));
            Rect button = new Rect(rect.xMax - buttonWidth, rect.y + 16, buttonWidth, 30);
            float textWidth = Math.Max(1, button.x - rect.x - 68);
            Widgets.Label(new Rect(rect.x + 56, rect.y + 7, textWidth, 24), name.Truncate(textWidth));
            Widgets.Label(new Rect(rect.x + 56, rect.y + 32, textWidth, 24),
                (current ? details : SSCBondOverviewText.T("RelationChanged")).Truncate(textWidth));
            if (current) Hover(new Rect(rect.x, rect.y, Math.Max(1, button.x - rect.x - 4), rect.height), entry, name, details);
            bool select = false;
            bool available = current && SSCBondOverviewNavigation.CanPreview(entry.Location, out select);
            if (Mouse.IsOver(button) && !available) TooltipHandler.TipRegion(button,
                SSCBondOverviewText.T(current ? "CannotJump" : "RelationChanged"));
            return SSCUIWidgets.Button(button, SSCBondOverviewText.T(select ? "JumpSelect" : "Jump"), available);
        }
    }
}
