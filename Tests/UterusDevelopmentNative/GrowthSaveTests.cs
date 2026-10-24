// AI 辅助生成：真实 Scribe 跨引用及任务开始/完成状态，不保存另一份开发进度。
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using rjw;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>把既有 Pawn/Lord/器官作为外部世界登记；新任务和快照由真实 Scribe 恢复。</summary>
    private static GrowthSceneSave RoundTripGrowth(JobDriver_Sex source, JobDriver_Sex restored,
        GrowthScene scene, Action<XDocument> edit = null, CompSexSlaveTraining training = null)
    {
        string path = Path.Combine(saveDirectory, "growth-" + ++nextId + ".xml");
        var saved = new GrowthSceneSave(source, training);
        Scribe.saver.InitSaving(path, "UterusGrowthFixture");
        Scribe_Deep.Look(ref saved, "scene"); Scribe.saver.FinalizeSaving();
        if (edit != null) Edit(path, edit);
        GrowthSceneSave loaded = null;
        Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        // 这层宿主不重建完整地图/Pawn；登记外部实体，让生产快照实际执行引用解析。
        foreach (var pawn in new[] { scene.Source, scene.Target })
        {
            directory.RegisterLoaded(pawn);
            foreach (var hediff in pawn.health.hediffSet.hediffs)
                directory.RegisterLoaded(hediff);
        }
        if (training?.bindingRitualLord != null)
            directory.RegisterLoaded(training.bindingRitualLord);
        Scribe_Deep.Look(ref loaded, "scene", restored, training);
        Scribe.loader.FinalizeLoading();
        return loaded;
    }

    /// <summary>恢复新的驱动对象但保持同一存档 Job 身份，避免沿用原驱动弱表而掩盖存档缺失。</summary>
    private static T RestoreDriver<T>(JobDriver_Sex old) where T : JobDriver_SexBaseInitiator, new()
    {
        var restored = new T { pawn = old.pawn, job = old.job, ticks_left = old.ticks_left };
        old.pawn.jobs.curDriver = restored; return restored;
    }

    /// <summary>验证新字段往返、旧档缺字段、已认领任务恢复，以及仪式跨阶段事实的真实深度序列化。</summary>
    private static void GrowthSaveTests(Harmony harmony)
    {
        // 原生 JobDriver.ExposeData 包含 SetupToils 等地图生命周期；只隔离这一层。
        // RJW ExposeData、SSC 日常/仪式字段、守卫以及新快照的 Scribe 全部继续执行。
        harmony.Patch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.ExposeData)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(JobDriver_Sex), nameof(JobDriver_Sex.ExposeData)),
            postfix: new HarmonyMethod(typeof(Program), nameof(GrowthGuardSaveBoundary)));
        // 独立宿主没有 RJW 旧版 IMGUI 模块，不能把其整程序集加入 GenTypes 扫描；
        // 仅将这一个已知类名映射到真实 SexProps，后续 Scribe 构造/引用/ExposeData 均为原生。
        harmony.Patch(AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly)),
            prefix: new HarmonyMethod(typeof(Program), nameof(RjwScribeTypeBoundary)));
        foreach (bool completed in new[] { false, true })
            Check("batch3 real Scribe ordinary new driver restores evidence/claim: " + completed, () =>
            {
                var s = OrdinaryGrowth(); if (completed) FinishOrdinary(s.scene);
                var restored = RestoreDriver<GrowthOrdinaryDriver>(s.driver);
                RoundTripGrowth(s.driver, restored, s.scene);
                Require(restored.Sexprops._resolved == null);
                using var context = (SSCOrdinarySexOutcomeUtility.Context)Hidden(guardType, null, "PrepareOrdinaryOutcome", restored.Sexprops);
                if (completed) Require(context == null);
                else { Require(context?.TryClaim() == true); context.Dispatch(); }
                Near(.035f, Progress(s.scene.Target)); Require(restored.Sexprops._resolved == null);
            });
        foreach (string missing in new[] { "receivedVaginalPenetration", "sscOrdinarySexParts" })
            Check("batch3 real Scribe old ordinary scene skips new growth only: " + missing, () =>
            {
                var s = OrdinaryGrowth(); var restored = RestoreDriver<GrowthOrdinaryDriver>(s.driver);
                RoundTripGrowth(s.driver, restored, s.scene, doc => doc.Descendants(missing).Remove());
                // 读档后原生再次进入 Start 也不能为旧场补捕获新字段。
                MarkStarted(restored);
                using var context = (SSCOrdinarySexOutcomeUtility.Context)Hidden(guardType, null, "PrepareOrdinaryOutcome", restored.Sexprops);
                Require(context?.TryClaim() == true); context.Dispatch(); Near(.01f, Progress(s.scene.Target));
                if (missing == "receivedVaginalPenetration")
                    Require(s.scene.Target.health.hediffSet.hediffs.Any(h => h.def.defName == "SSC_Exp_Genitals"));
            });
        foreach (bool completed in new[] { false, true })
            Check("batch3 real Scribe daily restores original payout and claim: " + completed, () =>
            {
                var s = DailyGrowth(); if (completed) s.payout.initAction();
                var restored = RestoreDriver<JobDriver_Training>(s.driver);
                RoundTripGrowth(s.driver, restored, s.scene);
                var toils = ((IEnumerable<Toil>)Hidden(typeof(JobDriver_Training), restored, "MakeNewToils")).ToArray();
                toils.Last().initAction(); toils.Last().initAction();
                Near(.035f, Progress(s.scene.Target)); Equal(1, sexProcessCalls); Equal(1, dailyOutcomes);
            });
        Check("batch3 real Scribe old daily missing contact flag never backfills growth", () =>
        {
            var s = DailyGrowth(); var restored = RestoreDriver<JobDriver_Training>(s.driver);
            RoundTripGrowth(s.driver, restored, s.scene, doc => doc.Descendants("receivedVaginalPenetration").Remove());
            var toils = ((IEnumerable<Toil>)Hidden(typeof(JobDriver_Training), restored, "MakeNewToils")).ToArray();
            toils.Last().initAction(); Near(.01f, Progress(s.scene.Target)); Equal(1, dailyOutcomes);
        });
        foreach (bool old in new[] { false, true })
            Check("batch3 real Scribe ritual completed-phase evidence persists or defaults empty: " + old, () =>
            {
                ResetGrowthFlow(); var f = new GrowthRitual(); f.Phase();
                var original = (JobDriver_RitualTraining)f.Scene.Source.jobs.curDriver;
                var restored = RestoreDriver<JobDriver_RitualTraining>(original);
                var evidence = AccessTools.Field(typeof(CompSexSlaveTraining), "uterusRitualEvidence");
                // 先保存再在加载阶段覆写同一外部组件，避免保留原列表假装字段恢复成功。
                RoundTripGrowth(original, restored, f.Scene,
                    doc => { if (old) doc.Descendants("uterusRitualEvidence").Remove(); evidence.SetValue(f.Comp, null); }, f.Comp);
                if (old) Require(evidence.GetValue(f.Comp) == null);
                else Require(evidence.GetValue(f.Comp) is System.Collections.IList list && list.Count == 1);
                for (int i = 1; i < 6; i++) f.Phase(contact: false);
                Near(old ? .01f : .02875f, Progress(f.Scene.Target));
            });
    }

    /// <summary>只提供实际 RJW 类的程序集定位，不替换保存数据或序列化行为。</summary>
    private static bool RjwScribeTypeBoundary(string typeName, ref Type __result)
    {
        if (typeName != typeof(SexProps).FullName) return true;
        __result = typeof(SexProps); return false;
    }

    /// <summary>通过真实基类 ExposeData 后缀执行守卫，保持先基类/守卫、后日常或仪式派生字段的顺序。</summary>
    private static void GrowthGuardSaveBoundary(JobDriver_Sex __instance)
        => Hidden(guardType, null, "ExposeData", __instance);
}

/// <summary>原生 Scribe 外壳：实体属于外部世界，任务与新增仪式字段委托生产入口保存和恢复。</summary>
public sealed class GrowthSceneSave : IExposable
{
    private readonly JobDriver_Sex driver;
    private readonly CompSexSlaveTraining training;
    /// <summary>外部世界提供本次任务及可选仪式组件，不序列化夹具自己的影子状态。</summary>
    public GrowthSceneSave(JobDriver_Sex driver, CompSexSlaveTraining training) { this.driver = driver; this.training = training; }
    /// <summary>执行真实 RJW/SSC 任务序列化，并以生产 partial 的同一方法保存仪式接触字段。</summary>
    public void ExposeData()
    {
        driver.ExposeData();
        if (training != null)
        {
            AccessTools.Method(typeof(CompSexSlaveTraining), "ExposeUterusRitualEvidence").Invoke(training, null);
            AccessTools.Method(typeof(CompSexSlaveTraining), "ExposeStomachRitualEvidence").Invoke(training, null);
        }
    }
}
