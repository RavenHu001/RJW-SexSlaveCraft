using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 覆盖当前身体清单的全部类别；测试构造不同 Def 实例，确保不依赖 DefOf 实例相等。
    private static readonly string[] PnaBodyNames = {
        "Hediff_PNA_Debuff", "SSC_Hediff_PnaRootedLoad", "PNA_Addiction",
        "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus", "Hediff_PNA_Consciousness_Record",
        "SSC_Purple_HyperLactation", "SSC_Hediff_RabbitCloneLowPNA"
    };

    /// <summary>运行真实克隆入口及 PNA 协调，游戏复制器仅提供已复制的输入身体。</summary>
    private static void RunP3Tests()
    {
        Run("P3 身体清单明确覆盖八类且不吞专精",()=>
        {
            foreach(string name in PnaBodyNames) Assert(SSCPnaExposureUtility.IsBodyStateDef(new HediffDef{defName=name}),name);
            foreach(string name in new[]{"SSC_Hediff_Combatant_Final","SSC_TestPnaSpecialization","Other"})
                Assert(!SSCPnaExposureUtility.IsBodyStateDef(new HediffDef{defName=name}),"专精及其他状态保留");
            Assert(!SSCPnaExposureUtility.IsBodyStateDef(null),"空定义");
        });
        Run("P3 新生去除全部来源 PNA 且不修改来源",()=>
        {
            Pawn source=CloneSource(); AddAllBodyStates(source,.6f);source.needs.PnaNeed=.23f;
            var sourceRecords=source.health.hediffSet.hediffs.ToArray();
            var body=CopiedBody(source);var keep=SscDef("SSC_TestKeep");body.health.AddHediff(keep).Severity=.7f;
            Assert(RabbitCloneUtility.TryCreateRabbitClone(source,out Pawn clone,false),"创建成功");
            Assert(clone==body && clone.Spawned,"准备后生成");
            Assert(!clone.health.hediffSet.hediffs.Any(h=>SSCPnaExposureUtility.IsBodyStateDef(h.def)),"全部来源 PNA 移除");
            Assert(clone.needs.PnaNeed==null,"继承需求移除");
            Near(.7,clone.health.hediffSet.GetFirstHediffOfDef(keep).Severity);
            Assert(sourceRecords.SequenceEqual(source.health.hediffSet.hediffs),"来源实例未移除或替换");
            foreach(var record in sourceRecords.Where(h=>SSCPnaExposureUtility.IsBodyStateDef(h.def))) Near(.6,record.Severity);
            Near(.23,source.needs.PnaNeed.Value);
        });
        Run("P3 新生无成瘾也清理复制出的孤立需求",()=>
        {
            Pawn source=CloneSource();source.needs.PnaNeed=.19f;CopiedBody(source);
            Assert(RabbitCloneUtility.TryCreateRabbitClone(source,out Pawn clone,false),"创建成功");
            Assert(clone.needs.PnaNeed==null && SSCPnaExposureUtility.GetAddiction(clone)==null,"无孤立需求或凭空成瘾");
            Near(.19,source.needs.PnaNeed.Value);
        });
        Run("P3 新生仍合法的需求采用原版初值",()=>
        {
            Pawn source=CloneSource();source.needs.PnaNeed=.17f;var copied=CopiedBody(source);copied.needs.OtherPnaNeedSource=true;
            Assert(RabbitCloneUtility.TryCreateRabbitClone(source,out Pawn clone,false),"创建成功");
            var need=SSCPnaExposureUtility.GetNeed(clone);Assert(need!=null,"原版准入保留");
            Near(.8,need.CurLevel);Equal(1,need.Initializations);Near(.17,source.needs.PnaNeed.Value);
        });
        Run("P3 新生有效链不制造负荷或成瘾",()=>
        {
            Pawn source=CloneSource();AddAllBodyStates(source,.7f);source.needs.PnaNeed=.2f;
            var copied=CopiedBody(source);copied.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=copied,def=ChainDef,LinkedPawn=NewPawn()});
            Assert(RabbitCloneUtility.TryCreateRabbitClone(source,out Pawn clone,false),"创建成功");
            Assert(SSCPnaExposureUtility.IsRooted(clone) && SSCPnaExposureUtility.GetBodyRecord(clone)==null,"链合法但无来源负荷");
            Assert(SSCPnaExposureUtility.GetAddiction(clone)==null && clone.needs.PnaNeed==null,"无成瘾需求");
        });
        Run("P3 成熟保留自身负荷恢复满足度及半批时间",()=>
        {
            Pawn source=CloneSource();AddAllBodyStates(source,.9f);source.needs.PnaNeed=.94f;
            Pawn clone=NewPawn();Input(clone,.4f);AddOldAddiction(clone,.35f);clone.needs.PnaNeed=.27f;Tick(clone,50);
            foreach(string name in PnaBodyNames.Skip(3)) AddBodyState(clone,name,.18f);
            var own=clone.health.hediffSet.hediffs.ToArray();var need=SSCPnaExposureUtility.GetNeed(clone);
            var marker=SscDef("SSC_MaturityMarker");source.health.AddHediff(marker).Severity=.76f;
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            foreach(var record in own)
                Assert(clone.health.hediffSet.hediffs.Contains(record) != SSCPnaExposureUtility.IsLegacyCombatBuffDef(record.def),"保留合法自身实例，废弃旧强化");
            Near(.4,SSCPnaExposureUtility.GetLoad(clone));Near(.35,SSCPnaExposureUtility.GetAddiction(clone).Severity);
            Assert(SSCPnaExposureUtility.GetNeed(clone)==need,"保留自身需求实例");Near(.27,need.CurLevel);Equal(0,need.Initializations);
            Equal(50,BodyComp(clone).PendingTicks);
            foreach(var record in own.Where(h=>PnaBodyNames.Skip(3).Contains(h.def.defName))) Near(.18,record.Severity);
            Near(.76,clone.health.hediffSet.GetFirstHediffOfDef(marker).Severity);Near(.94,source.needs.PnaNeed.Value);
        });
        Run("P3 成熟不从来源补建 PNA 但自产出照常生成",()=>
        {
            Pawn source=CloneSource();AddAllBodyStates(source,.9f);source.needs.PnaNeed=.8f;Pawn clone=NewPawn();
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            var own=clone.health.hediffSet.hediffs.Where(h=>SSCPnaExposureUtility.IsBodyStateDef(h.def)).ToArray();
            Equal(1,own.Length);Assert(own[0].def==RabbitCloneUtility.RabbitCloneLowPnaDef,"仅成熟自行生成的产出");Near(.01,own[0].Severity);
            Assert(SSCPnaExposureUtility.GetAddiction(clone)==null && clone.needs.PnaNeed==null,"不继承来源成瘾满足度");
        });
        // 绕过链补丁修改回调结果，确保入口末尾的生产协调确实负责最终链核对。
        Run("P3 成熟复制回调建立有效链后立即保护自身 PNA",()=>
        {
            Pawn source=CloneSource();var marker=SscDef("SSC_ChainCallback");source.health.AddHediff(marker);
            Pawn clone=NewPawn();Input(clone,.4f);AddOldAddiction(clone,.5f);clone.needs.PnaNeed=.3f;var load=Record(clone);
            clone.health.OnAdded=h=>{if(h.def==marker)clone.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=clone,def=ChainDef,LinkedPawn=NewPawn()});};
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            Assert(SSCPnaExposureUtility.GetRootedRecord(clone)==load,"原实例转性奴负荷");Near(.4,SSCPnaExposureUtility.GetLoad(clone));
            Assert(SSCPnaExposureUtility.GetAddiction(clone)==null && clone.needs.PnaNeed==null,"最终链清成瘾需求");
        });
        Run("P3 成熟复制回调使链失效后转回普通负荷",()=>
        {
            Pawn source=CloneSource();var marker=SscDef("SSC_UnbindCallback");source.health.AddHediff(marker);
            Pawn clone=NewPawn();Input(clone,.4f);var chain=Hediff_ChainOfSexSlave.AddToPawn(clone,NewPawn());var load=SSCPnaExposureUtility.GetRootedRecord(clone);
            clone.health.OnAdded=h=>{if(h.def==marker)chain.LinkedPawn=null;};
            RabbitCloneUtility.MatureRabbitClone(clone,source,CloneLink(clone,source));
            Assert(Record(clone)==load,"保留原实例转回普通");Near(.4,SSCPnaExposureUtility.GetLoad(clone));
            Assert(SSCPnaExposureUtility.GetAddiction(clone)==null,"不补建成瘾");
        });
        Run("P3 成熟及重复读档不把自身旧满值当作新输入",()=>
        {
            Pawn source=CloneSource();Pawn clone=NewPawn();Raw(clone,2);var link=CloneLink(clone,source);
            RabbitCloneUtility.MatureRabbitClone(clone,source,link);var load=Record(clone);
            RabbitCloneUtility.MatureRabbitClone(clone,source,link);
            SSCPnaGameComponent.NormalizePawn(clone);SSCPnaGameComponent.NormalizePawn(clone);
            Assert(Record(clone)==load && link.mature,"同一身体与成熟标记");Near(1,SSCPnaExposureUtility.GetLoad(clone));
            Assert(SSCPnaExposureUtility.GetAddiction(clone)==null && clone.needs.PnaNeed==null,"无虚假输入");
            Equal(1,clone.health.hediffSet.hediffs.Count(h=>h.def==RabbitCloneUtility.RabbitCloneLowPnaDef));
        });
        Run("P3 组内需求同步不搬迁 PNA 满足度",()=>
        {
            Pawn source=CloneSource();Pawn clone=NewPawn();source.needs.PnaNeed=.2f;clone.needs.PnaNeed=.7f;
            source.needs.rest.CurLevel=.1f;clone.needs.rest.CurLevel=.9f;
            RabbitCloneUtility.SyncRabbitCloneNeeds(source,new List<Pawn>{clone});
            Near(.5,source.needs.rest.CurLevel);Near(.5,clone.needs.rest.CurLevel);
            Near(.2,source.needs.PnaNeed.Value);Near(.7,clone.needs.PnaNeed.Value);
        });
        Run("P3 空壳继续有效健康消退和原版成瘾恢复",()=>
        {
            Pawn shell=NewPawn();shell.health.AddHediff(SscDef("Hediff_SSC_PersonalityExcreted_Done"));Input(shell,1);
            NativeRecoveryDay(shell);Near(.9,SSCPnaExposureUtility.GetAddiction(shell).Severity,.00002);
            Near(.7892521413,SSCPnaExposureUtility.GetLoad(shell),.00002);
        });
    }

    /// <summary>准备 SSC 来源及定义库；复制器返回值由各用例提供，不开放游戏内克隆入口。</summary>
    private static Pawn CloneSource()
    {
        DefDatabase<HediffDef>.AllDefs=new List<HediffDef>{SscDef("SSC_Hediff_RabbitCloneLink"),SscDef("SSC_Hediff_RabbitCloneLowPNA")};
        Pawn source=NewPawn();source.Map=new Map();source.Spawned=true;source.Comp=new CompSexSlaveTraining();
        var rabbit=(HediffWithComps)source.health.AddHediff(PetSpecializationUtility.RabbitDef);
        rabbit.comps.Add(new HediffComp_RabbitCloneSource{parent=rabbit});return source;
    }
    private static HediffDef SscDef(string name)=>new HediffDef{defName=name,modContentPack=new ModContentPack{PackageId="rjw.sexslavecraft.TieJinVer"}};
    private static void AddBodyState(Pawn pawn,string name,float severity)
    {
        HediffDef def=name==SSCPnaExposureUtility.LoadDefName ? SSCDefOf.SSC_PnaLoad :
            name==SSCPnaExposureUtility.RootedLoadDefName ? SSCDefOf.SSC_PnaRootedLoad :
            name==SSCPnaExposureUtility.AddictionDefName ? SSCDefOf.SSC_PnaAddiction :
            name=="SSC_Hediff_RabbitCloneLowPNA" ? RabbitCloneUtility.RabbitCloneLowPnaDef : SscDef(name);
        def.modContentPack=new ModContentPack{PackageId="rjw.sexslavecraft.TieJinVer"};
        var h=HediffMaker.MakeHediff(def,pawn);h.Severity=severity;pawn.health.hediffSet.hediffs.Add(h);
    }
    private static void AddAllBodyStates(Pawn pawn,float severity) { foreach(string name in PnaBodyNames)AddBodyState(pawn,name,severity); }
    private static Pawn CopiedBody(Pawn source)
    {
        // 原版 CopyHediffs 使用直接添加，CopyNeeds 复制当前值；这里仅构造这两个边界的结果。
        Pawn body=NewPawn();body.Comp=new CompSexSlaveTraining();
        foreach(var h in source.health.hediffSet.hediffs.Where(h=>SSCPnaExposureUtility.IsBodyStateDef(h.def)))
        {
            var copied=HediffMaker.MakeHediff(h.def,body);copied.Severity=h.Severity;body.health.hediffSet.hediffs.Add(copied);
        }
        body.needs.PnaNeed=source.needs.PnaNeed;Find.PawnDuplicator.OnDuplicate=_=>body;return body;
    }
    private static HediffComp_RabbitCloneLink CloneLink(Pawn clone,Pawn source)
        =>new HediffComp_RabbitCloneLink{source=source,parent=(HediffWithComps)clone.health.AddHediff(RabbitCloneUtility.RabbitCloneLinkDef)};
}
