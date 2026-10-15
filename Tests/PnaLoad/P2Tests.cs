using System;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 取得原版组件的宿主边界替身，核对生产接入；游戏调度仍需实机验证。
    private static HediffComp_SeverityPerDay Recovery(Pawn pawn)
        => (SSCPnaExposureUtility.GetAddiction(pawn) as HediffWithComps)?.GetComp<HediffComp_SeverityPerDay>();

    /// <summary>按依赖、来源、恢复、旧档和武器执行范围组织 P2 回归。</summary>
    private static void RunP2Tests()
    {
        // 满值建立／复发及健康回调：检查旧进度、通知和失效结果，不依赖随机成瘾。
        Run("未满暴露不建立依赖或需求",()=>{var p=NewPawn();Input(p,.97f);Assert(SSCPnaExposureUtility.GetAddiction(p)==null && p.needs.PnaNeed==null,"未满");});
        Run("旧满无依赖由零实际增量正输入建立",()=>{var p=NewPawn();Raw(p,2);var r=Input(p,.08f);Assert(r.AddictionCreated,"有效输入建依赖");Near(0,r.AddedLoad);});
        Run("重复满值不重置进度且只通知一次",()=>{var p=NewPawn();p.NotifyPlayer=true;Input(p,1);SSCPnaExposureUtility.GetAddiction(p).Severity=.35f;Assert(!Input(p,.7f).AddictionCreated,"不重建");Near(.35,SSCPnaExposureUtility.GetAddiction(p).Severity);Equal(1,Messages.Sent.Count);});
        Run("满值依赖回调绑定清新依赖与需求",()=>{var p=NewPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsAddictionDef(h.def))Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());};var r=Input(p,1);Assert(r.Rooted && !r.AddictionCreated,"链分流");Assert(SSCPnaExposureUtility.GetAddiction(p)==null && p.needs.PnaNeed==null,"无普通依赖");});
        Run("依赖添加被回调移除不报告创建",()=>{var p=NewPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsAddictionDef(h.def))p.health.RemoveHediff(h);};Assert(!Input(p,1).AddictionCreated,"无虚假创建");Assert(p.needs.PnaNeed==null,"无需求");});
        Run("建依赖回调移走身体载体不误报成功",()=>{var p=NewPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsAddictionDef(h.def))p.health.RemoveHediff(Record(p));};var r=Input(p,1);Assert(!r.Accepted && !r.AddictionCreated,"失效结果");Assert(SSCPnaExposureUtility.GetAddiction(p)==null,"不残留新依赖");});
        Run("无健康载体摄入不调用原化学修正",()=>{var p=NewPawn();p.health=null;new IngestionOutcomeDoer_SSCPna{severity=.4f,toleranceChemical=new ChemicalDef()}.DoIngestionOutcome(p,new Thing(),1);Equal(0,AddictionUtility.ModifierCalls);});
        // 原版需求准入与来源补充：首次新需求和已有需求采用不同初始化规则。
        Run("新依赖不强制替换已有合法需求满足度",()=>{var p=NewPawn();p.needs.PnaNeed=.27f;Input(p,1);Near(.27,p.needs.PnaNeed.Value);});
        Run("没有合法需求仍可建依赖",()=>{var p=NewPawn();p.needs.AllowPnaNeed=false;Input(p,1);Assert(p.needs.PnaNeed==null,"不强制需求");Assert(Recovery(p)!=null,"依赖存在");});
        Run("原依赖不因低负荷补给反向增长",()=>{var p=NewPawn();AddOldAddiction(p,.35f);p.needs.PnaNeed=0;SSCPnaExposureUtility.ApplyExposure(p,.08f,SSCPnaExposureSource.OrdinaryExposure);Near(.35,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.12,p.needs.PnaNeed.Value);});
        Run("枪械需求按上限后的实际增量",()=>{var p=NewPawn();Raw(p,1.98f);AddOldAddiction(p,.4f);p.needs.PnaNeed=.1f;var r=SSCPnaExposureUtility.ApplyExposure(p,.075f,SSCPnaExposureSource.Weapon);Near(.01,r.AddedLoad);Near(.12,p.needs.PnaNeed.Value);});
        Run("已满枪械暴露不凭空补需求",()=>{var p=NewPawn();Input(p,1);p.needs.PnaNeed=.1f;SSCPnaExposureUtility.ApplyExposure(p,.075f,SSCPnaExposureSource.Weapon);Near(.1,p.needs.PnaNeed.Value);});
        // 摄入链路：运行生产 Doer 和真实 Harmony 前缀，核对份数、修正及来源隔离。
        Run("普通药物每份20%且不随机成瘾",()=>{var p=NewPawn();var doer=new IngestionOutcomeDoer_SSCPna{severity=.4f};var drug=Drug(doer);drug.PrePostIngested(p);doer.DoIngestionOutcome(p,drug.parent,1);Near(.20,SSCPnaExposureUtility.GetLoad(p));Equal(0,drug.OriginalCalls);Assert(SSCPnaExposureUtility.GetAddiction(p)==null,"未满不随机");});
        Run("浓缩实际两份达满只建一次依赖",()=>{var p=NewPawn();var d=new IngestionOutcomeDoer_SSCPna{severity=1.4f};d.DoIngestionOutcome(p,new Thing(),2);Near(1,SSCPnaExposureUtility.GetLoad(p));Near(1,p.needs.PnaNeed.Value);Equal(1,p.health.hediffSet.hediffs.Count(h=>SSCPnaExposureUtility.IsAddictionDef(h.def)));});
        Run("摄入三份普通不是单份或重复剂量",()=>{var p=NewPawn();new IngestionOutcomeDoer_SSCPna{severity=.4f}.DoIngestionOutcome(p,new Thing(),3);Near(.6,SSCPnaExposureUtility.GetLoad(p));});
        Run("原化学和体型修正只应用一次",()=>{var p=NewPawn();p.BodySize=2;p.ChemicalEffectFactor=.5f;new IngestionOutcomeDoer_SSCPna{severity=.4f,toleranceChemical=new ChemicalDef(),divideByBodySize=true}.DoIngestionOutcome(p,new Thing(),3);Near(.15,SSCPnaExposureUtility.GetLoad(p));Equal(1,AddictionUtility.ModifierCalls);});
        Run("零份与非法药物剂量不创建负荷",()=>{var p=NewPawn();foreach(float s in new[]{0f,-1f,float.NaN,float.PositiveInfinity})new IngestionOutcomeDoer_SSCPna{severity=s}.DoIngestionOutcome(p,new Thing(),2);new IngestionOutcomeDoer_SSCPna{severity=.4f}.DoIngestionOutcome(p,new Thing(),0);Assert(Record(p)==null,"拒绝");});
        Run("已适配普通PNA关闭旧成瘾并保留其他药物",()=>{var p=NewPawn();var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=.4f});d.PrePostIngested(p);Equal(0,d.OriginalCalls);d.Props.chemical.addictionHediff=new HediffDef{defName="Other"};d.PrePostIngested(p);Equal(1,d.OriginalCalls);});
        Run("第三方未适配PNA保留旧入口",()=>{var d=Drug(null);d.PrePostIngested(NewPawn());Equal(1,d.OriginalCalls);});
        Run("摄入为已有需求补满但不重置依赖",()=>{var p=NewPawn();AddOldAddiction(p,.3f);p.needs.PnaNeed=.1f;new IngestionOutcomeDoer_SSCPna{severity=.4f}.DoIngestionOutcome(p,new Thing(),1);Near(1,p.needs.PnaNeed.Value);Near(.3,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("已有依赖摄入直接走原版且需求不重复补充",()=>{var p=NewPawn();p.BodySize=2;AddOldAddiction(p,.3f);p.needs.PnaNeed=.1f;var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=.4f});d.PrePostIngested(p);d.parent.def.ingestible.outcomeDoers[0].DoIngestionOutcome(p,d.parent,3);Equal(1,d.OriginalCalls);Near(.4,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.6,p.needs.PnaNeed.Value);Near(.6,SSCPnaExposureUtility.GetLoad(p));});
        Run("原版服药已有成瘾增加受Def上限限制",()=>{var p=NewPawn();AddOldAddiction(p,.98f);var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=.4f});d.PrePostIngested(p);Near(1,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("原版服药读取物品已有成瘾增量而非自定义常量",()=>{var p=NewPawn();AddOldAddiction(p,.3f);var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=.4f});d.Props.existingAddictionSeverityOffset=.2f;d.PrePostIngested(p);Near(.5,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("高负荷满需求也会持续恢复",()=>{var p=NewPawn();Input(p,1);p.needs.PnaNeed=1;NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("锁链对象药物只输入性奴负荷",()=>{var p=NewPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=1.4f});d.PrePostIngested(p);d.parent.def.ingestible.outcomeDoers[0].DoIngestionOutcome(p,d.parent,2);Near(1,SSCPnaExposureUtility.GetLoad(p));Assert(Record(p)==null && p.needs.PnaNeed==null && SSCPnaExposureUtility.GetAddiction(p)==null,"受保护");});
        // 原版恢复不受身体负荷或戒断门槛限制，暂停仍由健康更新调度控制。
        foreach(float need in new[]{.01f,0f})Run("低负荷戒断按日恢复 "+need,()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=need;Raw(p,.4f);NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("未戒断也按原版恢复",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=.011f;NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("高负荷戒断也按原版恢复",()=>{var p=NewPawn();Raw(p,1.8f);AddOldAddiction(p,.4f);p.needs.PnaNeed=0;NativeRecoveryDay(p);Near(.3,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("缺合法需求低负荷可恢复",()=>{var p=NewPawn();p.needs.AllowPnaNeed=false;AddOldAddiction(p,1);NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("暂停与非正健康delta不恢复",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=0;p.Suspended=true;NativeRecoveryDay(p);p.Suspended=false;Tick(p,0);Tick(p,-1);Near(1,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("十个有效恢复日撤销成瘾与需求",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=0;for(int i=0;i<10;i++)NativeRecoveryDay(p);Tick(p,200);Assert(SSCPnaExposureUtility.GetAddiction(p)==null && p.needs.PnaNeed==null,"完全解除");Assert(Input(p,1).AddictionCreated,"再次达满复发");Near(1,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        foreach(int delta in new[]{1,15,150,200})Run("原版恢复调度步长 "+delta,()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=0;for(int t=0;t<60000;t+=delta)Tick(p,Math.Min(delta,60000-t));Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.002);});
        // 保存兼容与只读显示：旧速率不参与新恢复，旧进度去重但不相加或重置。
        Run("旧恢复速率字段不再驱动PNA",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=0;Scribe.Data["severityPerDay"]=-.066f;Scribe.mode=LoadSaveMode.LoadingVars;Recovery(p).CompExposeData();Scribe.mode=LoadSaveMode.Inactive;SSCPnaGameComponent.NormalizePawn(p);NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("原版恢复悬停只读且不受满足度限制",()=>{var p=NewPawn();AddOldAddiction(p,.4f);p.needs.PnaNeed=0;Assert(Recovery(p).CompTipStringExtra=="DaysToRecover","原版天数显示");p.needs.PnaNeed=1;Assert(Recovery(p).CompTipStringExtra=="DaysToRecover","满满足度仍显示原版倒计时");Near(.4,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("重复旧依赖取最大单份且旧需求不灌满",()=>{var p=NewPawn();var a=HediffMaker.MakeHediff(AddictionDef,p);a.Severity=.3f;var b=HediffMaker.MakeHediff(AddictionDef,p);b.Severity=.4f;p.health.hediffSet.hediffs.Add(a);p.health.hediffSet.hediffs.Add(b);p.needs.PnaNeed=.27f;SSCPnaGameComponent.NormalizePawn(p);Near(.4,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.27,p.needs.PnaNeed.Value);Equal(1,p.health.hediffSet.hediffs.Count);});
        Run("非法旧依赖和需求值清理",()=>{var p=NewPawn();var a=HediffMaker.MakeHediff(AddictionDef,p);a.RawSeverity=float.NaN;p.health.hediffSet.hediffs.Add(a);p.needs.PnaNeed=float.NaN;SSCPnaGameComponent.NormalizePawn(p);Assert(SSCPnaExposureUtility.GetAddiction(p)==null && p.needs.PnaNeed==null,"清理非法");});
        Run("依赖体状态识别覆盖两种负荷且不扩大范围",()=>{foreach(string name in new[]{"Hediff_PNA_Debuff","SSC_Hediff_PnaRootedLoad","PNA_Addiction"})Assert(SSCPnaExposureUtility.IsBodyStateDef(new HediffDef{defName=name}),"身体记录");Assert(!SSCPnaExposureUtility.IsBodyStateDef(new HediffDef{defName="Other"}),"其他不受影响");});
        // 武器执行范围：伤害传播、同 tick 独立命中和重入分别验证一次输入语义。
        Run("低阶枪基数0.15一次输入7.5%",()=>{var p=NewPawn();var w=new DamageWorker_LowTier();w.Apply(Shot(),p);Near(.075,SSCPnaExposureUtility.GetLoad(p));Equal(1,w.InjuryCalls);});
        Run("同次枪伤传播三部位只输入一次且保留伤害",()=>{var p=NewPawn();var w=new DamageWorker_LowTier();var shot=Shot();shot.Parts=3;w.Apply(shot,p);Near(.075,SSCPnaExposureUtility.GetLoad(p));Equal(3,w.InjuryCalls);});
        Run("同tick两次独立命中均输入",()=>{var p=NewPawn();var w=new DamageWorker_LowTier();w.Apply(Shot(),p);w.Apply(Shot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));});
        Run("完整护甲偏转不输入PNA",()=>{var p=NewPawn();var s=Shot();s.Deflected=true;new DamageWorker_LowTier().Apply(s,p);Assert(Record(p)==null,"无暴露");});
        // 原武器分支兼容：抗性、有效链治疗／强化和高阶参数仍保持原职责。
        Run("高意识快照抗性与低意识保底保留",()=>{var high=NewPawn();high.health.capacities.Consciousness=3;var s=Shot();new DamageWorker_LowTier().Apply(s,high);Near(.025,SSCPnaExposureUtility.GetLoad(high));var low=NewPawn();low.health.capacities.Consciousness=.1f;new DamageWorker_LowTier().Apply(s,low);Near(.375,SSCPnaExposureUtility.GetLoad(low));});
        Run("无效残链不触发枪械治疗保护",()=>{var p=NewPawn();p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef});var w=new DamageWorker_LowTier();w.Apply(Shot(),p);Near(.075,SSCPnaExposureUtility.GetLoad(p));Equal(1,w.InjuryCalls);});
        Run("有效链枪械保留治疗并输入性奴负荷且不成瘾",()=>{var p=NewPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());var injury=new Hediff_Injury{pawn=p,def=new HediffDef{defName="Injury"}};p.health.hediffSet.hediffs.Add(injury);var w=new DamageWorker_LowTier();w.Apply(Shot(),p);Near(5,injury.Healed);Near(.075,SSCPnaExposureUtility.GetLoad(p));Assert(RootedRecord(p)!=null,"性奴载体");Assert(Record(p)==null && SSCPnaExposureUtility.GetAddiction(p)==null,"无普通影响");Equal(0,w.InjuryCalls);});
        Run("高阶现有固定剂量也接共享工具",()=>{var p=NewPawn();var s=Shot();((PNA_DWExtension)s.Def.Extension).fixedSeverity=.5f;new DamageWorker_HighTier().Apply(s,p);Near(.25,SSCPnaExposureUtility.GetLoad(p));});
        Run("武器重入不吞掉外层和内层独立暴露",()=>{var outer=NewPawn();var inner=NewPawn();var w=new DamageWorker_LowTier();w.BeforePart=p=>{w.BeforePart=null;w.Apply(Shot(),inner);};w.Apply(Shot(),outer);Near(.075,SSCPnaExposureUtility.GetLoad(outer));Near(.075,SSCPnaExposureUtility.GetLoad(inner));});
        Run("截图约6%满足度不再阻止原版恢复",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=.0567000248f;NativeRecoveryDay(p);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);p.needs.PnaNeed=0;NativeRecoveryDay(p);Near(.8,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("其他成瘾组件保持原版速率",()=>{var p=NewPawn();var h=new HediffWithComps{pawn=p,def=new HediffDef{defName="OtherAddiction"}};var c=new HediffComp_SeverityPerDay{parent=h,severityPerDay=-.0333f};Near(-.0333,c.SeverityChangePerDay());});
        Run("PNA大步长也沿用原版200tick调度",()=>{var p=NewPawn();AddOldAddiction(p,1);p.needs.PnaNeed=0;for(int t=0;t<60000;t+=250)Tick(p,250);Near(.92,SSCPnaExposureUtility.GetAddiction(p).Severity,.00002);});
        Run("真实摄入与枪械来源参数XML",SourceDefinitions);
        Run("浓缩液非末期只增加原版成瘾0.2",()=>{var p=NewPawn();AddOldAddiction(p,.4f);var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=1.4f});d.Props.existingAddictionSeverityOffset=.2f;d.PrePostIngested(p);d.parent.def.ingestible.outcomeDoers[0].DoIngestionOutcome(p,d.parent,1);Near(.6,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.7,SSCPnaExposureUtility.GetLoad(p));});
        Run("浓缩液进入末期成瘾总增加0.4",()=>{var p=NewPawn();Raw(p,.6f);AddOldAddiction(p,.4f);var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=1.4f});d.Props.existingAddictionSeverityOffset=.2f;d.PrePostIngested(p);d.parent.def.ingestible.outcomeDoers[0].DoIngestionOutcome(p,d.parent,1);Near(.8,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(1,SSCPnaExposureUtility.GetLoad(p));Equal(1,d.OriginalCalls);});
        Run("末期服药原版增量和强化各执行一次",()=>{var p=NewPawn();Raw(p,1.6f);AddOldAddiction(p,.4f);p.needs.PnaNeed=0;var d=Drug(new IngestionOutcomeDoer_SSCPna{severity=.4f});d.PrePostIngested(p);d.parent.def.ingestible.outcomeDoers[0].DoIngestionOutcome(p,d.parent,1);Near(.7,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(1,SSCPnaExposureUtility.GetLoad(p));Equal(1,d.OriginalCalls);});
        foreach(var source in new[]{SSCPnaExposureSource.Weapon,SSCPnaExposureSource.OrdinaryExposure})Run("真实来源末期输入强化 "+source,()=>{var p=NewPawn();Raw(p,1.8f);AddOldAddiction(p,.4f);SSCPnaExposureUtility.ApplyExposure(p,.08f,source);Near(.6,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("末期阈值边界和重复正输入各强化一次",()=>{var p=NewPawn();var record=Raw(p,1.939f);AddOldAddiction(p,.3f);SSCPnaExposureUtility.ApplyExposure(p,.0001f,SSCPnaExposureSource.Weapon);Near(.3,SSCPnaExposureUtility.GetAddiction(p).Severity);record.Severity=1.94f;SSCPnaExposureUtility.ApplyExposure(p,float.Epsilon,SSCPnaExposureSource.Weapon);Near(.5,SSCPnaExposureUtility.GetAddiction(p).Severity);SSCPnaExposureUtility.ApplyExposure(p,.005f,SSCPnaExposureSource.Weapon);Near(.7,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("已满真实输入强化但不突破成瘾上限",()=>{var p=NewPawn();Raw(p,2);AddOldAddiction(p,.7f);var r=SSCPnaExposureUtility.ApplyExposure(p,.08f,SSCPnaExposureSource.OrdinaryExposure);Near(0,r.AddedLoad);Near(.9,SSCPnaExposureUtility.GetAddiction(p).Severity);SSCPnaExposureUtility.ApplyExposure(p,.08f,SSCPnaExposureSource.OrdinaryExposure);Near(1,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("调试末期输入不额外加深已有成瘾",()=>{var p=NewPawn();Raw(p,2);AddOldAddiction(p,.4f);Input(p,.2f);Near(.4,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        Run("首次创建成瘾不因同次末期再强化",()=>{var p=NewPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsAddictionDef(h.def))h.Severity=.35f;};var r=SSCPnaExposureUtility.ApplyExposure(p,1,SSCPnaExposureSource.Ingestion);Assert(r.AddictionCreated,"首次创建");Near(.35,SSCPnaExposureUtility.GetAddiction(p).Severity);});
        RunRefreshOptimizationTests();
        MeasureSteadyRefreshAllocations();
    }

    // 验证刷新次数、原始清理顺序和同步回调，避免优化改变健康状态更新语义。
    private static void RunRefreshOptimizationTests()
    {
        Run("强制协调清单份依赖只刷新需求一次",()=>
        {
            var p=NewPawn();AddOldAddiction(p,.4f);p.needs.PnaNeed=.3f;
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef,LinkedPawn=NewPawn()});
            int before=p.needs.Refreshes;
            SSCPnaExposureUtility.RefreshBodyState(p,true);
            Equal(1,p.needs.Refreshes-before);
            Assert(SSCPnaExposureUtility.GetAddiction(p)==null && p.needs.PnaNeed==null,"依赖与需求清除");
        });
        Run("重复依赖每次移除仍即时刷新但外层不重复",()=>
        {
            var p=NewPawn();AddOldAddiction(p,.3f);p.needs.PnaNeed=.3f;
            var second=HediffMaker.MakeHediff(AddictionDef,p);second.Severity=.6f;p.health.hediffSet.hediffs.Add(second);
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef,LinkedPawn=NewPawn()});
            int before=p.needs.Refreshes,callbacks=0;
            p.health.OnRemoved=h=>{if(SSCPnaExposureUtility.IsAddictionDef(h.def)){callbacks++;Assert(p.needs.PnaNeed!=null,"移除回调前的需求时序保留");}};
            SSCPnaExposureUtility.RefreshBodyState(p,true);
            Equal(2,callbacks);Equal(2,p.needs.Refreshes-before);
            Assert(p.needs.PnaNeed==null,"最后一份移除后撤销需求");
        });
        Run("独立移除依赖仍由原补丁即时撤销需求",()=>
        {
            var p=NewPawn();var addiction=AddOldAddiction(p,.4f);p.needs.PnaNeed=.3f;int before=p.needs.Refreshes;
            p.health.RemoveHediff(addiction);
            Equal(1,p.needs.Refreshes-before);Assert(p.needs.PnaNeed==null,"没有延迟刷新");
        });
        Run("无依赖的有链强制协调仍清孤立需求",()=>
        {
            var p=NewPawn();p.needs.PnaNeed=.2f;
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef,LinkedPawn=NewPawn()});
            int before=p.needs.Refreshes;SSCPnaExposureUtility.RefreshBodyState(p,true);
            Equal(1,p.needs.Refreshes-before);Assert(p.needs.PnaNeed==null,"强制准入刷新保留");
        });
        Run("重复负荷清理保留原顺序及回调修改的严重度",()=>
        {
            var p=NewPawn();var a=Raw(p,.4f);var b=Raw(p,.2f);var keep=Raw(p,1.2f);
            var removed=new System.Collections.Generic.List<Hediff>();
            p.health.OnRemoved=h=>{removed.Add(h);if(h==a)keep.Severity=1.4f;};
            SSCPnaExposureUtility.RefreshBodyState(p);
            Equal(2,removed.Count);Assert(removed[0]==a && removed[1]==b,"原始移除顺序");
            Assert(Record(p)==keep,"原实例保留");Near(1.4,keep.Severity);
        });
        Run("同最大严重度保留首实例与组件时间",()=>
        {
            var p=NewPawn();var first=Raw(p,.8f);Tick(p,50);Raw(p,.8f);
            var comp=Comp(p);SSCPnaExposureUtility.RefreshBodyState(p);
            Assert(Record(p)==first && Comp(p)==comp,"稳定选择首实例");Equal(50,comp.PendingTicks);
        });
    }

    // 仅比较宿主中同一生产方法的临时分配，不将结果当作游戏帧率或Unity实测。
    private static void MeasureSteadyRefreshAllocations()
    {
        var p=NewPawn();Raw(p,.8f);
        for(int i=0;i<100;i++)SSCPnaExposureUtility.RefreshBodyState(p);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<1000;i++)SSCPnaExposureUtility.RefreshBodyState(p);
        Console.WriteLine("诊断：单份稳态刷新1000次分配 "+(GC.GetAllocatedBytesForCurrentThread()-before)+" 字节（含宿主边界）");
    }

    // 运行原版组件的正常健康更新节奏，不用一次超大 delta 代替一天的 tick。
    private static void NativeRecoveryDay(Pawn pawn)
    {
        for(int tick=0;tick<60000;tick+=150)Tick(pawn,150);
    }

    // 构造已适配或未适配 PNA 物品，仅隔离原版组件边界；补丁运行生产实现。
    private static CompDrug Drug(IngestionOutcomeDoer_SSCPna doer)
    {
        var result=new CompDrug{Props=new CompProperties_Drug{chemical=new ChemicalDef{addictionHediff=AddictionDef}}};
        result.parent.Comp=result;
        if(doer!=null)result.parent.def.ingestible.outcomeDoers.Add(doer);
        return result;
    }
    // 使用同一组确认后的低阶参数，让多部位／重入场景共享输入基准。
    private static DamageInfo Shot()=>new DamageInfo
    {
        Amount=1,Parts=1,Def=new DamageDef{Extension=new PNA_DWExtension
        {baseSeverity=.15f,trueDamageAmount=1,healAmount=5,recordHediff=new HediffDef{defName="Resistance",maxSeverity=100},combatBuff=new HediffDef{defName="Hediff_PNA_Combat_Buff_Plus"},adapterHediff=ChainDef}}
    };
    // 读取实际作者 XML，确认测试执行的剂量与运行资源一致且不存在并行旧 Doer。
    private static void SourceDefinitions()
    {
        foreach(var item in new[]{("Purple_Aphrodisiac_Nanofluid",.4),("Purple_Aphrodisiac_Nanofluid_Plus",1.4)})
        {
            var def=LoadDef("Defs/ThingsDefs/Nanofluid.xml",item.Item1);
            var doer=def.Element("ingestible").Element("outcomeDoers").Elements().Single();
            Equal("SexSlaveCraft.IngestionOutcomeDoer_SSCPna",(string)doer.Attribute("Class"));Near(item.Item2,Number(doer.Element("severity")));
            Assert(doer.Element("hediffDef")==null,"无旧Doer剂量");
            var offset=def.Element("comps").Element("li").Element("existingAddictionSeverityOffset");
            Near(item.Item1.EndsWith("_Plus")?.2:.1,offset==null?.1:Number(offset));
        }
        Near(.15,Number(LoadDef("Defs/DemageDefs/DemageDef_Gun.xml","Damage_PNA_Damage_LowTier").Element("modExtensions").Element("li").Element("baseSeverity")));
    }
}
