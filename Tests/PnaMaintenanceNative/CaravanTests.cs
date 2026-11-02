// AI 辅助生成：真实个人重载、原版120tick节拍、旅队容器与Thing.Ingested集成验证。
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static readonly HashSet<Thing> blockedDrugs = new();
    private static WorldObjectsHolder worldObjects;
    private static bool nightRest;
    private static int caravanIngestions;
    private static Action<Thing> onDrugQuery;
    private static Action<Pawn> onCaravanIngest;
    private static MethodInfo personalDrugCheck;

    /// <summary>在既有宿主中安装真实生产补丁；仅隔离世界时区／图形／Stat等整场环境。</summary>
    private static void RunCaravanTests(Harmony harmony, string repo)
    {
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        worldObjects = new WorldObjectsHolder(); Current.Game.World.worldObjects = worldObjects;
        Current.Game.storyteller = Bare<Storyteller>(); Current.Game.storyteller.difficulty = new Difficulty();
        Patch(AccessTools.PropertyGetter(typeof(Caravan), "NightResting"), nameof(NightRest));
        Patch(AccessTools.Method(typeof(ThingOwner), "NotifyRemoved", new[] { typeof(Thing) }), nameof(Skip));
        // 最后一份销毁时无需访问独立宿主未初始化的地图预约／指定服务；销毁与容器移除仍走原版。
        Patch(AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis"), nameof(Skip));
        Patch(AccessTools.Method(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) }), nameof(NoStat));
        Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.IngestibleNow)), nameof(DrugQuery));
        Patch(AccessTools.Method(typeof(Caravan_NeedsTracker), nameof(Caravan_NeedsTracker.IngestDrug)), nameof(ObserveCaravanIngest));
        personalDrugCheck = AccessTools.DeclaredMethod(typeof(CaravanDrugPolicyUtility), "TryTakeScheduledDrugs", new[] { typeof(Pawn), typeof(Caravan) });
        // 安装生产特性而非在夹具重写转发；目标必须精确落在私有个人重载。
        harmony.CreateClassProcessor(typeof(Harmony_PnaCaravanMaintenance)).Patch();
        Require(personalDrugCheck.IsPrivate && Harmony.GetPatchInfo(personalDrugCheck)?.Postfixes.Any(
            patch => patch.PatchMethod.DeclaringType == typeof(Harmony_PnaCaravanMaintenance)) == true);
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/ThingsDefs/Nanofluid.xml"));
        rawDef.ingestible.outcomeDoers = new() { DirectXmlToObject.ObjectFromXml<IngestionOutcomeDoer>(
            xml.SelectSingleNode("/Defs/ThingDef[defName='Purple_Aphrodisiac_Nanofluid']/ingestible/outcomeDoers/li"), true) };
        rawDef.comps = new(); rawDef.destroyable = true;
        SetStatic(typeof(SSCDefOf), nameof(SSCDefOf.SSC_PnaRootedLoad), loadDef);
        pawnDef.race.lifeStageAges = new() { new LifeStageAge { def = new LifeStageDef { defName = "PnaAdult", developmentalStage = DevelopmentalStage.Adult } } };

        Check("旅队实际个人补丁沿原版120tick节拍，优先自有药且不受离图旧任务阻挡", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var own = Raw(pawn);
            var other = Traveler(false); Join(caravan, other); var shared = Raw(other);
            pawn.jobs.curJob = JobMaker.MakeJob(JobDefOf.Wait); pawn.CurJob.playerForced = true;
            pawn.jobs.jobQueue.EnqueueLast(JobMaker.MakeJob(JobDefOf.Wait));
            caravanIngestions = 0;
            for (int tick = 1; tick <= 120; tick++)
            {
                Set(Current.Game.tickManager, "ticksGameInt", tick);
                CaravanDrugPolicyUtility.CheckTakeScheduledDrugs(caravan, 1);
            }
            Equal(1, caravanIngestions); Equal(2, own.stackCount); Equal(3, shared.stackCount);
            Near(.42f, SSCPnaExposureUtility.GetBodyRecord(pawn).Severity);
            Require(pawn.CurJob.playerForced && pawn.jobs.jobQueue.Count == 1);
        });
        Check("旅队共享驮畜库存跳过不可摄入栈，原生消费最后一份并刷新库存", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var animal = Traveler(false);
            var animalDef = Bare<ThingDef>(); animalDef.category = ThingCategory.Pawn;
            animalDef.race = new RaceProperties { intelligence = Intelligence.Animal }; animal.def = animalDef;
            Join(caravan, animal); var usable = Raw(animal); usable.stackCount = 1;
            var invalid = new ThingWithComps { def = rawDef, stackCount = 2, thingIDNumber = ++nextId };
            animal.inventory.innerContainer.TryAdd(invalid, canMergeWithExistingStacks: false); blockedDrugs.Add(invalid);
            var unrelated = Caravan(Traveler(false));
            onDrugQuery = _ => CaravanInventoryUtility.AllInventoryItems(unrelated);
            Set(caravan, "cachedImmobilizedForTicks", 111); Set(caravan, "cachedDaysWorthOfFoodForTicks", 222); caravan.hasShuttleDirty = false;
            try
            {
                Require(SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan));
                Require(usable.Destroyed && !animal.inventory.innerContainer.Contains(usable)); Equal(2, invalid.stackCount);
                Equal(-99999, (int)AccessTools.Field(typeof(Caravan), "cachedImmobilizedForTicks").GetValue(caravan));
                Equal(-99999, (int)AccessTools.Field(typeof(Caravan), "cachedDaysWorthOfFoodForTicks").GetValue(caravan)); Require(caravan.hasShuttleDirty);
                Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan));
            }
            finally { blockedDrugs.Clear(); onDrugQuery = null; }
        });
        Check("原版政策先摄入，达标不追加，未达标至多追加一份且关闭维持仍保留政策", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var raw = Raw(pawn);
            SetStage(pawn, SSCPnaMaintenanceStage.Second);
            var entry = new DrugPolicyEntry { drug = rawDef, allowScheduled = true, daysFrequency = 1f };
            Set(pawn.drugs.CurrentPolicy, "entriesInt", new List<DrugPolicyEntry> { entry });
            caravanIngestions = 0; personalDrugCheck.Invoke(null, new object[] { pawn, caravan });
            Equal(1, caravanIngestions); Near(.42f, SSCPnaExposureUtility.GetBodyRecord(pawn).Severity);
            Load(pawn, .02f); SetStage(pawn, SSCPnaMaintenanceStage.Fifth); caravanIngestions = 0;
            personalDrugCheck.Invoke(null, new object[] { pawn, caravan });
            Equal(2, caravanIngestions); Require(raw.Destroyed); Near(.82f, SSCPnaExposureUtility.GetBodyRecord(pawn).Severity);
            raw = Raw(pawn); SetStage(pawn, SSCPnaMaintenanceStage.Off); caravanIngestions = 0;
            personalDrugCheck.Invoke(null, new object[] { pawn, caravan }); Equal(1, caravanIngestions); Equal(2, raw.stackCount);
            SetStage(pawn, SSCPnaMaintenanceStage.Fifth); entry.allowScheduled = false; Load(pawn, .02f); caravanIngestions = 0;
            personalDrugCheck.Invoke(null, new object[] { pawn, caravan }); Equal(1, caravanIngestions); Equal(1, raw.stackCount);
        });
        Check("原方法被其他Prefix跳过时不追加，夜休／非玩家／倒地及失格也不消费", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var raw = Raw(pawn);
            var blocker = new Harmony("ssc.tests.pna-maintenance.skip-original");
            try
            {
                blocker.Patch(personalDrugCheck, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
                personalDrugCheck.Invoke(null, new object[] { pawn, caravan }); Equal(3, raw.stackCount);
            }
            finally { blocker.UnpatchAll(blocker.Id); }
            nightRest = true;
            try { Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); }
            finally { nightRest = false; }
            Set(caravan, "factionInt", new Faction()); Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Set(caravan, "factionInt", player);
            Set(pawn.health, "healthState", PawnHealthState.Down); Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Set(pawn.health, "healthState", PawnHealthState.Mobile);
            pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(3, raw.stackCount);
        });
        Check("旅队成员与地图转换按当前实际容器准入，分队后旧旅队不能继续补给", () =>
        {
            var pawn = Traveler(); var first = Caravan(pawn); var raw = Raw(pawn);
            first.pawns.Remove(pawn); var second = Caravan(pawn);
            Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, first));
            Require(SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, second)); Equal(2, raw.stackCount);
            second.pawns.Remove(pawn); Set(pawn, "mapIndexOrState", (sbyte)0);
            Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, second)); Require(SSCPnaMaintenanceUtility.CanStart(pawn));
            Set(pawn, "mapIndexOrState", (sbyte)-1); Join(first, pawn);
            Require(SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, first)); Equal(1, raw.stackCount);
            ((HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(worldObjects)).Remove(first);
            Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, first));
        });
        Check("旅队查询回调后原链／持有者及负荷复查拒绝过期请求", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var raw = Raw(pawn);
            void OnQuery(Action action) => onDrugQuery = thing => { if (thing != raw) return; onDrugQuery = null; action(); };
            try
            {
                OnQuery(() => SSCPnaMaintenanceUtility.FindChain(pawn).target = Pawn(false));
                Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(3, raw.stackCount);
                OnQuery(() => Load(pawn, 2f));
                Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(3, raw.stackCount); Load(pawn, .02f);
                var outsider = Traveler(false);
                OnQuery(() => { pawn.inventory.innerContainer.Remove(raw); outsider.inventory.innerContainer.TryAdd(raw); });
                Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(3, raw.stackCount);
            }
            finally { onDrugQuery = null; }
        });
        Check("原生摄入回调同角色重入不重复，异常退出后下一轮仍可补给", () =>
        {
            var pawn = Traveler(); var caravan = Caravan(pawn); var raw = Raw(pawn); caravanIngestions = 0;
            try
            {
                onCaravanIngest = p => Require(!SSCPnaCaravanMaintenanceUtility.TryMaintain(p, caravan));
                Require(SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(1, caravanIngestions); Equal(2, raw.stackCount);
                onCaravanIngest = _ => throw new InvalidOperationException("test ingest callback");
                try { SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan); throw new Exception("expected callback failure"); }
                catch (InvalidOperationException ex) when (ex.Message == "test ingest callback") { }
                onCaravanIngest = null;
                Require(SSCPnaCaravanMaintenanceUtility.TryMaintain(pawn, caravan)); Equal(1, raw.stackCount);
            }
            finally { onCaravanIngest = null; }
        });
    }

    /// <summary>复用既有活体／锁链夹具，补齐原版世界摄入必需的年龄、药政和搬运容器。</summary>
    private static Pawn Traveler(bool maintenance = true)
    {
        var pawn = Pawn(maintenance); pawn.ageTracker = new Pawn_AgeTracker(pawn); Set(pawn.ageTracker, "cachedLifeStageIndex", 0);
        pawn.drugs = new Pawn_DrugPolicyTracker(pawn); var policy = new DrugPolicy();
        Set(policy, "entriesInt", new List<DrugPolicyEntry>()); pawn.drugs.CurrentPolicy = policy;
        pawn.carryTracker = new Pawn_CarryTracker(pawn); Set(pawn, "mapIndexOrState", (sbyte)-1);
        if (maintenance) { SetStage(pawn, SSCPnaMaintenanceStage.Fifth); Load(pawn, .02f); }
        return pawn;
    }

    /// <summary>只登记世界对象与容器，不运行完整世界生成、图形或人物栏通知。</summary>
    private static Caravan Caravan(Pawn pawn)
    {
        var caravan = new Caravan(); Set(caravan, "factionInt", player); caravan.SetUniqueId(++nextId);
        ((HashSet<WorldObject>)AccessTools.Field(typeof(WorldObjectsHolder), "worldObjectsHashSet").GetValue(worldObjects)).Add(caravan);
        Join(caravan, pawn); return caravan;
    }

    /// <summary>成员归属由真实ThingOwner保存，供GetCaravan及库存owner查询使用。</summary>
    private static void Join(Caravan caravan, Pawn pawn) => Require(caravan.pawns.TryAdd(pawn));
    /// <summary>世界时间及地形留在宿主边界；生产代码仍按NightResting查询准入。</summary>
    private static bool NightRest(ref bool __result) { __result = nightRest; return false; }
    /// <summary>独立宿主没有完整StatDef集；原生摄入数量、扣栈与PNA Doer继续执行。</summary>
    private static bool NoStat(ref float __result) { __result = 0f; return false; }
    /// <summary>模拟失效栈与第三方查询回调，不替代生产药物筛选或原版正常可摄入结果。</summary>
    private static bool DrugQuery(Thing __instance, ref bool __result)
    {
        onDrugQuery?.Invoke(__instance);
        if (!blockedDrugs.Contains(__instance)) return true;
        __result = false; return false;
    }
    /// <summary>仅观察原生消费入口并触发指定重入／异常，不代替IngestDrug或Thing.Ingested。</summary>
    private static void ObserveCaravanIngest(Pawn pawn) { caravanIngestions++; onCaravanIngest?.Invoke(pawn); }
}
