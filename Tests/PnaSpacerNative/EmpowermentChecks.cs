// AI 辅助生成：直接执行生产共振查询；关系索引和翻译为宿主边界，不模拟真实锁链生命周期。
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
using SexSlaveCraft;

internal static partial class Program
{
    private static bool readingResonanceText;
    private static string[] resonanceTextArgs;
    private static int resonanceEnumerations;

    /// <summary>验证数值兼容与每个入口的一次枚举，不改生产筛选或刷新入口。</summary>
    private static void RunEmpowermentChecks()
    {
        var boundary = new Harmony("ssc.tests.resonance.translation");
        foreach (var method in typeof(Translator).GetMethods().Concat(typeof(TranslatorFormattedStringExtensions).GetMethods())
            .Where(m => m.Name == "Translate" && !m.ContainsGenericParameters && m.ReturnType == typeof(TaggedString)))
            boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(ResonanceTranslation)));
        try
        {
            Check("共振空主人及无缰绳维持零强度和空说明", () =>
            {
                foreach (var master in new Pawn[] { null, Pawn() })
                {
                    SameResonanceBits(0f, SSCMasterBondUtility.CalculateMasterEmpowermentSeverity(master));
                    Assert(ReadResonanceText(master) == "SSC_MasterEmpowerment_None");
                }
            });
            foreach (var sample in new[]
            {
                (Count: 0, Stage: -1, Min: 0f, Multiplier: 1f),
                (Count: 1, Stage: 0, Min: .01f, Multiplier: 1.1f),
                (Count: 2, Stage: 0, Min: .01f, Multiplier: 1.1f),
                (Count: 3, Stage: 1, Min: .25f, Multiplier: 1.3f),
                (Count: 4, Stage: 1, Min: .25f, Multiplier: 1.3f),
                (Count: 5, Stage: 2, Min: .50f, Multiplier: 1.5f),
                (Count: 7, Stage: 2, Min: .50f, Multiplier: 1.5f),
                (Count: 8, Stage: 3, Min: .75f, Multiplier: 1.8f)
            })
            {
                Check("共振人数档位与单次统计：" + sample.Count, () =>
                {
                    var master = ResonanceMaster(Enumerable.Repeat(0f, sample.Count).ToArray());
                    AssertResonance(master, sample.Count, 0f, sample.Min);
                    Equal(sample.Stage, SSCMasterBondUtility.GetEmpowermentStage(sample.Min));
                    resonanceEnumerations = 0;
                    SameResonanceBits(sample.Multiplier, SSCMasterBondUtility.GetTrainingCorruptionMultiplier(master));
                    Equal(1, resonanceEnumerations);
                });
            }
            Check("共振满恶堕保留最高强度及说明加成", () =>
            {
                var master = ResonanceMaster(Enumerable.Repeat(1f, 8).ToArray());
                AssertResonance(master, 8, 1f, 1f);
                Assert(resonanceTextArgs.Skip(4).SequenceEqual(new[] { .30f, .16f, .16f, .30f, .35f }.Select(v => v.ToStringPercent())));
            });
            Check("共振恶堕平均保留LINQ双精度累加与原平滑公式", () =>
            {
                // 连续累加 .6f 会暴露 float 累加替代 double 的舍入差异。
                float[] values = Enumerable.Repeat(.6f, 30).ToArray();
                var master = ResonanceMaster(values);
                AssertResonance(master, values.Length, values.Average(), LegacyResonance(values));
            });
            Check("共振筛选保留缺失需求零值及重复项而排除失效对象", () =>
            {
                var master = ResonanceMaster(.2f, .8f);
                var bridle = ShareBridles[master];
                var first = bridle.slaves[0];
                Set(first.health, "healthState", PawnHealthState.Down);
                var noNeed = Pawn();
                var dead = ResonancePawn(1f); Set(dead.health, "healthState", PawnHealthState.Dead);
                var destroyed = ResonancePawn(1f); Set(destroyed, "mapIndexOrState", (sbyte)-2);
                var wrongOwner = ResonancePawn(1f);
                bridle.slaves.AddRange(new[] { first, noNeed, dead, destroyed, wrongOwner, master, null });
                bridle.boundOverride = p => p != wrongOwner;
                float[] accepted = { .2f, .8f, .2f, 0f };
                AssertResonance(master, 4, accepted.Average(), LegacyResonance(accepted));
            });
            Check("共振独立查询立即读取恶堕及归属变化而无长期缓存", () =>
            {
                var master = ResonanceMaster(0f);
                AssertResonance(master, 1, 0f, .01f);
                var bridle = ShareBridles[master];
                bridle.slaves[0].needs.TryGetNeed<Need_Corruption>().CurLevel = 1f;
                AssertResonance(master, 1, 1f, .24f);
                bridle.boundOverride = _ => false;
                AssertResonance(master, 0, 0f, 0f);
            });
        }
        finally
        {
            readingResonanceText = false;
            ShareBridles.Clear();
            boundary.UnpatchAll(boundary.Id);
        }
    }

    private static Pawn ResonanceMaster(params float[] levels)
    {
        ShareBridles.Clear();
        var master = Pawn();
        var bridle = new Hediff_BridleOfSexSlave { pawn = master, slaves = levels.Select(ResonancePawn).ToList() };
        bridle.targetsOverride = CountResonanceTargets(bridle);
        ShareBridles.Add(master, bridle);
        return master;
    }

    private static Pawn ResonancePawn(float level)
    {
        var pawn = Pawn();
        pawn.needs = new Pawn_NeedsTracker(pawn);
        var need = (Need_Corruption)RuntimeHelpers.GetUninitializedObject(typeof(Need_Corruption));
        Set(need, "pawn", pawn);
        need.def = new NeedDef { defName = "ResonanceCorruption", needClass = typeof(Need_Corruption) };
        need.CurLevel = level;
        pawn.needs.AllNeeds.Add(need);
        return pawn;
    }

    private static IEnumerable<Pawn> CountResonanceTargets(Hediff_BridleOfSexSlave bridle)
    {
        resonanceEnumerations++;
        foreach (var pawn in bridle.slaves) yield return pawn;
    }

    private static void AssertResonance(Pawn master, int count, float average, float severity)
    {
        resonanceEnumerations = 0;
        SameResonanceBits(severity, SSCMasterBondUtility.CalculateMasterEmpowermentSeverity(master));
        Equal(1, resonanceEnumerations);
        resonanceEnumerations = 0;
        string key = ReadResonanceText(master);
        Equal(1, resonanceEnumerations);
        if (count == 0) { Assert(key == "SSC_MasterEmpowerment_None"); return; }
        Assert(key == "SSC_MasterEmpowerment_Summary");
        Assert(resonanceTextArgs.Take(4).SequenceEqual(new[]
        {
            count.ToString(), (SSCMasterBondUtility.GetEmpowermentStage(severity) + 1).ToString(),
            average.ToStringPercent(), severity.ToStringPercent()
        }));
    }

    // 保留重构前的 LINQ 平均、平滑和插值作为小范围差分基线；档位阈值另由明确样例核对。
    private static float LegacyResonance(float[] values)
    {
        int stage = SSCMasterBondUtility.GetEmpowermentStageFromSlaveCount(values.Length);
        float average = Mathf.Clamp01(values.Average());
        float smooth = average * average * (3f - 2f * average);
        return Mathf.Lerp(new[] { .01f, .25f, .50f, .75f }[stage],
            new[] { .24f, .49f, .74f, 1f }[stage], Mathf.Pow(smooth, 1.35f));
    }

    private static void SameResonanceBits(float expected, float actual)
    {
        if (BitConverter.SingleToInt32Bits(expected) != BitConverter.SingleToInt32Bits(actual))
            throw new Exception($"Resonance differs: expected {expected:R}, actual {actual:R}");
    }

    private static string ReadResonanceText(Pawn master)
    {
        readingResonanceText = true;
        resonanceTextArgs = Array.Empty<string>();
        try { return SSCMasterBondUtility.GetEmpowermentSummary(master); }
        finally { readingResonanceText = false; }
    }

    // 只截取传入原翻译入口的参数，不固定任何语言正文或替代说明生成逻辑。
    private static bool ResonanceTranslation(string __0, object[] __args, ref TaggedString __result)
    {
        if (!readingResonanceText) return true;
        resonanceTextArgs = __args.Skip(1)
            .SelectMany(a => a is NamedArgument[] array ? array.Cast<object>() : new[] { a })
            .Select(a => a is NamedArgument named ? named.arg?.ToString() : a?.ToString()).ToArray();
        __result = __0;
        return false;
    }
}
