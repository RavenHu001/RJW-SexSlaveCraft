// AI 辅助生成：真实 PNA 生产组件、催熟分支、身体 owner 与原生 Scribe；只隔离地图生成末端及食物容量。
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static HediffDef pnaProductionDef;
    private static readonly HashSet<Need_Food> pnaFoodFixtures = new();
    private static readonly HashSet<Pawn> pnaMapFixtures = new();
    private static int pnaSpawnBatches;

    /// <summary>以当前生产 XML 创建真实组件，覆盖进度、营养、催熟和身体保存的独立故障边界。</summary>
    private static void PnaProductionTests(string repo, Harmony harmony)
    {
        if (DefDatabase<ThingDef>.GetNamedSilentFail("Purple_Aphrodisiac_Nanofluid") == null)
            SurgeryItem("Purple_Aphrodisiac_Nanofluid");
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/HediffOfTrain.xml"));
        pnaProductionDef = DirectXmlToObject.ObjectFromXml<HediffDef>(
            xml.DocumentElement.SelectSingleNode("HediffDef[defName='SSC_Purple_HyperLactation']"), false);
        DefDatabase<HediffDef>.Add(pnaProductionDef);
        SSCDefOf.SSC_Hediff_Cow ??= ToggleDef("SSC_Hediff_Cow", typeof(Hediff));
        SSCDefOf.SSC_Hediff_Cow_Final ??= ToggleDef("SSC_Hediff_Cow_Final", typeof(Hediff));
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Require(errors.Count == 0, string.Join('\n', errors));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Need_Food), nameof(Need_Food.MaxLevel)),
            prefix: new HarmonyMethod(typeof(Program), nameof(PnaFoodCapacityBoundary)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)),
            prefix: new HarmonyMethod(typeof(Program), nameof(PnaMapBoundary)));
        harmony.Patch(AccessTools.Method(typeof(HediffComp_SeverityProduct), "SpawnProductOnMap"),
            prefix: new HarmonyMethod(typeof(Program), nameof(PnaSpawnBoundary)));

        Check("PNA production native pause retains progress, saves nutrition and resumes without catchup", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .42f); var food = PnaFood(pawn, .8f);
            var other = PnaManagedPawn(); var otherProduction = AddPnaProduction(other, .2f);
            PnaInterval(production); Near(.4202f, production.parent.Severity); Near(.79985f, food.CurLevel);
            SetPnaProduction(pawn, false); float progress = production.parent.Severity, nutrition = food.CurLevel;
            PnaInterval(production, 6); Near(progress, production.parent.Severity); Near(nutrition, food.CurLevel);
            PnaInterval(otherProduction); Near(.2002f, otherProduction.parent.Severity); Require(otherProduction.ProductionEnabled);
            SetPnaProduction(pawn, true); PnaInterval(production);
            Near(.4204f, production.parent.Severity); Near(.7997f, food.CurLevel);
        });
        Check("PNA production native original hunger and absent-food rules remain", () =>
        {
            var hungry = PnaManagedPawn(); var stalled = AddPnaProduction(hungry, .3f); var food = PnaFood(hungry, 0f);
            PnaInterval(stalled); Near(.3f, stalled.parent.Severity); Near(0f, food.CurLevel);
            var noFood = AddPnaProduction(PnaManagedPawn(), .3f); PnaInterval(noFood); Near(.3002f, noFood.parent.Severity);
            stalled.SetProductionEnabled(false);
            Equal("SSC_PnaProduction_ProgressPaused".Translate("30%").ToString(), stalled.CompLabelInBracketsExtra);
        });
        Check("PNA nutrition status shares read-only detail and health state with pause precedence", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .42f); var food = PnaFood(pawn, 0f);
            for (int i = 0; i < 2; ++i)
            {
                Require(production.IsWaitingForNutrition);
                Equal("SSC_PnaProduction_StorageNutritionInsufficient".Translate("42%").ToString(), SSCPnaProductionUtility.GetStorageDetail(pawn));
                Equal(Strings.Produce_ProgressStalled("42%"), production.CompLabelInBracketsExtra);
            }
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.PnaProduction);
            Require(view.Enabled && view.Active && view.CanChange); Near(.42f, production.parent.Severity); Near(0f, food.CurLevel);
            SetPnaProduction(pawn, false); Require(!production.IsWaitingForNutrition);
            Equal("SSC_PnaProduction_StorageDisabled".Translate("42%").ToString(), SSCPnaProductionUtility.GetStorageDetail(pawn));
            Equal("SSC_PnaProduction_ProgressPaused".Translate("42%").ToString(), production.CompLabelInBracketsExtra);
        });
        Check("PNA nutrition threshold uses configured absolute cost and equal payment resumes", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .42f); var food = PnaFood(pawn, 0f);
            float oldCost = production.Props.nutritionPerDay;
            try
            {
                production.Props.nutritionPerDay = .45f;
                float cost = (.45f / 60000f) * 60f; food.CurLevel = MathF.BitDecrement(cost);
                Require(production.IsWaitingForNutrition); PnaInterval(production); Near(.42f, production.parent.Severity);
                food.CurLevel = cost; Require(!production.IsWaitingForNutrition);
                Equal("SSC_PnaProduction_Storage".Translate("42%").ToString(), SSCPnaProductionUtility.GetStorageDetail(pawn));
                Equal(Strings.Produce_Progress("42%"), production.CompLabelInBracketsExtra);
                PnaInterval(production); Near(.4202f, production.parent.Severity); Near(0f, food.CurLevel);
            }
            finally { production.Props.nutritionPerDay = oldCost; }
        });
        Check("PNA nutrition absent food remains unblocked and continues free growth", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .3f);
            Require(pawn.needs.food == null && !production.IsWaitingForNutrition);
            Equal("SSC_PnaProduction_Storage".Translate("30%").ToString(), SSCPnaProductionUtility.GetStorageDetail(pawn));
            Equal(Strings.Produce_Progress("30%"), production.CompLabelInBracketsExtra);
            PnaInterval(production); Near(.3002f, production.parent.Severity);
        });
        Check("PNA nutrition zero configured cost permits growth with empty food", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .3f); var food = PnaFood(pawn, 0f);
            float oldCost = production.Props.nutritionPerDay;
            try
            {
                production.Props.nutritionPerDay = 0f; Require(!production.IsWaitingForNutrition);
                PnaInterval(production); Near(.3002f, production.parent.Severity); Near(0f, food.CurLevel);
            }
            finally { production.Props.nutritionPerDay = oldCost; }
        });
        Check("PNA nutrition uses raw progress and full batch settles without nutrition", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .9999f); var food = PnaFood(pawn, 0f);
            Require(production.IsWaitingForNutrition);
            production.parent.Severity = 1f; Require(!production.IsWaitingForNutrition);
            Equal("SSC_PnaProduction_Storage".Translate("100%").ToString(), SSCPnaProductionUtility.GetStorageDetail(pawn));
            Equal(Strings.Produce_Progress("100%"), production.CompLabelInBracketsExtra);
            pnaMapFixtures.Add(pawn); pnaSpawnBatches = 0;
            try { PnaInterval(production); Equal(1, pnaSpawnBatches); Near(0f, food.CurLevel); Near(0f, production.parent.Severity); }
            finally { pnaMapFixtures.Remove(pawn); }
        });
        Check("PNA production native paused full batch is retained and released once after resume", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, 1f); var food = PnaFood(pawn, .8f);
            pnaMapFixtures.Add(pawn); pnaSpawnBatches = 0;
            try
            {
                SetPnaProduction(pawn, false); PnaInterval(production, 3);
                Near(1f, production.parent.Severity); Near(.8f, food.CurLevel); Equal(0, pnaSpawnBatches);
                SetPnaProduction(pawn, true); PnaInterval(production);
                Near(0f, production.parent.Severity); Near(.8f, food.CurLevel); Equal(1, pnaSpawnBatches);
                PnaInterval(production); Near(.0002f, production.parent.Severity); Equal(1, pnaSpawnBatches);
            }
            finally { pnaMapFixtures.Remove(pawn); }
        });
        Check("PNA production native pause blocks free direct-feed acceleration while milk continues", () =>
        {
            var scene = PnaCow(); SetPnaProduction(scene.Pawn, false);
            ApplyPnaBoost(scene.Pawn, true); Near(.4f, scene.Pna.parent.Severity); Require(scene.Milk.CurrentCharge > .05f);
            Near(.8f, scene.Food.CurLevel);
            SetPnaProduction(scene.Pawn, true); ApplyPnaBoost(scene.Pawn, true);
            Near(.56f, scene.Pna.parent.Severity);
        });
        Check("PNA production native pause removes paid PNA cost without stopping milk acceleration", () =>
        {
            var scene = PnaCow(); SetPnaProduction(scene.Pawn, false);
            ApplyPnaBoost(scene.Pawn, false); Near(.4f, scene.Pna.parent.Severity); Require(scene.Milk.CurrentCharge > .05f);
            Near(.72f, scene.Food.CurLevel); // 仅母乳的 0.16 * 0.5 营养，不能再扣 PNA 的 0.12。
        });
        Check("PNA production native Scribe restores paused body choice and existing progress", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .427f); SetPnaProduction(pawn, false);
            var restored = Load(Save(pawn)); var loaded = SSCPnaProductionUtility.Find(restored);
            Require(loaded != null && !loaded.ProductionEnabled && !ReferenceEquals(loaded, production));
            Near(.427f, loaded.parent.Severity); PnaInterval(loaded); Near(.427f, loaded.parent.Severity);
        });
        Check("PNA production native legacy missing save field defaults to enabled without resetting progress", () =>
        {
            var pawn = PnaManagedPawn(); AddPnaProduction(pawn, .427f); SetPnaProduction(pawn, false);
            string path = Save(pawn); Edit(path, doc => doc.Descendants("sscPnaProductionEnabled").Remove());
            var loaded = SSCPnaProductionUtility.Find(Load(path)); Require(loaded != null && loaded.ProductionEnabled);
            Near(.427f, loaded.parent.Severity); PnaInterval(loaded); Near(.4272f, loaded.parent.Severity);
        });
        Check("PNA production native replaced owner rejects stale context and resets only new record", () =>
        {
            var pawn = PnaManagedPawn(); var original = AddPnaProduction(pawn, .42f); SetPnaProduction(pawn, false);
            var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.PnaProduction);
            pawn.health.RemoveHediff(original.parent); HyperLactationUtility.TryAddLactation(pawn);
            var replacement = SSCPnaProductionUtility.Find(pawn);
            Require(replacement != null && replacement.ProductionEnabled); Near(.01f, replacement.parent.Severity);
            Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.PnaProduction, false, view.Context, out _));
            Require(replacement.ProductionEnabled && !original.ProductionEnabled); SetPnaProduction(pawn, false);
            Require(!replacement.ProductionEnabled);
        });
        Check("PNA production native persona replacement rejects old request while current body remains manageable", () =>
        {
            var pawn = PnaManagedPawn(); var production = AddPnaProduction(pawn, .42f);
            var before = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.PnaProduction);
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(pawn, out var scope));
            try
            {
                Require(!SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.PnaProduction, false, before.Context, out _));
                SetPnaProduction(pawn, false); Require(!production.ProductionEnabled); Near(.42f, production.parent.Severity);
            }
            finally { scope.Dispose(); }
            SetPnaProduction(pawn, true); Require(production.ProductionEnabled);
        });
        Check("PNA production native persona preference transfer cannot move body pause or remove source ability", () =>
        {
            // 前面的成长专项屏蔽 PNA 来源副作用；本例临时移除此边界，实际验证来源识别。
            var producerMethod = AccessTools.Method(typeof(SSCPnaSourceUtility), nameof(SSCPnaSourceUtility.IsPnaProducer));
            var oldBoundary = AccessTools.Method(typeof(Program), nameof(NotProducer));
            harmony.Unpatch(producerMethod, oldBoundary);
            try
            {
                var source = PnaManagedPawn(); var production = AddPnaProduction(source, .42f); SetPnaProduction(source, false);
                var persona = source.GetComp<CompSexSlaveTraining>().abilityToggles;
                persona.Set(SSCAbilityToggleUtility.PnaProduction, true);
                var snapshot = persona.Copy(); SSCAbilityToggleUtility.ClearPersonalityState(source);
                Require(!production.ProductionEnabled && SSCPnaSourceUtility.IsPnaProducer(source)); Near(.42f, production.parent.Severity);
                var host = PnaManagedPawn(); var own = AddPnaProduction(host, .19f);
                snapshot.Set(SSCAbilityToggleUtility.PnaProduction, false); SSCAbilityToggleUtility.RestorePreference(host, snapshot);
                Require(own.ProductionEnabled && SSCPnaSourceUtility.IsPnaProducer(host)); Near(.19f, own.parent.Severity);
                SSCAbilityToggleUtility.RestorePreference(source, snapshot); Require(!production.ProductionEnabled);
            }
            finally { harmony.Patch(producerMethod, prefix: new HarmonyMethod(oldBoundary)); }
        });
        pnaFoodFixtures.Clear();
    }

    /// <summary>使用真实成年玩家角色与原训练组件，准入检查仍执行生产业务。</summary>
    private static Pawn PnaManagedPawn()
    {
        var pawn = SurgeryPawn(); pawn.SetFactionDirect(surgeryPlayer); return pawn;
    }

    /// <summary>由原版创建实际生产记录，测试仅设置场景起始进度。</summary>
    private static HediffComp_SeverityProduct AddPnaProduction(Pawn pawn, float progress)
    {
        HyperLactationUtility.TryAddLactation(pawn); var production = SSCPnaProductionUtility.Find(pawn);
        Require(production != null); production.parent.Severity = progress; return production;
    }

    /// <summary>捕获当前 UI 凭据并经公共提交，避免测试绕过真实编辑资格。</summary>
    private static void SetPnaProduction(Pawn pawn, bool enabled)
    {
        var view = SSCAbilityToggleUtility.GetView(pawn, SSCAbilityToggleUtility.PnaProduction);
        Require(view.Visible); Require(SSCAbilityToggleUtility.TrySet(pawn, SSCAbilityToggleUtility.PnaProduction, enabled, view.Context, out string reason), reason);
    }

    /// <summary>实际移动游戏时钟并逐 tick 调用生产组件，保留原版 hash 间隔判定。</summary>
    private static void PnaInterval(HediffComp_SeverityProduct production, int intervals = 1)
    {
        int start = Find.TickManager.TicksGame;
        for (int i = 1; i <= 60 * intervals; ++i)
        {
            Find.TickManager.DebugSetTicksGame(start + i); float adjustment = 0f; production.CompPostTick(ref adjustment);
        }
    }

    /// <summary>食物仍用真实 Need_Food 的存值及扣除，仅为无引擎身体提供容量 1。</summary>
    private static Need_Food PnaFood(Pawn pawn, float level)
    {
        var food = Bare<Need_Food>(); AccessTools.Field(typeof(Need), "pawn").SetValue(food, pawn);
        food.def = new NeedDef { defName = "Food" }; pnaFoodFixtures.Add(food);
        AccessTools.Field(typeof(Pawn_NeedsTracker), "needs").SetValue(pawn.needs, new List<Need> { food });
        pawn.needs.food = food;
        food.CurLevel = level; return food;
    }

    /// <summary>真实牛特化与原乳汁组件，用满培养进度避免触发与生产暂停无关的经验副作用。</summary>
    private static (Pawn Pawn, HediffComp_SeverityProduct Pna, HediffComp_PermanentLactating Milk, Need_Food Food) PnaCow()
    {
        var pawn = PnaManagedPawn(); var training = pawn.GetComp<CompSexSlaveTraining>();
        training.specializationType = SexSlaveSpecializationType.Cow; training.specializationProgress = 1f;
        pawn.health.AddHediff(SSCDefOf.SSC_Hediff_Cow_Final);
        var pna = AddPnaProduction(pawn, .4f);
        var milk = new HediffWithComps { pawn = pawn, def = SSCDefOf.SSC_Lactating_SubState, Severity = 1f, comps = new() };
        var component = new HediffComp_PermanentLactating { parent = milk, props = milk.def.comps.Single() };
        milk.comps.Add(component); pawn.health.AddHediff(milk); component.CurrentCharge = .05f;
        return (pawn, pna, component, PnaFood(pawn, .8f));
    }

    /// <summary>调用实际催熟分派，评分及场景创建不是本次暂停业务的替身。</summary>
    private static void ApplyPnaBoost(Pawn pawn, bool direct)
        => Hidden(typeof(Patch_ProcessSex_LactationBoost), null, "ApplyBoostWithLogic", new rjw.SexProps(pawn, NewPawn()), pawn, .1f, direct);

    /// <summary>仅隔离真实食物需求在最小宿主中查询身体容量的引擎前提。</summary>
    private static bool PnaFoodCapacityBoundary(Need_Food __instance, ref float __result)
    {
        if (!pnaFoodFixtures.Contains(__instance)) return true;
        __result = 1f; return false;
    }

    /// <summary>只为满批场景选择地图出货分支，不构造整张地图与区域管理器。</summary>
    private static bool PnaMapBoundary(Thing __instance, ref Map __result)
    {
        if (__instance is not Pawn pawn || !pnaMapFixtures.Contains(pawn)) return true;
        __result = Bare<Map>(); return false;
    }

    /// <summary>观察实际生产组件到达生成末端的次数，不模拟产品条件、清零或数量算法。</summary>
    private static bool PnaSpawnBoundary(HediffComp_SeverityProduct __instance)
    {
        if (!pnaMapFixtures.Contains(__instance.Pawn)) return true;
        pnaSpawnBatches++; return false;
    }
}
