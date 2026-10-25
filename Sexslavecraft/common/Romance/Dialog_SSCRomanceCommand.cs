// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>选择目标后预览一次性命令；窗口始终保留最初执行者凭据，不附带旧关系风险确认。</summary>
    public sealed class Dialog_SSCRomanceCommand : Window
    {
        private SSCRomanceCommandContext context;
        private Vector2 scroll;
        private float contentHeight = 700f;
        private string error;
        public override Vector2 InitialSize => new Vector2(Mathf.Min(660f, UI.screenWidth - 32f), Mathf.Min(680f, UI.screenHeight - 32f));
        /// <summary>打开仅捕获原上下文；签发授权和安排 Job 只在下达按钮成功提交时进行。</summary>
        public Dialog_SSCRomanceCommand(Pawn pawn)
        {
            context = SSCRomanceCommandUtility.Capture(pawn);
            optionalTitle = "SSC_Romance_CommandTitle".Translate(); doCloseX = true; doCloseButton = true; absorbInputAroundWindow = true;
        }
        /// <summary>使用窗口内单一滚动区域，长姓名/长译文按真实宽度换行，始终恢复 GUI 状态。</summary>
        public override void DoWindowContents(Rect inRect)
        {
            var font = Text.Font; var anchor = Text.Anchor; bool wrap = Text.WordWrap, enabled = GUI.enabled; Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                Rect viewport = new Rect(0, 0, inRect.width, Mathf.Max(1f, inRect.height - 48f));
                Rect content = new Rect(0, 0, Mathf.Max(1f, viewport.width - 20f), Mathf.Max(viewport.height, contentHeight));
                Widgets.BeginScrollView(viewport, ref scroll, content);
                var listing = new Listing_Standard { maxOneColumn = true }; listing.Begin(content);
                try
                {
                    listing.Label("SSC_Romance_Executor".Translate(context.Actor?.LabelShort ?? "?"));
                    if (Button(listing, "SSC_Romance_ChooseAction".Translate(ActionLabel(context.Action)))) ChooseAction();
                    if (Button(listing, "SSC_Romance_ChooseTarget".Translate(context.Target?.LabelShort ?? "SSC_Romance_NoTarget".Translate().ToString()))) ChooseTarget();
                    if (context.Target != null)
                    {
                        listing.Label("SSC_Romance_RelationPreview".Translate(RelationLabel(context.Actor, context.Target)));
                        listing.Label("SSC_Romance_CommandOverride".Translate());
                        if (context.Action == SSCRomanceAction.Breakup) listing.Label("SSC_Romance_BreakupPreview".Translate());
                        else
                        {
                            var decision = SSCRomancePolicy.Evaluate(context.Actor, context.Target, context.Action);
                            listing.Label(("SSC_Romance_Reception_" + decision.Reception).Translate());
                        }
                    }
                    string failure = SSCRomanceCommandUtility.Failure(context);
                    if (failure != null) listing.Label(SSCRomanceCommandUtility.Explain(failure));
                    bool oldEnabled = GUI.enabled;
                    try
                    {
                        GUI.enabled = oldEnabled && failure == null;
                        if (Button(listing, "SSC_Romance_Submit".Translate()) && GUI.enabled)
                        {
                            if (SSCRomanceCommandUtility.TrySubmit(context, out var record, out string reason))
                            {
                                // 原调度器可能同步跑完短任务；已经有结果时不再报告“互动尚待执行”。
                                if (record.Terminal) SSCRomanceCommandGameComponent.Feedback(record);
                                else Messages.Message("SSC_Romance_Scheduled".Translate(context.Actor.LabelShort, context.Target.LabelShort), context.Actor, MessageTypeDefOf.NeutralEvent, false);
                                Close();
                            }
                            else error = SSCRomanceCommandUtility.Explain(reason);
                        }
                    }
                    finally { GUI.enabled = oldEnabled; }
                    if (error != null) listing.Label(error);
                }
                finally { contentHeight = listing.CurHeight + 12f; listing.End(); Widgets.EndScrollView(); }
            }
            finally { Text.Font = font; Text.Anchor = anchor; Text.WordWrap = wrap; GUI.enabled = enabled; GUI.color = color; }
        }
        /// <summary>按钮高度包含实际译文换行，不固定成一行。</summary>
        private static bool Button(Listing_Standard listing, string text)
            => Widgets.ButtonText(listing.GetRect(Mathf.Max(32f, Text.CalcHeight(text, Mathf.Max(1f, listing.ColumnWidth - 12f)) + 12f)), text);
        /// <summary>行为切换保留原执行者凭据，目标资格由同一 Failure 查询重新计算。</summary>
        private void ChooseAction()
        {
            var options = new List<FloatMenuOption>();
            foreach (SSCRomanceAction action in Enum.GetValues(typeof(SSCRomanceAction)))
            { var chosen = action; options.Add(new FloatMenuOption(ActionLabel(chosen), () => { context = context.ForTarget(context.Target, chosen); error = null; })); }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        /// <summary>列出执行者地图角色及既有伴侣；不以被动必拒或零成功率剔除目标，失效者展示首个原因。</summary>
        private void ChooseTarget()
        {
            var candidates = new List<Pawn>();
            if (context.Actor?.Map != null) candidates.AddRange(context.Actor.Map.mapPawns.AllPawnsSpawned);
            if (context.Actor?.relations != null) candidates.AddRange(context.Actor.relations.DirectRelations.Select(r => r.otherPawn));
            var options = new List<FloatMenuOption>();
            foreach (var pawn in candidates.Where(p => p != null && p != context.Actor && p.RaceProps?.Humanlike == true).Distinct().OrderBy(p => p.LabelShort))
            {
                var preview = context.ForTarget(pawn, context.Action);
                string failure = SSCRomanceCommandUtility.Failure(preview);
                string label = pawn.LabelShort + " — " + RelationLabel(context.Actor, pawn);
                if (failure != null) label += " (" + SSCRomanceCommandUtility.Explain(failure) + ")";
                options.Add(new FloatMenuOption(label, failure == null ? () => { context = preview; error = null; } : null));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("SSC_Romance_NoTarget".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
        /// <summary>原版当前关系作预览，不把 SSC 奖励关系当成 Lover 或婚约。</summary>
        public static string RelationLabel(Pawn actor, Pawn target)
        {
            foreach (var def in new[] { PawnRelationDefOf.Spouse, PawnRelationDefOf.Fiance, PawnRelationDefOf.Lover })
                if (actor?.relations?.DirectRelationExists(def, target) == true) return def.GetGenderSpecificLabel(target);
            return "SSC_Romance_NoRelationshipLabel".Translate();
        }
        /// <summary>行为枚举共用四语标签，菜单和预览不另写固定中文。</summary>
        private static string ActionLabel(SSCRomanceAction action) => ("SSC_Romance_Action_" + action).Translate();
    }
}
