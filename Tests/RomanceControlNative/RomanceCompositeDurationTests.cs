// AI 辅助生成：复现通用期限后处理导致的原版替换误拒绝，不加载整套外部模组。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static string durationPostprocessMode;

    /// <summary>模拟已取证 VEF 对默认期限的显式化，实际 tracker、worker、原版记忆及 SSC 提交全部执行。</summary>
    private static void RomanceCompositeDurationTests()
    {
        foreach (string mode in new[] { "equivalent", "changed" })
        foreach (string action in new[] { "romance", "proposal", "passive" })
        foreach (bool masochist in new[] { false, true })
            Check("composite duration postprocess " + mode + "/" + action + "/masochist=" + masochist, () =>
            {
                var boundary = new Harmony("ssc.tests.composite.duration-postprocess");
                try
                {
                    durationPostprocessMode = mode;
                    boundary.Patch(AccessTools.Method(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) }),
                        postfix: new HarmonyMethod(typeof(Program), nameof(DurationPostprocess)));
                    CommandReset(action == "passive" ? 0f : 1f, 1f); socialOpinionValue = action == "passive" ? -10 : 30;
                    Pawn owner; ThoughtDef native;
                    if (action == "passive")
                    {
                        var actor = SocialPawn(); owner = SocialSlave(); Comp(owner).romanceConfig = new() { allowAcceptRomance = false };
                        if (masochist) owner.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                        native = ThoughtDefOf.FailedRomanceAttemptOnMeLowOpinionMood;
                        Require(actor.interactions.TryInteractWith(owner, InteractionDefOf.RomanceAttempt));
                    }
                    else
                    {
                        var kind = action == "proposal" ? SSCRomanceAction.Proposal : SSCRomanceAction.Romance;
                        var pair = CommandPair(kind); owner = pair.actor;
                        if (masochist) owner.story.traits.allTraits.Add(new Trait(SSCRomanceMoodDefOf.Masochist, 0));
                        native = DefDatabase<ThoughtDef>.GetNamed(action == "proposal" ? "RejectedMyProposalMood" : "RebuffedMyRomanceAttemptMood");
                        SubmitAndRun(owner, pair.target, kind, out _);
                    }
                    var memories = owner.needs.mood.thoughts.memories.Memories;
                    Equal(mode == "equivalent" ? 1 : 0, memories.OfType<Thought_MemorySSCCompositeRomance>().Count());
                    Equal(mode == "equivalent" ? 0 : 1, memories.Count(m => m.def == native));
                    if (mode == "equivalent")
                    {
                        string branch = memories.OfType<Thought_MemorySSCCompositeRomance>().Single().Data.BranchId;
                        string trait = masochist ? "M" : "N";
                        Equal(action == "romance" ? "E07-S4-" + trait + "-G" : action == "proposal" ? "E09-S4-" + trait + "-G" : "E03-S4-" + trait + "-D", branch);
                    }
                }
                finally { boundary.UnpatchAll(boundary.Id); durationPostprocessMode = null; }
            });
    }
    /// <summary>只提供期限后处理边界：等价显式化或真正延长；不在测试中替代 SSC 撤销与分组实现。</summary>
    private static void DurationPostprocess(Thought_Memory __0)
    {
        if (__0.pawn == null || __0 is Thought_MemorySSCRomance) return;
        __0.durationTicksOverride = __0.DurationTicks + (durationPostprocessMode == "changed" ? 60000 : 0);
    }
}
