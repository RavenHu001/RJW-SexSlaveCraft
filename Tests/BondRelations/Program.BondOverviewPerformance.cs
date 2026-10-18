// AI 辅助生成
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    sealed class ComponentHolderRoot : Container, IThingHolder
    {
        internal ThingComp Component;
        void IThingHolder.GetChildHolders(System.Collections.Generic.List<IThingHolder> children) => children.Add(Component);
    }

    // 在独立且禁止内联的方法内创建候选，避免测试调用栈的局部变量延长 Pawn 生命周期。
    // 成功与枚举中抛错分别返回弱引用，检查 Dispose 是否都清除了复用缓冲中的强引用。
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference CreateOverviewWeakCandidate(bool throwDuringQuery)
    {
        var pawn = new Pawn(); var weak = new WeakReference(pawn);
        var source = Master();
        System.Collections.Generic.IEnumerable<Pawn> Input()
        {
            yield return pawn;
            if (throwDuringQuery) throw new InvalidOperationException("fixture");
        }
        try { SSCBondOverviewUtility.BuildFromCandidates(source, null, Input()); }
        catch (InvalidOperationException) when (throwDuringQuery) { }
        return weak;
    }

    static void RunBondOverviewPerformanceTests()
    {
        // 先从互相独立的来源验证候选并集，再检查地图、世界与飞船重叠持有树的访问次数。
        // 清空缰绳条目可防止反向索引掩盖其他来源的漏项；状态对照继续保证查询只读。
        Check("overview perf: independent alive dead temporary and transport sources retain coverage", () =>
        {
            var master = Master(); var alive = NewOverviewSlave(master); var dead = NewOverviewSlave(master); dead.Dead = true;
            var temporary = NewOverviewSlave(master); var temporaryDead = NewOverviewSlave(master); temporaryDead.Dead = true;
            var member = NewOverviewSlave(master); var reverseOnly = NewOverviewSlave(master);
            SSCRestrictionGameComponent.Pawns.Add(alive); Find.WorldPawns.AllPawnsDead.Add(dead);
            PawnsFinder.TemporaryPawns.AddRange(new[] { temporary, temporaryDead }); PawnsFinder.TransportMembers.Add(member);
            var bridle = SSCBondUtility.GetBridle(master); bridle.targets.Clear(); bridle.targets.Add(reverseOnly);
            var before = new[] { master, alive, dead, temporary, temporaryDead, member, reverseOnly }.Select(p => new BondState(p)).ToArray();
            var view = SSCBondOverviewUtility.Build(master, null);
            Require(view.TotalSlaveCount == 6 && view.LivingSlaveCount == 4);
            Require(PawnsFinder.TemporaryQueries == 1 && PawnsFinder.TransportQueries == 1 && SSCRestrictionGameComponent.QueryCount == 0);
            foreach (var state in before) state.AssertUnchanged();
        });
        Check("overview perf: one map holder pass collects live pawn and nested corpse without indexes", () =>
        {
            var master = Master(); var alive = NewOverviewSlave(master); var dead = NewOverviewSlave(master);
            var map = new Map(); Find.Maps.Add(map); var outer = new Container(); Place(outer, map);
            var inner = new Container { ParentHolder = outer }; outer.Contents.Add(inner);
            alive.ParentHolder = inner; inner.Contents.Add(alive);
            var corpse = DeadBody(dead); corpse.ParentHolder = inner; inner.Contents.Add(corpse);
            SSCBondUtility.GetBridle(master).targets.Clear();
            var view = SSCBondOverviewUtility.Build(master, map);
            Require(view.TotalSlaveCount == 2 && view.LivingSlaveCount == 1);
            Require(view.Slaves.All(e => e.Location.Kind == SSCBondOverviewLocationKind.MapContainer));
            Require(ThingOwnerUtility.MapQueryCount == 1 && SSCRestrictionGameComponent.QueryCount == 0);
            Require(ThingOwnerUtility.Visits[outer] == 1 && ThingOwnerUtility.Visits[inner] == 1 && ThingOwnerUtility.Visits[corpse] == 1);
        });
        Check("overview perf: spawned map pawn needs neither world list nor reverse index", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map); Place(f.slave, map);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var view = SSCBondOverviewUtility.Build(f.master, map);
            Require(view.TotalSlaveCount == 1 && view.Slaves[0].Location.Kind == SSCBondOverviewLocationKind.Map);
        });
        Check("overview perf: shared map world and ship holders are traversed once", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map); var box = new Container(); Place(box, map);
            var corpse = DeadBody(f.slave); corpse.ParentHolder = box; box.Contents.Add(corpse);
            var root = new HeldWorldObject(); root.Contents.Add(box); Find.WorldObjects.AllWorldObjects.Add(root);
            Find.CurrentGravship = new Gravship(); Find.CurrentGravship.Things.Add(box);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            Require(SSCBondOverviewUtility.Build(f.master, map).TotalSlaveCount == 1);
            Require(ThingOwnerUtility.Visits[box] == 1 && ThingOwnerUtility.Visits[corpse] == 1);
        });
        // 用确定性的枚举计数约束复杂度：候选收集一次、按需位置索引一次，不按结果行重扫。
        // 地图行和空结果紧接压力场景执行，也覆盖复用字典非 null 但 Count 为零的边界。
        Check("overview perf: unresolved 200 rows scan 20000 ship items twice in total", () =>
        {
            var master = Master();
            foreach (int i in Enumerable.Range(0, 200)) SSCRestrictionGameComponent.Pawns.Add(NewOverviewSlave(master));
            SSCBondUtility.GetBridle(master).targets.Clear();
            var ship = new Gravship(); Find.CurrentGravship = ship; Find.WorldObjects.AllWorldObjects.Add(ship);
            for (int i = 0; i < 20000; i++) ship.Things.Add(new Thing());
            var view = SSCBondOverviewUtility.Build(master, null);
            Require(view.TotalSlaveCount == 200 && view.Slaves.All(e => e.Location.Kind == SSCBondOverviewLocationKind.Unknown));
            Require(ship.Things.Enumerations == 2 && ship.Things.ItemVisits == 40000, "cargo was scanned per row");
            Require(ship.Pawns.Enumerations == 3, "member reads must not grow with relation count");
        });
        Check("overview perf: spawned rows skip ship location index scan", () =>
        {
            var master = Master(); var map = new Map(); Find.Maps.Add(map);
            for (int i = 0; i < 200; i++) Place(NewOverviewSlave(master), map);
            var ship = new Gravship(); Find.CurrentGravship = ship;
            for (int i = 0; i < 1000; i++) ship.Things.Add(new Thing());
            Require(SSCBondOverviewUtility.Build(master, map).TotalSlaveCount == 200);
            Require(ship.Things.Enumerations == 1 && ship.Things.ItemVisits == 1000);
        });
        Check("overview perf: empty relation set skips indexing cargo", () =>
        {
            var master = Master(); var ship = new Gravship(); Find.CurrentGravship = ship;
            for (int i = 0; i < 1000; i++) ship.Things.Add(new Thing());
            Require(SSCBondOverviewUtility.Build(master, null).TotalSlaveCount == 0);
            Require(ship.Things.Enumerations == 1 && ship.Things.ItemVisits == 1000);
        });
        // 优化索引仍须保留独立成员、组件 parent 和不同祖先命中的旧位置优先级。
        // 随后在查询间移动角色，确认重建索引且旧快照仅保存历史显示事实。
        Check("overview perf: ship member not present in Things remains discoverable", () =>
        {
            var f = Bound(); var ship = new Gravship(); Find.CurrentGravship = ship; ship.Pawns.Add(f.slave);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.Kind == SSCBondOverviewLocationKind.Transport && entry.Location.WorldObject == ship);
        });
        Check("overview perf: ThingComp parent without IThingHolder retains ship location", () =>
        {
            var f = Bound(); SSCRestrictionGameComponent.Pawns.Add(f.slave);
            var container = new Thing(); var comp = new ThingComp { parent = container }; f.slave.ParentHolder = comp;
            var ship = new Gravship(); Find.CurrentGravship = ship; ship.Things.Add(container);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            var entry = SSCBondOverviewUtility.Build(f.master, null).Slaves.Single();
            Require(entry.Location.WorldObject == ship && entry.Location.Kind == SSCBondOverviewLocationKind.Transport);
        });
        Check("overview perf: current ship outranks another ship matching a different ancestor", () =>
        {
            var f = Bound(); SSCRestrictionGameComponent.Pawns.Add(f.slave);
            var container = new Thing(); f.slave.ParentHolder = new ThingComp { parent = container };
            var other = new Gravship(); other.Pawns.Add(f.slave); Find.WorldObjects.AllWorldObjects.Add(other);
            var current = new Gravship(); current.Things.Add(container); Find.CurrentGravship = current;
            Require(SSCBondOverviewUtility.Build(f.master, null).Slaves.Single().Location.WorldObject == current);
        });
        Check("overview perf: map and world holders outrank ship dictionary membership", () =>
        {
            var f = Bound(); var ship = new Gravship(); ship.Pawns.Add(f.slave); Find.CurrentGravship = ship;
            var caravan = new Caravan(); f.slave.ParentHolder = caravan; caravan.Contents.Add(f.slave);
            Require(SSCBondOverviewUtility.Build(f.master, null).Slaves.Single().Location.Kind == SSCBondOverviewLocationKind.Caravan);
            var map = new Map(); Place(f.slave, map);
            Require(SSCBondOverviewUtility.Build(f.master, null).Slaves.Single().Location.Kind == SSCBondOverviewLocationKind.Map);
        });
        Check("overview perf: location index is rebuilt between queries without retaining old ship", () =>
        {
            var f = Bound(); var first = new Gravship(); first.Pawns.Add(f.slave); Find.CurrentGravship = first;
            var old = SSCBondOverviewUtility.Build(f.master, null);
            var next = new Gravship(); next.Pawns.Add(f.slave); Find.CurrentGravship = next;
            Require(SSCBondOverviewUtility.Build(f.master, null).Slaves.Single().Location.WorldObject == next);
            Require(old.Slaves[0].Location.WorldObject == first && SSCBondOverviewUtility.IsCurrent(old, old.Slaves[0]));
        });
        Check("overview perf: empty snapshot has no mutable rows and rejects foreign entries", () =>
        {
            var f = Bound(); var foreign = Overview(f.master, f.slave).Slaves[0];
            var empty = Overview(f.master);
            Require(empty.TotalSlaveCount == 0 && !SSCBondOverviewUtility.IsCurrent(empty, foreign));
            Require(((System.Collections.Generic.ICollection<SSCBondOverviewEntry>)empty.Slaves).IsReadOnly);
        });
        Check("overview perf: component child holder is visited even when not a direct Thing child", () =>
        {
            var f = Bound(); var map = new Map(); Find.Maps.Add(map);
            var root = new ComponentHolderRoot(); Place(root, map);
            var comp = new ThingComp { parent = root }; root.Component = comp;
            f.slave.ParentHolder = comp; comp.Contents.Add(f.slave);
            SSCBondUtility.GetBridle(f.master).targets.Clear();
            Require(SSCBondOverviewUtility.Build(f.master, map).Slaves.Single().Pawn == f.slave);
            Require(ThingOwnerUtility.Visits[root] == 1 && ThingOwnerUtility.Visits[comp] == 1);
        });
        // 缓冲允许保留容量，但不得在成功或异常退出后保留候选对象本身。
        foreach (bool exceptional in new[] { false, true })
            Check("overview perf: cleared scratch retains no Pawn after " + (exceptional ? "exception" : "success"), () =>
            {
                WeakReference weak = CreateOverviewWeakCandidate(exceptional);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Require(!weak.IsAlive, "query scratch retained a Pawn");
            });
    }
}
