// AI 辅助生成：运行生产点名菜单构建，窗口投递和完整地图仍由原生宿主边界隔离。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn menuBlockedTarget;
    private static string menuBlockedReason;

    /// <summary>验证清单只保留可选择目标，并保留被动必拒、零概率及原上下文提交规则。</summary>
    private static void TargetMenuTests()
    {
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("target menu filters necessary relationship and duplicates for " + action, () =>
            {
                CommandReset(); var (a, valid) = CommandPair(action); var invalid = SocialPawn();
                if (action == SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, invalid);
                var dialog = TargetDialog(a, action);
                var options = TargetOptions(dialog, new Pawn[] { null, a, invalid, valid, valid });
                Equal(1, options.Count); Require(options[0].action != null && options[0].Label.StartsWith(valid.LabelShort));
                options[0].action(); var record = SSCRomanceCommandGameComponent.Current.Records.Single();
                Equal(valid, record.Stamp.Target); Equal(action, record.Stamp.Action); Equal(1, commandFeedback);
            });
        foreach (string reason in new[] { "InvalidRomancePair", "Unreachable", "SameMapRequired", "UnableToInteract" })
            Check("target menu excludes task-ineligible pawn rather than disabled row " + reason, () =>
            {
                CommandReset(); var (a, valid) = CommandPair(SSCRomanceAction.Romance); menuBlockedTarget = SocialPawn(); menuBlockedReason = reason;
                var boundary = new Harmony("ssc.tests.romance.target-menu-filter");
                try
                {
                    boundary.Patch(AccessTools.Method(typeof(SSCRomanceCommandUtility), nameof(SSCRomanceCommandUtility.TaskFailure)),
                        prefix: new HarmonyMethod(typeof(Program), nameof(MenuTaskFailure)) { priority = Priority.First });
                    var options = TargetOptions(TargetDialog(a), new[] { menuBlockedTarget, valid });
                    Equal(1, options.Count); Require(options[0].action != null && options[0].Label.StartsWith(valid.LabelShort));
                }
                finally { boundary.UnpatchAll(boundary.Id); menuBlockedTarget = null; menuBlockedReason = null; }
            });
        foreach (bool denied in new[] { false, true })
            Check("target menu retains zero native chance and passive refusal target " + denied, () =>
            {
                CommandReset(); socialChance = 0f; var (a, b) = CommandPair(SSCRomanceAction.Romance, true);
                Comp(b).romanceConfig = new() { allowAcceptRomance = !denied };
                var options = TargetOptions(TargetDialog(a), new[] { b });
                Equal(1, options.Count); Require(options[0].action != null);
                Equal(0f, InteractionWorker_RomanceAttempt.SuccessChance(a, b));
            });
        Check("target menu empty list contains only no-target placeholder", () =>
        {
            CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Proposal);
            var options = TargetOptions(TargetDialog(a, SSCRomanceAction.Proposal), new[] { a, SocialPawn() });
            Equal(1, options.Count); Require(options[0].action == null); Equal("SSC_Romance_NoTarget".Translate().ToString(), options[0].Label);
        });
        foreach (string change in new[] { "personality", "targetPersonality" })
            Check("target menu old click does not renew changed personality " + change, () =>
            {
                CommandReset(); var (a, b) = CommandPair(SSCRomanceAction.Romance); var dialog = TargetDialog(a);
                var options = TargetOptions(dialog, new[] { b }); Require(options[0].action != null);
                ChangeCommandContext(a, b, change); options[0].action();
                Equal(0, SSCRomanceCommandGameComponent.Current.Records.Count); Equal(1, commandFeedback);
            });
        foreach (var mode in new[] { ScheduleMode.Current, ScheduleMode.Queue, ScheduleMode.Reject, ScheduleMode.Synchronous })
            Check("direct target click schedules once and reports only the correct result " + mode, () =>
            {
                CommandReset(0f); scheduleMode = mode; var (a, b) = CommandPair(SSCRomanceAction.Romance);
                var options = TargetOptions(TargetDialog(a), new[] { b }); options.Single().action();
                var record = SSCRomanceCommandGameComponent.Current.Records.Single(); Equal(b, record.Stamp.Target);
                Equal(1, commandFeedback); Equal(mode == ScheduleMode.Synchronous ? 1 : 0, playLogCalls);
                if (mode == ScheduleMode.Synchronous) Equal(SSCRomanceCommandStatus.Completed, record.Status);
                if (mode == ScheduleMode.Reject) Require(record.Terminal && record.Outcome == SSCRomanceCommandOutcome.NotOccurred);
                // 同一菜单动作再次触发只能拒绝旧请求，不能安排或执行第二个 Job。
                options.Single().action(); Equal(1, SSCRomanceCommandGameComponent.Current.Records.Count);
                Equal(mode == ScheduleMode.Synchronous ? 1 : 0, playLogCalls);
            });
    }

    /// <summary>捕获按钮持有的最初上下文，不初始化图形窗口或替换菜单构建逻辑。</summary>
    private static SSCRomanceCommandContext TargetDialog(Pawn actor, SSCRomanceAction action = SSCRomanceAction.Romance)
        => SSCRomanceCommandUtility.Capture(actor, null, action);

    /// <summary>调用生产私有构建器，检查真实 FloatMenuOption 的内容及点击动作。</summary>
    private static List<FloatMenuOption> TargetOptions(SSCRomanceCommandContext context, IEnumerable<Pawn> candidates)
        => (List<FloatMenuOption>)AccessTools.Method(typeof(SSCRomanceCommandMenu), "BuildTargetOptions").Invoke(null, new object[] { context, candidates });

    /// <summary>仅隔离特定目标的物理任务条件，资格、关系、预览和选项生成均执行生产代码。</summary>
    private static bool MenuTaskFailure(Pawn target, ref string __result)
    {
        if (target != menuBlockedTarget) return true;
        __result = menuBlockedReason;
        return false;
    }
}
