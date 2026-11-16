using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly List<LookTargets> fullImprintLetters = new();
    private static Pawn observedImprintPawn;
    private static Action<Hediff_SSCImprint> onDifferentiatedImprintAdded;
    private static Hediff throwAfterRollbackRemoval;

    private static void FullCorruptionImprintTests()
    {
        var boundary = new Harmony("ssc.tests.full-corruption-imprint");
        var letterStackField = AccessTools.Field(typeof(Game), "letterStack");
        object previousLetterStack = letterStackField.GetValue(Current.Game);
        var receiveLetter = AccessTools.GetDeclaredMethods(typeof(LetterStack))
            .Single(method => method.Name == nameof(LetterStack.ReceiveLetter) && method.GetParameters().Length == 10);
        try
        {
            letterStackField.SetValue(Current.Game, new LetterStack());
            boundary.Patch(receiveLetter, prefix: new HarmonyMethod(typeof(Program), nameof(CaptureFullImprintLetter)));
            boundary.Patch(AccessTools.PropertyGetter(typeof(Need), "IsFrozen"),
                prefix: new HarmonyMethod(typeof(Program), nameof(FullImprintNeedNeverFrozen)));
            boundary.Patch(AccessTools.Method(typeof(Hediff_SSCImprint), nameof(Hediff_SSCImprint.PostAdd)),
                postfix: new HarmonyMethod(typeof(Program), nameof(ObserveDifferentiatedImprint)));
            boundary.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff)),
                postfix: new HarmonyMethod(typeof(Program), nameof(ThrowAfterRollbackRemoval)));

            Check("full imprint uses the actual value and selects the SSC slave identity without a bond", () =>
            {
                var pawn = ImprintPawn(age: 12);
                pawn.RaceProps.lifeStageAges[0].def.developmentalStage = DevelopmentalStage.Child;
                var initial = Mark(pawn, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(pawn);
                need.RestoreCorruption(.98f, 1f);
                need.SetCorruption(MathF.BitDecrement(1f));
                Require(SSCImprintUtility.Find(pawn) == initial);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(0, fullImprintLetters.Count);

                need.SetCorruption(1f);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));
                Require(SSCBondUtility.GetBoundMaster(pawn) == null);
                Require(SSCIdentityUtility.IsIdentityLocked(pawn));
                Equal(1, fullImprintLetters.Count);
                Require(fullImprintLetters[0].targets.Single().Thing == pawn);
                need.SetCorruption(1f);
                need.NeedInterval();
                Equal(1, fullImprintLetters.Count);
            });

            Check("full corruption preserves a selected SSC master and does not backfill a restored need", () =>
            {
                fullImprintLetters.Clear();
                var master = ImprintPawn(PawnIdentity.Master);
                var masterInitial = Mark(master, SSCImprintUtility.InitialDefName);
                AddFullImprintNeed(master).SetCorruption(1f);
                Equal(PawnIdentity.Master, SSCIdentityUtility.GetIdentity(master));
                Require(SSCImprintUtility.Find(master) == masterInitial);

                var slave = ImprintPawn(PawnIdentity.Slave);
                var slaveInitial = Mark(slave, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(slave);
                need.RestoreCorruption(1f, 1f);
                need.NeedInterval();
                Require(SSCImprintUtility.Find(slave) == slaveInitial);
                Equal(0, fullImprintLetters.Count);
            });

            Check("personality implantation evaluates full corruption after restoring the identity and initial imprint", () =>
            {
                fullImprintLetters.Clear();
                var source = ImprintPawn(PawnIdentity.Slave);
                Mark(source, SSCImprintUtility.InitialDefName);
                AddFullImprintNeed(source).RestoreCorruption(1f, 1f);
                var data = new CompPersonalityStore();
                data.StorePawnData(source);

                var host = ImprintPawn();
                var need = AddFullImprintNeed(host);
                Require(ExcretionUtility.InheritEverything(host, data));
                Near(1f, need.CurLevel);
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(host));
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(host));
                Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(source));
                Equal(1, fullImprintLetters.Count);
                Require(fullImprintLetters[0].targets.Single().Thing == host);

                var master = ImprintPawn(PawnIdentity.Master);
                Mark(master, SSCImprintUtility.InitialDefName);
                AddFullImprintNeed(master).RestoreCorruption(1f, 1f);
                data.StorePawnData(master);
                Require(ExcretionUtility.InheritEverything(host, data));
                Near(1f, need.CurLevel);
                Equal(PawnIdentity.Master, SSCIdentityUtility.GetIdentity(host));
                Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(host));
                Equal(1, fullImprintLetters.Count);
            });

            Check("failed replacement restores the same initial imprint and identity before a successful need retry", () =>
            {
                fullImprintLetters.Clear();
                var pawn = ImprintPawn(age: 40);
                var initial = Mark(pawn, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(pawn);
                observedImprintPawn = pawn;
                onDifferentiatedImprintAdded = imprint =>
                {
                    throwAfterRollbackRemoval = imprint;
                    throw new InvalidOperationException("Simulated imprint PostAdd failure.");
                };
                need.SetCorruption(1f);

                Require(onDifferentiatedImprintAdded == null && throwAfterRollbackRemoval == null);
                Require(SSCImprintUtility.Find(pawn) == initial);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(40L * GenDate.TicksPerYear, pawn.ageTracker.AgeBiologicalTicks);
                Equal(0, fullImprintLetters.Count);

                need.NeedInterval();
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks);
                Equal(1, fullImprintLetters.Count);
            });

            Check("implantation during an imprint callback queues the incoming full personality", () =>
            {
                fullImprintLetters.Clear();
                var source = ImprintPawn(PawnIdentity.Slave);
                Mark(source, SSCImprintUtility.InitialDefName);
                AddFullImprintNeed(source).RestoreCorruption(1f, 1f);
                var data = new CompPersonalityStore();
                data.StorePawnData(source);

                var host = ImprintPawn();
                var oldInitial = Mark(host, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(host);
                bool implanted = false;
                observedImprintPawn = host;
                onDifferentiatedImprintAdded = _ => implanted = ExcretionUtility.InheritEverything(host, data);
                need.SetCorruption(1f);
                Require(implanted);
                Require(SSCImprintUtility.Find(host) != oldInitial);
                Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(host));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(host));
                Equal(0, fullImprintLetters.Count);

                need.NeedInterval();
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(host));
                Equal(1, fullImprintLetters.Count);
                Require(fullImprintLetters[0].targets.Single().Thing == host);
            });

            Check("first ritual binding shares full-imprint success and defers a failed attempt to the next refresh", () =>
            {
                foreach (bool legacy in new[] { false, true })
                {
                    fullImprintLetters.Clear();
                    SSCMod.settings.useOldScoring = legacy;
                    try
                    {
                        var master = ImprintPawn(PawnIdentity.Master);
                        Mark(master, SSCImprintUtility.InitialDefName);
                        AddFullImprintNeed(master).SetCorruption(1f);
                        Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(master));
                        var slave = ImprintPawn(PawnIdentity.Slave);
                        var initial = Mark(slave, SSCImprintUtility.InitialDefName);
                        var need = AddFullImprintNeed(slave);
                        need.RestoreCorruption(.90f, .90f);
                        if (legacy)
                        {
                            observedImprintPawn = slave;
                            onDifferentiatedImprintAdded = imprint => slave.health.RemoveHediff(imprint);
                        }

                        ConditioningUtility.ExecuteRitualOutcome(master, slave, 1f);
                        Require(SSCBondUtility.IsBoundTo(slave, master));
                        Equal(SSCImprintKind.Master, SSCImprintUtility.GetKind(master));
                        Near(1f, need.CurLevel);
                        if (legacy)
                        {
                            Require(onDifferentiatedImprintAdded == null);
                            Require(SSCImprintUtility.Find(slave) == initial);
                            Equal(0, fullImprintLetters.Count);
                            need.NeedInterval();
                        }
                        Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(slave));
                        Equal(1, fullImprintLetters.Count);
                        Require(fullImprintLetters[0].targets.Single().Thing == slave);
                        Require(SSCBondUtility.IsBoundTo(slave, master));
                        need.SetCorruption(1f);
                        Equal(1, fullImprintLetters.Count);
                    }
                    finally { SSCMod.settings.useOldScoring = false; }
                }
            });
        }
        finally
        {
            observedImprintPawn = null;
            onDifferentiatedImprintAdded = null;
            throwAfterRollbackRemoval = null;
            fullImprintLetters.Clear();
            boundary.UnpatchAll(boundary.Id);
            letterStackField.SetValue(Current.Game, previousLetterStack);
        }
    }

    // 无界面宿主没有世界小人管理器，需求本身仍使用原版追踪器和生产刷新逻辑。
    private static bool FullImprintNeedNeverFrozen(Need __instance, ref bool __result)
    {
        if (__instance.def?.defName != "SSC_FullImprintTestCorruption") return true;
        __result = false;
        return false;
    }

    private static Need_Corruption AddFullImprintNeed(Pawn pawn)
    {
        var need = new Need_Corruption(pawn)
        {
            def = new NeedDef { defName = "SSC_FullImprintTestCorruption", needClass = typeof(Need_Corruption) }
        };
        pawn.needs.AllNeeds.Add(need);
        return need;
    }

    // 发信使用原版公开入口，测试在 UI 投递边界截获小人目标。
    private static bool CaptureFullImprintLetter(TaggedString label, LookTargets lookTargets)
    {
        if (label.RawText != "SSC_Imprint_FullCorruptionLetterLabel".Translate().RawText) return true;
        fullImprintLetters.Add(lookTargets);
        return false;
    }

    // 在真实健康添加后模拟外部回调。先消费回调，避免嵌套健康写入重放。
    private static void ObserveDifferentiatedImprint(Hediff_SSCImprint __instance)
    {
        if (__instance.pawn != observedImprintPawn || __instance.def.defName != SSCImprintUtility.SlaveDefName) return;
        var callback = onDifferentiatedImprintAdded;
        observedImprintPawn = null;
        onDifferentiatedImprintAdded = null;
        callback?.Invoke(__instance);
    }

    private static void ThrowAfterRollbackRemoval(Hediff hediff)
    {
        if (hediff != throwAfterRollbackRemoval) return;
        throwAfterRollbackRemoval = null;
        throw new InvalidOperationException("Simulated callback failure after removing the rejected imprint.");
    }
}
