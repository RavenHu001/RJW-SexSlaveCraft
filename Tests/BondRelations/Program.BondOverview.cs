// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    static void ResetOverviewWorld()
    {
        Current.Game = new Game();
        Current.ProgramState = ProgramState.Playing;
        Scribe.mode = LoadSaveMode.Inactive;
        Find.Maps.Clear();
        Find.World = new object();
        Find.WorldObjects = new WorldObjectsHolder();
        Find.WorldPawns = new WorldPawns();
        Find.CurrentGravship = null;
        RimWorld.PawnsFinder.TemporaryPawns.Clear();
        RimWorld.PawnsFinder.TransportMembers.Clear();
        RimWorld.PawnsFinder.TemporaryQueries = RimWorld.PawnsFinder.TransportQueries = 0;
        SSCRestrictionGameComponent.QueryCount = 0;
        ThingOwnerUtility.MapQueryCount = ThingOwnerUtility.HolderQueryCount = 0;
        ThingOwnerUtility.HolderVisitCount = 0;
        ThingOwnerUtility.Visits.Clear();
    }

    static Pawn NewOverviewSlave(Pawn master, string name = "Pawn")
    {
        var slave = new Pawn { LabelShort = name };
        Require(SSCBondUtility.Bind(master, slave));
        return slave;
    }

    static void Place(Thing thing, Map map)
    {
        thing.Spawned = true;
        thing.Map = map;
        map.Things.Add(thing);
    }

    static Corpse DeadBody(Pawn pawn)
    {
        pawn.Dead = true;
        var corpse = new Corpse { InnerPawn = pawn };
        pawn.ParentHolder = corpse;
        return corpse;
    }

    static SSCBondOverviewSnapshot Overview(Pawn source, params Pawn[] candidates)
        => SSCBondOverviewUtility.BuildFromCandidates(source, null, candidates);

    /// <summary>捕捉生产查询会接触的保存数据及通知次数；保留对象和列表顺序以检查隐式修复。</summary>
    sealed class BondState
    {
        readonly Pawn pawn;
        readonly Hediff[] hediffs;
        readonly float[] severities;
        readonly Thing[] targets;
        readonly Pawn[][] bridleTargets;
        readonly List<Pawn>[] bridleLists;
        readonly bool[] shares;
        readonly PawnIdentity identity;
        readonly Pawn trainer;
        readonly SexSlaveSpecializationType specialization;
        readonly RimWorld.DirectPawnRelation[] relations;
        readonly int notifications;

        internal BondState(Pawn pawn)
        {
            this.pawn = pawn;
            hediffs = pawn.health.hediffSet.hediffs.ToArray();
            severities = hediffs.Select(h => h.Severity).ToArray();
            targets = hediffs.Select(h => (h as HediffWithTarget)?.target).ToArray();
            bridleLists = hediffs.Select(h => (h as Hediff_BridleOfSexSlave)?.targets).ToArray();
            bridleTargets = bridleLists.Select(list => list?.ToArray()).ToArray();
            shares = hediffs.Select(h => (h as Hediff_BridleOfSexSlave)?.shareDamageEnabled ?? false).ToArray();
            identity = pawn.Training?.pawnIdentity ?? PawnIdentity.Unset;
            trainer = pawn.Training?.selectedTrainer;
            specialization = pawn.Training?.specializationType ?? SexSlaveSpecializationType.None;
            relations = pawn.relations.DirectRelations.ToArray();
            notifications = pawn.relations.CacheNotifications;
        }

        internal void AssertUnchanged()
        {
            Require(pawn.health.hediffSet.hediffs.SequenceEqual(hediffs), "health instances/order changed");
            for (int i = 0; i < hediffs.Length; i++)
            {
                Require(hediffs[i].Severity == severities[i], "severity changed");
                Require(ReferenceEquals((hediffs[i] as HediffWithTarget)?.target, targets[i]), "chain target changed");
                if (hediffs[i] is Hediff_BridleOfSexSlave bridle)
                {
                    Require(ReferenceEquals(bridle.targets, bridleLists[i]), "bridle list replaced");
                    Require(bridle.targets == null ? bridleTargets[i] == null : bridle.targets.SequenceEqual(bridleTargets[i]), "bridle entries changed");
                    Require(bridle.shareDamageEnabled == shares[i], "damage-share preference changed");
                }
            }
            Require((pawn.Training?.pawnIdentity ?? PawnIdentity.Unset) == identity, "identity changed");
            Require(ReferenceEquals(pawn.Training?.selectedTrainer, trainer), "assignment changed");
            Require((pawn.Training?.specializationType ?? SexSlaveSpecializationType.None) == specialization, "specialization changed");
            Require(pawn.relations.DirectRelations.SequenceEqual(relations) && pawn.relations.CacheNotifications == notifications,
                "social storage/notifications changed");
        }
    }

    // 故意提供相同 ID、姓名和 Equals 的两名不同 Pawn，验证名单确实按引用去重。
    sealed class EqualPawn : Pawn
    {
        public override bool Equals(object other) => other is EqualPawn;
        public override int GetHashCode() => 1;
    }

    static void RunBondOverviewTests()
    {
        Check("overview: identity alone creates neither direction", () =>
        {
            var master = Master(); var slave = new Pawn();
            slave.Training.pawnIdentity = PawnIdentity.Slave;
            foreach (Pawn source in new[] { master, slave })
            {
                var view = Overview(source);
                Require(view.State == SSCBondOverviewQueryState.Ready && view.MasterState == SSCBondOverviewMasterState.NoChain);
                Require(view.Master == null && view.TotalSlaveCount == 0 && view.Identity == source.Training.pawnIdentity);
            }
        });
        Check("overview: raw trainer assignment never becomes ownership", () =>
        {
            var source = new Pawn(); var trainer = Master(); source.Training.selectedTrainer = trainer;
            Require(SSCBondUtility.GetResolvedMaster(source) == trainer);
            Require(Overview(source, trainer).MasterState == SSCBondOverviewMasterState.NoChain);
        });
        Check("overview: direct directions and chain stage without social reward", () =>
        {
            var f = Bound(); var thirdParty = Master(); f.slave.Training.selectedTrainer = thirdParty;
            SSCBondUtility.GetChain(f.slave).Severity = .6f;
            var forward = Overview(f.master, f.slave); var reverse = Overview(f.slave);
            Require(forward.TotalSlaveCount == 1 && forward.LivingSlaveCount == 1 && !Has(f.master, f.slave));
            Require(forward.Slaves[0].Pawn == f.slave && forward.Slaves[0].ChainStage == 3);
            Require(reverse.Master.Pawn == f.master && reverse.Master.ChainStage == 3);
            Require(reverse.Master.Direction == SSCBondOverviewDirection.Master && forward.Slaves[0].Direction == SSCBondOverviewDirection.Slave);
        });
        Check("overview: assignment is secondary and preserves inactive raw reference", () =>
        {
            var f = Bound(); var thirdParty = Master(); f.slave.Training.selectedTrainer = thirdParty;
            var active = Overview(f.slave);
            Require(active.AssignedTrainer == thirdParty && active.AssignedTrainerState == SSCBondOverviewTrainerState.Active);
            Require(active.Master.Pawn == f.master && active.TotalSlaveCount == 0);
            thirdParty.Training.TrainerActive = false;
            Require(Overview(f.slave).AssignedTrainerState == SSCBondOverviewTrainerState.Inactive);
            thirdParty.Training.TrainerActive = true; thirdParty.Dead = true;
            Require(Overview(f.slave).AssignedTrainerState == SSCBondOverviewTrainerState.Inactive);
            thirdParty.Discarded = true;
            Require(Overview(f.slave).AssignedTrainerState == SSCBondOverviewTrainerState.InvalidTarget);
            Require(f.slave.Training.selectedTrainer == thirdParty);
        });
        Check("overview: stale duplicate null and invalid candidates are filtered", () =>
        {
            var f = Bound(); var stale = NewOverviewSlave(Master());
            var destroyed = NewOverviewSlave(f.master); destroyed.Destroyed = true;
            var discarded = NewOverviewSlave(f.master); discarded.Discarded = true;
            var view = Overview(f.master, null, f.master, f.slave, f.slave, stale, destroyed, discarded);
            Require(view.TotalSlaveCount == 1 && ReferenceEquals(view.Slaves[0].Pawn, f.slave));
        });
        Check("overview: dedup uses references even with identical IDs and Equals", () =>
        {
            var master = Master(); var one = new EqualPawn(); var two = new EqualPawn();
            Require(SSCBondUtility.Bind(master, one) && SSCBondUtility.Bind(master, two));
            one.thingIDNumber = two.thingIDNumber;
            Require(Overview(master, one, one, two).TotalSlaveCount == 2);
        });
        Check("overview: missing bridle and unset identity still expose actual chains", () =>
        {
            var f = Bound(); f.master.health.hediffSet.hediffs.Remove(SSCBondUtility.GetBridle(f.master));
            f.master.Training.pawnIdentity = PawnIdentity.Unset; f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            SSCRestrictionGameComponent.Pawns.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(f.master, null).TotalSlaveCount == 1);
            Require(Overview(f.slave).Master.Pawn == f.master && SSCBondUtility.GetBridle(f.master) == null);
        });
        Check("overview: missing training comp does not hide an actual pair", () =>
        {
            var f = Bound(); f.master.Training = null; f.slave.Training = null;
            var view = Overview(f.master, f.slave);
            Require(view.Identity == PawnIdentity.Unset && view.Slaves[0].Specialization == null);
            Require(Overview(f.slave).Master.Pawn == f.master);
        });
        Check("overview: paused direct target rewrite needs no Tick and performs no repair", () =>
        {
            var f = Bound(); var next = Master(); var oldBridle = SSCBondUtility.GetBridle(f.master);
            SSCBondUtility.GetChain(f.slave).target = next;
            SSCRestrictionGameComponent.Pawns.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(f.master, null).TotalSlaveCount == 0);
            Require(SSCBondOverviewUtility.Build(next, null).TotalSlaveCount == 1);
            Require(oldBridle.targets.Contains(f.slave) && SSCBondUtility.GetBridle(next) == null);
        });
        Check("overview: direct AddToPawn takeover with missing new index is discoverable", () =>
        {
            var f = Bound(); var next = Master();
            Hediff_ChainOfSexSlave.AddToPawn(f.slave, next);
            Require(SSCBondUtility.GetBridle(next) == null);
            SSCRestrictionGameComponent.Pawns.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(next, null).TotalSlaveCount == 1);
            Require(SSCBondUtility.GetBridle(next) == null && Overview(f.slave).Master.Pawn == next);
        });
        Check("overview: invalid chain targets differ from missing chain", () =>
        {
            var f = Bound(); var chain = SSCBondUtility.GetChain(f.slave);
            foreach (Thing target in new Thing[] { null, new Thing(), f.slave, new Pawn { Destroyed = true }, new Pawn { Discarded = true } })
            {
                chain.target = target;
                var view = Overview(f.slave);
                Require(view.MasterState == SSCBondOverviewMasterState.InvalidTarget && view.Master == null);
            }
            Require(SSCBondUtility.GetChain(f.slave) == chain);
        });
        Check("overview: dead master remains bound and actionable for relation recheck", () =>
        {
            var f = Bound(); f.master.Dead = true;
            var view = Overview(f.slave);
            Require(view.MasterState == SSCBondOverviewMasterState.Bound && view.Master.IsDead);
            Require(SSCBondOverviewUtility.IsCurrent(view, view.Master));
        });
        Check("overview: dead slave and revival change counts only on rebuild", () =>
        {
            var f = Bound(); f.slave.Dead = true;
            var dead = Overview(f.master, f.slave);
            Require(dead.TotalSlaveCount == 1 && dead.LivingSlaveCount == 0);
            f.slave.Dead = false;
            Require(dead.LivingSlaveCount == 0 && dead.Slaves[0].IsDead);
            Require(Overview(f.master, f.slave).LivingSlaveCount == 1);
        });
        Check("overview: corpse absent from world list and bridle is collected on map", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map);
            var corpse = DeadBody(f.slave); Place(corpse, map);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var view = SSCBondOverviewUtility.Build(f.master, map);
            Require(SSCRestrictionGameComponent.Pawns.Count == 0 && view.TotalSlaveCount == 1 && view.LivingSlaveCount == 0);
            Require(view.Slaves[0].Location.Kind == SSCBondOverviewLocationKind.Map && view.Slaves[0].Location.Subject == corpse);
            Require(ThingOwnerUtility.MapQueryCount == 1 && SSCRestrictionGameComponent.QueryCount == 0);
            Require(RimWorld.PawnsFinder.TemporaryQueries == 1 && RimWorld.PawnsFinder.TransportQueries == 1);
            Require(SSCBondUtility.GetBridle(f.master).targets.Count == 0);
        });
        Check("overview: nested map container corpse is collected and classified", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map);
            var outer = new Container(); Place(outer, map);
            var inner = new Container { ParentHolder = outer }; outer.Contents.Add(inner);
            var corpse = DeadBody(f.slave); corpse.ParentHolder = inner; inner.Contents.Add(corpse);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, map).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.MapContainer && entry.Location.Map == map);
            Require(entry.Location.Container == outer && entry.Location.Subject == corpse);
        });
        Check("overview: pawn carried corpse in caravan is discovered through holders", () =>
        {
            var f = Bound(); var caravan = new Caravan(); Find.WorldObjects.AllWorldObjects.Add(caravan);
            var carrier = new Pawn { ParentHolder = caravan }; caravan.Contents.Add(carrier);
            var corpse = DeadBody(f.slave); corpse.ParentHolder = carrier; carrier.Inventory.Add(corpse);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.Caravan && entry.Location.WorldObject == caravan);
        });
        Check("overview: transport container corpse is collected without pawn membership", () =>
        {
            var f = Bound(); var transport = new TravellingTransporters(); Find.WorldObjects.AllWorldObjects.Add(transport);
            var container = new Container { ParentHolder = transport }; transport.Contents.Add(container);
            var corpse = DeadBody(f.slave); corpse.ParentHolder = container; container.Contents.Add(corpse);
            SSCBondUtility.GetBridle(f.master).targets = null;
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.Transport && entry.Location.WorldObject == transport);
            Require(SSCBondUtility.GetBridle(f.master).targets == null);
        });
        Check("overview: generic world holder uses world-object location", () =>
        {
            var f = Bound(); var holder = new HeldWorldObject(); Find.WorldObjects.AllWorldObjects.Add(holder);
            f.slave.ParentHolder = holder; holder.Contents.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(f.master, null).Slaves.Single().Location.Kind == SSCBondOverviewLocationKind.WorldObject);
        });
        Check("overview: current gravship dictionary and nested corpse bypass holder gap", () =>
        {
            var f = Bound(); Find.CurrentGravship = new Gravship { Spawned = false };
            var box = new Container(); Find.CurrentGravship.Things.Add(box);
            var corpse = DeadBody(f.slave); corpse.ParentHolder = box; box.Contents.Add(corpse);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.Transport && entry.Location.WorldObject == Find.CurrentGravship);
        });
        Check("overview: world gravship contents do not require CurrentGravship", () =>
        {
            var f = Bound(); var ship = new Gravship(); Find.WorldObjects.AllWorldObjects.Add(ship);
            ship.Things.Add(DeadBody(f.slave)); SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.Transport && entry.Location.WorldObject == ship);
        });
        Check("overview: duplicate candidate sources do not duplicate corpse or live entries", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map); Place(DeadBody(f.slave), map);
            SSCRestrictionGameComponent.Pawns.AddRange(new[] { f.slave, f.slave });
            SSCBondUtility.GetBridle(f.master).targets.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(f.master, map).TotalSlaveCount == 1);
        });
        Check("overview: world tile and unknown position preserve live bindings", () =>
        {
            var f = Bound();
            Require(Overview(f.master, f.slave).Slaves[0].Location.Kind == SSCBondOverviewLocationKind.Unknown);
            f.slave.Tile = new PlanetTile(true);
            Require(Overview(f.master, f.slave).Slaves[0].Location.Kind == SSCBondOverviewLocationKind.WorldTile);
        });
        Check("overview: living map container and ThingComp holder have map location", () =>
        {
            var f = Bound(); var map = new Map(); var container = new Container(); Place(container, map);
            var comp = new ThingComp { parent = container }; f.slave.ParentHolder = comp;
            var location = Overview(f.master, f.slave).Slaves[0].Location;
            Require(location.Kind == SSCBondOverviewLocationKind.MapContainer && location.Container == container && location.Map == map);
        });
        Check("overview: malformed parent cycle produces unknown location", () =>
        {
            var f = Bound(); var box = new Container(); box.ParentHolder = box; f.slave.ParentHolder = box;
            Require(Overview(f.master, f.slave).Slaves[0].Location.Kind == SSCBondOverviewLocationKind.Unknown);
        });
        Check("overview: map alive then elsewhere alive then dead with stable same-name IDs", () =>
        {
            var master = Master(); var map = new Map(); var otherMap = new Map();
            var local = NewOverviewSlave(master, "Z"); Place(local, map);
            var one = NewOverviewSlave(master, "Same"); var two = NewOverviewSlave(master, "Same");
            var dead = NewOverviewSlave(master, "A"); dead.Dead = true;
            Place(two, otherMap);
            var input = new[] { dead, two, one, local };
            var view = SSCBondOverviewUtility.BuildFromCandidates(master, map, input);
            Require(view.Slaves.Select(e => e.Pawn).SequenceEqual(new[] { local, one, two, dead }));
            var reversed = SSCBondOverviewUtility.BuildFromCandidates(master, map, input.Reverse());
            Require(view.Slaves.Select(e => e.Pawn).SequenceEqual(reversed.Slaves.Select(e => e.Pawn)));
            Require(SSCBondOverviewUtility.BuildFromCandidates(master, otherMap, input).Slaves[0].Pawn == two);
        });
        Check("overview: source may have both direct directions without recursive expansion", () =>
        {
            var grand = Master(); var middle = NewOverviewSlave(grand); middle.Training.pawnIdentity = PawnIdentity.Master;
            var leaf = NewOverviewSlave(middle);
            var view = Overview(middle, grand, middle, leaf);
            Require(view.Master.Pawn == grand && view.TotalSlaveCount == 1 && view.Slaves[0].Pawn == leaf);
            Require(Overview(grand, middle, leaf).TotalSlaveCount == 1);
        });
        Check("overview: cyclic bonds remain two finite direct directions", () =>
        {
            var f = Bound(); var chain = new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, target = f.slave };
            f.master.health.AddHediff(chain);
            var view = Overview(f.master, f.slave);
            Require(view.Master.Pawn == f.slave && view.Slaves.Single().Pawn == f.slave);
        });
        Check("overview: normal unbind and rebind revoke stale rows", () =>
        {
            var f = Bound(); var old = Overview(f.master, f.slave); var reverse = Overview(f.slave);
            Require(SSCBondOverviewUtility.IsCurrent(old, old.Slaves[0]));
            SSCBondUtility.Unbind(f.slave);
            Require(!SSCBondOverviewUtility.IsCurrent(old, old.Slaves[0]) && !SSCBondOverviewUtility.IsCurrent(reverse, reverse.Master));
            Require(SSCBondUtility.Bind(Master(), f.slave));
            Require(!SSCBondOverviewUtility.IsCurrent(old, old.Slaves[0]) && Overview(f.master, f.slave).TotalSlaveCount == 0);
        });
        Check("overview: direct rewrite invalidates old row before Tick", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave);
            SSCBondUtility.GetChain(f.slave).target = Master();
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
        });
        Check("overview: discarded related pawn and destroyed source revoke old rows", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave);
            f.slave.Discarded = true;
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
            f.slave.Discarded = false; f.master.Destroyed = true;
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
        });
        Check("overview: same-master replacement chain preserves relation recheck", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave); var old = SSCBondUtility.GetChain(f.slave);
            f.slave.health.hediffSet.hediffs.Remove(old);
            f.slave.health.AddHediff(new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, target = f.master });
            Require(SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
        });
        Check("overview: CE direct removal invalidates row through real production notification patch", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave); var chain = SSCBondUtility.GetChain(f.slave);
            f.slave.health.hediffSet.hediffs.Remove(chain); f.slave.health.Notify_HediffChanged(chain);
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]) && Overview(f.master, f.slave).TotalSlaveCount == 0);
        });
        Check("overview: cross-game and foreign snapshot rows are rejected", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave); var another = Overview(f.master, f.slave);
            Require(!SSCBondOverviewUtility.IsCurrent(view, another.Slaves[0]));
            Current.Game = new Game();
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
        });
        Check("overview: destroyed discarded and missing sources perform no world scan", () =>
        {
            foreach (Pawn source in new Pawn[] { null, new Pawn { Destroyed = true }, new Pawn { Discarded = true } })
                Require(SSCBondOverviewUtility.Build(source, null).State == SSCBondOverviewQueryState.InvalidSource);
            Require(SSCRestrictionGameComponent.QueryCount == 0 && ThingOwnerUtility.MapQueryCount == 0);
        });
        Check("overview: loading entry and absent game are unavailable rather than no chain", () =>
        {
            var pawn = Master();
            Scribe.mode = LoadSaveMode.PostLoadInit;
            Require(SSCBondOverviewUtility.Build(pawn, null).MasterState == SSCBondOverviewMasterState.Unavailable);
            Scribe.mode = LoadSaveMode.Inactive; Current.ProgramState = ProgramState.Entry;
            Require(SSCBondOverviewUtility.Build(pawn, null).State == SSCBondOverviewQueryState.Unavailable);
            Current.ProgramState = ProgramState.Playing; Current.Game = null;
            Require(SSCBondOverviewUtility.Build(pawn, null).State == SSCBondOverviewQueryState.Unavailable);
            Require(SSCRestrictionGameComponent.QueryCount == 0);
        });
        Check("overview: missing defs block query without erasing a chain", () =>
        {
            var f = Bound(); var def = SSCDefOf.ChainOfSexSlave;
            try { SSCDefOf.ChainOfSexSlave = null; Require(SSCBondOverviewUtility.Build(f.slave, null).State == SSCBondOverviewQueryState.Unavailable); }
            finally { SSCDefOf.ChainOfSexSlave = def; }
            Require(Overview(f.slave).Master.Pawn == f.master && SSCRestrictionGameComponent.QueryCount == 0);
        });
        Check("overview: absent bridle def and load phase disable existing rows", () =>
        {
            var f = Bound(); var view = Overview(f.master, f.slave); var def = SSCDefOf.BridleOfSexSlave;
            try
            {
                SSCDefOf.BridleOfSexSlave = null;
                Require(SSCBondOverviewUtility.Build(f.master, null).State == SSCBondOverviewQueryState.Unavailable);
                Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
            }
            finally { SSCDefOf.BridleOfSexSlave = def; }
            Scribe.mode = LoadSaveMode.PostLoadInit;
            Require(!SSCBondOverviewUtility.IsCurrent(view, view.Slaves[0]));
            Require(SSCBondOverviewUtility.Build(f.slave, null).AssignedTrainerState == SSCBondOverviewTrainerState.Unavailable);
        });
        Check("overview: snapshot collections and captured fields are immutable", () =>
        {
            var f = Bound(); f.slave.LabelShort = "Before"; f.slave.Training.specializationType = SexSlaveSpecializationType.Combatant;
            var view = Overview(f.master, f.slave);
            bool threw = false;
            try { ((IList<SSCBondOverviewEntry>)view.Slaves).Clear(); } catch (NotSupportedException) { threw = true; }
            f.slave.LabelShort = "After"; f.slave.Training.specializationType = SexSlaveSpecializationType.None; f.slave.Dead = true;
            Require(threw && view.TotalSlaveCount == 1 && view.LivingSlaveCount == 1 && view.Slaves[0].Name == "Before");
            Require(view.Slaves[0].Specialization == SexSlaveSpecializationType.Combatant && !view.Slaves[0].IsDead);
        });
        Check("overview: repeated full reads preserve corrupt indexes rewards assignment and preferences", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var trainer = Master();
            f.slave.Training.selectedTrainer = trainer; f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            f.slave.Training.specializationType = SexSlaveSpecializationType.PetCat;
            var bridle = SSCBondUtility.GetBridle(f.master); bridle.shareDamageEnabled = false;
            var other = NewOverviewSlave(Master());
            bridle.targets.AddRange(new[] { f.slave, null, other });
            var missing = NewOverviewSlave(f.master); bridle.targets.Remove(missing);
            SSCRestrictionGameComponent.Pawns.AddRange(new[] { f.slave, missing, other });
            var before = new[] { f.master, f.slave, trainer, missing, other }.Select(p => new BondState(p)).ToArray();
            for (int i = 0; i < 20; i++)
            {
                var view = SSCBondOverviewUtility.Build(f.master, null);
                Require(view.TotalSlaveCount == 2 && view.LivingSlaveCount == 2);
                Require(Overview(f.slave).Master.Pawn == f.master);
            }
            foreach (var state in before) state.AssertUnchanged();
        });
        Check("overview: one candidate pass handles large mixed lists consistently", () =>
        {
            var master = Master(); var valid = Enumerable.Range(0, 160).Select(i => NewOverviewSlave(master, "Pawn" + i.ToString("D3"))).ToArray();
            for (int i = 0; i < valid.Length; i += 4) valid[i].Dead = true;
            int enumerations = 0;
            IEnumerable<Pawn> Candidates()
            {
                Require(++enumerations == 1, "candidate source enumerated twice");
                foreach (Pawn pawn in valid.Reverse()) { yield return pawn; yield return pawn; }
                foreach (Pawn unrelated in Enumerable.Range(0, 500).Select(_ => new Pawn())) yield return unrelated;
            }
            var view = SSCBondOverviewUtility.BuildFromCandidates(master, null, Candidates());
            Require(view.TotalSlaveCount == 160 && view.LivingSlaveCount == 120 && enumerations == 1);
            Require(view.Slaves.All(entry => SSCBondOverviewUtility.IsCurrent(view, entry)));
        });
    }
}
