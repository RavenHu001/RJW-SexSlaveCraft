// AI 辅助生成：生产组件与实际1.7电网、Hediff、XML解析、Scribe；不启动游戏。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

internal static class Entry
{
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("repositoryRoot gameRoot");
        string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
        var info = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "1.7/BUILD-INFO.json")));
        string[] dirs = new[] { game, Path.GetDirectoryName(typeof(Entry).Assembly.Location) }.Concat(
            info.RootElement.GetProperty("references").EnumerateArray().Select(x => Path.GetDirectoryName(x.GetProperty("path").GetString()))).ToArray();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string dir in dirs) { string p = Path.Combine(dir, name.Name + ".dll"); if (File.Exists(p)) return context.LoadFromAssemblyPath(p); }
            return null;
        };
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { repo, game });
    }
}

public class SavedEmitter : IExposable
{
    public CompSSCAphrodisiacEmitter emitter = new();
    public void ExposeData() => emitter.PostExposeData();
}

// 代表最终环境改写Hediff类的合法PostMake回调，不替换生产加药算法。
public class Hediff_EmitterReviewHook : HediffWithComps
{
    public static Action AfterMake;
    public override void PostMake() { base.PostMake(); AfterMake?.Invoke(); }
}

internal static class Program
{
    private static readonly List<Map> maps = new();
    private static readonly Dictionary<Thing, Room> rooms = new();
    private static readonly Dictionary<IntVec3, Room> smokeRooms = new();
    private static readonly Dictionary<CompPower, PowerNet> networks = new();
    private static Faction player;
    private static Map map;
    private static Room room, otherRoom;
    private static HediffDef effect;
    private static int passed, failed, nextId = 3000;
    private static bool blackout;
    private static string saves;
    private static Action onHealthAdded;
    private static int smokeDispatches;
    private static readonly List<string> errors = new();

    internal static int Run(string repo, string game)
    {
        using var log = new StringWriter();
        var harmony = new Harmony("ssc.tests.aphrodisiac-emitter.native");
        void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        saves = Path.Combine(repo, ".builds/aphrodisiac-emitter/userdata");
        Directory.CreateDirectory(Path.Combine(saves, "Config"));
        File.WriteAllText(Path.Combine(saves, "Config/ModsConfig.xml"), "<ModsConfigData><version>1.7.4995</version><activeMods/><knownExpansions/></ModsConfigData>");
        Patch(AccessTools.PropertyGetter(typeof(GenFilePaths), nameof(GenFilePaths.SaveDataFolderPath)), nameof(SavePath));
        Patch(AccessTools.Method(typeof(ModLister), "RebuildModList"), nameof(Skip));
        Patch(AccessTools.Method(typeof(ContentFinder<Texture2D>), "Get"), nameof(NoTexture));
        foreach (var method in typeof(SolidColorMaterials).GetMethods().Where(m => m.Name == nameof(SolidColorMaterials.NewSolidColorTexture)))
            Patch(method, nameof(NoTexture));
        foreach (var type in new[] { typeof(Translator), typeof(TranslatorFormattedStringExtensions) })
            foreach (var method in type.GetMethods().Where(m => m.Name == "Translate" && m.ReturnType == typeof(TaggedString) && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string)))
                Patch(method, nameof(Translate));
        Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.Maps)), nameof(Maps));
        Patch(AccessTools.PropertyGetter(typeof(Faction), nameof(Faction.OfPlayer)), nameof(Player));
        Patch(AccessTools.PropertyGetter(typeof(CompPower), nameof(CompPower.PowerNet)), nameof(Network));
        Patch(AccessTools.Method(typeof(RegionAndRoomQuery), nameof(RegionAndRoomQuery.GetRoom)), nameof(RoomFor));
        Patch(AccessTools.Method(typeof(RegionAndRoomQuery), nameof(RegionAndRoomQuery.RoomAt)), nameof(SmokeRoomFor));
        Patch(AccessTools.Method(typeof(GameConditionManager), nameof(GameConditionManager.ElectricityDisabled)), nameof(Blackout));
        // 仅隔离绘制、音效与健康后续整图通知；PowerOn、Flick意愿、电网分配及Hediff列表变更走原实现。
        Patch(AccessTools.Method(typeof(CompPowerTrader), "UpdateOverlays"), nameof(Skip));
        Patch(AccessTools.Method(typeof(CompSSCAphrodisiacEmitter), "TryEmitSmoke"), nameof(SmokeBoundary));
        foreach (var m in typeof(SoundStarter).GetMethods().Where(m => m.Name == "PlayOneShot")) Patch(m, nameof(Skip));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange)), nameof(Skip));
        Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)), nameof(Skip));
        Patch(AccessTools.Method(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.Notify_CapacityLevelsDirty)), nameof(Skip));
        harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) }),
            postfix: new HarmonyMethod(typeof(Program), nameof(AfterAdd)));
        foreach (var m in typeof(Log).GetMethods().Where(m => m.Name == "Error" && m.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))) Patch(m, nameof(LogError));
        try
        {
            Current.Game = Bare<Game>(); Current.Game.tickManager = new TickManager(); Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.questManager = new QuestManager();
            SetStatic(typeof(UnityData), "mainThreadId", Environment.CurrentManagedThreadId);
            SetStatic(typeof(Prefs), "data", new PrefsData());
            player = new Faction { def = new FactionDef { defName = "EmitterTestPlayer", isPlayer = true } };
            map = new Map(); maps.Add(map); map.mapPawns = new MapPawns(map); map.gameConditionManager = new GameConditionManager(map);
            map.info.Size = new IntVec3(50, 1, 50);
            room = Bare<Room>(); otherRoom = Bare<Room>();
            var pack = Bare<ModContentPack>(); pack.assemblies = new ModAssemblyHandler(pack);
            pack.assemblies.loadedAssemblies.Add(typeof(CompSSCAphrodisiacEmitter).Assembly);
            pack.assemblies.loadedAssemblies.Add(typeof(Entry).Assembly);
            ((List<ModContentPack>)AccessTools.Field(typeof(LoadedModManager), "runningMods").GetValue(null)).Add(pack);
            LoadEffect(game);
            errors.Clear();
            Check("authored component XML resolves actual types and Def references", () =>
            {
                var doc = new XmlDocument(); doc.Load(Path.Combine(repo, "1.7/Defs/ThingsDefs/SSC_AphrodisiacEmitter.xml"));
                XmlNodeList nodes = doc.SelectNodes("/Defs/ThingDef/comps/li");
                var powerProps = DirectXmlToObject.ObjectFromXml<CompProperties>(nodes[0], true) as CompProperties_SSCAphrodisiacPower;
                var emitterProps = DirectXmlToObject.ObjectFromXml<CompProperties>(nodes[1], true) as CompProperties_SSCAphrodisiacEmitter;
                DefDatabase<FleckDef>.Add(new FleckDef { defName = "SSC_AphrodisiacSmoke" });
                DefDatabase<FleckDef>.Add(new FleckDef { defName = "SSC_AphrodisiacSourceSmoke" });
                DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                Require(powerProps != null && emitterProps != null); Near(500, powerProps.PowerConsumption);
                Equal(typeof(CompPowerTrader), powerProps.compClass); Equal(typeof(CompSSCAphrodisiacEmitter), emitterProps.compClass);
                Require(ReferenceEquals(effect, emitterProps.hediff)); Require(emitterProps.smoke != null && emitterProps.sourceSmoke != null);
            });
            Check("defaults, integer radius and per-instance demand", () =>
            {
                var (a, p, _) = Device(); var (b, q, _) = Device();
                Equal(10, a.Radius); Near(-500, p.PowerOutput);
                Require(a.TrySetRadius(20)); Equal(20, a.Radius); Near(-1700, p.PowerOutput); Near(-500, q.PowerOutput);
                Require(a.TrySetRadius(5)); Near(-200, p.PowerOutput);
                Require(a.TrySetRadius(12.4f)); Equal(12, a.Radius); Near(-676, p.PowerOutput);
                Require(!a.TrySetRadius(float.NaN)); Equal(12, a.Radius);
                Require(a.TrySetRadius(99)); Equal(20, a.Radius);
            });
            Check("manual off obeyed by actual power net, restart with demand", () =>
            {
                var (a, p, net) = Device(); PowerSource(net, 2000);
                p.PowerOn = true; Require(a.TrySetEnabled(false)); Require(!p.PowerOn); Near(0, p.PowerOutput);
                Require(!FlickUtility.WantsToBeOn(a.parent)); net.PowerNetTick(); Require(!p.PowerOn);
                Require(a.TrySetRadius(20)); Require(a.TrySetEnabled(true)); Near(-1700, p.PowerOutput);
                net.PowerNetTick(); Require(p.PowerOn); Near(300, net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick);
            });
            Check("direct toggle and integer slider use captured building", () =>
            {
                var (a, p, _) = Device();
                var gizmos = a.CompGetGizmosExtra().ToArray();
                ((Command_Toggle)gizmos[0]).toggleAction(); Require(!a.Enabled); Near(0, p.PowerOutput);
                var slider = (Gizmo_SSCAphrodisiacRadius)gizmos[1];
                Equal(15, (int)AccessTools.Property(slider.GetType(), "Increments").GetValue(slider));
                Set(slider, "dragging", true);
                var target = AccessTools.Property(slider.GetType(), "Target");
                target.SetValue(slider, 1f); Equal(20, a.Radius);
                target.SetValue(slider, 1f / 3f); Equal(10, a.Radius);
                target.SetValue(slider, 0f); Equal(5, a.Radius);
                Set(a.parent, "mapIndexOrState", (sbyte)-1);
                target.SetValue(slider, 1f); Equal(5, a.Radius);
            });
            Check("insufficient supply does not bypass power allocation", () =>
            {
                var (a, p, net) = Device(); PowerSource(net, 600); a.TrySetRadius(20);
                net.PowerNetTick(); Require(!p.PowerOn); Near(-1700, p.PowerOutput);
                a.TrySetRadius(5); net.PowerNetTick(); Require(p.PowerOn);
            });
            Check("coverage and final Def shared accumulation without identity filters", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true;
                Pawn animal = PawnAt(10, 9, Intelligence.Animal), distant = PawnAt(30, 30), across = PawnAt(11, 10);
                rooms[across] = otherRoom;
                a.ApplyExposurePulse(); Near(.01f, Severity(animal)); Near(0, Severity(distant)); Near(0, Severity(across));
                a.ApplyExposurePulse(); Near(.02f, Severity(animal)); Equal(1, animal.health.hediffSet.hediffs.Count);
                Require(ReferenceEquals(effect, animal.health.hediffSet.hediffs[0].def));
            });
            Check("overlap additive and runtime stage/maxSeverity consumed", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); var (b, q, _) = Device(); p.PowerOn = q.PowerOn = true;
                Pawn target = PawnAt(10, 10); effect.maxSeverity = .035f;
                effect.stages = new() { new HediffStage(), new HediffStage { minSeverity = .025f, label = "ExternalStage" } };
                a.ApplyExposurePulse(); b.ApplyExposurePulse(); a.ApplyExposurePulse(); b.ApplyExposurePulse();
                Near(.035f, Severity(target)); Equal(1, target.health.hediffSet.hediffs[0].CurStageIndex);
                effect.maxSeverity = 1; effect.stages = new() { new HediffStage() };
            });
            Check("actual normal ticks give one dose per pulse, no disabled catch-up", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn target = PawnAt(10, 10);
                for (int t = 1; t <= 120; t++) { Tick(t); a.CompTick(); a.CompTickInterval(1); }
                Near(.01f, Severity(target)); a.TrySetEnabled(false);
                for (int t = 121; t <= 600; t++) { Tick(t); a.CompTick(); }
                Near(.01f, Severity(target)); a.TrySetEnabled(true); p.PowerOn = true;
                for (int t = 601; t <= 720; t++) { Tick(t); a.CompTick(); }
                Near(.02f, Severity(target));
            });
            Check("blackout and absent room do not expose or delete existing effect", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn target = PawnAt(10, 10);
                a.ApplyExposurePulse(); blackout = true; a.ApplyExposurePulse(); blackout = false;
                rooms[a.parent] = null; rooms[target] = null; a.ApplyExposurePulse(); Near(.01f, Severity(target));
                Require(a.Enabled); Near(-500, p.PowerOutput);
            });
            Check("health callback stopping source cancels remaining targets", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn first = PawnAt(10, 10), second = PawnAt(10, 11);
                onHealthAdded = () => { onHealthAdded = null; a.TrySetEnabled(false); a.ApplyExposurePulse(); };
                smokeDispatches = 0;
                for (int t = 0; !a.parent.IsHashIntervalTick(120);) Tick(++t);
                a.CompTick(); Near(.01f, Severity(first)); Near(0, Severity(second)); Equal(0, smokeDispatches);
            });
            Check("PostMake shutdown does not attach a late effect", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn target = PawnAt(10, 10);
                Type original = effect.hediffClass;
                try
                {
                    effect.hediffClass = typeof(Hediff_EmitterReviewHook);
                    Hediff_EmitterReviewHook.AfterMake = () => a.TrySetEnabled(false);
                    a.ApplyExposurePulse(); Near(0, Severity(target));
                }
                finally { effect.hediffClass = original; Hediff_EmitterReviewHook.AfterMake = null; }
            });
            Check("PostMake health replacement does not receive an old request", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn target = PawnAt(10, 10);
                Type original = effect.hediffClass; var oldHealth = target.health;
                try
                {
                    effect.hediffClass = typeof(Hediff_EmitterReviewHook);
                    Hediff_EmitterReviewHook.AfterMake = () => target.health = new Pawn_HealthTracker(target);
                    a.ApplyExposurePulse(); Near(0, Severity(target)); Equal(0, oldHealth.hediffSet.hediffs.Count);
                }
                finally { effect.hediffClass = original; Hediff_EmitterReviewHook.AfterMake = null; }
            });
            Check("moved target and removed source rejected", () =>
            {
                ResetPawns(); var (a, p, _) = Device(); p.PowerOn = true; Pawn first = PawnAt(10, 10), second = PawnAt(10, 11);
                onHealthAdded = () => { onHealthAdded = null; rooms[second] = otherRoom; };
                a.ApplyExposurePulse(); Near(0, Severity(second));
                Set(a.parent, "mapIndexOrState", (sbyte)-1); Require(!a.TrySetRadius(5)); Require(!a.TrySetEnabled(false));
                a.ApplyExposurePulse(); Near(.01f, Severity(first));
            });
            Check("smoke tile sampling finds narrow coverage and rechecks room changes", () =>
            {
                var (a, _, _) = Device();
                var sample = AccessTools.Method(typeof(CompSSCAphrodisiacEmitter), "TryFindSmokeCell");
                // 本小片只有一格属于源房间，另一格属于邻室，其余无房间；不能靠随机重试漏掉合法格。
                var only = new IntVec3(12, 0, 12);
                smokeRooms.Clear(); smokeRooms[only] = room; smokeRooms[new IntVec3(11, 0, 12)] = otherRoom;
                Rand.PushState(741);
                try
                {
                    object[] input = { map, room, 10, 10, default(IntVec3), 0 };
                    Require((bool)sample.Invoke(a, input)); Equal(only, (IntVec3)input[4]); Equal(1, (int)input[5]);
                    // 不保存房间格缓存：同一位置改属邻室，下次采样立即失效。
                    smokeRooms[only] = otherRoom;
                    Require(!(bool)sample.Invoke(a, input)); Equal(0, (int)input[5]);
                    // 目标房间引用相同仍须满足地图及半径边界。
                    smokeRooms.Clear(); smokeRooms[new IntVec3(40, 0, 40)] = room;
                    input[2] = 40; input[3] = 40; Require(!(bool)sample.Invoke(a, input));
                    smokeRooms.Clear(); smokeRooms[new IntVec3(-1, 0, 10)] = room;
                    input[2] = -5; input[3] = 10; Require(!(bool)sample.Invoke(a, input));
                }
                finally { Rand.PopState(); smokeRooms.Clear(); }
            });
            Check("real Scribe preserves switch and radius", () =>
            {
                var (a, p, _) = Device(); a.TrySetRadius(17); a.TrySetEnabled(false);
                string path = Path.Combine(saves, "emitter.xml"); SavedEmitter holder = new() { emitter = a };
                Scribe.saver.InitSaving(path, "save"); Scribe_Deep.Look(ref holder, "emitter"); Scribe.saver.FinalizeSaving();
                holder = null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref holder, "emitter"); Scribe.loader.FinalizeLoading();
                Equal(17, holder.emitter.Radius); Require(!holder.emitter.Enabled);
            });
        }
        finally { harmony.UnpatchAll(harmony.Id); Current.Game = null; }
        Console.WriteLine($"{passed}/{passed + failed} cases passed"); return failed == 0 ? 0 : 1;
    }

    private static void LoadEffect(string game)
    {
        // 读取实际RJW原始HediffDef，由原版XML解析器建立类型；阶段可随后由测试代表外部补丁替换。
        var xml = new XmlDocument(); xml.Load(Path.Combine(game, "Mods/RJW_核心_本体/1.7/Defs/Drugs/Humpshroom.xml"));
        foreach (string name in new[] { "SexFrequency", "SexSatisfaction" }) DefDatabase<StatDef>.Add(new StatDef { defName = name });
        effect = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.SelectSingleNode("/Defs/HediffDef[defName='HumpShroomEffect']"), true);
        DefDatabase<HediffDef>.Add(effect); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        if (effect.hediffClass == null) throw new Exception("Hediff type not resolved: " + string.Join('\n', errors));
    }
    private static (CompSSCAphrodisiacEmitter, CompPowerTrader, PowerNet) Device()
    {
        var props = new CompProperties_SSCAphrodisiacEmitter { hediff = effect };
        var powerProps = new CompProperties_Power { compClass = typeof(CompPowerTrader) };
        Set(powerProps, "basePowerConsumption", 500f);
        var def = Bare<ThingDef>(); def.defName = "EmitterFixture"; def.category = ThingCategory.Building; def.comps = new() { powerProps, props };
        var building = new Building { def = def, thingIDNumber = ++nextId };
        Set(building, "factionInt", player); Set(building, "mapIndexOrState", (sbyte)0); Set(building, "positionInt", new IntVec3(10, 0, 10));
        var emitter = new CompSSCAphrodisiacEmitter { parent = building, props = props };
        var power = new CompPowerTrader { parent = building, props = powerProps };
        Set(building, "comps", new List<ThingComp> { power, emitter });
        var net = Bare<PowerNet>(); net.powerNetManager = new PowerNetManager(map); net.powerComps = new() { power }; net.batteryComps = new(); net.connectors = new(); net.transmitters = new();
        networks[power] = net; rooms[building] = room; emitter.PostSpawnSetup(false); Tick(0); return (emitter, power, net);
    }
    private static void PowerSource(PowerNet net, float watts)
    {
        var props = new CompProperties_Power { compClass = typeof(CompPowerTrader) }; Set(props, "basePowerConsumption", -watts);
        var def = Bare<ThingDef>(); def.comps = new() { props }; def.defName = "GeneratorFixture";
        var thing = new Building { def = def }; Set(thing, "mapIndexOrState", (sbyte)0);
        var source = new CompPowerTrader { parent = thing, props = props }; Set(thing, "comps", new List<ThingComp> { source });
        source.PowerOutput = watts; Set(source, "powerOnInt", true); net.powerComps.Add(source); networks[source] = net;
    }
    private static Pawn PawnAt(int x, int z, Intelligence intelligence = Intelligence.Humanlike)
    {
        var def = Bare<ThingDef>(); def.defName = "TestPawn"; def.category = ThingCategory.Pawn; def.race = new RaceProperties { intelligence = intelligence };
        def.race.body = new BodyDef { defName = "FixtureBody", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Torso" } } }; def.race.body.ResolveReferences();
        var pawn = new Pawn { def = def, thingIDNumber = ++nextId };
        Set(pawn, "mapIndexOrState", (sbyte)0); Set(pawn, "positionInt", new IntVec3(x, 0, z));
        pawn.health = new Pawn_HealthTracker(pawn); pawn.mindState = new Pawn_MindState(pawn); pawn.needs = new Pawn_NeedsTracker(pawn);
        ((List<Pawn>)AccessTools.Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).Add(pawn); rooms[pawn] = room;
        return pawn;
    }
    private static void ResetPawns() { ((List<Pawn>)AccessTools.Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).Clear(); blackout = false; onHealthAdded = null; }
    private static float Severity(Pawn pawn) => pawn.health.hediffSet.GetFirstHediffOfDef(effect)?.Severity ?? 0;
    private static void Tick(int tick) => Set(Current.Game.tickManager, "ticksGameInt", tick);
    private static T Bare<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    private static void Set(object obj, string name, object value) => AccessTools.Field(obj.GetType(), name).SetValue(obj, value);
    private static void SetStatic(Type type, string name, object value) => AccessTools.Field(type, name).SetValue(null, value);
    private static void Require(bool value) { if (!value) throw new Exception("assertion failed"); }
    private static void Equal<T>(T expected, T actual) => Require(EqualityComparer<T>.Default.Equals(expected, actual));
    private static void Near(float expected, float actual) { if (Math.Abs(expected - actual) > .0001f) throw new Exception($"expected {expected}, actual {actual}"); }
    private static void Check(string name, Action test)
    {
        if (!SscTestProfile.ShouldRun(name)) return;
        errors.Clear();
        try { test(); Require(errors.Count == 0); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex + "\n" + string.Join('\n', errors)); }
    }
    private static bool Skip() => false;
    private static bool SmokeBoundary() { smokeDispatches++; return false; }
    private static bool NoTexture(ref Texture2D __result) { __result = null; return false; }
    private static bool Translate(string key, ref TaggedString __result) { __result = key; return false; }
    private static bool SavePath(ref string __result) { __result = saves; return false; }
    private static bool Maps(ref List<Map> __result) { __result = maps; return false; }
    private static bool Player(ref Faction __result) { __result = player; return false; }
    private static bool Network(CompPower __instance, ref PowerNet __result) { networks.TryGetValue(__instance, out __result); return false; }
    private static bool RoomFor(Thing thing, ref Room __result) { rooms.TryGetValue(thing, out __result); return false; }
    private static bool SmokeRoomFor(IntVec3 c, ref Room __result) { smokeRooms.TryGetValue(c, out __result); return false; }
    private static bool Blackout(ref bool __result) { __result = blackout; return false; }
    private static void AfterAdd() => onHealthAdded?.Invoke();
    private static bool LogError(string text) { errors.Add(text); return false; }
}
