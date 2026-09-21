using System;
using System.Collections.Generic;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed;
    private static int failed;

    /// <summary>执行人格特化快照、整体替换和旧数据兼容的独立生产逻辑回归。</summary>
    private static int Main()
    {
        Run("公交历史随奶牛人格迁移到新身体", HistoryTransfersToFreshBody);
        Run("身体当前公交进度不会污染源人格公交历史", CurrentHostHistoryIsReplaced);
        Run("身体非当前方向历史不会残留", InactiveHostHistoryIsReplaced);
        Run("同一身体重植保留所有源人格方向", SameBodyRetainsAllDirections);
        Run("导出覆盖当前方向陈旧缓存", ExportUsesCurrentProgress);
        Run("恢复以当前字段覆盖快照同方向缓存", RestoreUsesCurrentProgress);
        Run("导出快照与源组件互不共享字典", ExportIsIndependent);
        Run("恢复组件与输入快照互不共享字典", RestoreIsIndependent);
        Run("旧凝胶仅恢复当前方向并清除身体历史", LegacySnapshotRestoresKnownDirectionOnly);
        Run("无当前方向仍保留源人格已知历史", NoneRetainsKnownHistory);
        Run("无方向的旧凝胶不继承任何身体历史", LegacyNoneClearsHost);
        Run("零进度覆盖身体已训练的同方向", ZeroProgressReplacesHost);
        Run("非法方向回退无方向并保留合法历史", InvalidTypeFallsBackToNone);
        Run("未知键及非规范方向键不会导入", UnknownHistoryKeysAreIgnored);
        Run("非有限进度归零", NonFiniteProgressIsReset);
        Run("越界有限进度限制在有效区间", FiniteProgressIsClamped);
        Run("导出损坏的当前进度不会污染快照", ExportNormalizesCurrentProgress);
        Run("恢复方向不再改写旧例外，仅同步繁殖模式", RestoreKeepsLegacyRestrictions);
        Run("无当前方向仍请求清理基础健康状态", NoneRestoreRequestsCleanup);
        Run("多次人格覆盖不会累积先前历史", RepeatedRestoreDoesNotAccumulate);
        Run("恢复进度不迁移或清空身体奶量", RestoreKeepsBodyResource);
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>逐项记录通过或失败，并继续检查其余独立场景。</summary>
    private static void Run(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("通过：" + name);
        }
        catch (Exception error)
        {
            failed++;
            Console.WriteLine("失败：" + name + "\n" + error);
        }
    }

    /// <summary>失败时报告违反的迁移约束。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>比较浮点进度并拒绝非有限结果。</summary>
    private static void Equal(float expected, float actual, string message)
    {
        Assert(!float.IsNaN(actual) && Math.Abs(expected - actual) < 0.00001f,
            $"{message}：期望 {expected}，实际 {actual}");
    }

    /// <summary>建立公交训练一半后转奶牛并训练两成的源人格。</summary>
    private static CompSexSlaveTraining Source()
    {
        var comp = new CompSexSlaveTraining();
        comp.SetSpecialization(SexSlaveSpecializationType.Bus);
        comp.specializationProgress = 0.5f;
        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
        comp.specializationProgress = 0.2f;
        return comp;
    }

    /// <summary>通过生产导出与恢复接口把源组件的人格进度移入目标组件。</summary>
    private static void Transfer(CompSexSlaveTraining source, CompSexSlaveTraining target)
    {
        target.RestoreSpecializationProgress(source.specializationType, source.specializationProgress,
            source.ExportSpecializationProgress());
    }

    /// <summary>验证新身体恢复当前奶牛进度后，切回公交仍能获得源人格历史。</summary>
    private static void HistoryTransfersToFreshBody()
    {
        var target = new CompSexSlaveTraining();
        Transfer(Source(), target);
        Assert(target.specializationType == SexSlaveSpecializationType.Cow, "应恢复奶牛方向");
        Equal(0.2f, target.specializationProgress, "当前奶牛进度");
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, target.specializationProgress, "切回公交的历史进度");
    }

    /// <summary>验证恢复不会先把身体当前八成公交重新归档到源人格字典。</summary>
    private static void CurrentHostHistoryIsReplaced()
    {
        var target = new CompSexSlaveTraining();
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        target.specializationProgress = 0.8f;
        Transfer(Source(), target);
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, target.specializationProgress, "身体当前方向应被源人格历史替换");
    }

    /// <summary>验证源人格没有训练过的方向不会借用身体原先归档的记录。</summary>
    private static void InactiveHostHistoryIsReplaced()
    {
        var target = new CompSexSlaveTraining();
        target.SetSpecialization(SexSlaveSpecializationType.PetDog);
        target.specializationProgress = 0.9f;
        target.SetSpecialization(SexSlaveSpecializationType.Cow);
        Transfer(Source(), target);
        target.SetSpecialization(SexSlaveSpecializationType.PetDog);
        Equal(0f, target.specializationProgress, "源人格没有宠物狗历史");
    }

    /// <summary>验证植回原身体也依赖完整快照，并保留三个已训练方向。</summary>
    private static void SameBodyRetainsAllDirections()
    {
        var source = Source();
        source.SetSpecialization(SexSlaveSpecializationType.PetCat);
        source.specializationProgress = 0.3f;
        Transfer(source, source);
        Equal(0.3f, source.specializationProgress, "当前宠物猫进度");
        source.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, source.specializationProgress, "公交历史");
        source.SetSpecialization(SexSlaveSpecializationType.Cow);
        Equal(0.2f, source.specializationProgress, "奶牛历史");
    }

    /// <summary>验证训练后尚未切换的最新进度优先于字典中的旧缓存。</summary>
    private static void ExportUsesCurrentProgress()
    {
        var source = Source();
        source.SetSpecialization(SexSlaveSpecializationType.Bus);
        source.specializationProgress = 0.7f;
        Equal(0.7f, source.ExportSpecializationProgress()["Bus"], "导出采用最新当前进度");
    }

    /// <summary>验证恢复当前方向时独立字段优先于历史字典中陈旧的同方向数值。</summary>
    private static void RestoreUsesCurrentProgress()
    {
        var target = new CompSexSlaveTraining();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Bus, 0.6f,
            new Dictionary<string, float> { ["Bus"] = 0.1f });
        Equal(0.6f, target.specializationProgress, "当前字段优先");
        Equal(0.6f, target.ExportSpecializationProgress()["Bus"], "快照与当前字段一致");
    }

    /// <summary>验证修改已导出的历史或随后训练源身体都不会影响另一侧。</summary>
    private static void ExportIsIndependent()
    {
        var source = Source();
        Dictionary<string, float> snapshot = source.ExportSpecializationProgress();
        snapshot["Bus"] = 0.99f;
        source.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, source.specializationProgress, "改快照不影响源组件");
        source.specializationProgress = 0.8f;
        source.SetSpecialization(SexSlaveSpecializationType.Cow);
        Equal(0.99f, snapshot["Bus"], "更新组件不改旧快照");
    }

    /// <summary>验证导入操作复制字典，目标训练与输入快照后续修改互不影响。</summary>
    private static void RestoreIsIndependent()
    {
        var snapshot = new Dictionary<string, float> { ["Bus"] = 0.5f, ["Cow"] = 0.1f };
        var target = new CompSexSlaveTraining();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Cow, 0.2f, snapshot);
        Equal(0.1f, snapshot["Cow"], "恢复不改输入字典");
        snapshot["Bus"] = 0.99f;
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, target.specializationProgress, "改输入不影响目标历史");
        target.specializationProgress = 0.8f;
        target.SetSpecialization(SexSlaveSpecializationType.Cow);
        Equal(0.99f, snapshot["Bus"], "目标训练不改输入字典");
    }

    /// <summary>验证旧凝胶的空历史只恢复已知当前值，并主动排除身体旧进度。</summary>
    private static void LegacySnapshotRestoresKnownDirectionOnly()
    {
        var target = Source();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Cow, 0.4f, null);
        Equal(0.4f, target.specializationProgress, "旧凝胶当前进度");
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0f, target.specializationProgress, "旧凝胶不猜公交历史");
    }

    /// <summary>验证主动停用特化的源人格仍能迁移先前已保存的训练历史。</summary>
    private static void NoneRetainsKnownHistory()
    {
        var source = Source();
        source.SetSpecialization(SexSlaveSpecializationType.None);
        var target = new CompSexSlaveTraining();
        Transfer(source, target);
        Assert(target.specializationType == SexSlaveSpecializationType.None, "仍未选择方向");
        Equal(0f, target.specializationProgress, "无方向当前进度为零");
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.5f, target.specializationProgress, "停用后仍保留公交历史");
    }

    /// <summary>验证既无方向又无历史的旧凝胶彻底清除目标训练历史。</summary>
    private static void LegacyNoneClearsHost()
    {
        var target = Source();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.None, 0.8f, null);
        Assert(target.ExportSpecializationProgress().Count == 0, "不保留身体历史或 None 键");
        Equal(0f, target.specializationProgress, "无方向忽略无意义当前值");
    }

    /// <summary>验证零是有效的明确进度，不会被误判为缺失并回退身体历史。</summary>
    private static void ZeroProgressReplacesHost()
    {
        var target = Source();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Cow, 0f,
            new Dictionary<string, float> { ["Cow"] = 0.8f });
        Equal(0f, target.specializationProgress, "零进度必须覆盖旧值");
    }

    /// <summary>验证未知枚举不能成为当前方向，但独立合法的历史仍可恢复。</summary>
    private static void InvalidTypeFallsBackToNone()
    {
        var target = Source();
        target.RestoreSpecializationProgress((SexSlaveSpecializationType)999, 0.8f,
            new Dictionary<string, float> { ["Bus"] = 0.4f });
        Assert(target.specializationType == SexSlaveSpecializationType.None, "非法方向回退 None");
        Equal(0f, target.specializationProgress, "非法方向不能保留当前进度");
        target.SetSpecialization(SexSlaveSpecializationType.Bus);
        Equal(0.4f, target.specializationProgress, "合法历史仍可使用");
    }

    /// <summary>验证仅接受已定义方向的规范名称，排除未知键、数字别名与 None。</summary>
    private static void UnknownHistoryKeysAreIgnored()
    {
        var target = new CompSexSlaveTraining();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.None, 0f,
            new Dictionary<string, float>
            {
                ["Bus"] = 0.4f, ["None"] = 0.8f, ["bus"] = 0.9f, ["1"] = 0.9f,
                ["FutureDirection"] = 0.9f, [""] = 0.9f, [" Bus"] = 0.9f
            });
        Dictionary<string, float> snapshot = target.ExportSpecializationProgress();
        Assert(snapshot.Count == 1 && snapshot.ContainsKey("Bus"), "仅规范 Bus 键有效");
    }

    /// <summary>验证 NaN 和正负无穷不会随快照进入训练状态。</summary>
    private static void NonFiniteProgressIsReset()
    {
        var target = new CompSexSlaveTraining();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Bus, float.NaN,
            new Dictionary<string, float>
            {
                ["Cow"] = float.PositiveInfinity, ["PetCat"] = float.NegativeInfinity,
                ["PetDog"] = float.NaN
            });
        foreach (float progress in target.ExportSpecializationProgress().Values)
            Equal(0f, progress, "非有限值归零");
    }

    /// <summary>验证有限越界值不会产生负进度或超过完成状态的进度。</summary>
    private static void FiniteProgressIsClamped()
    {
        var target = new CompSexSlaveTraining();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.Bus, 5f,
            new Dictionary<string, float> { ["Cow"] = -0.2f, ["PetCat"] = 2f });
        Equal(1f, target.specializationProgress, "当前值上界");
        Equal(0f, target.ExportSpecializationProgress()["Cow"], "历史值下界");
        Equal(1f, target.ExportSpecializationProgress()["PetCat"], "历史值上界");
    }

    /// <summary>验证导出也清理运行期损坏数据，同时不擅自修改源角色状态。</summary>
    private static void ExportNormalizesCurrentProgress()
    {
        var source = Source();
        source.specializationProgress = float.PositiveInfinity;
        Equal(0f, source.ExportSpecializationProgress()["Cow"], "导出清理无穷值");
        Assert(float.IsPositiveInfinity(source.specializationProgress), "导出不修改源状态");
    }

    /// <summary>验证方向恢复不再修改旧例外输入，非兔方向仍按原规则重置繁殖模式。</summary>
    private static void RestoreKeepsLegacyRestrictions()
    {
        foreach (bool legacy in new[] { false, true })
        {
            var target = new CompSexSlaveTraining { rabbitReproductionMode = RabbitReproductionMode.Clone,
                allowOthersForTrainingOrSex = legacy };
            foreach (var type in new[] { SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow,
                SexSlaveSpecializationType.None })
            {
                target.RestoreSpecializationProgress(type, 0.5f, null);
                Assert(target.allowOthersForTrainingOrSex == legacy, "方向恢复不能写入旧例外");
                Assert(target.rabbitReproductionMode == RabbitReproductionMode.Offspring, "非兔方向重置繁殖模式");
            }
        }
    }

    /// <summary>验证恢复无方向的人格仍清理身体残留的基础状态，即使组件本身已无方向。</summary>
    private static void NoneRestoreRequestsCleanup()
    {
        var target = new CompSexSlaveTraining { parent = new Pawn() };
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.None, 0f, null);
        Assert(target.inactiveStateCleanupCalls == 1, "应请求一次基础状态清理");
        Assert(target.lastKeptType == SexSlaveSpecializationType.None, "不保留任何基础方向");
    }

    /// <summary>验证一个身体连续接收不同人格时，每次都只保留最新源人格的历史。</summary>
    private static void RepeatedRestoreDoesNotAccumulate()
    {
        var target = Source();
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.PetDog, 0.6f,
            new Dictionary<string, float> { ["PetCat"] = 0.4f });
        target.RestoreSpecializationProgress(SexSlaveSpecializationType.PetRabbit, 0.3f, null);
        Dictionary<string, float> snapshot = target.ExportSpecializationProgress();
        Assert(snapshot.Count == 1 && snapshot.ContainsKey("PetRabbit"), "只保留最后人格历史");
    }

    /// <summary>验证特化进度迁移的范围不包含目标身体储存的奶量资源。</summary>
    private static void RestoreKeepsBodyResource()
    {
        var target = new CompSexSlaveTraining { savedCowReservoirCharge = 0.7f };
        Transfer(Source(), target);
        Equal(0.7f, target.savedCowReservoirCharge, "身体奶量保持目标值");
        Assert(target.ExportSpecializationProgress().Count == 2, "导出只包含两个训练方向");
    }
}
