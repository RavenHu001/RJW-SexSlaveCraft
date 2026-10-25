// AI 辅助生成：第二批提交前的状态变化窗口与真实原版入口复核。
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>可交互性检查可能被外部补丁回调；首次前缀通过后，副作用开始前仍须按当前状态拒绝。</summary>
    private static void Batch2ReviewTests(Harmony harmony)
    {
        foreach (var action in Enum.GetValues<SSCRomanceAction>())
            Check("batch2 review callback cannot bypass active denial before first effect " + action, () =>
            {
                ResetSocial(0f); var a = SocialSlave(); var b = SocialPawn(); if (action != SSCRomanceAction.Romance) a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
                socialDuringCanInteract = () => Comp(a).romanceConfig = new() { allowInitiateRomance = false, allowInitiateProposal = false, allowInitiateBreakup = false };
                Require(!a.interactions.TryInteractWith(b, SocialDef(action)));
                Equal(0, bubbleCalls + playLogCalls + taleCalls + shareBedCalls);
                Equal(0, a.needs.mood.thoughts.memories.Memories.Count + b.needs.mood.thoughts.memories.Memories.Count);
                Require(a.interactions.LastInteractionDef == null);
            });
        Check("batch2 review callback cannot propose after prerequisite lover relation disappears", () =>
        {
            ResetSocial(0f); var a = SocialSlave(); var b = SocialPawn(); a.relations.AddDirectRelation(PawnRelationDefOf.Lover, b);
            socialDuringCanInteract = () => a.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, b);
            Require(!a.interactions.TryInteractWith(b, InteractionDefOf.MarriageProposal));
            Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b)); Equal(0, bubbleCalls + playLogCalls);
        });
        Check("batch2 review callback cannot retain old-owner target under only-master rule", () =>
        {
            ResetSocial(0f); var oldOwner = SocialPawn(); var a = SocialSlave(oldOwner); var nextOwner = SocialPawn();
            Comp(a).romanceConfig = new() { initiateOnlyMaster = true }; socialDuringCanInteract = () => SSCBondUtility.GetChain(a).target = nextOwner;
            Require(!a.interactions.TryInteractWith(oldOwner, InteractionDefOf.RomanceAttempt)); Equal(0, bubbleCalls + playLogCalls);
        });
        ReviewFinalGuardTests(); ReviewNativeEntryTests();
    }

    /// <summary>验证共同入口的新末次检查安装于唯一原生锚点；分支到达时不得跳过，异常区不猜改。</summary>
    private static void ReviewFinalGuardTests()
    {
        var method = AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith");
        var enter = AccessTools.Method(typeof(SSCRomanceExecutionScope), nameof(SSCRomanceExecutionScope.BeforeEffects));
        Check("batch2 review final guard production transpiler is actually installed", () =>
            Require(Harmony.GetPatchInfo(method).Transpilers.Any(p => p.PatchMethod.DeclaringType == typeof(Harmony_SSCRomanceInteraction))));
        Check("batch2 review actual tracker IL gets one final guard without changing native probability calls", () =>
        {
            var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            var generator = new DynamicMethod("review_tracker", typeof(void), Type.EmptyTypes).GetILGenerator();
            var rewritten = Harmony_SSCRomanceInteraction.Transpiler(original, generator).ToList();
            Equal(original.Count + 8, rewritten.Count); Equal(1, rewritten.Count(c => c.Calls(enter)));
        });
        foreach (bool duplicate in new[] { false, true })
            Check("batch2 review final guard unmatched/nonunique anchor preserves original " + duplicate, () =>
            {
                var original = duplicate ? PatchProcessor.GetOriginalInstructions(method).ToList() : new List<CodeInstruction> { new(OpCodes.Ret) };
                if (duplicate) original.AddRange(PatchProcessor.GetOriginalInstructions(method));
                var generator = new DynamicMethod("review_tracker_fallback", typeof(void), Type.EmptyTypes).GetILGenerator();
                var rewritten = Harmony_SSCRomanceInteraction.Transpiler(original, generator).ToList();
                Require(original.Zip(rewritten).All(p => p.First.opcode == p.Second.opcode && Equals(p.First.operand, p.Second.operand))); Equal(original.Count, rewritten.Count);
            });
        Check("batch2 review final guard does not generate early ret inside external exception region", () =>
        {
            var original = PatchProcessor.GetOriginalInstructions(method).ToList(); original[0].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            var generator = new DynamicMethod("review_tracker_exception", typeof(void), Type.EmptyTypes).GetILGenerator();
            var rewritten = Harmony_SSCRomanceInteraction.Transpiler(original, generator).ToList(); Equal(original.Count, rewritten.Count); Equal(0, rewritten.Count(c => c.Calls(enter)));
            Equal(1, rewritten[0].blocks.Count);
        });
        Check("batch2 review both native cooldown branches must pass through moved final guard label", () =>
        {
            var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            var constructor = AccessTools.Constructor(typeof(List<RulePackDef>), Type.EmptyTypes);
            int at = original.FindIndex(c => c.opcode == OpCodes.Newobj && Equals(c.operand, constructor));
            var labels = original[at].labels.ToArray(); Require(labels.Length > 0);
            var generator = new DynamicMethod("review_tracker_labels", typeof(void), Type.EmptyTypes).GetILGenerator();
            var rewritten = Harmony_SSCRomanceInteraction.Transpiler(original, generator).ToList();
            Equal(OpCodes.Ldarg_0, rewritten[at].opcode); Require(labels.All(l => rewritten[at].labels.Contains(l)));
            Require(labels.All(l => !rewritten[at + 8].labels.Contains(l)));
        });
    }

    /// <summary>补查原 Toil 与原菜单方法的实际接线；地图出生及原菜单资格仍为明确的宿主边界。</summary>
    private static void ReviewNativeEntryTests()
    {
        Check("batch2 review actual original interaction Toil cannot bypass active denial", () =>
        {
            ResetSocial(); var a = SocialSlave(); var b = SocialPawn(); Comp(a).romanceConfig = new() { allowInitiateRomance = false };
            var maps = AccessTools.Field(typeof(Game), "maps"); var previousMaps = maps.GetValue(Current.Game);
            var mapIndex = AccessTools.Field(typeof(Thing), "mapIndexOrState"); var previousIndex = mapIndex.GetValue(b);
            try
            {
                // 只提供 Spawned 检查所需地图槽；禁止入口在任何地图业务执行前返回。
                maps.SetValue(Current.Game, new List<Map> { Bare<Map>() }); mapIndex.SetValue(b, Convert.ChangeType(0, mapIndex.FieldType));
                a.jobs = new Pawn_JobTracker(a); a.jobs.curJob = new Job { def = JobDefOf.TryRomance, targetA = b, interaction = InteractionDefOf.RomanceAttempt };
                var toil = Toils_Interpersonal.Interact(TargetIndex.A, InteractionDefOf.RomanceAttempt); toil.actor = a; toil.initAction();
                Equal(0, bubbleCalls + playLogCalls + taleCalls); Require(a.interactions.LastInteractionDef == null);
                Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            }
            finally { mapIndex.SetValue(b, previousIndex); maps.SetValue(Current.Game, previousMaps); }
        });
        Check("batch2 review actual original RomanceOption keeps passive-denied target native probability and actionable", () =>
        {
            var boundary = new Harmony("ssc.tests.romance-control.review-menu-boundary");
            try
            {
                // 提供原菜单资格与性向的可用输入，实际 RomanceOption 与生产 Postfix 都照常执行。
                boundary.Patch(AccessTools.Method(typeof(RelationsUtility), "RomanceEligiblePair"), prefix: new HarmonyMethod(typeof(Program), nameof(ReviewEligiblePair)));
                boundary.Patch(AccessTools.Method(typeof(RelationsUtility), "AttractedToGender", new[] { typeof(Pawn), typeof(Gender) }), prefix: new HarmonyMethod(typeof(Program), nameof(ReviewAttracted)));
                ResetSocial(); var a = SocialPawn(); var b = SocialSlave(a); Comp(b).romanceConfig = new() { allowAcceptRomance = false };
                Require(RelationsUtility.RomanceOption(a, b, out var option, out var chance)); Require(option.action != null); Equal(.6f, chance);
                Comp(b).romanceConfig.alwaysAcceptMaster = true; Require(RelationsUtility.RomanceOption(a, b, out option, out chance)); Require(option.action != null); Equal(1f, chance);
                var slaveActor = SocialSlave(); Comp(slaveActor).romanceConfig = new() { allowInitiateRomance = false };
                Require(!RelationsUtility.RomanceOption(slaveActor, SocialPawn(), out option, out chance)); Require(option.action == null);
            }
            finally { boundary.UnpatchAll(boundary.Id); }
        });
        Check("batch2 review NaN native acceptance remains rejected without SSC override", () =>
        {
            ResetSocial(0f); socialChance = float.NaN; var a = SocialPawn(); var b = SocialPawn();
            Require(a.interactions.TryInteractWith(b, InteractionDefOf.RomanceAttempt)); Require(!a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b));
            Require(a.needs.mood.thoughts.memories.Memories.Any(m => m.def == ThoughtDefOf.RebuffedMyRomanceAttempt));
        });
    }

    /// <summary>只隔离菜单资格的地图／身体输入，不替换生产主动策略或接收判断。</summary>
    private static bool ReviewEligiblePair(ref AcceptanceReport __result) { __result = true; return false; }
    /// <summary>只为原菜单方法的接线回归提供可用性向输入，不改变被测菜单后置逻辑。</summary>
    private static bool ReviewAttracted(ref bool __result) { __result = true; return false; }
}
