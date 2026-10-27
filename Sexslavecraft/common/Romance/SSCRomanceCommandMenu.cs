// AI 辅助生成
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
            if (context == null) return;
            var candidates = new List<Pawn>();
            if (context.Actor?.Map != null) candidates.AddRange(context.Actor.Map.mapPawns.AllPawnsSpawned);
            if (context.Actor?.relations != null) candidates.AddRange(context.Actor.relations.DirectRelations.Select(r => r.otherPawn));
            Find.WindowStack.Add(new FloatMenu(BuildTargetOptions(context, candidates)));
        }

        /// <summary>只列当前行为可选择的目标；被动必拒和零概率不排除，点击保留同一原预览提交。</summary>
        internal static List<FloatMenuOption> BuildTargetOptions(SSCRomanceCommandContext context, IEnumerable<Pawn> candidates)
        {
            var options = new List<FloatMenuOption>();
            foreach (var pawn in candidates.Where(p => p != null && p != context.Actor && p.RaceProps?.Humanlike == true).Distinct().OrderBy(p => p.LabelShort))
            {
                var preview = context.ForTarget(pawn, context.Action);
                if (SSCRomanceCommandUtility.Failure(preview) != null) continue;
                options.Add(new FloatMenuOption(pawn.LabelShort + " — " + RelationLabel(context.Actor, pawn), () => Submit(preview)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("SSC_Romance_NoTarget".Translate(), null));
            return options;
        }

        /// <summary>选中目标直接下达一次任务；失败显示当前原因，同步完成只报告真实结果，不再提示待执行。</summary>
        private static void Submit(SSCRomanceCommandContext context)
        {
            if (!SSCRomanceCommandUtility.TrySubmit(context, out var record, out string reason))
            {
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
