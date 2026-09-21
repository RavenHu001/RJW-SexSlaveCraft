using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>可保存个体规则并测试统一入口的最小界面；实际任务仍由旧系统运行。</summary>
    internal sealed class Dialog_SSCRestrictions : Window
    {
        private Pawn editedPawn;
        private Pawn initiator;
        private Pawn receiver;
        private SSCInteractionKind kind = SSCInteractionKind.Consensual;
        private bool testing;
        private Vector2 scrollPosition;
        private float contentHeight = 1200f;
        private string error;

        /// <summary>限制初始窗口尺寸，使关闭按钮与滚动区域在较小屏幕上仍可访问。</summary>
        public override Vector2 InitialSize => new Vector2(Mathf.Min(760f, UI.screenWidth - 32f), Mathf.Min(760f, UI.screenHeight - 32f));

        /// <summary>绑定初始编辑对象及测试方向；打开窗口只读取关系，不初始化角色配置。</summary>
        public Dialog_SSCRestrictions(Pawn pawn)
        {
            editedPawn = pawn;
            Pawn owner = SSCBondUtility.GetBoundMaster(pawn);
            initiator = owner ?? pawn;
            receiver = owner != null ? pawn : null;
            optionalTitle = "SSC_Restrictions_Title".Translate();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>绘制编辑与测试分页及独立滚动内容，始终恢复字体、颜色和输入状态。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float width = (inRect.width - 8f) / 2f;
                if (Widgets.ButtonText(new Rect(0f, 0f, width, 32f), (testing ? "" : "▶ ") + "SSC_Restrictions_EditTab".Translate()))
                    SelectPage(false);
                if (Widgets.ButtonText(new Rect(width + 8f, 0f, width, 32f), (testing ? "▶ " : "") + "SSC_Restrictions_TestTab".Translate()))
                    SelectPage(true);
                Rect viewport = new Rect(0f, 42f, inRect.width, Mathf.Max(1f, inRect.height - 104f));
                Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f), Mathf.Max(viewport.height, contentHeight));
                Widgets.BeginScrollView(viewport, ref scrollPosition, content);
                try
                {
                    var listing = new Listing_Standard { maxOneColumn = true };
                    listing.Begin(content);
                    try
                    {
                        if (testing)
                        {
                            listing.Label("SSC_Restrictions_TestNotice".Translate());
                            listing.Label("SSC_Restrictions_OwnerNotice".Translate());
                            listing.GapLine();
                        }
                        DrawGlobalSwitches(listing);
                        listing.GapLine();
                        if (testing) DrawTest(listing); else DrawEditor(listing);
                        if (!string.IsNullOrEmpty(error)) listing.Label("SSC_Restrictions_Error".Translate(error));
                    }
                    finally { contentHeight = listing.CurHeight + 12f; listing.End(); }
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.Font = oldFont; Text.Anchor = oldAnchor; Text.WordWrap = oldWrap;
                GUI.color = oldColor; GUI.enabled = oldEnabled;
            }
        }

        /// <summary>切换分页并重置滚动位置，保留已选角色、用途及存档中的个体设置。</summary>
        private void SelectPage(bool showTest)
        {
            testing = showTest;
            scrollPosition = Vector2.zero;
            error = null;
        }

        /// <summary>编辑并保存两个全局开关；总开关与旧保护共用，特化开关仅影响新规则。</summary>
        private static void DrawGlobalSwitches(Listing_Standard listing)
        {
            SSCSettings settings = SSCMod.settings;
            if (settings == null) return;
            bool enabled = settings.enableSexSlaveProtectionRules;
            bool specialization = settings.enableSpecializationRestrictionOverrides;
            listing.CheckboxLabeled("SSC_Restrictions_SystemSwitch".Translate(), ref enabled, "SSC_Restrictions_SystemSwitchTip".Translate());
            listing.CheckboxLabeled("SSC_Restrictions_ProfileSwitch".Translate(), ref specialization, "SSC_Restrictions_ProfileSwitchTip".Translate());
            if (enabled == settings.enableSexSlaveProtectionRules && specialization == settings.enableSpecializationRestrictionOverrides) return;
            settings.enableSexSlaveProtectionRules = enabled;
            settings.enableSpecializationRestrictionOverrides = specialization;
            SSCRestrictionGameComponent.SettingsChanged();
            settings.Write();
        }

        /// <summary>选择当前编辑角色并复用正式角色控件，保留跳转到许可测试的入口。</summary>
        private void DrawEditor(Listing_Standard listing)
        {
            if (listing.ButtonText("SSC_Restrictions_EditPawn".Translate(PawnLabel(editedPawn))))
                ChoosePawn(p => { editedPawn = p; scrollPosition = Vector2.zero; error = null; }, false);
            SSCRestrictionUI.DrawPawn(listing, editedPawn);
            listing.Gap();
            if (listing.ButtonText("SSC_Restrictions_TestAsReceiver".Translate()))
            {
                receiver = editedPawn;
                SelectPage(true);
            }
        }

        /// <summary>用当前保存配置实时测试指定方向和用途；显示许可原因及来源，不启动实际任务。</summary>
        private void DrawTest(Listing_Standard listing)
        {
            if (listing.ButtonText("SSC_Restrictions_Initiator".Translate(PawnLabel(initiator)))) ChoosePawn(p => initiator = p, false);
            if (listing.ButtonText("SSC_Restrictions_Receiver".Translate(PawnLabel(receiver)))) ChoosePawn(p => receiver = p, true);
            if (listing.ButtonText("SSC_Restrictions_Swap".Translate())) { Pawn previous = initiator; initiator = receiver; receiver = previous; }
            if (listing.ButtonText("SSC_Restrictions_Kind".Translate(("SSC_Restrictions_Kind_" + kind).Translate())))
            {
                var options = new List<FloatMenuOption>();
                foreach (SSCInteractionKind value in Enum.GetValues(typeof(SSCInteractionKind)))
                {
                    if (value == SSCInteractionKind.Unknown) continue;
                    SSCInteractionKind choice = value;
                    options.Add(new FloatMenuOption(("SSC_Restrictions_Kind_" + choice).Translate(), () => kind = choice));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            listing.Label("SSC_Restrictions_SoloTip".Translate());
            listing.GapLine();
            try
            {
                Pawn target = kind == SSCInteractionKind.Masturbation ? null : receiver;
                SSCRestrictionDecision decision = SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(initiator, target, kind, directionKnown: true));
                listing.Label("SSC_Restrictions_Result".Translate(ValueLabel(decision.Allowed ? SSCRestrictionValue.Allow : SSCRestrictionValue.Deny), ("SSC_Restrictions_Reason_" + decision.Reason).Translate()));
                if (decision.Subject != null) listing.Label("SSC_Restrictions_Subject".Translate(PawnLabel(decision.Subject)));
                if (decision.Entry != null)
                    listing.Label("SSC_Restrictions_ResultEntry".Translate(("SSC_Restrictions_Rule_" + decision.Entry.Rule).Translate(), SourceLabel(decision.Entry)));
            }
            catch (Exception exception) { listing.Label("SSC_Restrictions_Error".Translate(exception.Message)); }
            listing.GapLine();
            if (initiator != null && listing.ButtonText("SSC_Restrictions_EditInitiator".Translate())) { editedPawn = initiator; SelectPage(false); }
            if (receiver != null && listing.ButtonText("SSC_Restrictions_EditReceiver".Translate())) { editedPawn = receiver; SelectPage(false); }
        }

        /// <summary>列出当前地图人形角色及已选对象；可选择空接收者来测试缺失上下文。</summary>
        private void ChoosePawn(Action<Pawn> select, bool allowNone)
        {
            var candidates = new List<Pawn>();
            if (Find.CurrentMap != null) candidates.AddRange(Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike));
            candidates.Add(editedPawn); candidates.Add(initiator); candidates.Add(receiver);
            var options = new List<FloatMenuOption>();
            if (allowNone) options.Add(new FloatMenuOption("SSC_Restrictions_None".Translate(), () => select(null)));
            foreach (Pawn pawn in candidates.Where(p => p != null).Distinct().OrderBy(p => p.LabelShort))
            {
                Pawn selected = pawn;
                options.Add(new FloatMenuOption(selected.LabelShort, () => select(selected)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>为角色或空选择提供统一显示名称。</summary>
        private static string PawnLabel(Pawn pawn) => pawn?.LabelShort ?? "SSC_Restrictions_None".Translate().ToString();

        /// <summary>将规则枚举转换为界面中的本地化保存值或许可结果。</summary>
        private static string ValueLabel(SSCRestrictionValue value) => ("SSC_Restrictions_Value_" + value).Translate();

        /// <summary>显示保存值、全局停用、特化或装备来源，并附定义名供精确核对。</summary>
        private static string SourceLabel(SSCRestrictionResolution entry)
        {
            string label = ("SSC_Restrictions_Source_" + entry.Source).Translate();
            return entry.SourceDef == null ? label : label + " (" + entry.SourceDef + ")";
        }
    }
}
