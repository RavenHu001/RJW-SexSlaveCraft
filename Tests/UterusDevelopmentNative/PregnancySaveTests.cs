// AI 辅助生成：真正妊娠实例经 HediffSet/Scribe 恢复，不复制组件的判定算法。
using System.Xml.Linq;
using HarmonyLib;
using rjw;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>覆盖四种妊娠及旧档缺字段；RJW 时间随原 ExposeData 保存，不在加载时改写。</summary>
    private static void PregnancySaveTests(Harmony harmony)
    {
        foreach (string name in pregnancyNames)
        foreach (bool boosted in new[] { false, true })
            Check("batch4 real Scribe preserves pregnancy decision and existing progress/times: " + name + "/" + boosted, () =>
            {
                var p = NewPawn(); Start(p, 1); if (boosted) Toggle(p, true); var h = NewPregnancy(p, name);
                h.Severity = .4f;
                if (h is Hediff_BasePregnancy rjw) { rjw.p_start_tick = 123; rjw.p_end_tick = 456789; rjw.babies = null; }
                var loaded = Load(Save(p)); var restored = loaded.health.hediffSet.hediffs.Single(x => x.def.defName == name);
                Require(restored != h); Near(.4f, restored.Severity);
                Equal(boosted, restored.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                if (restored is Hediff_BasePregnancy result)
                {
                    Near(123, result.p_start_tick); Near(456789, result.p_end_tick);
                    result.babies = new() { NewPawn() }; result.Initialize(loaded, null, default);
                    Near(boosted ? 960000 : 1200000, result.p_end_tick - result.p_start_tick);
                }
            });
        // 共用Comp在一种RJW妊娠覆盖三种异常；其余类型验证旧档接入，不重复完整异常矩阵。
        foreach (var (name, bad) in new[]
        {
            ("Pregnant", "missing"), ("PregnantHuman", "missing"),
            ("RJW_pregnancy", "missing"), ("RJW_pregnancy", "Undecided"),
            ("RJW_pregnancy", "999"), ("RJW_pregnancy_beast", "missing")
        })
            Check("batch4 old or invalid saved decision stays normal despite enabled mother: " + name + "/" + bad, () =>
            {
                var p = NewPawn(); Start(p, 1); Toggle(p, true); NewPregnancy(p, name);
                string path = Save(p);
                Edit(path, doc =>
                {
                    foreach (var node in doc.Descendants("sscUterusPregnancyDecision").ToList())
                        if (bad == "missing") node.Remove(); else node.Value = bad;
                });
                var loaded = Load(path); Require(SSCUterusDevelopmentUtility.Find(loaded).FertilityEnabled);
                var h = loaded.health.hediffSet.hediffs.Single(x => x.def.defName == name);
                Require(!h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                h.PostAdd(null); Require(!h.TryGetComp<HediffComp_SSCUterusPregnancy>().IsShortened);
                if (h is Hediff_BasePregnancy rjw)
                {
                    rjw.babies = new() { NewPawn() }; rjw.Initialize(loaded, null, default);
                    Near(1200000, rjw.p_end_tick - rjw.p_start_tick);
                }
            });
    }

    /// <summary>只定位 RJW 两个真实妊娠类型，避免扫描整程序集中的旧版 UI；Scribe 后续处理不替换。</summary>
    private static bool PregnancyTypeBoundary(string typeName, ref Type __result)
    {
        if (typeName == typeof(Hediff_HumanlikePregnancy).FullName) __result = typeof(Hediff_HumanlikePregnancy);
        else if (typeName == typeof(Hediff_BestialPregnancy).FullName) __result = typeof(Hediff_BestialPregnancy);
        else if (typeName == typeof(Hediff_BasePregnancy).FullName) __result = typeof(Hediff_BasePregnancy);
        else if (typeName == typeof(Hediff_MechanoidPregnancy).FullName) __result = typeof(Hediff_MechanoidPregnancy);
        else return true;
        return false;
    }
}
