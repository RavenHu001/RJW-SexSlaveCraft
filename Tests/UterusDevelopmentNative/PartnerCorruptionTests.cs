// AI 辅助生成：复用实际 RJW Orgasm、需求和 Scribe 宿主；任务与位置由夹具提供。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static readonly HashSet<Pawn> sceneFixturePawns = new();

    private static void PartnerCorruptionTests(string repo, Harmony harmony)
    {
        // 戒律资格与生成由 CultureNative 解析实际 XML；这里沿实际 Ideo 缓存和 RJW 事件验证消费者。
        var defs = new List<PreceptDef>();
        var issue = new IssueDef { defName = "SSC_Issue_PartnerCorruption" };
        foreach (var node in XDocument.Load(Path.Combine(repo, "1.7/Defs/PreceptDefs/SSC_Precepts_PartnerCorruption.xml")).Root.Elements("PreceptDef"))
        {
            var def = new PreceptDef { defName = (string)node.Element("defName"), issue = issue };
            DefDatabase<PreceptDef>.Add(def); defs.Add(def);
        }
        var ideology = AccessTools.Field(typeof(ModsConfig), "ideologyActive");
        object originalIdeology = ideology.GetValue(null);
        var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
        int originalTick = Find.TickManager.TicksGame;
        var spawned = AccessTools.PropertyGetter(typeof(Thing), "Spawned");
        var setup = AccessTools.Method(typeof(JobDriver_Sex), "SetupOrgasmTicks");
        harmony.Patch(spawned, prefix: new HarmonyMethod(typeof(Program), nameof(PartnerFixtureSpawned)));
        harmony.Patch(setup, prefix: new HarmonyMethod(typeof(Program), nameof(PartnerFixtureReset)));
        ideology.SetValue(null, true);
        try
        {
            Check("scene corruption reads the source precept and credits only the matching participant's own orgasm", () =>
            {
                var hub = PartnerPawn(); var source = PartnerPawn(); var target = PartnerPawn(Gender.Female);
                var need = GelUseNeed(target); var receive = PartnerReceiver(hub, source);
                var first = PartnerInitiator(source, receive); var observed = PartnerInitiator(target, receive);
                PartnerPrecept(target, "Unrestricted"); // 对象自己的启用档不能替施加者发放。
                var sourceComp = AttachToggleTraining(source); sourceComp.pawnIdentity = PawnIdentity.Slave;
                PartnerPrecept(source, "FemaleOnly"); GelFire(observed); Near(0, need.CurLevel);
                PartnerChain(source);
                PartnerPrecept(source, null); GelFire(observed); Near(0, need.CurLevel);
                PartnerPrecept(source, "Off"); GelFire(observed); Near(0, need.CurLevel);
                PartnerPrecept(source, "MaleOnly"); GelFire(observed); Near(0, need.CurLevel);
                PartnerPrecept(source, "FemaleOnly");
                GelFire(first); Near(0, need.CurLevel);
                observed.sex_ticks = 100; observed.Orgasm(); Near(0, need.CurLevel);
                // 两名发起者只共享接收端、没有互相配对，仍是同一活动场次。
                GelFire(observed); Near(.005f, need.CurLevel);
            });
            Check("scene corruption rejects SSC via a vanilla receiver and rejects stale or newly installed sources", () =>
            {
                var target = PartnerPawn(Gender.Male); var source = PartnerPawn(); var stale = PartnerPawn();
                var need = GelUseNeed(target); var receive = PartnerReceiver(target, source);
                PartnerChain(source); PartnerPrecept(source, "MaleOnly");
                PartnerInitiator(source, receive, new JobDriver_RitualTraining());
                GelFire(receive); Near(0, need.CurLevel);
                receive.parteners.Add(stale); PartnerChain(stale); PartnerPrecept(stale, "Unrestricted");
                PartnerInitiator(source, receive);
                // SSC 行为退出后可以结算；但本次回调中换上的新任务不能领旧事件，未开始的登记也不计入。
                gelOrgasmCallback = () => PartnerInitiator(source, receive);
                GelFire(receive); Near(0, need.CurLevel);
                GelFire(receive); Near(.005f, need.CurLevel);
            });
            Check("scene corruption merges multiple eligible participants once and stacks independently with gel", () =>
            {
                var target = PartnerPawn(Gender.None); var hub = PartnerPawn(); var other = PartnerPawn();
                var receive = PartnerReceiver(hub, target); var observed = PartnerInitiator(target, receive);
                PartnerInitiator(other, receive);
                PartnerChain(hub); PartnerPrecept(hub, "Unrestricted");
                PartnerChain(other); PartnerPrecept(other, "Unrestricted");
                var need = GelUseNeed(target); GelDose(target);
                GelFire(observed); Near(.025f, need.CurLevel);
                GelFire(observed); Near(.045f, need.CurLevel); // 只有新来源冷却，凝胶照常。
            });
            Check("scene corruption keeps SSC masters and full targets off cooldown and clips the final positive gain", () =>
            {
                var target = PartnerPawn(); var source = PartnerPawn(); var receive = PartnerReceiver(target, source);
                PartnerChain(source); PartnerPrecept(source, "Unrestricted"); PartnerInitiator(source, receive);
                var need = GelUseNeed(target); var comp = AttachToggleTraining(target);
                comp.pawnIdentity = PawnIdentity.Master; GelFire(receive); Near(0, need.CurLevel);
                comp.pawnIdentity = PawnIdentity.Unset;
                need.SetCorruption(1f); GelFire(receive);
                Require(SSCCulturePartnerCorruptionGameComponent.Current.CanClaim(target));
                need.SetCorruption(.999f); GelFire(receive);
                Near(1f, need.CurLevel); Near(1f, need.HighestCorruptionLevel);
                Require(!SSCCulturePartnerCorruptionGameComponent.Current.CanClaim(target));
            });
            Check("scene corruption cooldown survives native Scribe, a new scene and a changed source", () =>
            {
                SetPartnerTick(1000);
                var target = PartnerPawn(); var source = PartnerPawn(); var receive = PartnerReceiver(target, source);
                var need = GelUseNeed(target); PartnerChain(source); PartnerPrecept(source, "Unrestricted"); PartnerInitiator(source, receive);
                GelFire(receive); Near(.005f, need.CurLevel);

                // 原生 Scribe 保存截止 tick，而非剩余 tick 或 Job；读档登记真实 Pawn 的跨引用。
                var cooldown = SSCCulturePartnerCorruptionGameComponent.Current;
                string path = Path.Combine(saveDirectory, "ssc-culture-partner-cooldown.xml");
                Scribe.saver.InitSaving(path, "Cooldown");
                Scribe_Deep.Look(ref cooldown, "component"); Scribe.saver.FinalizeSaving();
                SetPartnerTick(1599);
                var loaded = LoadPartnerCooldown(path, target);
                components.Remove(cooldown); components.Add(loaded);
                var replacement = PartnerPawn(); PartnerChain(replacement); PartnerPrecept(replacement, "Unrestricted");
                receive = PartnerReceiver(target, replacement); PartnerInitiator(replacement, receive);
                Near(.005f, need.CurLevel); GelFire(receive); Near(.005f, need.CurLevel);
                SetPartnerTick(1600); Near(.005f, need.CurLevel); GelFire(receive); Near(.01f, need.CurLevel);

                // 旧档缺字段不创建虚假冷却，且加载过程不补发收益。
                Edit(path, xml => xml.Descendants("sscCulturePartnerCorruptionDeadlines").Remove());
                Require(LoadPartnerCooldown(path, target).CanClaim(target));
                Near(.01f, need.CurLevel);
            });
        }
        finally
        {
            ideology.SetValue(null, originalIdeology); SetPartnerTick(originalTick);
            components.RemoveAll(c => c is SSCCulturePartnerCorruptionGameComponent);
            var remove = AccessTools.Method(typeof(DefDatabase<PreceptDef>), "Remove");
            foreach (var def in defs) remove.Invoke(null, new object[] { def });
            sceneFixturePawns.Clear(); gelOrgasmCallback = null;
            harmony.Unpatch(spawned, AccessTools.Method(typeof(Program), nameof(PartnerFixtureSpawned)));
            harmony.Unpatch(setup, AccessTools.Method(typeof(Program), nameof(PartnerFixtureReset)));
        }
    }

    // 只设置场景外部输入，不替换生产的当前任务、疾患、戒律和配对判断。
    private static Pawn PartnerPawn(Gender gender = Gender.None)
    {
        if (SSCCulturePartnerCorruptionGameComponent.Current == null)
        {
            var components = (List<GameComponent>)AccessTools.Field(typeof(Game), "components").GetValue(Current.Game);
            components.Add(new SSCCulturePartnerCorruptionGameComponent(Current.Game));
        }
        var pawn = GelConsumer(); pawn.gender = gender; sceneFixturePawns.Add(pawn); return pawn;
    }

    private static void PartnerPrecept(Pawn pawn, string option)
    {
        pawn.ideo ??= new Pawn_IdeoTracker(pawn);
        var ideo = new Ideo();
        if (option != null) ideo.AddPrecept(PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("SSC_Precept_PartnerCorruption_" + option)));
        AccessTools.Field(typeof(Pawn_IdeoTracker), "ideo").SetValue(pawn.ideo, ideo);
    }

    private static void PartnerChain(Pawn pawn) => pawn.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave
        { pawn = pawn, def = SSCDefOf.ChainOfSexSlave });

    private static T PartnerJob<T>(T driver, Pawn pawn, Pawn partner) where T : JobDriver_Sex
    {
        pawn.jobs ??= new Pawn_JobTracker(pawn); driver.pawn = pawn;
        driver.job = new Job { def = gelJob, loadID = ++nextId, targetA = partner };
        driver.orgasmstick = 1; driver.sex_ticks = 100;
        pawn.jobs.curDriver = driver; pawn.jobs.curJob = driver.job; return driver;
    }

    private static JobDriver_SexBaseReciever PartnerReceiver(Pawn pawn, Pawn initialPartner)
    {
        var driver = PartnerJob(new JobDriver_SexBaseReciever(), pawn, initialPartner);
        driver.Sexprops = new SexProps(pawn, initialPartner) { usedCondom = true, isReceiver = true };
        return driver;
    }

    private static JobDriver_SexBaseInitiator PartnerInitiator(Pawn pawn, JobDriver_SexBaseReciever receiver, JobDriver_SexBaseInitiator driver = null)
    {
        var result = PartnerJob(driver ?? new JobDriver_SexQuick(), pawn, receiver.pawn);
        result.Sexprops = new SexProps(pawn, receiver.pawn) { usedCondom = true };
        if (!receiver.parteners.Contains(pawn)) receiver.parteners.Add(pawn);
        return result;
    }

    private static bool PartnerFixtureSpawned(Thing __instance, ref bool __result)
    {
        if (__instance is not Pawn pawn || !sceneFixturePawns.Contains(pawn)) return true;
        __result = true; return false;
    }

    private static bool PartnerFixtureReset(JobDriver_Sex __instance)
    {
        if (!sceneFixturePawns.Contains(__instance.pawn)) return true;
        __instance.sex_ticks = 100; return false;
    }

    private static void SetPartnerTick(int now) => AccessTools.Field(typeof(TickManager), "ticksGameInt").SetValue(Find.TickManager, now);

    private static SSCCulturePartnerCorruptionGameComponent LoadPartnerCooldown(string path, Pawn target)
    {
        SSCCulturePartnerCorruptionGameComponent loaded = null;
        Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        directory.RegisterLoaded(target);
        Scribe_Deep.Look(ref loaded, "component", Current.Game); Scribe.loader.FinalizeLoading(); return loaded;
    }
}
