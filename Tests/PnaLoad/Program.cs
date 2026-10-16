using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static int passed, failed;
    private static string root;
    internal static HediffDef ChainDef = new HediffDef { defName = "Hediff_ChainOfSexSlave" };
    private static readonly HediffDef AddictionDef = new HediffDef { defName = "PNA_Addiction", maxSeverity = 1f, initialSeverity=1f, HasRecoveryComp=true };

    private static int Main(string[] args)
    {
        root = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        var harmony = new Harmony("ssc.tests.pna-load");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Run("真实 Harmony 注册五个 PNA 目标及两个能力生死目标", () => Equal(7, harmony.GetPatchedMethods().Count(), "补丁目标数"));
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Run("拒绝异常剂量 " + invalid, () => { var pawn = NewPawn(); var r = Input(pawn, invalid); Assert(!r.Accepted && Record(pawn) == null, "拒绝且不创建"); });
        Run("拒绝空目标和非法来源", () => { Assert(!Input(null, .08f).Accepted, "空目标"); Assert(!SSCPnaExposureUtility.ApplyExposure(NewPawn(), .08f, (SSCPnaExposureSource)99).Accepted, "非法来源"); });
        foreach (string invalid in new[] { "dead", "destroyed", "discarded", "suspended", "nonflesh", "nohealth" })
            Run("拒绝无效目标 " + invalid, () => InvalidPawn(invalid));
        Run("保存加载阶段不能制造输入", () => { var pawn = NewPawn(); Scribe.mode = LoadSaveMode.PostLoadInit; Assert(!Input(pawn, .08f).Accepted, "读档输入拒绝"); });
        Run("新实例准确写 .16，不带 .01 隐藏初值", () => { var pawn = NewPawn(); var r = Input(pawn, .08f); Near(.16, Record(pawn).Severity); Near(.08, r.AddedLoad); });
        Run("普通输入不要求调教组件、身份或恶堕", () => { var pawn = NewPawn(); pawn.IsSlave = true; pawn.Corruption = .12f; Assert(Input(pawn, .08f).Accepted, "输入"); Near(.08, SSCPnaExposureUtility.GetLoad(pawn)); });
        Run("主人身份免疫共享 PNA 输入", () => { var pawn = NewPawn(); pawn.IsMaster = true; Assert(!Input(pawn, .08f).Accepted, "免疫拒绝"); Near(0, SSCPnaExposureUtility.GetLoad(pawn)); });
        Run("五份同 tick .20 满值且只有一个负荷实例", () => { var pawn = NewPawn(); for (int i=0;i<5;i++) Input(pawn,.20f); Near(1, SSCPnaExposureUtility.GetLoad(pawn)); Equal(1, pawn.health.hediffSet.hediffs.Count(h=>SSCPnaExposureUtility.IsBodyLoadDef(h.def))); });
        Run("满值真实暴露和零增加量分开", () => { var pawn=NewPawn(); Input(pawn,1); var r=Input(pawn,.08f); Assert(r.Accepted && r.IsFull, "真实输入仍满"); Near(.08,r.RequestedDose); Near(0,r.AddedLoad); });
        Run("P2 满值建立单份依赖并将首次需求设满", () => { var pawn=NewPawn(); Assert(Input(pawn,1).AddictionCreated,"创建结果"); Near(1,SSCPnaExposureUtility.GetAddiction(pawn).Severity); Near(1,pawn.needs.PnaNeed.Value); });
        Run("大有限正剂量钳制不溢出", () => { var pawn=NewPawn(); Assert(Input(pawn,float.MaxValue).IsFull,"满值"); Near(2,Record(pawn).Severity); });
        Run("满值断供一天按分段积分", () => Near(.7892521413, SSCPnaMath.AdvanceSeverity(2,1)/2));
        Run("等于末期门槛后继续下降", () => Assert(SSCPnaMath.AdvanceSeverity(1.94f,.01) < 1.94f,"持续下降"));
        Run("跨末期长步与分步一致", () => Near(SSCPnaMath.AdvanceSeverity(2,1), SSCPnaMath.AdvanceSeverity(SSCPnaMath.AdvanceSeverity(2,.05),.95)));
        Run("极低量结束、负时间不倒灌", () => { Near(0,SSCPnaMath.AdvanceSeverity(.16f,2)); Near(.16,SSCPnaMath.AdvanceSeverity(.16f,-1)); });
        Run("异常严重度规范化为零", () => { foreach(float s in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}) Near(0,SSCPnaMath.NormalizeSeverity(s)); Near(2,SSCPnaMath.NormalizeSeverity(3)); });
        foreach (int rate in new[] { 1, 15, 150, 250, 60000 })
            Run("原生有效 delta 分段 " + rate, () => NativeDay(rate));
        Run("零负 delta 不衰减，旧逐 tick 回调不双扣", () => { var p=NewPawn(); Input(p,1); Tick(p,0); Tick(p,-1); float adjustment=0; Comp(p).CompPostTick(ref adjustment); Near(1,SSCPnaExposureUtility.GetLoad(p)); });
        Run("输入前结算半批，下一批不重复清除", PendingBeforeInput);
        Run("暂停/冷冻不累积补算，已有半批保留", Suspension);
        Run("当前查询、悬停和百分比显示无副作用", ReadOnlyDisplay);
        Run("负荷清除后健康条目实际移除", () => { var p=NewPawn(); Input(p,.08f); Tick(p,120000); Assert(Record(p)==null,"移除"); });
        foreach (int daily in new[] { 1,2,3,4,6 }) Run("均匀每日输入 " + daily, () => RepeatedDoses(daily));
        Run("集群输入峰值可高于均匀输入", () => { var p=NewPawn(); for(int i=0;i<13;i++) { for(int j=0;j<3;j++) Input(p,.08f); if(SSCPnaExposureUtility.GetLoad(p)>=1) return; Tick(p,60000); } throw new Exception("集群应在该周期内满"); });
        Run("有效零阶段锁链转换同严重度且只清依赖", RootedZero);
        Run("失效残链不构成保护", () => { var p=NewPawn(); var master=NewPawn(); master.Discarded=true; p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,LinkedPawn=master,def=ChainDef}); Assert(!Input(p,.08f).Rooted,"普通分支"); Near(.08,SSCPnaExposureUtility.GetLoad(p)); });
        Run("重复链中有效链不被第一个残链遮蔽", () => { var p=NewPawn(); p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef}); p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,LinkedPawn=NewPawn(),def=ChainDef}); Assert(Input(p,.08f).Rooted,"有效第二份"); });
        Run("外部直接 AddHediff 不能绕过锁链保护", DirectRootedAddition);
        Run("已有残链更新到有效目标即清理", () => { var p=NewPawn(); Input(p,.70f); p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef}); Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn()); Assert(Record(p)==null,"更新链清理"); });
        Run("解绑转换回同严重度普通负荷", () => { var p=NewPawn(); Input(p,.70f); Hediff before=Record(p); var chain=Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn()); p.health.RemoveHediff(chain); Assert(Record(p)==before,"原实例恢复"); Near(.70,SSCPnaExposureUtility.GetLoad(p)); Near(.78,Input(p,.08f).FinalLoad); });
        Run("PNA 旧药物分支只对有效链关闭", DrugGuard);
        Run("严重度健康回调中建立链不会留下输入", MutationBinds);
        Run("健康回调移除旧载体，不误报满值", () => { var p=NewPawn(); Input(p,.70f); p.health.OnChanged=h=>p.health.RemoveHediff(h); Assert(!Input(p,.30f).Accepted,"失效拒绝"); Assert(Record(p)==null,"无残留"); });
        Run("新建回调移除实例，不误报成功", () => { var p=NewPawn(); p.health.OnAdded=h=>p.health.RemoveHediff(h); Assert(!Input(p,.08f).Accepted,"失效拒绝"); });
        Run("旧满无依赖静默保留", () => { var p=NewPawn(); Raw(p,2); SSCPnaGameComponent.NormalizePawn(p); Near(1,SSCPnaExposureUtility.GetLoad(p)); Assert(p.health.hediffSet.hediffs.Count==1,"不补依赖"); });
        Run("重复旧记录取最大单份不相加", () => { var p=NewPawn(); Raw(p,.80f); Raw(p,.90f); SSCPnaGameComponent.NormalizePawn(p); Near(.90,Record(p).Severity); Equal(1,p.health.hediffSet.hediffs.Count); });
        Run("旧档非法值清理和重复加载幂等", MigrationIdempotent);
        Run("普通旧依赖和实际满足度保持", () => { var p=NewPawn(); Raw(p,.8f); var addiction=AddOldAddiction(p,.3f); p.needs.PnaNeed=.27f; SSCPnaGameComponent.NormalizePawn(p); Near(.3,addiction.Severity); Near(.27,p.needs.PnaNeed.Value); });
        Run("全存档快照初始化覆盖离图和无组件身体", () => { var p=NewPawn(); Raw(p,3); SSCRestrictionGameComponent.Pawns.Add(p); new SSCPnaGameComponent(new Game()).FinalizeInit(); Near(2,Record(p).Severity); });
        Run("组件存读档保存半批而不扣历史时间", SaveRoundTrip);
        Run("旧组件缺保存字段从零累计", LegacyComp);
        Run("损坏累计字段清理", BadPending);
        Run("转换保留半批有效时间且只结算一次", ConversionPending);
        Run("反复绑定解绑不重置负荷或补回依赖", ConversionCycles);
        Run("双形态旧记录取最大单份不相加", DuplicateForms);
        Run("有链旧普通满值转换但无新成瘾", RootedMigration);
        Run("无链旧性奴状态转普通但不补成瘾", UnrootedMigration);
        Run("性奴状态存读档保留形态与半批时间", RootedSaveRoundTrip);
        Run("链引用失效在有效健康更新中转换回普通", InvalidatedChain);
        Run("重复有效链移除一个不会提前转普通", RemainingChain);
        Run("外部普通加量保留返回实例并接性奴载体", RootedIncoming);
        Run("外部性奴载体在无链目标上转普通", UnrootedIncoming);
        Run("性奴五档固定活化与阶段心情", RootedDefinitionContract);
        Run("调试四个加量动作均走共享入口", DebugActions);
        Run("真实 Def 五档、满足收益、能力与阶段心情", DefinitionContract);
        Run("原成瘾类、戒断减益与原版恢复 XML", AddictionContract);
        foreach(string language in new[]{"ChineseSimplified","ChineseTraditional","English","Russian"})
            Run("四语 PNA 标签与占位符 " + language, () => Localization(language));
        RunP2Tests();
        RunP3Tests();
        RunEffectsStep1Tests();
        RunEffectsStep3Tests();
        RunEffectsStep4Tests();
        RunEffectsStep5Tests();
        RunAbilityToggleLifecycleTests();
        RunInfestationTests();
        RunRefinementTests();
        Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");
        return failed==0 ? 0:1;
    }

    private static void Run(string name,Action test)
    {
        Scribe.mode=LoadSaveMode.Inactive; Scribe.Data.Clear(); SSCRestrictionGameComponent.Pawns.Clear(); Messages.Sent.Clear(); MoteMaker.Sent.Clear();
        SSCDefOf.SSC_PnaLoad=new HediffDef { defName=SSCPnaExposureUtility.LoadDefName, HasPnaComp=true, thresholds=new[]{0f,.30f,.80f,1.40f,1.94f} };
        SSCDefOf.SSC_PnaRootedLoad=new HediffDef { defName=SSCPnaExposureUtility.RootedLoadDefName, HasPnaComp=true, thresholds=new[]{0f,.30f,.80f,1.40f,1.94f} };
        SSCDefOf.SSC_PnaAddiction=AddictionDef;
        SSCDefOf.SSC_PnaNeed=new RimWorld.NeedDef{defName=SSCPnaExposureUtility.NeedDefName};
        RimWorld.AddictionUtility.ModifierCalls=0;
        try { test(); passed++; Console.WriteLine("PASS "+name); }
        catch(Exception error) { failed++; Console.WriteLine("FAIL "+name+": "+error.Message); }
    }
    private static Pawn NewPawn()=>new Pawn();
    private static SSCPnaExposureResult Input(Pawn p,float dose)=>SSCPnaExposureUtility.ApplyExposure(p,dose,SSCPnaExposureSource.Debug);
    private static Hediff Record(Pawn p)=>SSCPnaExposureUtility.GetRecord(p);
    private static Hediff RootedRecord(Pawn p)=>SSCPnaExposureUtility.GetRootedRecord(p);
    private static HediffComp_SSCPnaLoad BodyComp(Pawn p)=>(SSCPnaExposureUtility.GetBodyRecord(p) as HediffWithComps)?.GetComp<HediffComp_SSCPnaLoad>();
    private static HediffComp_SSCPnaLoad Comp(Pawn p)=>(Record(p) as HediffWithComps)?.GetComp<HediffComp_SSCPnaLoad>();
    private static Hediff Raw(Pawn p,float severity) { var h=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaLoad,p); h.RawSeverity=severity; p.health.hediffSet.hediffs.Add(h); return h; }
    private static Hediff AddOldAddiction(Pawn p,float severity) { var h=HediffMaker.MakeHediff(AddictionDef,p); h.Severity=severity; p.health.AddHediff(h); return h; }
    private static void Tick(Pawn p,int delta)
    {
        // 宿主模拟原版暂停身体不调度健康更新，而不是在恢复组件中另加限制。
        if(p.Suspended || delta<=0)return;
        p.NativeTicks+=delta;
        foreach(var h in p.health.hediffSet.hediffs.OfType<HediffWithComps>().ToArray())
        {
            float adjustment=0;
            foreach(var c in h.comps.ToArray()) c.CompPostTickInterval(ref adjustment,delta);
            if(!p.health.hediffSet.hediffs.Contains(h))continue;
            if(adjustment!=0)h.Severity+=adjustment;
            if(h.Severity<=0f || h.comps.Any(c=>c.CompShouldRemove))p.health.RemoveHediff(h);
        }
    }
    private static void InvalidPawn(string kind)
    {
        var p=NewPawn(); switch(kind) { case "dead":p.Dead=true;break; case "destroyed":p.Destroyed=true;break;case "discarded":p.Discarded=true;break;case "suspended":p.Suspended=true;break;case "nonflesh":p.RaceProps.IsFlesh=false;break;case "nohealth":p.health=null;break; }
        Assert(!Input(p,.08f).Accepted,"无效目标");
    }
    private static void NativeDay(int rate)
    {
        var p=NewPawn(); Input(p,1); for(int t=0;t<60000;t+=rate) Tick(p,Math.Min(rate,60000-t));
        Comp(p).SettlePending(); Near(.7892521413,SSCPnaExposureUtility.GetLoad(p),.000006);
    }
    private static void PendingBeforeInput()
    {
        var p=NewPawn(); Input(p,.70f); Tick(p,50);
        double expected=SSCPnaMath.AdvanceSeverity(1.4f,50d/60000)/2+.08;
        Near(expected,Input(p,.08f).FinalLoad); Equal(0,Comp(p).PendingTicks);
        Tick(p,150); Near(SSCPnaMath.AdvanceSeverity((float)(expected*2),150d/60000)/2,SSCPnaExposureUtility.GetLoad(p));
    }
    private static void Suspension()
    {
        var p=NewPawn(); Input(p,1); Tick(p,50); p.Suspended=true; Tick(p,600000);
        Near(1,SSCPnaExposureUtility.GetLoad(p)); Equal(50,Comp(p).PendingTicks);
        p.Suspended=false; Tick(p,100); Near(SSCPnaMath.AdvanceSeverity(2,150d/60000)/2,SSCPnaExposureUtility.GetLoad(p));
    }
    private static void ReadOnlyDisplay()
    {
        var p=NewPawn(); Input(p,.70f); Tick(p,50); var c=Comp(p); float before=Record(p).Severity;
        Equal("70%",Record(p).SeverityLabel); _=c.CompTipStringExtra; _=c.CompDebugString(); _=SSCPnaExposureUtility.GetLoad(p);
        Near(before,Record(p).Severity,0); Equal(50,c.PendingTicks);
    }
    private static void RepeatedDoses(int daily)
    {
        var p=NewPawn(); int first=0;
        for(int i=1;i<=600;i++) { var r=Input(p,.08f); if(r.IsFull){first=i;break;} Tick(p,60000/daily); }
        if(daily<=2) { Equal(0,first); Near(daily==1?.2113324453:.6106665556,SSCPnaExposureUtility.GetLoad(p),.000015); }
        else Equal(daily==3?39:daily==4?23:18,first);
    }
    private static void RootedZero()
    {
        var p=NewPawn(); Input(p,.70f); AddOldAddiction(p,.5f); p.needs.PnaNeed=.3f;
        var chain=Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn()); Near(0,chain.Severity);
        Assert(Record(p)==null && p.needs.PnaNeed==null,"普通负荷消失/清需求");
        Near(1.4,RootedRecord(p).Severity);
        Assert(!p.health.hediffSet.hediffs.Any(h=>SSCPnaExposureUtility.IsAddictionDef(h.def)),"清依赖");
        var r=Input(p,.70f); Assert(r.Accepted && r.Rooted && r.IsFull,"保护分流并累计身体负荷"); Assert(p.health.Notifications>=2,"实际健康通知");
    }
    private static void DirectRootedAddition()
    {
        var p=NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
        var load=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaLoad,p);load.Severity=1.4f;p.health.AddHediff(load);
        AddOldAddiction(p,.5f); Assert(Record(p)==null,"无普通负荷"); Near(1.4,RootedRecord(p).Severity); Assert(load==RootedRecord(p),"调用者仍持有真实载体"); Assert(!p.health.hediffSet.hediffs.Any(h=>SSCPnaExposureUtility.IsAddictionDef(h.def)),"拒绝依赖");
    }
    private static void DrugGuard()
    {
        var p=NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
        var drug=new RimWorld.CompDrug{Props=new RimWorld.CompProperties_Drug{chemical=new RimWorld.ChemicalDef{addictionHediff=AddictionDef}}};
        drug.PrePostIngested(p); Equal(0,drug.OriginalCalls);
        drug.PrePostIngested(NewPawn());Equal(1,drug.OriginalCalls);
        drug.Props.chemical.addictionHediff=new HediffDef{defName="OtherDrug"};drug.PrePostIngested(p);Equal(2,drug.OriginalCalls);
    }
    private static void MutationBinds()
    {
        var p=NewPawn();Input(p,.70f);p.health.OnChanged=h=>{p.health.OnChanged=null;Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());};
        var r=Input(p,.30f);Assert(r.Rooted && r.IsFull,"回调后重新分流，保留满值");Assert(Record(p)==null,"无普通残留");Near(2,RootedRecord(p).Severity);
    }
    private static void MigrationIdempotent()
    {
        var p=NewPawn();Raw(p,float.NaN);Raw(p,3);SSCPnaGameComponent.NormalizePawn(p);Near(2,Record(p).Severity);
        Hediff same=Record(p);SSCPnaGameComponent.NormalizePawn(p);Assert(same==Record(p),"保持实例");Equal(1,p.health.hediffSet.hediffs.Count);
        var bad=NewPawn();Raw(bad,float.PositiveInfinity);SSCPnaGameComponent.NormalizePawn(bad);Assert(Record(bad)==null,"清非法");
    }
    private static void SaveRoundTrip()
    {
        var p=NewPawn();Input(p,.70f);Tick(p,50);float severity=Record(p).Severity;
        Scribe.mode=LoadSaveMode.Saving;Comp(p).CompExposeData();var loaded=NewPawn();Raw(loaded,severity);
        Scribe.mode=LoadSaveMode.LoadingVars;Comp(loaded).CompExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;Comp(loaded).CompExposeData();
        Scribe.mode=LoadSaveMode.Inactive;SSCPnaGameComponent.NormalizePawn(loaded);Near(severity,Record(loaded).Severity,0);Equal(50,Comp(loaded).PendingTicks);
        Tick(loaded,100);Near(SSCPnaMath.AdvanceSeverity(severity,150d/60000),Record(loaded).Severity);
    }
    private static void LegacyComp()
    {
        var p=NewPawn();Raw(p,.7f);Scribe.Data["severityPerDay"]=-1f;Scribe.mode=LoadSaveMode.LoadingVars;Comp(p).CompExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;Comp(p).CompExposeData();Equal(0,Comp(p).PendingTicks);Near(.7,Record(p).Severity);
    }
    private static void BadPending()
    {
        foreach(int bad in new[]{-10,int.MaxValue}) { var p=NewPawn();Raw(p,.7f);Scribe.Data["sscPnaPendingTicks"]=bad;Scribe.mode=LoadSaveMode.LoadingVars;Comp(p).CompExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;Comp(p).CompExposeData();Assert(Comp(p).PendingTicks>=0 && Comp(p).PendingTicks<150,"范围"); }
    }
    private static void ConversionPending()
    {
        var p=NewPawn();Input(p,.70f);Tick(p,50);Hediff original=Record(p);
        var chain=Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());Assert(RootedRecord(p)==original,"原位转换");Near(1.4,original.Severity);Equal(50,BodyComp(p).PendingTicks);
        Equal("70%",original.SeverityLabel);_=BodyComp(p).CompTipStringExtra;Equal(50,BodyComp(p).PendingTicks);
        p.health.RemoveHediff(chain);Assert(Record(p)==original,"双向同实例");Equal(50,BodyComp(p).PendingTicks);
        Tick(p,100);Near(SSCPnaMath.AdvanceSeverity(1.4f,150d/60000),original.Severity);Equal(0,BodyComp(p).PendingTicks);
    }
    private static void ConversionCycles()
    {
        var p=NewPawn();Input(p,.70f);AddOldAddiction(p,.5f);Hediff original=Record(p);
        for(int i=0;i<5;i++){var chain=Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());Near(1.4,RootedRecord(p).Severity);p.health.RemoveHediff(chain);Assert(Record(p)==original,"实例不替换");Near(1.4,Record(p).Severity);}
        Assert(!p.health.hediffSet.hediffs.Any(h=>SSCPnaExposureUtility.IsAddictionDef(h.def)),"不补依赖");
    }
    private static void DuplicateForms()
    {
        var p=NewPawn();Raw(p,.80f);var rooted=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaRootedLoad,p);rooted.Severity=.90f;p.health.hediffSet.hediffs.Add(rooted);
        Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());Assert(RootedRecord(p)==rooted,"最大单份保留");Near(.90,rooted.Severity);Equal(1,p.health.hediffSet.hediffs.Count(h=>SSCPnaExposureUtility.IsBodyLoadDef(h.def)));
    }
    private static void RootedMigration()
    {
        var p=NewPawn();Raw(p,2);AddOldAddiction(p,.5f);p.needs.PnaNeed=.3f;
        p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef,LinkedPawn=NewPawn()});
        SSCPnaGameComponent.NormalizePawn(p);Near(2,RootedRecord(p).Severity);Assert(Record(p)==null&&p.needs.PnaNeed==null,"普通状态/依赖移除");
        Hediff same=RootedRecord(p);SSCPnaGameComponent.NormalizePawn(p);Assert(same==RootedRecord(p),"再次迁移幂等");
    }
    private static void UnrootedMigration()
    {
        var p=NewPawn();var h=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaRootedLoad,p);h.Severity=2;p.health.hediffSet.hediffs.Add(h);
        SSCPnaGameComponent.NormalizePawn(p);Assert(Record(p)==h&&RootedRecord(p)==null,"转换普通");Near(2,h.Severity);Assert(p.needs.PnaNeed==null,"旧满无新依赖");
    }
    private static void RootedSaveRoundTrip()
    {
        var p=NewPawn();Input(p,.70f);Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());Tick(p,50);
        Scribe.mode=LoadSaveMode.Saving;BodyComp(p).CompExposeData();var loaded=NewPawn();var h=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaRootedLoad,loaded);h.Severity=1.4f;loaded.health.hediffSet.hediffs.Add(h);
        loaded.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=loaded,def=ChainDef,LinkedPawn=NewPawn()});
        Scribe.mode=LoadSaveMode.LoadingVars;BodyComp(loaded).CompExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;BodyComp(loaded).CompExposeData();Scribe.mode=LoadSaveMode.Inactive;
        SSCPnaGameComponent.NormalizePawn(loaded);Assert(RootedRecord(loaded)==h,"读取性奴状态");Equal(50,BodyComp(loaded).PendingTicks);Tick(loaded,100);Near(SSCPnaMath.AdvanceSeverity(1.4f,150d/60000),h.Severity);
    }
    private static void InvalidatedChain()
    {
        var p=NewPawn();Input(p,.70f);var master=NewPawn();Hediff_ChainOfSexSlave.AddToPawn(p,master);master.Discarded=true;Tick(p,150);
        Assert(RootedRecord(p)==null&&Record(p)!=null,"恢复普通");Near(SSCPnaMath.AdvanceSeverity(1.4f,150d/60000),Record(p).Severity);
    }
    private static void RemainingChain()
    {
        var p=NewPawn();Input(p,.70f);var first=Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());var second=new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef,LinkedPawn=NewPawn()};p.health.hediffSet.hediffs.Add(second);
        p.health.RemoveHediff(first);Assert(RootedRecord(p)!=null,"另一条有效链仍在");p.health.RemoveHediff(second);Near(1.4,Record(p).Severity);
    }
    private static void RootedIncoming()
    {
        var p=NewPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());var h=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaLoad,p);h.Severity=.4f;p.health.AddHediff(h);
        Assert(RootedRecord(p)==h,"来件原位转形态");h.Severity=.8f;Near(.8,RootedRecord(p).Severity);
    }
    private static void UnrootedIncoming()
    {
        var p=NewPawn();var h=HediffMaker.MakeHediff(SSCDefOf.SSC_PnaRootedLoad,p);h.Severity=.4f;p.health.AddHediff(h);Assert(Record(p)==h,"无链普通形态");Near(.4,h.Severity);
    }
    private static void RootedDefinitionContract()
    {
        var def=LoadDef("Defs/HediffDefs/HediffOfBuff.xml",SSCPnaExposureUtility.RootedLoadDefName);Equal("HediffWithComps",def.Element("hediffClass").Value);Near(2,Number(def.Element("maxSeverity")));
        Equal("SexSlaveCraft.HediffCompProperties_SSCPnaLoad",(string)def.Element("comps").Elements().Single().Attribute("Class"));
        Equal("false",def.Element("isBad").Value);
        var stages=def.Element("stages").Elements().ToArray();Equal(5,stages.Length);
        double[] thresholds={0,.30,.80,1.40,1.94},libido={1.5,2,2.5,3.25,4},satisfaction={1.1,1.2,1.35,1.5,1.75};
        double[] consciousness={.02,.04,.06,.08,.10},movement={.03,.06,.10,.15,.20},pain={.95,.90,.80,.70,.60};
        for(int i=0;i<5;i++)
        {
            Near(thresholds[i],Number(stages[i].Element("minSeverity")));
            Near(libido[i],Number(stages[i].Element("statFactors").Element("SexFrequency")));
            Near(satisfaction[i],Number(stages[i].Element("statFactors").Element("SexSatisfaction")));
            Near(pain[i],Number(stages[i].Element("painFactor")));
            var mods=stages[i].Element("capMods").Elements().ToArray();Equal(3,mods.Length);
            foreach(string capacity in new[]{"Consciousness","Manipulation","Moving"})
                Near(capacity=="Consciousness"?consciousness[i]:movement[i],Number(mods.Single(m=>(string)m.Element("capacity")==capacity).Element("offset")));
            Assert(!stages[i].Descendants("setMax").Any(),"性奴无普通末期意识上限");
        }
        MoodDefinitionContract("SSC_PnaRootedLoad_Thought",SSCPnaExposureUtility.RootedLoadDefName,new[]{2,4,6,8,10});
    }
    private static void DebugActions()
    {
        var p=NewPawn();Find.Selector.SingleSelectedThing=p;
        foreach(string action in new[]{"AddOrdinary","AddTwenty","AddSeventy","Fill"}) typeof(DebugActions_SSCPna).GetMethod(action,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        Near(1,SSCPnaExposureUtility.GetLoad(p));Assert(SSCPnaExposureUtility.GetAddiction(p)!=null,"P2 调试满值建立依赖");Equal(4,Messages.Sent.Count);
        typeof(DebugActions_SSCPna).GetMethod("Clear",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);Assert(Record(p)==null,"清除");Assert(SSCPnaExposureUtility.GetAddiction(p)!=null,"清负荷保留依赖");
    }
    private static XElement LoadDef(string file,string name)=>XDocument.Load(SscTestPaths.Combine(root,file)).Root.Elements().Single(e=>(string)e.Element("defName")==name);
    private static double Number(XElement node)=>double.Parse(node.Value,CultureInfo.InvariantCulture);
    private static void DefinitionContract()
    {
        var def=LoadDef("Defs/HediffDefs/HediffOfBuff.xml",SSCPnaExposureUtility.LoadDefName);
        Equal("HediffWithComps",def.Element("hediffClass").Value);Near(2,Number(def.Element("maxSeverity")));
        Equal("SexSlaveCraft.HediffCompProperties_SSCPnaLoad",(string)def.Element("comps").Elements().Single().Attribute("Class"));
        var stages=def.Element("stages").Elements().ToArray();Equal(5,stages.Length);
        double[] thresholds={0,.30,.80,1.40,1.94},factors={1.5,2,2.5,3.25,4};
        for(int i=0;i<5;i++){Near(thresholds[i],Number(stages[i].Element("minSeverity")));Near(factors[i],Number(stages[i].Element("statFactors").Element("SexFrequency")));Near(new[]{1.1,1.2,1.35,1.5,1.75}[i],Number(stages[i].Element("statFactors").Element("SexSatisfaction")));}
        double[] movement={-.02,-.05,-.10,-.20,-.30},consciousness={-.01,-.03,-.06,-.12,-.20},pain={.90,.75,.50,.25,0};
        for(int i=0;i<5;i++)
        {
            var mods=stages[i].Element("capMods").Elements().ToArray();Equal(i<2?3:5,mods.Length,"各档保留能力影响");
            foreach(string capacity in new[]{"Manipulation","Moving","Consciousness"})
                Near(capacity=="Consciousness"?consciousness[i]:movement[i],Number(mods.Single(m=>(string)m.Element("capacity")==capacity).Element("offset")));
            if(i>=2)foreach(string capacity in new[]{"Sight","Hearing"})
                Near(new[]{-.05,-.15,-.30}[i-2],Number(mods.Single(m=>(string)m.Element("capacity")==capacity).Element("offset")));
            Near(pain[i],Number(stages[i].Element("painFactor")));
            if(i<4)Assert(!stages[i].Descendants("setMax").Any(),"末期前不封意识");
        }
        Near(.10,Number(stages[4].Element("capMods").Elements().Single(m=>(string)m.Element("capacity")=="Consciousness").Element("setMax")));
        MoodDefinitionContract("PNA_Debuff_Thought",SSCPnaExposureUtility.LoadDefName,new[]{1,2,4,6,8});
        foreach(float severity in new[]{.16f,.30f,.80f,1.40f,1.94f,2f}) { var p=NewPawn();Raw(p,severity);Equal(Array.FindLastIndex(SSCDefOf.SSC_PnaLoad.thresholds,t=>severity>=t),Record(p).CurStageIndex); }
    }
    private static void AddictionContract()
    {
        var def=LoadDef("Defs/HediffDefs/HediffOfAddiction.xml","PNA_Addiction");Equal("Hediff_Addiction",def.Element("hediffClass").Value);Equal(2,def.Element("stages").Elements().Count());
        Near(1,Number(def.Element("initialSeverity")));
        var comp=def.Element("comps").Element("li");Near(-.1,Number(comp.Element("severityPerDay")));Equal("HediffCompProperties_SeverityPerDay",(string)comp.Attribute("Class"));
        Assert(def.Element("stages").Elements().First().Element("label")==null,"普通档沿用原版无标签");
        var stages=def.Element("stages").Elements().ToArray();
        Near(1.25,Number(stages[0].Element("statFactors").Element("SexFrequency")));
        Assert(stages[0].Element("statOffsets")==null,"普通成瘾不额外增加脆弱度");
        Near(1.75,Number(stages[1].Element("statFactors").Element("SexFrequency")));
        Assert(stages[1].Element("statFactors").Element("WorkSpeedGlobal")==null,"戒断不单独修改全局工作速度");
        var manipulation=stages[1].Element("capMods").Elements().Single();
        Equal("Manipulation",manipulation.Element("capacity").Value);Near(.85,Number(manipulation.Element("postFactor")));
        Assert(manipulation.Element("offset")==null && manipulation.Element("setMax")==null,"操控只使用乘算");
        Near(.30,Number(stages[1].Element("statOffsets").Element("Vulnerability")));
        Near(-18,Number(LoadDef("Defs/HediffDefs/HediffOfAddiction.xml","PNA_Withdrawal_Thought").Element("stages").Elements().Last().Element("baseMoodEffect")));
    }
    // 只核对原版情境思绪的 XML 关联和完整五档，不在宿主另写思绪计算逻辑。
    private static void MoodDefinitionContract(string name,string hediffName,int[] moods)
    {
        var thought=LoadDef("Defs/HediffDefs/HediffOfBuff.xml",name);
        Equal("Thought_Situational",thought.Element("thoughtClass").Value);
        Equal("ThoughtWorker_Hediff",thought.Element("workerClass").Value);Equal(hediffName,thought.Element("hediff").Value);
        var stages=thought.Element("stages").Elements().ToArray();Equal(5,stages.Length);
        for(int i=0;i<5;i++){Near(moods[i],Number(stages[i].Element("baseMoodEffect")));Assert((string)stages[i].Element("visible")!="false","阶段心情可见");}
    }
    private static void Localization(string language)
    {
        string lang=SscTestPaths.Combine(root,"Languages",language);var buff=XDocument.Load(Path.Combine(lang,"DefInjected/HediffDef/HediffOfBuff.xml")).Root;
        for(int i=0;i<5;i++) Assert(!string.IsNullOrWhiteSpace(buff.Element($"Hediff_PNA_Debuff.stages.{i}.label")?.Value),"阶段标签 "+i);
        for(int i=0;i<5;i++) Assert(!string.IsNullOrWhiteSpace(buff.Element($"SSC_Hediff_PnaRootedLoad.stages.{i}.label")?.Value),"活化标签 "+i);
        var thoughts=XDocument.Load(Path.Combine(lang,"DefInjected/ThoughtDef/HediffOfBuff.xml")).Root;
        foreach(string name in new[]{"PNA_Debuff_Thought","SSC_PnaRootedLoad_Thought"})
            for(int i=0;i<5;i++)foreach(string field in new[]{"label","description"})
                Assert(!string.IsNullOrWhiteSpace(thoughts.Element($"{name}.stages.{i}.{field}")?.Value),"阶段思绪翻译");
        var addiction=XDocument.Load(Path.Combine(lang,"DefInjected/HediffDef/HediffOfAddiction.xml")).Root;
        Assert(addiction.Element("PNA_Addiction.stages.0.label")==null,"普通档不重新注入标签");
        Assert(addiction.Element("PNA_Addiction.stages.1.label")!=null,"戒断标签");
        Assert(buff.Element("SSC_Hediff_PnaRootedLoad.label")!=null,"性奴载体标签");
        var keyed=XDocument.Load(Path.Combine(lang,"Keyed/SSC_Pna.xml")).Root;Equal(14,keyed.Elements().Count());Assert(keyed.Element("SSC_Pna_LoadTooltip").Value.Contains("{0}"),"负荷参数");Assert(keyed.Element("SSC_Pna_DebugResult").Value.Contains("{0}"),"调试参数");Assert(keyed.Element("SSC_Pna_RootedLoadTooltip").Value.Contains("{0}"),"性奴负荷参数");
        foreach(string key in new[]{"SSC_Pna_InfoLoadPerDose","SSC_Pna_InfoLoadPerDoseDesc","SSC_Pna_InfoAddictionThreshold","SSC_Pna_InfoAddictionThresholdDesc"})
            Assert(!string.IsNullOrWhiteSpace(keyed.Element(key)?.Value),"信息栏翻译");
        Assert(keyed.Element("SSC_Pna_InfoAddictionThresholdDesc").Value.Contains("{0}") && keyed.Element("SSC_Pna_InfoAddictionThresholdDesc").Value.Contains("{1}"),"末期规则占位符");
        Assert(keyed.Element("SSC_Pna_NewDependence").Value.Contains("{0}"),"依赖通知");
        foreach(string key in new[]{"SSC_Pna_WeaponHealed","SSC_Pna_WeaponActivated"})
            Assert(!string.IsNullOrWhiteSpace(keyed.Element(key)?.Value),"枪械反馈翻译");
        Assert(keyed.Element("SSC_Pna_RecoveryActive")==null && keyed.Element("SSC_Pna_RecoveryPaused")==null,"恢复显示由原版提供");
        foreach(string file in new[]{"DefInjected/HediffDef/HediffOfBuff.xml","DefInjected/HediffDef/HediffOfAddiction.xml","DefInjected/ThoughtDef/HediffOfBuff.xml","Keyed/SSC_Pna.xml"}) Equal(File.ReadAllText(Path.Combine(lang,file)),File.ReadAllText(SscTestPaths.Combine(root,"Sexslavecraft/Languages",language,file)),"语言镜像");
    }
    private static void Assert(bool value,string message) { if(!value)throw new Exception(message); }
    private static void Equal<T>(T expected,T actual,string message="值不同") { if(!Equals(expected,actual))throw new Exception($"{message}: {expected} != {actual}"); }
    private static void Near(double expected,double actual,double tolerance=.000002) { if(double.IsNaN(actual)||Math.Abs(expected-actual)>tolerance)throw new Exception($"{expected:R} != {actual:R} (tol {tolerance})"); }
}
