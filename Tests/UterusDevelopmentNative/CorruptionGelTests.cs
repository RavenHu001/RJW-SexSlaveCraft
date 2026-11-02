// AI 辅助生成：生产候选真实Comp/开关/Scribe；只隔离位置与地图放置末端，不重写生产算法。
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static HediffDef gelProducer, gelExperience, gelOrgan;
    private static readonly HashSet<Pawn> gelPawns = new();
    private static bool gelLocation = true, gelAccept = true;
    private static int gelDeliveries;

    private static void CorruptionGelTests(string repo, Harmony harmony)
    {
        // 与既有独立分区一致：只预热宿主未加载的其他系统DefOf，实际凝胶XML错误仍在下方检查。
        bootstrap = true;
        try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(SSCDefOf).TypeHandle); }
        finally { bootstrap = false; }
        errors.Clear();
        // 工作属性只保留本批需要的原版基础值；照明／年龄等无关StatPart不参与凝胶代价验证。
        if (DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal") == null)
            DefDatabase<StatDef>.Add(new StatDef { defName = "WorkSpeedGlobal", defaultBaseValue = 1f });
        if (human.race.lifeStageAges.Count == 0)
            human.race.lifeStageAges.Add(new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "GelAdult", developmentalStage = DevelopmentalStage.Adult } });
        // 真正的RJW器官分类判断；身体结构位置由最小身体夹具提供。
        gelExperience = new HediffDef { defName = "SSC_Exp_Anus", hediffClass = typeof(Hediff), initialSeverity = 1, maxSeverity = 1 };
        gelOrgan = new rjw.HediffDef_SexPart { defName = "GelFixtureAnus", hediffClass = typeof(Hediff), initialSeverity = 1, genitalFamily = rjw.GenitalFamily.Anus };
        gelExperience = DefDatabase<HediffDef>.GetNamedSilentFail(gelExperience.defName) ?? AddGelDef(gelExperience);
        AddGelDef(gelOrgan);
        var product = Bare<ThingDef>(); product.defName = "SSC_CorruptionGel"; product.category = ThingCategory.Item; product.thingClass = typeof(Thing); product.stackLimit = 75; product.destroyable = true;
        DefDatabase<ThingDef>.Add(product);
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_CorruptionGelProduction.xml"));
        gelProducer = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
        DefDatabase<HediffDef>.Add(gelProducer); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Require(errors.Count == 0, string.Join('\n', errors));
        void Patch(Type type, string method, string prefix) => harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(Program), prefix));
        Patch(typeof(SSCIdentityUtility), nameof(SSCIdentityUtility.IsSupportedVanillaStatus), nameof(GelManagedBoundary));
        Patch(typeof(HediffComp_SSCCorruptionGelProduction), "HasDeliveryLocation", nameof(GelLocationBoundary));
        Patch(typeof(HediffComp_SSCCorruptionGelProduction), "TryDeliverProduct", nameof(GelDeliveryBoundary));
        try
        {
            Check("gel old completed body receives one disabled producer without query writes", () =>
            {
                var pawn = GelPawn(false); Require(SSCCorruptionGelProductionUtility.Find(pawn) == null);
                SSCAbilityToggleUtility.GetView(pawn, SSCCorruptionGelProductionUtility.AbilityId);
                Require(SSCCorruptionGelProductionUtility.Find(pawn) == null);
                SSCCorruptionGelProductionUtility.EnsureProducer(pawn); var comp = GelComp(pawn);
                Require(!comp.ProductionEnabled); Near(0, (float)comp.BatchProgress);
                SSCCorruptionGelProductionUtility.EnsureProducer(pawn);
                Require(ReferenceEquals(comp, GelComp(pawn))); Equal(1, pawn.health.hediffSet.hediffs.Count(h => h.def == gelProducer));
            });
            Check("gel natural interval uses delta and pause never catches up", () =>
            {
                var pawn = GelPawn(); var comp = GelComp(pawn); GelToggle(pawn, true);
                GelTick(comp, 30000); Near(.25f, (float)comp.BatchProgress);
                GelToggle(pawn, false); GelTick(comp, 600000); Near(.25f, (float)comp.BatchProgress);
                GelToggle(pawn, true); GelTick(comp, 30000); Near(.5f, (float)comp.BatchProgress);
                Require(!comp.parent.ShouldRemove);
            });
            Check("gel zero progress and explicit choice survive native health Scribe", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); var path = Save(pawn); var restored = Load(path);
                Require(GelComp(restored).ProductionEnabled); Near(0, (float)GelComp(restored).BatchProgress);
                Edit(path, d => { d.Descendants("sscCorruptionGelProductionEnabled").Remove(); });
                Require(!GelComp(Load(path)).ProductionEnabled);
            });
            Check("gel actual chain stage changes only future accumulation rate", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); var comp = GelComp(pawn);
                GelTick(comp, 12000); Near(.1f, (float)comp.BatchProgress);
                SSCDefOf.ChainOfSexSlave ??= AddGelDef(new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave), maxSeverity = 1 });
                var chain = new Hediff_ChainOfSexSlave { pawn = pawn, def = SSCDefOf.ChainOfSexSlave, comps = new(), Severity = .5f };
                pawn.health.hediffSet.hediffs.Add(chain);
                Near(.1f, (float)comp.BatchProgress); Near(1.5f, comp.RateMultiplier);
                GelTick(comp, 12000); Near(.25f, (float)comp.BatchProgress);
            });
            Check("gel nonzero saved inventory remains independent of persona dictionary", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); GelTick(GelComp(pawn), 48000); GelToggle(pawn, false);
                SSCAbilityToggleUtility.ClearPersonalityState(pawn);
                var restored = GelComp(Load(Save(pawn))); Near(.4f, (float)restored.BatchProgress); Require(!restored.ProductionEnabled);
            });
            Check("gel failed delivery retains one full batch and successful retry pays once", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); var comp = GelComp(pawn); gelAccept = false; gelDeliveries = 0;
                GelTick(comp, 240000); Near(1, (float)comp.BatchProgress); Equal(1, gelDeliveries);
                GelTick(comp, 60); Near(1, (float)comp.BatchProgress); Equal(1, gelDeliveries);
                gelAccept = true; GelTick(comp, 250); Near(0, (float)comp.BatchProgress); Equal(2, gelDeliveries);
                GelTick(comp, 60); Require(comp.BatchProgress > 0 && comp.BatchProgress < .01); Equal(2, gelDeliveries);
            });
            Check("gel unavailable location pauses without later world-time backfill", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); gelLocation = false; GelTick(GelComp(pawn), 600000);
                Near(0, (float)GelComp(pawn).BatchProgress); gelLocation = true; GelTick(GelComp(pawn), 6000);
                Near(.05f, (float)GelComp(pawn).BatchProgress);
            });
            Check("gel lost organ pauses and can safely disable while ineligible", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); var comp = GelComp(pawn);
                pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs.First(h => h.def == gelOrgan));
                Require(SSCCorruptionGelProductionUtility.ActivationFailure(pawn) != null); GelTick(comp, 60000);
                Near(0, (float)comp.BatchProgress); GelToggle(pawn, false);
                Require(!SSCAbilityToggleUtility.GetView(pawn, SSCCorruptionGelProductionUtility.AbilityId).CanChange);
            });
            Check("gel full development requires the exact completed threshold", () =>
            {
                var pawn = GelPawn(false); pawn.health.hediffSet.hediffs.First(h => h.def == gelExperience).Severity = .999f;
                SSCCorruptionGelProductionUtility.EnsureProducer(pawn); Require(SSCCorruptionGelProductionUtility.Find(pawn) == null);
            });
            Check("gel replaced owner rejects delayed UI request", () =>
            {
                var pawn = GelPawn(); var old = SSCAbilityToggleUtility.GetView(pawn, SSCCorruptionGelProductionUtility.AbilityId);
                pawn.health.RemoveHediff(GelComp(pawn).parent); SSCCorruptionGelProductionUtility.EnsureProducer(pawn);
                Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCCorruptionGelProductionUtility.AbilityId, true, old.Context, out _));
                Require(!GelComp(pawn).ProductionEnabled); GelToggle(pawn, true);
            });
            Check("gel foreign identity preserves choice but disables actual production", () =>
            {
                var pawn = GelPawn(); GelToggle(pawn, true); pawn.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                GelTick(GelComp(pawn), 60000); Near(0, (float)GelComp(pawn).BatchProgress); Require(GelComp(pawn).ProductionEnabled);
                GelToggle(pawn, false);
            });
            Check("gel clone source filter and personality processing cannot carry inventory", () =>
            {
                Require(!(bool)AccessTools.Method(typeof(RabbitCloneUtility), "ShouldKeepSscHediff").Invoke(null, new object[] { gelProducer }));
                var source = new CompPersonalityStore(); source.hediffTags[gelProducer] = 1;
                var copy = new CompPersonalityStore(); copy.CopyFrom(source); Require(!copy.hediffTags.ContainsKey(gelProducer));
            });
            // 使用原生旅队持有关系及真实库存容器，只有接收者挑选和整场通知由宿主隔离。
            harmony.Unpatch(AccessTools.Method(typeof(HediffComp_SSCCorruptionGelProduction), "TryDeliverProduct"), AccessTools.Method(typeof(Program), nameof(GelDeliveryBoundary)));
            var notify = AccessTools.Method(typeof(ThingOwner), "NotifyAdded", new[] { typeof(Thing) });
            harmony.Patch(notify, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            // 与既有旅队宿主一致，只隔离销毁时的整场地图预约服务；合并、持有和销毁本身仍走原版。
            var reservations = AccessTools.Method(typeof(Thing), "RemoveAllReservationsAndDesignationsOnThis");
            harmony.Patch(reservations, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            Patch(typeof(CaravanInventoryUtility), nameof(CaravanInventoryUtility.FindPawnToMoveInventoryTo), nameof(GelReceiverBoundary));
            Check("gel real caravan inventory accepts and stacks product before clearing progress", () =>
            {
                var pawn = GelPawn(); pawn.inventory = new Pawn_InventoryTracker(pawn);
                AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-1);
                var caravan = new Caravan(); Require(caravan.pawns.TryAdd(pawn)); gelReceiver = pawn;
                Require(ReferenceEquals(pawn.GetCaravan(), caravan));
                GelToggle(pawn, true); GelTick(GelComp(pawn), 120000); Near(0, (float)GelComp(pawn).BatchProgress);
                Equal(1, pawn.inventory.innerContainer.Sum(t => t.stackCount));
                GelTick(GelComp(pawn), 120000); Near(0, (float)GelComp(pawn).BatchProgress);
                Equal(2, pawn.inventory.innerContainer.Sum(t => t.stackCount)); Equal(1, pawn.inventory.innerContainer.Count);
            });
            Check("gel caravan without accepting inventory retains full pending batch", () =>
            {
                var pawn = GelPawn(); AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-1);
                var caravan = new Caravan(); Require(caravan.pawns.TryAdd(pawn)); gelReceiver = null;
                GelToggle(pawn, true); GelTick(GelComp(pawn), 120000); Near(1, (float)GelComp(pawn).BatchProgress);
            });
            // 真正创建物品后再改变生产选择/owner，复现回调使既有准入凭据过期的场景。
            var maker = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing));
            harmony.Patch(maker, postfix: new HarmonyMethod(typeof(Program), nameof(GelMadeBoundary)));
            try
            {
                Check("gel item creation callback disabling production cancels pending delivery", () =>
                {
                    var pawn = GelCaravanPawn(); var comp = GelComp(pawn); GelToggle(pawn, true);
                    gelMadeCallbacks = 0; gelOnMake = () => GelToggle(pawn, false);
                    GelTick(comp, 120000);
                    Equal(1, gelMadeCallbacks); Equal(0, pawn.inventory.innerContainer.Count);
                    Require(!comp.ProductionEnabled); Near(1, (float)comp.BatchProgress);
                    GelToggle(pawn, true); GelTick(comp, 60);
                    Equal(1, pawn.inventory.innerContainer.Sum(t => t.stackCount)); Near(0, (float)comp.BatchProgress);
                });
                Check("gel item creation callback replacing owner cannot deliver old inventory", () =>
                {
                    var pawn = GelCaravanPawn(); var original = GelComp(pawn); GelToggle(pawn, true);
                    gelMadeCallbacks = 0;
                    gelOnMake = () => { pawn.health.RemoveHediff(original.parent); SSCCorruptionGelProductionUtility.EnsureProducer(pawn); };
                    GelTick(original, 120000);
                    Equal(1, gelMadeCallbacks); Equal(0, pawn.inventory.innerContainer.Count);
                    Require(!ReferenceEquals(original, GelComp(pawn)) && !GelComp(pawn).ProductionEnabled);
                    Near(0, (float)GelComp(pawn).BatchProgress);
                });
            }
            finally { gelOnMake = null; harmony.Unpatch(maker, AccessTools.Method(typeof(Program), nameof(GelMadeBoundary))); }
            Check("gel map worker preserves failed batch, cleans temporary item and pays only after success", () =>
            {
                var pawn = GelPawn(); gelMapPawn = pawn; gelMap = Bare<Map>();
                AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)0);
                var mapsField = AccessTools.Field(typeof(Game), "maps");
                object oldMaps = mapsField.GetValue(Current.Game);
                mapsField.SetValue(Current.Game, new List<Map> { gelMap });
                var placement = AccessTools.GetDeclaredMethods(typeof(GenPlace)).Single(m => m.Name == nameof(GenPlace.TryPlaceThing) && m.GetParameters().Length == 9);

                harmony.Patch(placement, prefix: new HarmonyMethod(typeof(Program), nameof(GelPlaceBoundary)));
                try
                {
                    GelToggle(pawn, true); gelAccept = false; GelTick(GelComp(pawn), 120000);
                    Near(1, (float)GelComp(pawn).BatchProgress); Require(gelPlacedItem.Destroyed);
                    gelAccept = true; GelTick(GelComp(pawn), 250);
                    Near(0, (float)GelComp(pawn).BatchProgress); Require(!gelPlacedItem.Destroyed);
                    gelPlacedItem.Destroy();
                }
                finally
                {
                    mapsField.SetValue(Current.Game, oldMaps);
                    harmony.Unpatch(placement, AccessTools.Method(typeof(Program), nameof(GelPlaceBoundary)));
                    gelMapPawn = null;
                }
            });
            harmony.Unpatch(notify, AccessTools.Method(typeof(Program), nameof(Skip)));
            harmony.Unpatch(reservations, AccessTools.Method(typeof(Program), nameof(Skip)));
            harmony.Unpatch(AccessTools.Method(typeof(CaravanInventoryUtility), nameof(CaravanInventoryUtility.FindPawnToMoveInventoryTo)), AccessTools.Method(typeof(Program), nameof(GelReceiverBoundary)));
            GelSecondStepTests(repo, harmony);
        }
        finally
        {
            // 按精确方法卸载，只移除本分区边界，保留宿主原有补丁。
            harmony.Unpatch(AccessTools.Method(typeof(SSCIdentityUtility), nameof(SSCIdentityUtility.IsSupportedVanillaStatus)), AccessTools.Method(typeof(Program), nameof(GelManagedBoundary)));
            harmony.Unpatch(AccessTools.Method(typeof(HediffComp_SSCCorruptionGelProduction), "HasDeliveryLocation"), AccessTools.Method(typeof(Program), nameof(GelLocationBoundary)));
            harmony.Unpatch(AccessTools.Method(typeof(HediffComp_SSCCorruptionGelProduction), "TryDeliverProduct"), AccessTools.Method(typeof(Program), nameof(GelDeliveryBoundary)));
            gelPawns.Clear(); gelAccept = gelLocation = true;
        }
    }

    private static HediffDef AddGelDef(HediffDef def) { DefDatabase<HediffDef>.Add(def); return def; }
    private static Pawn GelPawn(bool producer = true)
    {
        var pawn = NewPawn(); gelPawns.Add(pawn); AttachToggleTraining(pawn).pawnIdentity = PawnIdentity.Slave;
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        pawn.health.AddHediff(gelOrgan, human.race.body.corePart);
        pawn.health.AddHediff(gelExperience, human.race.body.corePart).Severity = 1;
        if (producer) SSCCorruptionGelProductionUtility.EnsureProducer(pawn);
        return pawn;
    }
    private static HediffComp_SSCCorruptionGelProduction GelComp(Pawn pawn) => SSCCorruptionGelProductionUtility.Find(pawn);
    private static void GelToggle(Pawn pawn, bool enabled)
    {
        var view = SSCAbilityToggleUtility.GetView(pawn, SSCCorruptionGelProductionUtility.AbilityId);
        Require(SSCAbilityToggleUtility.TrySet(pawn, SSCCorruptionGelProductionUtility.AbilityId, enabled, view.Context, out string reason), reason);
    }
    private static void GelTick(HediffComp_SSCCorruptionGelProduction comp, int delta) { float value = 0; comp.CompPostTickInterval(ref value, delta); }
    private static bool GelManagedBoundary(Pawn pawn, ref bool __result) { if (!gelPawns.Contains(pawn)) return true; __result = true; return false; }
    private static bool GelLocationBoundary(HediffComp_SSCCorruptionGelProduction __instance, ref bool __result)
    { if (!gelPawns.Contains(__instance.Pawn)) return true; __result = gelLocation; return false; }
    private static bool GelDeliveryBoundary(HediffComp_SSCCorruptionGelProduction __instance, ref bool __result)
    { if (!gelPawns.Contains(__instance.Pawn)) return true; gelDeliveries++; __result = gelAccept; return false; }
    private static Pawn gelReceiver;
    private static bool GelReceiverBoundary(ref Pawn __result) { __result = gelReceiver; return false; }
    private static Pawn gelMapPawn;
    private static Map gelMap;
    private static Thing gelPlacedItem;
    private static bool GelPlaceBoundary(Thing thing, ref bool __result)
    { gelPlacedItem = thing; __result = gelAccept; return false; }
    private static Action gelOnMake;
    private static int gelMadeCallbacks;
    private static void GelMadeBoundary(ThingDef def)
    {
        if (def.defName != "SSC_CorruptionGel" || gelOnMake == null) return;
        Action callback = gelOnMake; gelOnMake = null; gelMadeCallbacks++; callback();
    }
    private static Pawn GelCaravanPawn()
    {
        var pawn = GelPawn(); pawn.inventory = new Pawn_InventoryTracker(pawn);
        AccessTools.Field(typeof(Thing), "mapIndexOrState").SetValue(pawn, (sbyte)-1);
        var caravan = new Caravan(); Require(caravan.pawns.TryAdd(pawn)); gelReceiver = pawn;
        return pawn;
    }
}
