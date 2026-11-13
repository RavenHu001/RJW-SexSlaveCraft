// AI 辅助生成。真实调教组件与 Scribe 跨引用往返。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void TrainerAssignmentSaveTests()
    {
        Check("trainer assignments migrate the legacy reference and save without the old field", () =>
        {
            Pawn target = ImprintPawn(PawnIdentity.Slave);
            Pawn trainer = ImprintPawn(PawnIdentity.Master);
            string path = SaveTrainerAssignments(target.TryGetComp<CompSexSlaveTraining>());
            Edit(path, xml =>
            {
                XElement node = xml.Root.Element("training");
                node.Element("sscTrainerAssignmentsVersion").Remove();
                node.Element("sscAssignedTrainers").Remove();
                node.Add(new XElement("selectedTrainer", trainer.GetUniqueLoadID()));
            });

            var migrated = LoadTrainerAssignments(path, trainer);
            Require(migrated.CopyAssignedTrainers().SequenceEqual(new[] { trainer }));
            path = SaveTrainerAssignments(migrated);
            Require(XDocument.Load(path).Root.Element("training").Element("selectedTrainer") == null);
        });

        Check("trainer assignments native ordered off-map members and explicit empty", () =>
        {
            Pawn target = ImprintPawn(PawnIdentity.Slave);
            Pawn a = ImprintPawn(PawnIdentity.Master);
            Pawn b = ImprintPawn(PawnIdentity.Master);
            var training = target.TryGetComp<CompSexSlaveTraining>();
            training.ReplaceAssignedTrainers(new[] { b, a });
            var loaded = LoadTrainerAssignments(SaveTrainerAssignments(training), a, b);
            Require(loaded.CopyAssignedTrainers().SequenceEqual(new[] { b, a }));

            loaded.ClearAssignedTrainers();
            string path = SaveTrainerAssignments(loaded);
            Edit(path, xml => xml.Root.Element("training").Add(new XElement("selectedTrainer", a.GetUniqueLoadID())));
            Equal(0, LoadTrainerAssignments(path, a, b).AssignedTrainerCount);
        });

        Check("trainer assignments owner-only coordination preserves a dead owner's lock", () =>
        {
            HediffDef previousChain = SSCDefOf.ChainOfSexSlave;
            SSCSettings previousSettings = SSCMod.settings;
            try
            {
                SSCMod.settings = new SSCSettings { enableSexSlaveProtectionRules = true };
                SSCDefOf.ChainOfSexSlave = previousChain ?? ImprintBoundaryDef("Hediff_ChainOfSexSlave", typeof(Hediff_ChainOfSexSlave));
                Pawn target = ImprintPawn(PawnIdentity.Slave);
                Pawn owner = ImprintPawn(PawnIdentity.Master);
                Pawn other = ImprintPawn(PawnIdentity.Master);
                target.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave
                    { def = SSCDefOf.ChainOfSexSlave, pawn = target, target = owner });
                var training = target.TryGetComp<CompSexSlaveTraining>();
                training.restrictionConfig = new SSCRestrictionConfig();
                training.ReplaceAssignedTrainers(new[] { other, owner });
                SSCRestrictionLifecycle.CoordinateTrainer(target);
                Require(training.CopyAssignedTrainers().SequenceEqual(new[] { owner }));

                AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(owner.health, PawnHealthState.Dead);
                SSCRestrictionLifecycle.CoordinateTrainer(target);
                Equal(0, training.AssignedTrainerCount);
                Equal(owner, SSCRestrictionLifecycle.GetForcedTrainer(target));
            }
            finally
            {
                SSCDefOf.ChainOfSexSlave = previousChain;
                SSCMod.settings = previousSettings;
            }
        });

        Check("trainer overview refreshes when only the second member changes and invalidates the old portrait", () =>
        {
            var state = Current.ProgramState;
            HediffDef chain = SSCDefOf.ChainOfSexSlave, bridle = SSCDefOf.BridleOfSexSlave;
            try
            {
                Current.ProgramState = ProgramState.Playing;
                SSCDefOf.ChainOfSexSlave ??= ImprintBoundaryDef("TrainerOverviewChain", typeof(Hediff_ChainOfSexSlave));
                SSCDefOf.BridleOfSexSlave ??= ImprintBoundaryDef("TrainerOverviewBridle", typeof(Hediff_BridleOfSexSlave));
                Pawn target = ImprintPawn(PawnIdentity.Slave);
                Pawn a = ImprintPawn(PawnIdentity.Master), b = ImprintPawn(PawnIdentity.Master), c = ImprintPawn(PawnIdentity.Master);
                var comp = target.TryGetComp<CompSexSlaveTraining>();
                comp.ReplaceAssignedTrainers(new[] { a, b });
                var view = new SSCBondOverviewView(target, null)
                    { Snapshot = SSCBondOverviewUtility.BuildLocal(target, null) };
                view.DirectChanged(true);
                Require(view.Snapshot.AssignedTrainers.Select(row => row.Pawn).SequenceEqual(new[] { a, b }));
                Require(SSCBondOverviewNavigation.IsTrainerCurrent(view.Snapshot, b));

                comp.ReplaceAssignedTrainers(new[] { a, c });
                Require(view.DirectChanged());
                var next = SSCBondOverviewUtility.BuildLocal(target, null);
                Require(!next.SamePresentation(view.Snapshot));
                Require(!SSCBondOverviewNavigation.IsTrainerCurrent(view.Snapshot, b));
                Require(SSCBondOverviewNavigation.IsTrainerCurrent(next, c));
            }
            finally
            {
                Current.ProgramState = state;
                SSCDefOf.ChainOfSexSlave = chain;
                SSCDefOf.BridleOfSexSlave = bridle;
            }
        });

        Check("later active trainer supplies the self-training snapshot and unbound progress exclusion", () =>
        {
            HediffDef chain = SSCDefOf.ChainOfSexSlave;
            try
            {
                SSCDefOf.ChainOfSexSlave ??= ImprintBoundaryDef("TrainerAssociationChain", typeof(Hediff_ChainOfSexSlave));
                Pawn target = ImprintPawn(PawnIdentity.Slave);
                Pawn inactive = ImprintPawn();
                Pawn active = ImprintPawn(PawnIdentity.Master);
                var training = target.TryGetComp<CompSexSlaveTraining>();
                training.ReplaceAssignedTrainers(new[] { inactive, active });
                var link = new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, pawn = target };
                target.health.hediffSet.hediffs.Add(link);

                Equal(active, BusSpecializationUtility.GetOwner(target));
                var snapshot = SSCSelfTrainingUtility.CaptureAtSceneStart(target);
                Require(snapshot?.Captured == true && snapshot.ImaginedPawn == active);

                Pawn owner = ImprintPawn(PawnIdentity.Master);
                link.target = owner;
                Equal(owner, BusSpecializationUtility.GetOwner(target));
                training.RemoveAssignedTrainer(active);
                Equal(owner, SSCSelfTrainingUtility.GetImaginedPawn(target));

                string path = Path.Combine(saveDirectory, "trainer-self-snapshot-" + ++nextId + ".xml");
                Scribe.saver.InitSaving(path, "TrainerSelfTrainingSnapshot");
                Scribe_Deep.Look(ref snapshot, "snapshot");
                Scribe.saver.FinalizeSaving();
                SSCSelfTrainingSnapshot loaded = null;
                Scribe.loader.InitLoading(path);
                var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
                directory.RegisterLoaded(active);
                Scribe_Deep.Look(ref loaded, "snapshot");
                Scribe.loader.FinalizeLoading();
                Require(loaded.Captured && loaded.ImaginedPawn == active &&
                    loaded.ImaginedPawnLabelAtStart == snapshot.ImaginedPawnLabelAtStart);
            }
            finally { SSCDefOf.ChainOfSexSlave = chain; }
        });
    }

    private static string SaveTrainerAssignments(CompSexSlaveTraining training)
    {
        string path = Path.Combine(saveDirectory, "trainer-assignment-" + ++nextId + ".xml");
        var fixture = new TrainerAssignmentSaveFixture(training);
        Scribe.saver.InitSaving(path, "TrainerAssignmentFixture");
        Scribe_Deep.Look(ref fixture, "training");
        Scribe.saver.FinalizeSaving();
        return path;
    }

    private static CompSexSlaveTraining LoadTrainerAssignments(string path, params Pawn[] members)
    {
        var training = ImprintPawn(PawnIdentity.Slave).TryGetComp<CompSexSlaveTraining>();
        TrainerAssignmentSaveFixture fixture = null;
        Scribe.loader.InitLoading(path);
        // 宿主以已存在的离图 Pawn 代替原版世界对象重建，引用解析与组件生命周期仍执行生产代码。
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (Pawn member in members) directory.RegisterLoaded(member);
        Scribe_Deep.Look(ref fixture, "training", training);
        Scribe.loader.FinalizeLoading();
        return fixture.Training;
    }
}

public sealed class TrainerAssignmentSaveFixture : IExposable
{
    public CompSexSlaveTraining Training { get; }
    public TrainerAssignmentSaveFixture(CompSexSlaveTraining training) => Training = training;
    public void ExposeData() => Training.PostExposeData();
}
