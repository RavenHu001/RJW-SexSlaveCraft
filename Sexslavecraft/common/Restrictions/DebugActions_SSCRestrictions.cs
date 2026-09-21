using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using Verse;

namespace SexSlaveCraft
{
    internal static class DebugActions_SSCRestrictions
    {
        /// <summary>以当前选中的角色为发起者，提供单人或地图目标选项，打开只读许可预览。</summary>
        [DebugAction("SSC", "Restrictions stage 1 (read-only)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Preview()
        {
            Pawn actor = Find.Selector.SingleSelectedThing as Pawn;
            if (actor == null)
            {
                Find.WindowStack.Add(new Dialog_MessageBox("Select one pawn as the initiator first. / 请先选中一个发起者。"));
                return;
            }
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Solo / 单人", () => ShowReport(actor, null))
            };
            foreach (Pawn candidate in Find.CurrentMap.mapPawns.AllPawnsSpawned.ToList())
            {
                Pawn target = candidate;
                if (target == actor) continue;
                options.Add(new FloatMenuOption(target.LabelShort, () => ShowReport(actor, target)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>比较保存配置与临时默认配置下的各用途判定，将报告写入日志并显示；不创建任务或修改配置。</summary>
        private static void ShowReport(Pawn actor, Pawn target)
        {
            var report = new StringBuilder();
            report.AppendLine("SSC restrictions — stage 1 / 新限制核心阶段 1");
            report.AppendLine("READ ONLY. Actual jobs still use legacy rules. / 只读预览，实际任务仍使用旧规则。");
            report.AppendLine("Actor: " + actor.LabelShort + " -> " + (target?.LabelShort ?? "(solo)"));
            report.AppendLine("Profiles loaded: " + DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Count);
            AppendPawn(report, "Actor", actor);
            if (target != null) AppendPawn(report, "Receiver", target);
            report.AppendLine("Actual = saved config; Preview = temporary defaults only when config is missing.");
            report.AppendLine("Preview does not initialize or migrate a pawn. / 预览不初始化、不迁移或写入角色。");
            SSCInteractionKind[] kinds = target == null ? new[] { SSCInteractionKind.Masturbation } : new[]
            {
                SSCInteractionKind.Consensual, SSCInteractionKind.Forced, SSCInteractionKind.DailyTraining,
                SSCInteractionKind.RitualTraining, SSCInteractionKind.PersonalityExcretion, SSCInteractionKind.BindingPreparation
            };
            foreach (SSCInteractionKind kind in kinds)
            {
                report.AppendLine();
                report.AppendLine(kind.ToString());
                try
                {
                    AppendDecision(report, "Actual", SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, target, kind, directionKnown: true)));
                    AppendDecision(report, "Preview", SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, target, kind, directionKnown: true) { PreviewDefaults = true }));
                }
                catch (Exception error) { report.AppendLine("Invalid definition/configuration: " + error.Message); }
            }
            string text = report.ToString();
            Log.Message(text);
            Find.WindowStack.Add(new Dialog_MessageBox(text));
        }

        /// <summary>向报告追加角色配置的初始化状态或版本，读取时不补建缺失配置。</summary>
        private static void AppendPawn(StringBuilder report, string label, Pawn pawn)
        {
            SSCRestrictionConfig config = pawn.TryGetComp<CompSexSlaveTraining>()?.restrictionConfig;
            report.AppendLine(label + " config: " + (config == null ? "uninitialized / 未初始化" : "v" + config.version));
        }

        /// <summary>向报告追加许可、原因、相关角色和最终规则来源，便于核对覆盖优先级。</summary>
        private static void AppendDecision(StringBuilder report, string label, SSCRestrictionDecision decision)
        {
            report.Append(label + ": " + (decision.Allowed ? "ALLOW" : "DENY") + " — " + decision.Reason);
            if (decision.Subject != null) report.Append(" (" + decision.Subject.LabelShort + ")");
            if (decision.Entry != null)
                report.Append(" [" + decision.Entry.Rule + "=" + decision.Entry.Value + "; " + decision.Entry.Source +
                    (decision.Entry.SourceDef == null ? "" : ":" + decision.Entry.SourceDef) + "]");
            report.AppendLine();
        }
    }
}
