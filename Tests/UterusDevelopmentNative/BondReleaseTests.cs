using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Action<Hediff_SSCOwnerlessSlave> onReleaseProgressAdded;
    private static void ReleaseProgressAdded(Hediff_SSCOwnerlessSlave __instance) => onReleaseProgressAdded?.Invoke(__instance);
    private static Action<Hediff_ChainOfSexSlave> onReleaseChainRemoved;
    private static void ReleaseChainRemoved(Hediff_ChainOfSexSlave __instance) => onReleaseChainRemoved?.Invoke(__instance);

    private static void BondReleaseTests(string repo)
    {
        var xml = new XmlDocument();
        xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_BondReleaseAftereffect.xml"));
        if (DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal") == null)
            DefDatabase<StatDef>.Add(new StatDef { defName = "WorkSpeedGlobal", defaultBaseValue = 1f });
        var aftereffectDef = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement["HediffDef"], false);
        DefDatabase<HediffDef>.Add(aftereffectDef);
        xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_BondReleaseAftereffect.xml"));
        var mood = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement["ThoughtDef"], false);
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);

        (Pawn Master, Pawn Subject, Pawn Other, Hediff_ChainOfSexSlave Chain) Bound(float severity)
        {
            var master = ImprintPawn(PawnIdentity.Master);
            var subject = ImprintPawn(PawnIdentity.Slave);
            var other = ImprintPawn(PawnIdentity.Master);
            Require(SSCBondUtility.Bind(master, subject));
            var chain = SSCBondUtility.GetChain(subject);
            chain.Severity = severity;
            subject.TryGetComp<CompSexSlaveTraining>().ReplaceAssignedTrainers(new[] { master, other });
            return (master, subject, other, chain);
        }

        Check("bond release transfers exact third-stage and zero-stage progress and starts one two-day aftereffect", () =>
        {
            foreach (var (severity, stage) in new[] { (.8076543f, 3), (0f, 0) })
            {
                var (master, subject, other, _) = Bound(severity);
                var need = AddFullImprintNeed(subject);
                need.RestoreCorruption(.86f, .94f);
                var request = new SSCBondUtility.ReleaseContext(subject);
                Require(SSCBondUtility.TryReleaseToOwnerless(request));
                var record = Hediff_SSCOwnerlessSlave.Find(subject);
                var effect = Hediff_SSCBondReleaseAftereffect.Find(subject);
                var timer = effect.GetComp<HediffComp_Disappears>();
                Equal(severity, record.Severity);
                Equal(stage, Hediff_SSCOwnerlessSlave.GetActiveStage(subject));
                Require(!record.ShouldRemove && SSCIdentityUtility.IsSexSlave(subject) && SSCIdentityUtility.IsIdentityLocked(subject));
                Require(SSCBondUtility.GetChain(subject) == null && SSCBondUtility.GetBridle(master) == null);
                Require(subject.TryGetComp<CompSexSlaveTraining>().CopyAssignedTrainers().SequenceEqual(new[] { other }));
                Equal(.86f, need.CurLevel);
                Equal(.94f, need.HighestCorruptionLevel);
                Equal(120000, timer.ticksToDisappear);
                timer.ticksToDisappear = 1;
                Require(!SSCBondUtility.TryReleaseToOwnerless(request));
                Require(!SSCBondUtility.TryReleaseToOwnerless(new SSCBondUtility.ReleaseContext(subject)));
                Equal(1, subject.health.hediffSet.hediffs.OfType<Hediff_SSCBondReleaseAftereffect>().Count());
                Equal(1, timer.ticksToDisappear);
                Require(!SSCBondUtility.CanBindNewWithProgress(other, subject));

                subject.needs.mood = new Need_Mood(subject);
                var thoughts = new List<Thought>();
                subject.needs.mood.thoughts.situational.AppendMoodThoughts(mood, thoughts);
                Equal(-8f, thoughts.Single().MoodOffset());
                Equal(.75f, effect.CurStage.statFactors.Single().value);
                subject.health.HealthTick();
                Require(Hediff_SSCBondReleaseAftereffect.Find(subject) == null);
                Require(SSCBondUtility.CanBindNewWithProgress(other, subject));
                thoughts.Clear();
                subject.needs.mood.thoughts.situational.AppendMoodThoughts(mood, thoughts);
                Require(thoughts.Count == 0);
            }
        });

        Check("bond release abandons its staged record when the original binding changes in a health callback", () =>
        {
            var (_, subject, other, chain) = Bound(.8076543f);
            var patch = new Harmony("ssc.tests.bond-release.staged-progress");
            patch.Patch(AccessTools.Method(typeof(Hediff_SSCOwnerlessSlave), nameof(Hediff_SSCOwnerlessSlave.PostAdd)),
                postfix: new HarmonyMethod(typeof(Program), nameof(ReleaseProgressAdded)));
            try
            {
                var request = new SSCBondUtility.ReleaseContext(subject);
                onReleaseProgressAdded = written =>
                {
                    if (written.pawn != subject) return;
                    Require(!SSCBondUtility.TryReleaseToOwnerless(request));
                    Require(SSCBondUtility.Bind(other, subject, replaceExisting: true));
                };
                Require(!SSCBondUtility.TryReleaseToOwnerless(request));
                Require(SSCBondUtility.IsBoundTo(subject, other));
                Equal(.8076543f, chain.Severity);
                Require(Hediff_SSCOwnerlessSlave.Find(subject) == null && Hediff_SSCBondReleaseAftereffect.Find(subject) == null);
            }
            finally
            {
                onReleaseProgressAdded = null;
                patch.UnpatchAll(patch.Id);
            }
        });

        Check("bond release keeps the new record and aftereffect when a post-removal callback advances progress", () =>
        {
            var (_, subject, _, chain) = Bound(.8076543f);
            var patch = new Harmony("ssc.tests.bond-release.committed-progress");
            patch.Patch(AccessTools.Method(typeof(Hediff_ChainOfSexSlave), nameof(Hediff_ChainOfSexSlave.PostRemoved)),
                postfix: new HarmonyMethod(typeof(Program), nameof(ReleaseChainRemoved)));
            try
            {
                onReleaseChainRemoved = removed =>
                {
                    if (removed != chain) return;
                    var finalProgress = TrainingOutcomeUtility.GetSexSlaveProgress(subject);
                    Require(finalProgress is Hediff_SSCOwnerlessSlave);
                    Equal(.8076543f, finalProgress.Severity);
                    finalProgress.Severity = .9f;
                };
                Require(SSCBondUtility.TryReleaseToOwnerless(new SSCBondUtility.ReleaseContext(subject)));
                Require(SSCBondUtility.GetChain(subject) == null);
                Equal(.9f, Hediff_SSCOwnerlessSlave.Find(subject).Severity);
                Equal(120000, Hediff_SSCBondReleaseAftereffect.Active(subject).GetComp<HediffComp_Disappears>().ticksToDisappear);
            }
            finally
            {
                onReleaseChainRemoved = null;
                patch.UnpatchAll(patch.Id);
            }
        });

        Check("owner death consumes only the original bridle targets and legacy migration works without the bridle", () =>
        {
            int oldTick = Find.TickManager.TicksGame;
            try
            {
                SetBreakdownTick(4500);
                var (master, subject, other, _) = Bound(.8076543f);
                var second = ImprintPawn(PawnIdentity.Slave);
                var changed = ImprintPawn(PawnIdentity.Slave);
                Require(SSCBondUtility.Bind(master, second) && SSCBondUtility.Bind(master, changed));
                SSCBondUtility.GetChain(second).Severity = .2f;
                second.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                SSCBondOwnerDeath.Prefix(master, out var snapshot);
                Require(snapshot.Pending.Length == 3);
                SSCBondOwnerDeath.Postfix(master, snapshot);
                Require(SSCBondUtility.IsBoundTo(subject, master));
                Require(SSCBondUtility.Bind(other, changed, replaceExisting: true));
                master.health.SetDead();
                SSCBondOwnerDeath.Postfix(master, snapshot);
                Equal(.8076543f, Hediff_SSCOwnerlessSlave.Find(subject).Severity);
                Equal(.2f, Hediff_SSCOwnerlessSlave.Find(second).Severity);
                Require(SSCIdentityUtility.IsSexSlave(second));
                Equal(120000, Hediff_SSCBondReleaseAftereffect.Active(subject).GetComp<HediffComp_Disappears>().ticksToDisappear);
                Require(SSCBondUtility.IsBoundTo(changed, other));

                AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(master.health, PawnHealthState.Mobile);
                SSCBondUtility.RepairReciprocalLink(subject);
                Require(SSCBondUtility.GetChain(subject) == null && SSCBondUtility.GetBridle(master) == null);

                var (former, legacy, _, _) = Bound(.9678912f);
                former.health.RemoveHediff(SSCBondUtility.GetBridle(former));
                former.health.SetDead();
                var before = ImprintPawn(PawnIdentity.Slave);
                Load(Save(legacy), before, former);
                before.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                Require(SSCBondUtility.GetBridle(former) == null);
                SSCBondUtility.ReleaseUnavailableOwner(before);
                Require(SSCIdentityUtility.IsSexSlave(before));
                Equal(.9678912f, Hediff_SSCOwnerlessSlave.Find(before).Severity);
                var after = ImprintPawn(PawnIdentity.Slave);
                Hediff_SSCBondReleaseAftereffect.Find(before).GetComp<HediffComp_Disappears>().ticksToDisappear = 119500;
                string path = Save(before);
                SetBreakdownTick(5000);
                Load(path, after);
                SSCBondUtility.ReleaseUnavailableOwner(after);
                Equal(119500, Hediff_SSCBondReleaseAftereffect.Active(after).GetComp<HediffComp_Disappears>().ticksToDisappear);
                Equal(.9678912f, Hediff_SSCOwnerlessSlave.Find(after).Severity);

                // 用原开发版字段替换原版组件字段，验证原主人死亡前后的两种旧档都能加载。
                Edit(path, doc =>
                {
                    var entry = doc.Descendants("li")
                        .Single(node => (string)node.Attribute("Class") == typeof(Hediff_SSCBondReleaseAftereffect).FullName);
                    entry.Elements().Where(e => e.Name.LocalName is "ticksToDisappear" or "disappearsAfterTicks" or "seed").Remove();
                    entry.Add(new XElement("sscBondReleaseExpiresAt", 124500));
                });
                var oldAfter = Load(path, ImprintPawn(PawnIdentity.Slave));
                Equal(119500, Hediff_SSCBondReleaseAftereffect.Active(oldAfter).GetComp<HediffComp_Disappears>().ticksToDisappear);
            }
            finally { SetBreakdownTick(oldTick); }
        });
    }
}
