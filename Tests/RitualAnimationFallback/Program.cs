using System;
using System.Collections.Generic;
using System.Linq;
using rjw;
using SexSlaveCraft;
using Verse;
#if SSC_TEST_WITH_ANIMATIONS
using Rimworld_Animations;
#endif

internal static class Program
{
    private static int passed, failed, callbackTicks, callbackCount;

    /// <summary>执行链接生产代码的备用启动回归，返回失败退出码供自动检查使用。</summary>
    private static int Main()
    {
#if SSC_TEST_WITH_ANIMATIONS
        Run("从派生定义库筛选并启动，保留角色顺序、床锚点和时长", TypedCandidateStarts);
        Run("零优先级候选仍可备用启动，接收者不重复加入", ZeroPriorityStarts);
        Run("重复调用读取当前定义库，不保留旧候选", DefinitionsRefresh);
        Run("没有动画定义时不启动或改写时长", EmptyDatabase);
        Run("全部不匹配时保留警告，不强行启动", NoCompatibleAnimation);
        Run("接收任务缺失时安全返回", MissingReceiver);
#else
        Run("未加载动画框架类型时安全跳过", MissingFramework);
#endif
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>隔离测试数据并运行单项检查，汇总可定位的失败信息。</summary>
    private static void Run(string name, Action test)
    {
        DefDatabase<Def>.AllDefsListForReading.Clear();
        SSCLog.Warnings.Clear();
        SSCLog.Errors.Clear();
        callbackTicks = -1;
        callbackCount = 0;
#if SSC_TEST_WITH_ANIMATIONS
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Clear();
        AnimationUtility.Reset();
#endif
        try
        {
            test();
            Require(SSCLog.Errors.Count == 0, "不应发生异常：" + string.Join("; ", SSCLog.Errors));
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    /// <summary>条件不成立时终止当前测试。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>创建双方角色及接收任务，不模拟游戏调度或渲染。</summary>
    private static (Pawn initiator, Pawn receiver) CreateScene()
    {
        var initiator = new Pawn();
        var receiver = new Pawn();
        receiver.jobs.curDriver = new JobDriver_SexBaseReciever { parteners = new List<Pawn> { initiator } };
        return (initiator, receiver);
    }

    /// <summary>记录生产代码返回的动画时长和回调次数。</summary>
    private static void CaptureTicks(int ticks)
    {
        callbackTicks = ticks;
        callbackCount++;
    }

#if SSC_TEST_WITH_ANIMATIONS
    /// <summary>验证真实问题场景：基类库无动画，派生库中的有效候选能经过筛选并启动。</summary>
    private static void TypedCandidateStarts()
    {
        var scene = CreateScene();
        var bed = new Building();
        var order = new List<Pawn> { scene.receiver, scene.initiator };
        var rejected = new GroupAnimationDef { Compatible = false, Order = order };
        var invalidOrder = new GroupAnimationDef { Order = null };
        var valid = new GroupAnimationDef { defName = "CompatibleAnimation", Order = order };
        DefDatabase<Def>.AllDefsListForReading.Add(new Def { defName = "UnrelatedDef" });
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.AddRange(new[] { rejected, invalidOrder, valid });

        Require(RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, bed, CaptureTicks),
            "派生库中有匹配动画，备用启动仍然失败。");
        Require(rejected.Checks == 1 && invalidOrder.Checks == 1 && valid.Checks == 1, "应逐个执行框架匹配检查。");
        Require(AnimationUtility.Starts == 1 && AnimationUtility.Selected == valid, "不能启动不匹配或缺失角色顺序的动画。");
        Require(AnimationUtility.Participants.SequenceEqual(order) && AnimationUtility.Anchor == bed, "应保留框架角色顺序及床锚点。");
        Require(valid.SeenParticipants.SequenceEqual(new[] { scene.initiator, scene.receiver }), "接收者应加入参与者列表。");
        Require(callbackTicks == 120 && callbackCount == 1 && AnimationUtility.LengthPawn == scene.initiator,
            "应读取发起者的动画时长并回调一次。");
        Require(SSCLog.Warnings.Count == 0, "成功启动不应误报没有匹配动画。");
    }

    /// <summary>保留原有备用选择规则，验证零优先级及无床场景不会被本次修复改变。</summary>
    private static void ZeroPriorityStarts()
    {
        var scene = CreateScene();
        ((JobDriver_SexBaseReciever)scene.receiver.jobs.curDriver).parteners.Add(scene.receiver);
        var valid = new GroupAnimationDef { Priority = 0, Order = new List<Pawn> { scene.initiator, scene.receiver } };
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Add(valid);
        Require(RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks),
            "框架允许的零优先级候选仍应可用于备用播放。");
        Require(valid.SeenParticipants.Count == 2 && AnimationUtility.Anchor == scene.receiver,
            "接收者不应重复加入，无床时应以接收者为锚点。");
    }

    /// <summary>验证备用入口每次使用当前定义数据，避免把某次场景的候选缓存到后续调用。</summary>
    private static void DefinitionsRefresh()
    {
        var scene = CreateScene();
        var order = new List<Pawn> { scene.initiator, scene.receiver };
        var previous = new GroupAnimationDef { Order = order };
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Add(previous);
        Require(RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks), "首次启动失败。");
        var current = new GroupAnimationDef { Order = order };
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Clear();
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Add(current);
        Require(RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks), "更新定义后启动失败。");
        Require(AnimationUtility.Selected == current && previous.Checks == 1 && current.Checks == 1,
            "后续调用应读取当前定义并重新匹配。");
    }

    /// <summary>验证资源确实为空时仍返回失败，并保留原有诊断及调用方时长。</summary>
    private static void EmptyDatabase() => RequireNoAnimation(CreateScene());

    /// <summary>验证有资源但角色不匹配时不会绕过框架限制。</summary>
    private static void NoCompatibleAnimation()
    {
        var scene = CreateScene();
        var rejected = new GroupAnimationDef { Compatible = false, Order = new List<Pawn> { scene.initiator, scene.receiver } };
        DefDatabase<GroupAnimationDef>.AllDefsListForReading.Add(rejected);
        RequireNoAnimation(scene);
        Require(rejected.Checks == 1, "应查询现有动画后再报告没有匹配项。");
    }

    /// <summary>共同验证没有可用动画时的返回值、警告、启动次数和时长回调。</summary>
    private static void RequireNoAnimation((Pawn initiator, Pawn receiver) scene)
    {
        Require(!RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks), "无匹配项时应返回 false。");
        Require(AnimationUtility.Starts == 0 && callbackCount == 0 && callbackTicks == -1, "失败时不得启动或修改时长。");
        Require(SSCLog.Warnings.Count == 1 && SSCLog.Warnings[0].Contains("no matching animations"), "应保留真实缺失匹配项的警告。");
    }

    /// <summary>验证接收任务不存在时维持原有的安全退出。</summary>
    private static void MissingReceiver()
    {
        var scene = CreateScene();
        scene.receiver.jobs.curDriver = null;
        Require(!RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks), "没有接收任务时应失败。");
        Require(AnimationUtility.Starts == 0 && callbackCount == 0, "没有接收任务时不应启动或回调。");
    }
#else
    /// <summary>在完全不编译框架类型的宿主中验证可选依赖仍然可缺省。</summary>
    private static void MissingFramework()
    {
        var scene = CreateScene();
        Require(!RitualTrainingUtility.TryStartFallbackAnimation(scene.initiator, scene.receiver, null, CaptureTicks), "框架未安装时应跳过。");
        Require(callbackCount == 0 && SSCLog.Warnings.Count == 0, "可选框架缺省不应报警或改写时长。");
    }
#endif
}
