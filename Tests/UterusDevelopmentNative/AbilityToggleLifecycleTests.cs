// AI 辅助生成：真实五项载体和原版 Scribe；仅隔离全图、图形刷新及完整 Pawn 世界序列化。
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>以实际 Comp／Hediff 序列化入口验证五项全部真假组合、旧字段及身体复制，不模拟业务 setter。</summary>
    private static void AbilityToggleLifecycleTests(string repo)
    {
        // 外观定义提供最小实际组件，业务参数／旧档迁移执行生产实现；不加载 GPU 或完整游戏 Mod 启动。
        SSCDefOf.SSC_FullGelatinizedBody = ToggleDef("SSC_FullGelatinizedBody", typeof(HediffWithComps));
        SSCDefOf.SSC_FullGelatinizedBody.comps = new() { new HediffCompProperties_FullGelAppearance() };
        SSCDefOf.BridleOfSexSlave = ToggleDef("Hediff_BridleOfSexSlave", typeof(Hediff_BridleOfSexSlave));
        var marker = ToggleDef(SSCAbilityToggleUtility.InfestationMarker, typeof(Hediff_SSCToggleAbilityMarker));
        foreach (int bits in Enumerable.Range(0, 32))
            Check("toggle lifecycle native five-storage Scribe combination " + bits, () =>
            {
                var p = NewPawn(); var comp = AttachToggleTraining(p);
                comp.pawnIdentity = PawnIdentity.Master;
                comp.abilityToggles.Set(SSCAbilityToggleUtility.MasterDesireInfestation, (bits & 1) != 0);
                comp.abilityToggles.Set("SSC_UnknownFuture", false); comp.milkProductionEnabled = (bits & 4) != 0;
                var bridle = (Hediff_BridleOfSexSlave)p.health.AddHediff(SSCDefOf.BridleOfSexSlave);
                bridle.shareDamageEnabled = (bits & 2) != 0;
                Start(p, 1); Toggle(p, (bits & 8) != 0);
                var gel = (HediffWithComps)p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody);
                SetNativeGel(gel, (bits & 16) != 0);
                // 两个原生节点分别恢复后组合查询；不宣称已执行完整 Pawn 世界跨引用保存。
                var restored = Load(Save(p)); var loadedComp = LoadToggleTraining(SaveToggleTraining(comp), restored);
                var expected = new[] { (bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0 };
                for (int i = 0; i < ToggleIds.Length; ++i) Equal(expected[i], SSCAbilityToggleUtility.GetEnabled(restored, ToggleIds[i]));
                Require(!loadedComp.abilityToggles.Get("SSC_UnknownFuture", true));
                Require(loadedComp.abilityToggles.Get(SSCAbilityToggleUtility.MilkProduction, true));
                // 上句证明没有向字典保存显式 false；缺键按调用者默认解释。身体值仍由原字段独立返回。
            });
        foreach (bool legacyGlobal in new[] { false, true })
            Check("toggle lifecycle native all legacy defaults, gel global=" + legacyGlobal, () =>
            {
                var p = NewPawn(); var comp = AttachToggleTraining(p); comp.milkProductionEnabled = false;
                p.health.AddHediff(SSCDefOf.BridleOfSexSlave); Start(p, 1);
                var gel = (HediffWithComps)p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody); SetNativeGel(gel, false);
                string bodyPath = Save(p), compPath = SaveToggleTraining(comp);
                Edit(bodyPath, doc => { foreach (var node in doc.Descendants().Where(e => e.Name.LocalName is "shareDamageEnabled" or "sscUterusFertilityEnabled" or "sscGelRenderingEnabled").ToList()) node.Remove(); });
                Edit(compPath, doc => { foreach (var node in doc.Descendants().Where(e => e.Name.LocalName is "sscAbilityToggles" or "milkProductionEnabled").ToList()) node.Remove(); });
                var settings = SSCMod.settings; SSCMod.settings = new SSCSettings { enableFullGelatinizedBodyTint = legacyGlobal };
                try
                {
                    var restored = Load(bodyPath); var loaded = LoadToggleTraining(compPath, restored);
                    Require(!SSCAbilityToggleUtility.GetEnabled(restored, ToggleIds[0]));
                    Require(SSCAbilityToggleUtility.GetEnabled(restored, ToggleIds[1]) && loaded.milkProductionEnabled);
                    Require(!SSCAbilityToggleUtility.GetEnabled(restored, ToggleIds[3]));
                    Equal(legacyGlobal, SSCAbilityToggleUtility.GetEnabled(restored, ToggleIds[4]));
                    int count = restored.health.hediffSet.hediffs.Count;
                    foreach (string id in ToggleIds) SSCAbilityToggleUtility.GetView(restored, id);
                    Equal(count, restored.health.hediffSet.hediffs.Count); Require(loaded.abilityToggles == null || !loaded.abilityToggles.HasEntries);
                }
                finally { SSCMod.settings = settings; }
            });
        Check("toggle lifecycle native carrier replacement invalidates all five captured requests", () =>
        {
            var p = NewPawn(); var comp = AttachToggleTraining(p); Start(p, 1);
            AttachLactationStage(p, SSCDefOf.SSC_Lactating_SubState);
            p.health.AddHediff(SSCDefOf.BridleOfSexSlave); p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody);
            var views = ToggleIds.Select(id => SSCAbilityToggleUtility.GetView(p, id)).ToArray();
            AttachToggleTraining(p);
            for (int i = 0; i < ToggleIds.Length; ++i) Require(!SSCAbilityToggleUtility.TrySet(p, ToggleIds[i], !views[i].Enabled, views[i].Context, out _));
            Require(comp.milkProductionEnabled && p.TryGetComp<CompSexSlaveTraining>().milkProductionEnabled);
        });
        Check("toggle lifecycle native body clone copy retains original exceptions and independent gel", () =>
        {
            var source = NewPawn(); var comp = AttachToggleTraining(source); comp.milkProductionEnabled = false;
            var record = Start(source, 1); Toggle(source, true);
            var owner = (HediffWithComps)source.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody); SetNativeGel(owner, false);
            var bridle = (Hediff_BridleOfSexSlave)source.health.AddHediff(SSCDefOf.BridleOfSexSlave); bridle.shareDamageEnabled = false;
            var clone = NewPawn(); var own = AttachToggleTraining(clone); own.milkProductionEnabled = true;
            Hidden(typeof(RabbitCloneUtility), null, "CopySscHediffsFromSource", source, clone);
            Require(!SSCUterusDevelopmentUtility.Find(clone).FertilityEnabled && record.FertilityEnabled);
            Require(own.milkProductionEnabled && !comp.milkProductionEnabled); // 成熟健康复制不覆写 Comp 旧字段。
            Require(!SSCAbilityToggleUtility.GetEnabled(clone, ToggleIds[4]));
            SetNativeGel(FullGelOwner(clone), true); Require(!SSCAbilityToggleUtility.GetEnabled(source, ToggleIds[4]));
            // 再次健康复制时既有外观／生育选择保持；本复制阶段新缰绳沿原默认，不搬运来源 false。
            // 完整克隆后续仍按原关系修复处理实际目标，不把这里的最小空缰绳当作有效绑定。
            Toggle(clone, true); Hidden(typeof(RabbitCloneUtility), null, "CopySscHediffsFromSource", source, clone);
            Require(SSCAbilityToggleUtility.GetEnabled(clone, ToggleIds[4]) && SSCUterusDevelopmentUtility.Find(clone).FertilityEnabled);
            var copiedBridle = SSCBondUtility.GetBridle(clone); Require(copiedBridle != null && copiedBridle.shareDamageEnabled);
        });
        Check("toggle lifecycle native marker repair leaves body and relationship choices intact", () =>
        {
            var p = NewPawn(); var comp = AttachToggleTraining(p); comp.pawnIdentity = PawnIdentity.Master;
            comp.milkProductionEnabled = false; comp.abilityToggles.Set(ToggleIds[0], true);
            var bridle = (Hediff_BridleOfSexSlave)p.health.AddHediff(SSCDefOf.BridleOfSexSlave); bridle.shareDamageEnabled = false;
            var record = Start(p, 1); Toggle(p, true);
            var owner = (HediffWithComps)p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody); SetNativeGel(owner, false);
            SSCAbilityToggleUtility.SyncMarkers(p); Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == marker));
            comp.pawnIdentity = PawnIdentity.Unset; SSCAbilityToggleUtility.SyncMarkers(p); Require(!p.health.hediffSet.HasHediff(marker));
            comp.pawnIdentity = PawnIdentity.Master; SSCAbilityToggleUtility.SyncMarkers(p); Require(p.health.hediffSet.HasHediff(marker));
            Require(!comp.milkProductionEnabled && !bridle.shareDamageEnabled && record.FertilityEnabled && !SSCAbilityToggleUtility.GetEnabled(p, ToggleIds[4]));
        });
        Check("toggle lifecycle native mutation pauses personality and invalidates old body requests without resetting fields", () =>
        {
            var p = NewPawn(); var comp = AttachToggleTraining(p); comp.pawnIdentity = PawnIdentity.Master;
            comp.abilityToggles.Set(ToggleIds[0], true); Start(p, 1); AttachLactationStage(p, SSCDefOf.SSC_Lactating_SubState);
            p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody); p.health.AddHediff(SSCDefOf.BridleOfSexSlave);
            var before = ToggleIds.Select(id => SSCAbilityToggleUtility.GetView(p, id)).ToArray();
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope));
            try
            {
                Require(!SSCAbilityToggleUtility.IsActive(p, ToggleIds[0]) && comp.abilityToggles.Get(ToggleIds[0]));
                for (int i = 0; i < ToggleIds.Length; ++i) Require(!SSCAbilityToggleUtility.TrySet(p, ToggleIds[i], !before[i].Enabled, before[i].Context, out _));
                // 身体项沿原独立 setter；不因人格暂停新增统一门槛，新请求只检查当前身体 owner。
                foreach (int i in new[] { 2, 3 })
                {
                    var current = SSCAbilityToggleUtility.GetView(p, ToggleIds[i]);
                    Require(SSCAbilityToggleUtility.TrySet(p, ToggleIds[i], !current.Enabled, current.Context, out string reason), reason);
                }
            }
            finally { scope.Dispose(); scope.Dispose(); }
            Require(SSCAbilityToggleUtility.IsActive(p, ToggleIds[0]) && !comp.milkProductionEnabled && SSCUterusDevelopmentUtility.Find(p).FertilityEnabled);
            Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == marker));
        });
        foreach (int index in new[] { 1, 2, 3, 4 })
            Check("toggle lifecycle native old owner cannot modify replacement " + ToggleIds[index], () =>
            {
                var p = NewPawn(); AttachToggleTraining(p); Start(p, 1); AttachLactationStage(p, SSCDefOf.SSC_Lactating_SubState);
                p.health.AddHediff(SSCDefOf.BridleOfSexSlave); p.health.AddHediff(SSCDefOf.SSC_FullGelatinizedBody);
                var view = SSCAbilityToggleUtility.GetView(p, ToggleIds[index]);
                HediffDef def = index switch { 1 => SSCDefOf.BridleOfSexSlave, 2 => SSCDefOf.SSC_Lactating_SubState, 3 => development, _ => SSCDefOf.SSC_FullGelatinizedBody };
                var original = p.health.hediffSet.GetFirstHediffOfDef(def); p.health.RemoveHediff(original);
                if (index == 3) Start(p, 1); else if (index == 2) AttachLactationStage(p, def); else p.health.AddHediff(def);
                Require(!SSCAbilityToggleUtility.IsCurrent(p, ToggleIds[index], view.Context));
                Require(!SSCAbilityToggleUtility.TrySet(p, ToggleIds[index], !view.Enabled, view.Context, out _));
                Equal(index != 3, SSCAbilityToggleUtility.GetEnabled(p, ToggleIds[index]));
            });
    }

    private static readonly string[] ToggleIds = { SSCAbilityToggleUtility.MasterDesireInfestation, SSCAbilityToggleUtility.MasterDamageSharing,
        SSCAbilityToggleUtility.MilkProduction, SSCAbilityToggleUtility.UterusFertility, SSCAbilityToggleUtility.GelRendering };

    /// <summary>登记最小真实类型定义，仅为实际组件／Scribe 提供引用解析。</summary>
    private static HediffDef ToggleDef(string name, Type type)
    {
        var existing = DefDatabase<HediffDef>.GetNamedSilentFail(name);
        if (existing != null) return existing;
        var def = new HediffDef { defName = name, hediffClass = type, label = name, initialSeverity = 1, maxSeverity = 1 };
        DefDatabase<HediffDef>.Add(def); return def;
    }
    /// <summary>调用原外观组件的实际 setter，避免图形刷新进入无 GPU 宿主。</summary>
    private static void SetNativeGel(HediffWithComps owner, bool enabled)
        => Hidden(typeof(HediffComp_FullGelAppearance), owner.TryGetComp<HediffComp_FullGelAppearance>(), "SetRenderingEnabled", enabled);
    /// <summary>只读取得实际凝胶 owner，不建立替代状态。</summary>
    private static HediffWithComps FullGelOwner(Pawn pawn) => (HediffWithComps)pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizedBody);
    /// <summary>执行真实训练组件全部 PostExposeData 字段保存到隔离节点。</summary>
    private static string SaveToggleTraining(CompSexSlaveTraining comp)
    {
        string path = Path.Combine(saveDirectory, "toggle-training-" + ++nextId + ".xml"); var fixture = new ToggleTrainingFixture(comp);
        Scribe.saver.InitSaving(path, "ToggleFixture"); Scribe_Deep.Look(ref fixture, "training"); Scribe.saver.FinalizeSaving(); return path;
    }
    /// <summary>使用预置真实 Comp 恢复训练节点，执行原生字段缺省与 PostLoadInit。</summary>
    private static CompSexSlaveTraining LoadToggleTraining(string path, Pawn pawn)
    {
        var comp = AttachToggleTraining(NewPawn()); ToggleTrainingFixture fixture = null; Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref fixture, "training", comp); Scribe.loader.FinalizeLoading();
        // 分节点恢复后才组合，避免将未构造跨引用目标的空缰绳误当成完整关系存档。
        fixture.Comp.parent = pawn;
        AccessTools.Field(typeof(ThingWithComps), "comps").SetValue(pawn, new List<ThingComp> { fixture.Comp });
        return fixture.Comp;
    }
}

/// <summary>只为真实训练组件提供 Scribe 节点，不复制字段名、默认值或载入算法。</summary>
public sealed class ToggleTrainingFixture : IExposable
{
    public CompSexSlaveTraining Comp;
    /// <summary>载入时提供已经挂载在当前测试身体的真实组件。</summary>
    public ToggleTrainingFixture(CompSexSlaveTraining comp) => Comp = comp;
    /// <summary>所有字段及阶段分支都执行生产组件的方法。</summary>
    public void ExposeData() => Comp.PostExposeData();
}
