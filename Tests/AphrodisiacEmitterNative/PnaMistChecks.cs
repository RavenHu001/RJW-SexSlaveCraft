using System.Xml;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

/// <summary>通过原版 Scribe 保存设备的档位、半径和首次入账余额。</summary>
public sealed class SavedPnaMistEmitter : IExposable
{
    public CompSSCPnaMistEmitter emitter = Program.NewPnaMistDevice();
    public void ExposeData() => emitter.PostExposeData();
}

internal static partial class Program
{
    private static CompProperties_SSCPnaMistEmitter mistProps;

    /// <summary>用测试版 XML 的档位参数创建设备，补足燃料并连接测试电源。</summary>
    public static CompSSCPnaMistEmitter NewPnaMistDevice()
    {
        var (component, _, net) = Device(mistProps);
        var emitter = (CompSSCPnaMistEmitter)component;
        emitter.parent.TryGetComp<CompRefuelable>().Refuel(10);
        PowerSource(net, 3000);
        return emitter;
    }

    /// <summary>执行原版电网分配，确认设备已满足投送条件。</summary>
    private static void AllocateMistPower(CompSSCPnaMistEmitter emitter)
    {
        emitter.parent.TryGetComp<CompPowerTrader>().PowerNet.PowerNetTick();
        Require(emitter.CanRunNow);
    }

    /// <summary>创建具备生产 PNA 负荷条件的测试角色，保留真实健康组件。</summary>
    private static Pawn MistPawnAt(int x, int z)
    {
        Pawn pawn = PawnAt(x, z);
        Set(pawn.def.race, "fleshType", new FleshTypeDef { defName = "MistTestFlesh", isOrganic = true });
        // 隔离原版需求数据库和通知队列，负荷及自然消退仍走生产 Hediff。
        pawn.needs = null;
        return pawn;
    }

    /// <summary>以 60 Tick 推进原版健康时间，再调用设备的实际投送入口。</summary>
    private static void SimulateMist(CompSSCPnaMistEmitter emitter, int start, int pulses, params Pawn[] pawns)
    {
        for (int i = 1; i <= pulses; i++)
        {
            Tick(start + i * 60);
            foreach (Pawn pawn in pawns)
            {
                var record = SSCPnaExposureUtility.GetBodyRecord(pawn) as HediffWithComps;
                float adjustment = 0f;
                record?.GetComp<HediffComp_SSCPnaLoad>()?.CompPostTickInterval(ref adjustment, 60);
            }
            emitter.ApplyExposurePulse();
        }
    }

    /// <summary>验证高低档的实际负荷曲线、覆盖边界，以及断电和读档恢复。</summary>
    private static void RunPnaMistChecks(string repo)
    {
        Current.Game.World = Bare<World>();
        Current.Game.World.worldPawns = new WorldPawns();
        var xml = new XmlDocument();
        xml.Load(Path.Combine(repo, "1.7/Defs/ThingsDefs/SSC_PnaMistEmitter.xml"));
        mistProps = (CompProperties_SSCPnaMistEmitter)DirectXmlToObject.ObjectFromXml<CompProperties>(
            xml.SelectSingleNode("/Defs/ThingDef/comps/li[@Class='SexSlaveCraft.CompProperties_SSCPnaMistEmitter']"), true);
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        SSCDefOf.SSC_PnaLoad = new HediffDef
        {
            defName = SSCPnaExposureUtility.LoadDefName, hediffClass = typeof(HediffWithComps), maxSeverity = 2f,
            comps = new() { new HediffCompProperties_SSCPnaLoad() }
        };

        Check("PNA mist low starts at zero and converges from both sides", () =>
        {
            ResetPawns();
            var emitter = NewPnaMistDevice();
            Pawn rising = MistPawnAt(10, 11), falling = MistPawnAt(11, 10);
            Require(emitter.TrySetMode(SSCPnaMistMode.Low));
            AllocateMistPower(emitter);
            SSCPnaExposureUtility.ApplyExposure(falling, .65f, SSCPnaExposureSource.Debug);
            emitter.ApplyExposurePulse();
            SimulateMist(emitter, 0, 14_000, rising, falling);
            NearWithin(.35f, SSCPnaExposureUtility.GetLoad(rising), .025f);
            NearWithin(.35f, SSCPnaExposureUtility.GetLoad(falling), .025f);
        });

        Check("PNA mist high fills only current coverage and radius does not change mode", () =>
        {
            ResetPawns();
            var emitter = NewPnaMistDevice();
            Pawn inside = MistPawnAt(17, 10), outside = MistPawnAt(18, 10);
            Require(emitter.TrySetMode(SSCPnaMistMode.High));
            Require(emitter.TrySetRadius(7));
            Equal(SSCPnaMistMode.High, emitter.Mode);
            AllocateMistPower(emitter);
            // 为高档执行完整普通 Tick，覆盖生产调度中的供电、扣料和错峰投送。
            int firstPulse = 0;
            while (!emitter.parent.IsHashIntervalTick(60)) Tick(++firstPulse);
            for (int tick = firstPulse; tick <= firstPulse + 2460; tick++) { Tick(tick); emitter.CompTick(); }
            Near(1f, SSCPnaExposureUtility.GetLoad(inside));
            Near(0f, SSCPnaExposureUtility.GetLoad(outside));
        });

        Check("PNA mist Scribe keeps a covered startup dose and blackout clears it", () =>
        {
            ResetPawns();
            var holder = new SavedPnaMistEmitter();
            Pawn pawn = MistPawnAt(11, 10);
            Require(holder.emitter.TrySetRadius(8) && holder.emitter.TrySetMode(SSCPnaMistMode.Low));
            AllocateMistPower(holder.emitter);
            Tick(0); holder.emitter.ApplyExposurePulse();
            SimulateMist(holder.emitter, 0, 40);
            Near(0f, SSCPnaExposureUtility.GetLoad(pawn));

            // 角色由地图持有，存档只保存目标引用，在交叉引用阶段登记同一角色。
            string path = Path.Combine(saves, "pna-mist-emitter.xml");
            Scribe.saver.InitSaving(path, "save"); Scribe_Deep.Look(ref holder, "emitter"); Scribe.saver.FinalizeSaving();
            holder = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref holder, "emitter");
            var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory")
                .GetValue(Scribe.loader.crossRefs);
            directory.RegisterLoaded(pawn);
            Scribe.loader.FinalizeLoading();

            var emitter = holder.emitter;
            Equal(SSCPnaMistMode.Low, emitter.Mode); Equal(8, emitter.Radius);
            emitter.PostSpawnSetup(true);
            AllocateMistPower(emitter);
            Tick(2400); emitter.ApplyExposurePulse();
            SimulateMist(emitter, 2400, 35);
            Near(.0075f, SSCPnaExposureUtility.GetLoad(pawn));

            // 在相邻采样之间断电，短暂停机也应清掉尚未入账的旧剂量。
            Pawn newTarget = MistPawnAt(12, 10);
            SimulateMist(emitter, 4500, 40);
            blackout = true; Tick(6901); emitter.CompTick(); blackout = false;
            Tick(6960); emitter.ApplyExposurePulse();
            SimulateMist(emitter, 6960, 40);
            Near(0f, SSCPnaExposureUtility.GetLoad(newTarget));
        });
    }
}
