// AI 辅助生成
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

/// <summary>隔离模型的同机比较入口，不计入机制测试项数，不宣称实机 FPS／TPS。</summary>
internal static class BondOverviewBenchmark
{
    static SSCBondOverviewSnapshot sink;

    internal static int Run()
    {
        Sample("small-map", 100, 8, 0, true, 0);
        Sample("world-10000-30-hediffs", 10000, 8, 0, true, 30);
        Sample("world-10000-no-relations", 10000, 0, 0, true, 30);
        Sample("off-map-200-no-cargo", 200, 200, 0, false, 0);
        Sample("off-map-200-20000-cargo", 200, 200, 20000, false, 0);
        Sample("on-map-200-20000-cargo", 200, 200, 20000, true, 0);
        Sample("map-200-containers-10000-items", 200, 200, 0, false, 0, 200);
        return 0;
    }

    static void Sample(string name, int candidateCount, int relatedCount, int cargoCount,
        bool spawned, int hediffCount, int containerCount = 0)
    {
        // 场景构造全部放在计时外；基线与当前查询使用同一套角色、货物和引擎边界。
        // 每个场景重置世界入口，但保留查询缓冲的实际复用行为，以测量预热后的稳态成本。
        Current.Game = new Game(); Current.ProgramState = ProgramState.Playing; Scribe.mode = LoadSaveMode.Inactive;
        Find.World = new object(); Find.WorldObjects = new WorldObjectsHolder(); Find.WorldPawns = new WorldPawns(); Find.Maps.Clear();
        Find.CurrentGravship = cargoCount == 0 ? null : new Gravship();
        PawnsFinder.TemporaryPawns.Clear(); PawnsFinder.TransportMembers.Clear();
        SSCRestrictionGameComponent.Pawns.Clear(); ThingOwnerUtility.Visits.Clear();
        var ship = Find.CurrentGravship;
        if (ship != null) for (int i = 0; i < cargoCount; i++) ship.Things.Add(new Thing());
        var master = new Pawn(); master.Training.pawnIdentity = PawnIdentity.Master;
        var map = new Map();
        if (spawned && relatedCount > 0 || containerCount > 0) Find.Maps.Add(map);
        var containers = new Container[containerCount];
        for (int i = 0; i < containerCount; i++)
        {
            containers[i] = new Container { Spawned = true, Map = map }; map.Things.Add(containers[i]);
            for (int j = 0; j < 50; j++) containers[i].Contents.Add(new Thing { ParentHolder = containers[i] });
        }
        for (int i = 0; i < candidateCount; i++)
        {
            var pawn = new Pawn { LabelShort = "Pawn" + i.ToString("D5") };
            for (int h = 0; h < hediffCount; h++) pawn.health.hediffSet.hediffs.Add(new Hediff { def = new HediffDef() });
            if (i < relatedCount)
                pawn.health.AddHediff(new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, target = master });
            if (i < relatedCount && spawned)
            {
                pawn.Spawned = true; pawn.Map = map; map.Things.Add(pawn);
            }
            else if (containerCount > 0)
            {
                var box = containers[i % containerCount]; pawn.ParentHolder = box; box.Contents.Add(pawn);
            }
            else SSCRestrictionGameComponent.Pawns.Add(pawn);
        }

        // 预热覆盖 JIT 和集合扩容，不能据此推断首次查询零分配；完整 GC 放在采样循环之前。
        // sink 保持结果可观察，摘要、序列化及输出不计入单次查询的耗时或分配。
        for (int i = 0; i < 16; i++) sink = SSCBondOverviewUtility.Build(master, map);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        const int samples = 31;
        var elapsed = new double[samples]; var allocated = new long[samples];
        int mapPasses = 0, holderVisits = 0, shipPasses = 0, shipVisits = 0;
        for (int i = 0; i < samples; i++)
        {
            // 操作计数取查询前后差值，用于辨别扫描次数是否随关联人数增长。
            // 计时与当前线程分配只包围生产 Build；不使用易受机器负载影响的耗时阈值断言。
            int oldMaps = ThingOwnerUtility.MapQueryCount, oldHolders = ThingOwnerUtility.HolderVisitCount;
            int oldShips = ship?.Things.Enumerations ?? 0, oldItems = ship?.Things.ItemVisits ?? 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            long time = Stopwatch.GetTimestamp();
            sink = SSCBondOverviewUtility.Build(master, map);
            elapsed[i] = Stopwatch.GetElapsedTime(time).TotalMilliseconds;
            allocated[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            mapPasses = ThingOwnerUtility.MapQueryCount - oldMaps;
            holderVisits = ThingOwnerUtility.HolderVisitCount - oldHolders;
            shipPasses = (ship?.Things.Enumerations ?? 0) - oldShips;
            shipVisits = (ship?.Things.ItemVisits ?? 0) - oldItems;
        }
        Array.Sort(elapsed); Array.Sort(allocated);
        // 除数量外，将有序行的稳定 ID、姓名、阶段、死亡和位置种类纳入摘要，供脚本逐轮配对。
        // 摘要覆盖这些模型字段，不等价于真实存档、所有位置引用或第三方容器的实机验收。
        if (sink.TotalSlaveCount != relatedCount) throw new InvalidOperationException("candidate coverage mismatch");
        string content = string.Join(";", sink.Slaves.Select(e => e.StableId + ":" + e.Name + ":" + e.ChainStage + ":" + e.IsDead + ":" + e.Location.Kind));
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        Console.WriteLine(JsonSerializer.Serialize(new {
            name, candidateCount, relatedCount, cargoCount, spawned, hediffCount, containerCount,
            medianMs = Math.Round(elapsed[samples / 2], 4), p95Ms = Math.Round(elapsed[(int)(samples * .95)], 4),
            medianAllocatedBytes = allocated[samples / 2], mapPasses, holderVisits, shipPasses, shipVisits,
            count = sink.TotalSlaveCount, digest,
            boundary = "net9 model with production query; shared engine boundaries and instrumented enumerables; not in-game timing"
        }));
    }
}
