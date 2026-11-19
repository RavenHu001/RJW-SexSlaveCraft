using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly List<LookTargets> fullImprintLetters = new();
    private static readonly List<(string Key, TaggedString Body, Pawn Pawn)> imprintLetters = new();
    private static readonly string[] imprintLetterKeys =
    {
        "Qualification", "SeedMaster", "SeedSlave", "BindingMaster", "BindingSlave", "FullCorruption", "ImplantedSlave", "CopiedSlave"
    };
    private static Pawn observedImprintPawn;
    private static Action<Hediff_SSCImprint> onDifferentiatedImprintAdded;
    private static Hediff throwAfterRollbackRemoval;

    private static void FullCorruptionImprintTests(string repo)
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
            ImprintAcquisitionLetterTests(boundary);

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

            Check("first failed refresh holds full corruption for a second retry that sends one letter", () =>
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

                FailNextFullImprint(pawn);
                need.NeedInterval();
                Equal(1f, need.CurLevel);
                Require(SSCImprintUtility.Find(pawn) == initial);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(0, fullImprintLetters.Count);
                // 仍为满值的常规写入不能抢先消费第二次刷新机会。
                need.SetCorruption(1f);
                Equal(0, fullImprintLetters.Count);

                need.NeedInterval();
                Equal(SSCImprintKind.Slave, SSCImprintUtility.GetKind(pawn));
                Equal(PawnIdentity.Slave, SSCIdentityUtility.GetIdentity(pawn));
                Equal(SSCImprintUtility.YoungAgeTicks, pawn.ageTracker.AgeBiologicalTicks);
                Require(need.CurLevel < 1f);
                need.NeedInterval();
                Equal(1, fullImprintLetters.Count);
            });

            Check("second failed refresh clears the pending imprint and lets corruption decay", () =>
            {
                fullImprintLetters.Clear();
                var pawn = ImprintPawn();
                var initial = Mark(pawn, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(pawn);
                FailNextFullImprint(pawn);
                need.SetCorruption(1f);
                FailNextFullImprint(pawn);
                need.NeedInterval();
                Equal(1f, need.CurLevel);
                FailNextFullImprint(pawn);
                need.NeedInterval();
                Require(need.CurLevel < 1f);
                Require(SSCImprintUtility.Find(pawn) == initial);
                Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(pawn));
                Equal(0, fullImprintLetters.Count);

                // 恢复满值不登记新事件。未清理的旧待办会在这次刷新发信。
                need.RestoreCorruption(1f, 1f);
                need.NeedInterval();
                Equal(0, fullImprintLetters.Count);
                need.SetCorruption(1f);
                Equal(1, fullImprintLetters.Count);
            });

            Check("selecting master cancels the pending imprint even if the pawn leaves that role before refresh", () =>
            {
                fullImprintLetters.Clear();
                var pawn = ImprintPawn();
                var initial = Mark(pawn, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(pawn);
                FailNextFullImprint(pawn);
                need.SetCorruption(1f);
                pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
                Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
                Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
                need.NeedInterval();
                Require(SSCImprintUtility.Find(pawn) == initial);
                Require(need.CurLevel < 1f);
                Equal(0, fullImprintLetters.Count);
            });

            Check("implanting another personality discards a failed full imprint request", () =>
            {
                fullImprintLetters.Clear();
                var host = ImprintPawn();
                var oldInitial = Mark(host, SSCImprintUtility.InitialDefName);
                var need = AddFullImprintNeed(host);
                FailNextFullImprint(host);
                need.SetCorruption(1f);

                var source = ImprintPawn();
                Mark(source, SSCImprintUtility.InitialDefName);
                AddFullImprintNeed(source).RestoreCorruption(.50f, .50f);
                var data = new CompPersonalityStore();
                data.StorePawnData(source);
                Require(ExcretionUtility.InheritEverything(host, data));
                var newInitial = SSCImprintUtility.Find(host);
                Require(newInitial != oldInitial);
                Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(host));
                need.RestoreCorruption(1f, 1f);
                need.NeedInterval();
                Require(SSCImprintUtility.Find(host) == newInitial);
                Require(need.CurLevel < 1f);
                Equal(0, fullImprintLetters.Count);
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
                    imprintLetters.Clear();
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
                        Require(imprintLetters.Select(x => (x.Key, x.Pawn)).SequenceEqual(
                            new[] { ("BindingMaster", master), ("FullCorruption", slave) }));
                        Require(fullImprintLetters[0].targets.Single().Thing == slave);
                        Require(SSCBondUtility.IsBoundTo(slave, master));
                        need.SetCorruption(1f);
                        Equal(1, fullImprintLetters.Count);
                    }
                    finally { SSCMod.settings.useOldScoring = false; }
                }
            });
            SlaveBreakdownTests(repo, receiveLetter);
        }
        finally
        {
            observedImprintPawn = null;
            onDifferentiatedImprintAdded = null;
            throwAfterRollbackRemoval = null;
            fullImprintLetters.Clear();
            imprintLetters.Clear();
            boundary.UnpatchAll(boundary.Id);
            letterStackField.SetValue(Current.Game, previousLetterStack);
        }
    }

    // 走真实业务入口并在 LetterStack 边界采集途径和目标。文案内容及占位符的跨语一致性由资源检查负责。
    private static void ImprintAcquisitionLetterTests(Harmony boundary)
    {
        Check("seed letters distinguish research unlock, personal qualification, and both mature forms", () =>
        {
            imprintLetters.Clear();
            var techprintCount = AccessTools.PropertyGetter(typeof(ResearchProjectDef), nameof(ResearchProjectDef.TechprintCount));
            var oneTechprint = AccessTools.Method(typeof(Program), nameof(TrainingRequiresOneTechprint));
            boundary.Patch(techprintCount, prefix: new HarmonyMethod(oneTechprint));
            var research = SSCDefOf.SSC_BasicTraining;
            float previousCost = research.baseCost;
            research.baseCost = 500f;
            try
            {
                var first = ImprintPawn();
                var seed = Seed();
                UseSeed(first, seed);
                Equal(1, research.TechprintsApplied);
                var later = ImprintPawn();
                UseSeed(later, Seed());
                UseSeed(first, seed);
                var slave = ImprintPawn(PawnIdentity.Slave);
                UseSeed(slave, Seed());
                Require(!SSCMasterQualificationUtility.TryUseQualificationItem(first, seed, out _));

                Require(imprintLetters.Select(x => (x.Key, x.Pawn)).SequenceEqual(
                    new[] { ("Qualification", first), ("Qualification", later), ("SeedMaster", first), ("SeedSlave", slave) }));
                string researchText = "SSC_Imprint_QualificationResearch".Translate().RawText;
                Require(imprintLetters[0].Body.RawText.Contains(researchText));
                Require(!imprintLetters[1].Body.RawText.Contains(researchText));
            }
            finally
            {
                research.baseCost = previousCost;
                boundary.Unpatch(techprintCount, oneTechprint);
            }
        });

        Check("ordinary binding sends two distinct letters once and relation restore sends none", () =>
        {
            imprintLetters.Clear();
            var master = ImprintPawn(PawnIdentity.Master);
            var slave = ImprintPawn(PawnIdentity.Slave);
            Mark(master, SSCImprintUtility.InitialDefName);
            Mark(slave, SSCImprintUtility.InitialDefName);
            Require(SSCBondUtility.Bind(master, slave));
            Require(SSCBondUtility.Bind(master, slave));
            Require(imprintLetters.Select(x => (x.Key, x.Pawn)).SequenceEqual(
                new[] { ("BindingMaster", master), ("BindingSlave", slave) }));

            var another = ImprintPawn(PawnIdentity.Slave);
            Mark(another, SSCImprintUtility.InitialDefName);
            Require(SSCBondUtility.RestorePersonalityBinding(master, another));
            Equal(2, imprintLetters.Count);
        });

        Check("completed implantation sends a mature slave letter for each incoming personality", () =>
        {
            imprintLetters.Clear();
            var source = ImprintPawn(PawnIdentity.Slave);
            Mark(source, SSCImprintUtility.SlaveDefName);
            var data = new CompPersonalityStore();
            data.StorePawnData(source);
            var host = ImprintPawn();
            Require(ExcretionUtility.InheritEverything(host, data));
            Require(ExcretionUtility.InheritEverything(host, data));
            var master = ImprintPawn(PawnIdentity.Master);
            Mark(master, SSCImprintUtility.MasterDefName);
            data.StorePawnData(master);
            Require(ExcretionUtility.InheritEverything(host, data));
            Require(imprintLetters.Select(x => (x.Key, x.Pawn)).SequenceEqual(
                new[] { ("ImplantedSlave", host), ("ImplantedSlave", host) }));
        });

        Check("visible copy completion announces the retained slave imprint once", () =>
        {
            imprintLetters.Clear();
            var visible = AccessTools.TypeByName("SexSlaveCraft.Harmony_SSCImprintVisibleDuplicate");
            var awakening = AccessTools.TypeByName("SexSlaveCraft.Harmony_SSCImprintAwokenDuplicate");
            // 校验实际 Harmony 目标和参数绑定；地图生成的边界由下面的完成回调夹具提供。
            boundary.CreateClassProcessor(visible).Patch();
            boundary.CreateClassProcessor(awakening).Patch();
            var pawn = ImprintPawn(PawnIdentity.Slave);
            Mark(pawn, SSCImprintUtility.SlaveDefName);
            var mapIndex = AccessTools.Field(typeof(Thing), "mapIndexOrState");
            mapIndex.SetValue(pawn, (sbyte)-1);
            Hidden(visible, null, "Postfix", true, pawn, true);
            Equal(0, imprintLetters.Count);

            var mapsField = AccessTools.Field(typeof(Game), "maps");
            object previousMaps = mapsField.GetValue(Current.Game);
            mapsField.SetValue(Current.Game, new List<Map> { Bare<Map>() });
            mapIndex.SetValue(pawn, (sbyte)0);
            var awoken = ImprintPawn(PawnIdentity.Slave);
            Mark(awoken, SSCImprintUtility.SlaveDefName);
            mapIndex.SetValue(awoken, (sbyte)0);
            try
            {
                Require(pawn.Spawned);
                // 公开完成、同一实例的重复回调以及异常尸体首次与后续觉醒使用各自的完成条件。
                Hidden(visible, null, "Postfix", false, pawn, true);
                Equal(0, imprintLetters.Count);
                Hidden(visible, null, "Postfix", true, pawn, true);
                Hidden(visible, null, "Postfix", true, pawn, true);
                Hidden(awakening, null, "Postfix", true, awoken, true);
                Equal(1, imprintLetters.Count);
                Hidden(awakening, null, "Postfix", true, awoken, false);
                Require(imprintLetters.Select(x => (x.Key, x.Pawn)).SequenceEqual(
                    new[] { ("CopiedSlave", pawn), ("CopiedSlave", awoken) }));
            }
            finally
            {
                mapIndex.SetValue(pawn, (sbyte)-1);
                mapIndex.SetValue(awoken, (sbyte)-1);
                mapsField.SetValue(Current.Game, previousMaps);
            }
        });
    }

    // 模拟 Royalty 开放蓝图的入口条件；蓝图计数本身由真实 ResearchManager 更新。
    private static bool TrainingRequiresOneTechprint(ResearchProjectDef __instance, ref int __result)
    {
        if (__instance != SSCDefOf.SSC_BasicTraining) return true;
        __result = 1;
        return false;
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

    private static void FailNextFullImprint(Pawn pawn)
    {
        observedImprintPawn = pawn;
        onDifferentiatedImprintAdded = _ => throw new InvalidOperationException("Simulated imprint PostAdd failure.");
    }

    // 发信使用原版公开入口，测试在 UI 投递边界截获小人目标。
    private static bool CaptureFullImprintLetter(TaggedString label, TaggedString text, LookTargets lookTargets)
    {
        string key = imprintLetterKeys.FirstOrDefault(k => label.RawText == ("SSC_Imprint_" + k + "LetterLabel").Translate().RawText);
        if (key == null) return true;
        imprintLetters.Add((key, text, (Pawn)lookTargets.targets.Single().Thing));
        if (key == "FullCorruption") fullImprintLetters.Add(lookTargets);
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
