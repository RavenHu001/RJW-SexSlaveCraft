using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static Action inject;
    private static int passed;
    private static int failed;

    /// <summary>依次运行种族分页回归场景，汇总结果，并通过退出码标识是否全部通过。</summary>
    private static int Main()
    {
        Run("静态构造排队执行生产注入器", StartupQueuesInjection);
        Run("保留已解析列表、分页实例顺序及外部模组分页", PreserveResolvedTabs);
        Run("已有非共享 SSC 分页按类型防重", PreserveNonSharedTrainingTab);
        Run("重复注入不重复组件或分页且不重新解析", RepeatedInjectionIsStable);
        Run("HAR 式种族不触发解析覆盖方法", AlienRaceDoesNotResolveAgain);
        Run("未解析列表从原始分页恢复顺序并去重", RebuildMissingResolvedTabs);
        Run("缺少类型列表时保留已解析分页或创建 SSC 分页", MissingTypeLists);
        Run("保留类人、九莲白名单及排除规则", TargetSelectionIsUnchanged);
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>清空场景数据后执行指定测试，将注入警告视为失败，并继续记录后续场景结果。</summary>
    private static void Run(string name, Action test)
    {
        DefDatabase<ThingDef>.AllDefsListForReading.Clear();
        Log.Warnings.Clear();
        try
        {
            test();
            Assert(Log.Warnings.Count == 0, "注入不应产生警告：" + string.Join(" | ", Log.Warnings));
            passed++;
            Console.WriteLine("通过：" + name);
        }
        catch (Exception error)
        {
            failed++;
            Console.WriteLine("失败：" + name + "\n" + error);
        }
    }

    /// <summary>条件不满足时抛出带有原因的异常，交由场景执行器记录测试失败。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>创建具有指定名称的最小类人种族定义，供各场景设置组件和分页状态。</summary>
    private static ThingDef Human(string name = "Human")
    {
        return new ThingDef
        {
            defName = name,
            race = new RaceProperties { intelligence = Intelligence.Humanlike }
        };
    }

    /// <summary>将场景定义加入测试数据库，并调用从真实启动队列捕获的生产注入入口。</summary>
    private static void Inject(params ThingDef[] defs)
    {
        DefDatabase<ThingDef>.AllDefsListForReading.AddRange(defs);
        Assert(inject != null, "启动队列必须提供真实生产注入入口");
        inject();
    }

    /// <summary>检查目标种族恰有一份训练组件、分页类型和分页实例，且未再次解析 Def。</summary>
    private static void HasTraining(ThingDef def)
    {
        Assert(def.comps != null && def.comps.Count(c => c.compClass == typeof(CompSexSlaveTraining)) == 1,
            def.defName + " 应恰有一个 SSC 组件");
        Assert(def.inspectorTabs != null && def.inspectorTabs.Count(t => t == typeof(ITab_SexSlaveTraining)) == 1,
            def.defName + " 应恰有一个 SSC 分页类型");
        Assert(def.inspectorTabsResolved != null &&
            def.inspectorTabsResolved.Count(t => t != null && t.GetType() == typeof(ITab_SexSlaveTraining)) == 1,
            def.defName + " 应恰有一个 SSC 分页实例");
        Assert(def.ResolveCalls == 0, def.defName + " 不应再次解析 Def");
    }

    /// <summary>验证静态构造只排入一次任务、不立即注入，并保存可供后续场景调用的真实入口。</summary>
    private static void StartupQueuesInjection()
    {
        ThingDef def = Human();
        DefDatabase<ThingDef>.AllDefsListForReading.Add(def);
        RuntimeHelpers.RunClassConstructor(typeof(SexSlaveCraft_Injector).TypeHandle);
        Assert(LongEventHandler.QueuedActions.Count == 1, "静态构造应只排入一次注入任务");
        Assert(def.comps == null && def.inspectorTabsResolved == null, "排队前不应立即注入");
        inject = LongEventHandler.QueuedActions.Dequeue();
        inject();
        HasTraining(def);
    }

    /// <summary>验证追加 SSC 分页时，原列表、分页实例、顺序及仅由外部模组追加的分页均保留。</summary>
    private static void PreserveResolvedTabs()
    {
        ThingDef def = Human();
        var health = new HealthTab();
        var external = new ExternalOnlyTab();
        var social = new SocialTab();
        var resolved = new List<InspectTabBase> { health, external, social };
        var types = new List<Type> { typeof(HealthTab), typeof(SocialTab) };
        def.inspectorTabs = types;
        def.inspectorTabsResolved = resolved;
        Inject(def);
        HasTraining(def);
        Assert(ReferenceEquals(types, def.inspectorTabs), "必须保留原始分页类型列表实例");
        Assert(types.SequenceEqual(new[] { typeof(HealthTab), typeof(SocialTab), typeof(ITab_SexSlaveTraining) }),
            "类型列表应按原顺序追加 SSC");
        Assert(ReferenceEquals(resolved, def.inspectorTabsResolved), "必须保留已解析列表实例");
        Assert(resolved.Count == 4 && ReferenceEquals(resolved[0], health) &&
            ReferenceEquals(resolved[1], external) && ReferenceEquals(resolved[2], social),
            "原有分页实例及只在已解析列表存在的外部模组分页必须原位保留");
        Assert(ReferenceEquals(resolved[3], InspectTabManager.GetSharedInstance(typeof(ITab_SexSlaveTraining))),
            "新增 SSC 分页应使用共享实例");
    }

    /// <summary>验证已有非共享 SSC 分页也能按类型识别，避免追加第二个共享实例。</summary>
    private static void PreserveNonSharedTrainingTab()
    {
        ThingDef def = Human();
        var existingTraining = new ITab_SexSlaveTraining();
        var resolved = new List<InspectTabBase> { new HealthTab(), existingTraining, new SocialTab() };
        var before = resolved.ToArray();
        def.inspectorTabs = new List<Type> { typeof(HealthTab), typeof(ITab_SexSlaveTraining), typeof(SocialTab) };
        def.inspectorTabsResolved = resolved;
        Assert(!ReferenceEquals(existingTraining, InspectTabManager.GetSharedInstance(typeof(ITab_SexSlaveTraining))),
            "测试前提：已有 SSC 实例不是管理器共享实例");
        Inject(def);
        HasTraining(def);
        Assert(ReferenceEquals(resolved, def.inspectorTabsResolved) && resolved.SequenceEqual(before),
            "已有相同类型的非共享 SSC 分页应保留，不能额外追加共享实例");
    }

    /// <summary>验证重复执行注入不会增加组件或分页，也不会替换已有列表和实例。</summary>
    private static void RepeatedInjectionIsStable()
    {
        ThingDef def = Human();
        var otherComp = new CompProperties { compClass = typeof(OtherComp) };
        var originalTraining = new CompProperties_SexSlaveTraining();
        var comps = new List<CompProperties> { otherComp, originalTraining };
        def.comps = comps;
        def.inspectorTabs = new List<Type> { typeof(HealthTab) };
        def.inspectorTabsResolved = new List<InspectTabBase> { new HealthTab() };
        Inject(def);
        HasTraining(def);
        var types = def.inspectorTabs;
        var resolved = def.inspectorTabsResolved;
        var before = resolved.ToArray();
        inject();
        HasTraining(def);
        Assert(ReferenceEquals(comps, def.comps) && comps.Count == 2 &&
            ReferenceEquals(comps[0], otherComp) && ReferenceEquals(comps[1], originalTraining),
            "已有组件列表、外部组件及 SSC 组件实例都应保留");
        Assert(ReferenceEquals(types, def.inspectorTabs) && types.Count == 2 &&
            ReferenceEquals(resolved, def.inspectorTabsResolved) && resolved.SequenceEqual(before),
            "二次注入应保持分页列表和实例不变");
    }

    /// <summary>以再次解析就抛错的种族替身验证注入器不会触发 HAR 式初始化，后续种族也能正常处理。</summary>
    private static void AlienRaceDoesNotResolveAgain()
    {
        var alien = new RejectRepeatedResolveRace
        {
            defName = "AlienHumanlike",
            race = new RaceProperties { intelligence = Intelligence.Humanlike },
            inspectorTabs = new List<Type> { typeof(HealthTab) },
            inspectorTabsResolved = new List<InspectTabBase> { new HealthTab() }
        };
        ThingDef ordinary = Human("FollowingHuman");
        Inject(alien, ordinary);
        HasTraining(alien);
        HasTraining(ordinary);
        Assert(alien.ResolveCalls == 0, "不得进入 HAR 式 ResolveReferences 覆盖方法");
    }

    /// <summary>验证解析列表为 null 时，按原始类型首次出现的顺序恢复共享分页并去重。</summary>
    private static void RebuildMissingResolvedTabs()
    {
        ThingDef def = Human();
        var types = new List<Type>
        {
            typeof(HealthTab), typeof(SocialTab), typeof(HealthTab),
            typeof(ITab_SexSlaveTraining), typeof(SocialTab)
        };
        var originalTypes = types.ToArray();
        def.inspectorTabs = types;
        Inject(def);
        HasTraining(def);
        Assert(ReferenceEquals(types, def.inspectorTabs) && types.SequenceEqual(originalTypes),
            "恢复已解析分页不应改写已有类型列表");
        var expected = new[]
        {
            InspectTabManager.GetSharedInstance(typeof(HealthTab)),
            InspectTabManager.GetSharedInstance(typeof(SocialTab)),
            InspectTabManager.GetSharedInstance(typeof(ITab_SexSlaveTraining))
        };
        Assert(def.inspectorTabsResolved.SequenceEqual(expected),
            "缺少已解析列表时应按第一次出现的类型顺序恢复共享分页并去重");
    }

    /// <summary>验证缺少原始类型列表时仍能补充 SSC，并保留已有的外部模组分页。</summary>
    private static void MissingTypeLists()
    {
        ThingDef empty = Human("EmptyLists");
        ThingDef resolvedOnly = Human("OnlyResolved");
        var external = new ExternalOnlyTab();
        var resolved = new List<InspectTabBase> { external };
        resolvedOnly.inspectorTabsResolved = resolved;
        Inject(empty, resolvedOnly);
        HasTraining(empty);
        HasTraining(resolvedOnly);
        Assert(empty.inspectorTabs.Count == 1 && empty.inspectorTabsResolved.Count == 1,
            "两个分页列表均空时应只建立 SSC 分页");
        Assert(ReferenceEquals(resolved, resolvedOnly.inspectorTabsResolved) &&
            resolved.Count == 2 && ReferenceEquals(resolved[0], external),
            "缺少原始类型列表不应清空已解析的外部分页");
    }

    /// <summary>验证类人及两个九莲白名单仍被处理，其他种族和缺少必要字段的定义不被修改。</summary>
    private static void TargetSelectionIsUnchanged()
    {
        ThingDef human = Human();
        ThingDef fox = Human("Ninetailfox");
        ThingDef foxVariant = Human("Ninetailfoxwt");
        fox.race.intelligence = Intelligence.Animal;
        foxVariant.race.intelligence = Intelligence.ToolUser;
        ThingDef animal = Human("Animal");
        animal.race.intelligence = Intelligence.Animal;
        ThingDef toolUser = Human("ToolUser");
        toolUser.race.intelligence = Intelligence.ToolUser;
        ThingDef noRace = Human("Ninetailfox");
        noRace.race = null;
        ThingDef unnamed = Human("");
        ThingDef nullName = Human(null);
        Inject(human, fox, foxVariant, animal, toolUser, noRace, unnamed, nullName);
        foreach (ThingDef target in new[] { human, fox, foxVariant }) HasTraining(target);
        foreach (ThingDef excluded in new[] { animal, toolUser, noRace, unnamed, nullName })
        {
            Assert(excluded.comps == null && excluded.inspectorTabs == null &&
                excluded.inspectorTabsResolved == null && excluded.ResolveCalls == 0,
                "非目标 Def 必须保持未修改：" + excluded.defName);
        }
    }
}

public sealed class HealthTab : InspectTabBase { }
public sealed class SocialTab : InspectTabBase { }
public sealed class ExternalOnlyTab : InspectTabBase { }
public sealed class OtherComp { }

internal sealed class RejectRepeatedResolveRace : ThingDef
{
    /// <summary>记录意外的种族重新解析并立即抛错，使测试能发现注入器越过生命周期边界。</summary>
    public override void ResolveReferences()
    {
        ResolveCalls++;
        throw new InvalidOperationException("HAR 式种族初始化不可被注入器重复执行");
    }
}
