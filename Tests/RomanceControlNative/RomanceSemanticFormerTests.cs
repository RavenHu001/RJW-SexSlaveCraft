// AI 辅助生成：原版求爱附带旧关系与事件准入期间对方载体变化的独立专项。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn semanticPreparedOwner;
    private static bool semanticNativePrepared;
    private static int semanticLateDurationReads;
    private static Pawn semanticNativeMutationOwner;
    private static ThoughtDef semanticNativeMutationDef;
    private static bool semanticNativeSnapshotMutated;
    /// <summary>通过原版求爱 worker 实际移除或保留旧关系，不能用测试直接删除关系冒充原版附带结算。</summary>
    private static void RomanceSemanticFormerTests()
    {
        foreach (bool onActor in new[] { false, true })
        foreach (var relation in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance })
        foreach (bool ends in new[] { false, true })
            Check($"semantic native former relation actual removal {onActor}/{relation.defName}/{ends}", () =>
            {
                CommandReset(0f); socialOpinionValue = 30;
                var actor = SocialPawn(); var target = SocialPawn(); var owner = onActor ? actor : target;
                var former = SocialPawn(); var unrelated = SocialPawn(); owner.relations.AddDirectRelation(relation, former);
                var oldOwner = SeedFiveSemanticGroups(owner, former); var oldFormer = SeedFiveSemanticGroups(former, owner);
                var untouched = SemanticMemory(SSCRomanceCompositeEvent.CommandRomanceRejected, unrelated, raw: 60);
                former.needs.mood.thoughts.memories.TryGainMemory(untouched, unrelated); untouched.age = 2468;
                // 原版意愿输入控制实际循环：仅指定一端不愿增加关系数，移除一个旧关系后恢复愿意。
                if (ends)
                {
                    if (!onActor) commandWilling.Enqueue(true);
                    commandWilling.Enqueue(false); commandWilling.Enqueue(true);
                }
                Require(actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
                Require(actor.relations.DirectRelationExists(PawnRelationDefOf.Lover, target));
                Equal(!ends, owner.relations.DirectRelationExists(relation, former));
                Equal(!ends, former.relations.DirectRelationExists(relation, owner));
                foreach (var pair in new[] { (owner, former, oldOwner), (former, owner, oldFormer) })
                {
                    var list = pair.Item1.needs.mood.thoughts.memories.Memories;
                    Equal(ends ? 0 : 5, list.OfType<Thought_MemorySSCRomance>().Count(m => m.otherPawn == pair.Item2));
                    Require(pair.Item3.All(m => list.Contains(m) == !ends));
                    Require(pair.Item3.All(m => m.age == 321));
                }
                Require(former.needs.mood.thoughts.memories.Memories.Contains(untouched)); Equal(2468, untouched.age);
                // 原版背叛反馈照常保留；附带清理既不接管它，也不生成新的 SSC 分手经历。
                Equal(ends, former.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.CheatedOnMe && m.otherPawn == owner));
            });

        foreach (var relation in new[] { PawnRelationDefOf.Lover, PawnRelationDefOf.Fiance })
            Check("semantic actual removed deceased counterpart clears living owner " + relation.defName, () =>
            {
                CommandReset(0f); socialOpinionValue = 30;
                var actor = SocialPawn(); var target = SocialPawn(); var former = SocialPawn();
                actor.relations.AddDirectRelation(relation, former); var old = SeedFiveSemanticGroups(actor, former);
                AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(former.health, PawnHealthState.Dead);
                commandWilling.Enqueue(false); commandWilling.Enqueue(true);
                Require(actor.interactions.TryInteractWith(target, InteractionDefOf.RomanceAttempt));
                Require(!actor.relations.DirectRelationExists(relation, former));
                Require(old.All(m => !actor.needs.mood.thoughts.memories.Memories.Contains(m)));
                Require(!former.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any());
            });

        foreach (string when in new[] { "factory", "admission" })
        foreach (string change in new[] { "target-token", "relationship" })
            Check($"semantic original counterpart callback preserves history {when}/{change}", () =>
            {
                CommandReset(1f); socialOpinionValue = 30;
                var (actor, target) = CommandPair(SSCRomanceAction.Romance);
                var actorOld = SeedFiveSemanticGroups(actor, target); var targetOld = SeedFiveSemanticGroups(target, actor);
                var ownerHandler = actor.needs.mood.thoughts.memories; var targetHandler = target.needs.mood.thoughts.memories;
                var boundary = new Harmony("ssc.tests.semantic.former-original-counterpart"); bool invoked = false;
                Action mutation = () =>
                {
                    invoked = true;
                    if (change == "relationship") actor.relations.AddDirectRelation(PawnRelationDefOf.Lover, target);
                    else { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(target, out var scope)); scope.Dispose(); }
                };
                try
                {
                    if (when == "factory")
                    {
                        compositeReviewBeforeFactory = mutation;
                        boundary.Patch(AccessTools.Method(typeof(ThoughtMaker), nameof(ThoughtMaker.MakeThought), new[] { typeof(ThoughtDef), typeof(int) }),
                            prefix: new HarmonyMethod(typeof(Program), nameof(CompositeReviewFactoryPrefix)));
                    }
                    else
                    {
                        compositeAdmissionAction = mutation;
                        boundary.Patch(AccessTools.Method(typeof(ThoughtUtility), nameof(ThoughtUtility.CanGetThought)),
                            postfix: new HarmonyMethod(typeof(Program), nameof(CompositeAdmissionMutation)));
                    }
                    SubmitAndRun(actor, target, SSCRomanceAction.Romance, out _);
                    Require(invoked); Require(actorOld.All(ownerHandler.Memories.Contains)); Require(targetOld.All(targetHandler.Memories.Contains));
                    Require(actorOld.Concat(targetOld).All(m => m.age == 321));
                    Equal(5, ownerHandler.Memories.OfType<Thought_MemorySSCRomance>().Count());
                    Equal(5, targetHandler.Memories.OfType<Thought_MemorySSCRomance>().Count());
                    Require(ownerHandler.Memories.Any(m => m.def.defName == "RebuffedMyRomanceAttemptMood"));
                }
                finally { boundary.UnpatchAll(boundary.Id); compositeReviewBeforeFactory = null; compositeAdmissionAction = null; }
            });

        Check("semantic final native commit performs no later candidate duration callbacks", () =>
        {
            CommandReset(1f); socialOpinionValue = 30; var (actor, target) = CommandPair(SSCRomanceAction.Romance);
            var old = SeedFiveSemanticGroups(actor, target); semanticPreparedOwner = actor;
            semanticNativePrepared = false; semanticLateDurationReads = 0;
            var boundary = new Harmony("ssc.tests.semantic.prepared-native-boundary");
            try
            {
                boundary.Patch(AccessTools.Method(production.GetType("SexSlaveCraft.SSCRomanceNativeMood"), "PrepareUndo"),
                    postfix: new HarmonyMethod(typeof(Program), nameof(SemanticObservePreparedNative)));
                boundary.Patch(AccessTools.PropertyGetter(typeof(Thought_Memory), nameof(Thought_Memory.DurationTicks)),
                    postfix: new HarmonyMethod(typeof(Program), nameof(SemanticObserveLateDuration)));
                SubmitAndRun(actor, target, SSCRomanceAction.Romance, out _);
                Require(semanticNativePrepared); Equal(0, semanticLateDurationReads);
                Equal(5, actor.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Count());
                Require(!actor.needs.mood.thoughts.memories.Memories.Any(m => m.def.defName == "RebuffedMyRomanceAttemptMood"));
                var replaced = old.Single(m => m.Data.SemanticGroup == SSCRomanceSemanticGroup.CommandRomance);
                Require(!actor.needs.mood.thoughts.memories.Memories.Contains(replaced));
            }
            finally { boundary.UnpatchAll(boundary.Id); semanticPreparedOwner = null; semanticNativePrepared = false; }
        });

        Check("semantic native final snapshot rejects post-snapshot age mutation", () =>
        {
            CommandReset(1f); socialOpinionValue = 30; var (actor, target) = CommandPair(SSCRomanceAction.Romance);
            var old = SeedFiveSemanticGroups(actor, target); var handler = actor.needs.mood.thoughts.memories;
            semanticNativeMutationOwner = actor; semanticNativeMutationDef = DefDatabase<ThoughtDef>.GetNamed("RebuffedMyRomanceAttemptMood");
            semanticNativeSnapshotMutated = false; var boundary = new Harmony("ssc.tests.semantic.native-snapshot-raw-fields");
            try
            {
                // 构造器边界不会依赖已被 JIT 内联的期限 getter，确定性制造“快照旧值、实例新值”。
                var state = production.GetType("SexSlaveCraft.SSCRomanceNativeMood+State");
                boundary.Patch(AccessTools.Constructor(state, new[] { typeof(Thought_Memory) }),
                    postfix: new HarmonyMethod(typeof(Program), nameof(SemanticMutateNativeAgeAfterSnapshot)));
                SubmitAndRun(actor, target, SSCRomanceAction.Romance, out _);
                Require(semanticNativeSnapshotMutated);
                Equal(1234, handler.Memories.Single(m => m.def == semanticNativeMutationDef).age);
                Require(old.All(handler.Memories.Contains)); Require(old.All(m => m.age == 321));
                Equal(5, handler.Memories.OfType<Thought_MemorySSCRomance>().Count());
            }
            finally { boundary.UnpatchAll(boundary.Id); semanticNativeMutationOwner = null; semanticNativeMutationDef = null; }
        });
    }

    /// <summary>只在请求候选已暂存时标记最终原版预检，早期资格预检不能冒充该边界已覆盖。</summary>
    private static void SemanticObservePreparedNative(Action __result)
    {
        if (__result == null || semanticPreparedOwner == null) return;
        if (semanticPreparedOwner.needs.mood.thoughts.memories.Memories.OfType<Thought_MemorySSCRomance>().Any(m =>
            AccessTools.Field(typeof(Thought_MemorySSCRomance), "FrozenRequest").GetValue(m) != null)) semanticNativePrepared = true;
    }

    /// <summary>模拟可补丁的期限 getter 边界；最终预检完成后不得再次读取暂存候选的虚拟期限。</summary>
    private static void SemanticObserveLateDuration(Thought_Memory __instance)
    {
        if (semanticNativePrepared && __instance is Thought_MemorySSCRomance memory && memory.pawn == semanticPreparedOwner &&
            AccessTools.Field(typeof(Thought_MemorySSCRomance), "FrozenRequest").GetValue(memory) != null) semanticLateDurationReads++;
    }

    /// <summary>候选暂存后，在原版快照构造完成时模拟一次读取回调改写，复现投影与真实字段分离。</summary>
    private static void SemanticMutateNativeAgeAfterSnapshot(Thought_Memory __0)
    {
        if (semanticNativeSnapshotMutated || semanticNativeMutationOwner == null || __0.def != semanticNativeMutationDef) return;
        var handler = semanticNativeMutationOwner.needs.mood.thoughts.memories;
        if (!handler.Memories.OfType<Thought_MemorySSCRomance>().Any(m =>
            AccessTools.Field(typeof(Thought_MemorySSCRomance), "FrozenRequest").GetValue(m) != null)) return;
        semanticNativeSnapshotMutated = true;
        __0.age = 1234;
    }
}
