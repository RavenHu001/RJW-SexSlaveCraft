// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>浪漫栏底部的三个命令共用目标菜单；选中即提交一次原请求，不另开行为／目标／确认窗口。</summary>
    public static class SSCRomanceCommandMenu
    {
        /// <summary>沿用按钮绘制时捕获的执行者上下文，收集当前地图及已有关系中的候选。</summary>
        public static void OpenTargets(SSCRomanceCommandContext context)
        {
            var menu = CreateTargetMenu(context);
            if (menu != null) Find.WindowStack.Add(menu);
        }

        /// <summary>复用目标筛选和提交，调用者可以为菜单实例加上原窗口的提交检查。</summary>
        internal static FloatMenu CreateTargetMenu(SSCRomanceCommandContext context, Func<FloatMenu, bool> canSubmit = null)
        {
            if (context == null) return null;
            var candidates = new List<Pawn>();
            if (context.Actor?.Map != null) candidates.AddRange(context.Actor.Map.mapPawns.AllPawnsSpawned);
            if (context.Actor?.relations != null) candidates.AddRange(context.Actor.relations.DirectRelations.Select(r => r.otherPawn));
            // 菜单构造完成后，选项回调才会取得并校验这一实例。
            FloatMenu menu = null;
            menu = new FloatMenu(BuildTargetOptions(context, candidates, canSubmit == null ? null : () => canSubmit(menu)));
            return menu;
        }

        /// <summary>可用目标排在前面，禁用目标直接附本次预检原因。可提交的选项继续保留原预览。</summary>
        internal static List<FloatMenuOption> BuildTargetOptions(SSCRomanceCommandContext context, IEnumerable<Pawn> candidates,
            Func<bool> canSubmit = null)
        {
            var options = new List<FloatMenuOption>();
            var unavailable = new List<FloatMenuOption>();
            foreach (var pawn in candidates.Where(p => p != null && p != context.Actor && p.RaceProps?.Humanlike == true).Distinct().OrderBy(p => p.LabelShort))
            {
                var preview = context.ForTarget(pawn, context.Action);
                string reason = SSCRomanceCommandUtility.Failure(preview);
                if (reason != null)
                {
                    unavailable.Add(new FloatMenuOption(pawn.LabelShort + " — " + SSCRomanceCommandUtility.Explain(reason), null));
                    continue;
                }
                options.Add(new FloatMenuOption(pawn.LabelShort + " — " + RelationLabel(context.Actor, pawn), () =>
                {
                    if (canSubmit == null || canSubmit()) Submit(preview, canSubmit);
                }));
            }
            // 保留可用目标的浏览顺序，候选多时也能先找到可以下令的对象。
            options.AddRange(unavailable);
            if (options.Count == 0) options.Add(new FloatMenuOption("SSC_Romance_NoTarget".Translate(), null));
            return options;
        }

        /// <summary>选中目标直接下达一次任务；失败显示当前原因，同步完成只报告真实结果，不再提示待执行。</summary>
        private static void Submit(SSCRomanceCommandContext context, Func<bool> canSubmit)
        {
            bool submitted = canSubmit == null
                ? SSCRomanceCommandUtility.TrySubmit(context, out var record, out string reason)
                : SSCRomanceCommandUtility.TrySubmitFromMenu(context, canSubmit, out record, out reason);
            if (!submitted)
            {
                if (canSubmit?.Invoke() != false)
                    Messages.Message(SSCRomanceCommandUtility.Explain(reason), context.Actor, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (record.Terminal) SSCRomanceCommandGameComponent.Feedback(record);
            else Messages.Message("SSC_Romance_Scheduled".Translate(context.Actor.LabelShort, context.Target.LabelShort), context.Actor, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>只显示原版当前恋爱／婚姻关系，不把 SSC 奖励关系作为求婚或分手前提。</summary>
        private static string RelationLabel(Pawn actor, Pawn target)
        {
            foreach (var def in new[] { PawnRelationDefOf.Spouse, PawnRelationDefOf.Fiance, PawnRelationDefOf.Lover })
                if (actor?.relations?.DirectRelationExists(def, target) == true) return def.GetGenderSpecificLabel(target);
            return "SSC_Romance_NoRelationshipLabel".Translate();
        }
    }
}
