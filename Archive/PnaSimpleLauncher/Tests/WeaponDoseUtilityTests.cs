// AI 辅助生成：共用抗性工具与两种物理入口的兼容性回归；同组用例也对提交前实现运行。
using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunWeaponDoseUtilityTests()
    {
        foreach (var row in new (float consciousness,float expected)[]
            {(-1f,.375f),(0f,.375f),(.1f,.375f),(.2f,.375f),(1f,.075f),(2f,.0375f),(3f,.025f)})
            Run("共用剂量两尺度等值 "+row,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=row.consciousness;
                var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance,SSCPnaWeaponResistancePolicy.Coating);
                Near(row.expected,SSCPnaWeaponDoseUtility.FromSeverity(.15f,sample));
                Near(row.expected,SSCPnaWeaponDoseUtility.FromLoad(.075f,sample));
                Equal(0,p.health.hediffSet.hediffs.Count);
            });
        Run("共用取样只读且高意识候选不等于写入",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance,SSCPnaWeaponResistancePolicy.Coating);
            Near(3,sample.Divisor);Near(3,sample.NewSnapshotSeverity);Equal(0,p.health.Notifications);
            Assert(!p.health.hediffSet.HasHediff(CoatingResistance),"未写入快照");
        });
        Run("共用快照已经存在时不读当前意识或创建第二份",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=9;
            var record=new Hediff{pawn=p,def=CoatingResistance,Severity=3};p.health.hediffSet.hediffs.Add(record);
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance,SSCPnaWeaponResistancePolicy.Coating);
            Near(3,sample.Divisor);Near(0,sample.NewSnapshotSeverity);
            Assert(SSCPnaWeaponDoseUtility.StoreSnapshot(p,CoatingResistance,sample)==null,"没有新记录");
            Assert(p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance)==record,"保留原实例");
        });
        Run("共用归一化极大基数不经乘二溢出",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=float.MaxValue;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance,SSCPnaWeaponResistancePolicy.Coating);
            Near(.5,SSCPnaWeaponDoseUtility.FromLoad(float.MaxValue,sample));
            Near(.5,SSCPnaWeaponDoseUtility.FromSeverity(float.MaxValue,sample));
        });
        Run("共用涂覆快照提交不覆盖期间建立的记录",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance,SSCPnaWeaponResistancePolicy.Coating);
            var nested=new Hediff{pawn=p,def=CoatingResistance,Severity=5};p.health.hediffSet.hediffs.Add(nested);
            Assert(SSCPnaWeaponDoseUtility.StoreSnapshot(p,CoatingResistance,sample)==null,"不创建外层记录");
            Near(5,nested.Severity);Near(.05,SSCPnaWeaponDoseUtility.FromLoad(.15f,sample));
        });
        Run("共用快照清理仅移除调用者持有实例",()=>
        {
            var p=CoatedPawn();var retained=new Hediff{pawn=p,def=CoatingResistance,Severity=5};
            p.health.hediffSet.hediffs.Add(retained);
            SSCPnaWeaponDoseUtility.RemoveUnusedSnapshot(p,new Hediff{pawn=p,def=CoatingResistance});
            SSCPnaWeaponDoseUtility.RemoveUnusedSnapshot(p,null);
            Assert(p.health.hediffSet.hediffs.Single()==retained,"其他实例保留");
        });

        // 下列从旧物理入口执行的特征用例，亦在 6908447 的未重构工人上运行。
        // 特别保留旧快照 AddHediff 初值可被回调观察的历史行为，不顺带修正或提前初始化。
        Run("旧枪快照初值回调随后赋值的顺序保持",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
            var recordDef=((PNA_DWExtension)shot.Def.Extension).recordHediff;
            recordDef.thresholds=new[]{0f,1f,2f,4f};var events=new List<string>();
            p.health.OnAdded=h=>{if(h.def==recordDef){Near(recordDef.initialSeverity,h.Severity);events.Add("added");}};
            p.health.OnChanged=h=>{if(h.def==recordDef){Near(3,h.Severity);events.Add("changed");}};
            var w=new DamageWorker_LowTier();w.Apply(shot,p);
            Equal("added,changed",string.Join(",",events));Near(.025,SSCPnaExposureUtility.GetLoad(p));Equal(1,w.InjuryCalls);
        });
        Run("旧枪快照回调移除记录仍按原样完成本击",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
            var recordDef=((PNA_DWExtension)shot.Def.Extension).recordHediff;
            p.health.OnAdded=h=>{if(h.def==recordDef)p.health.RemoveHediff(h);};
            var w=new DamageWorker_LowTier();w.Apply(shot,p);
            Near(.025,SSCPnaExposureUtility.GetLoad(p));Assert(!p.health.hediffSet.HasHediff(recordDef),"记录已移除");Equal(1,w.InjuryCalls);
        });
        Run("旧枪快照回调修改基础量仍在原时点读取",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
            var ext=(PNA_DWExtension)shot.Def.Extension;
            p.health.OnAdded=h=>{if(h.def==ext.recordHediff)ext.baseSeverity=.30f;};
            new DamageWorker_LowTier().Apply(shot,p);Near(.05,SSCPnaExposureUtility.GetLoad(p));
        });
        Run("旧枪快照回调变主人仍免疫但保留普通创伤",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
            var recordDef=((PNA_DWExtension)shot.Def.Extension).recordHediff;
            p.health.OnAdded=h=>{if(h.def==recordDef)p.IsMaster=true;};
            var w=new DamageWorker_LowTier();w.Apply(shot,p);
            Near(0,SSCPnaExposureUtility.GetLoad(p));Assert(!p.health.hediffSet.HasHediff(recordDef),"主人清快照");Equal(1,w.InjuryCalls);
        });
        Run("旧枪在新快照初值回调内嵌套的剂量保持",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
            var recordDef=((PNA_DWExtension)shot.Def.Extension).recordHediff;var w=new DamageWorker_LowTier();
            p.health.OnAdded=h=>{if(h.def!=recordDef)return;p.health.OnAdded=null;w.Apply(shot,p);};
            w.Apply(shot,p);Near(.525,SSCPnaExposureUtility.GetLoad(p));Equal(2,w.InjuryCalls);
            Near(3,p.health.hediffSet.GetFirstHediffOfDef(recordDef).Severity);
        });
        Run("涂覆在完整快照回调内嵌套的剂量保持",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var w=CoatedWorker();var shot=CoatedShot();
            p.health.OnAdded=h=>{if(h.def!=CoatingResistance)return;Near(3,h.Severity);p.health.OnAdded=null;w.Apply(shot,p);};
            w.Apply(shot,p);Near(.10,SSCPnaExposureUtility.GetLoad(p));Equal(2,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        foreach(bool launcherFirst in new[]{false,true})
            Run("两种入口顺序复用同一既有抗性快照 launcherFirst="+launcherFirst,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=3;var launcher=SharedLauncherShot();
                ((PNA_DWExtension)launcher.Def.Extension).recordHediff=CoatingResistance;
                var gun=new DamageWorker_LowTier();var coated=CoatedWorker();
                if(launcherFirst)gun.Apply(launcher,p);else coated.Apply(CoatedShot(),p);
                p.health.capacities.Consciousness=5;
                if(launcherFirst)coated.Apply(CoatedShot(),p);else gun.Apply(launcher,p);
                Near(.075,SSCPnaExposureUtility.GetLoad(p));Near(3,p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance).Severity);
                Equal(1,p.health.hediffSet.hediffs.Count(h=>h.def==CoatingResistance));Equal(1,gun.InjuryCalls);
            });
        foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            Run("旧枪非法既有抗性快照继续回退1 "+invalid,()=>
            {
                var p=CoatedPawn();var shot=SharedLauncherShot();var def=((PNA_DWExtension)shot.Def.Extension).recordHediff;
                p.health.hediffSet.hediffs.Add(new Hediff{pawn=p,def=def,RawSeverity=invalid});
                new DamageWorker_LowTier().Apply(shot,p);Near(.075,SSCPnaExposureUtility.GetLoad(p));
            });
        foreach(var row in new (float consciousness,float expected,bool snapshot)[]
            {(float.NaN,.075f,false),(float.PositiveInfinity,.075f,true),(float.NegativeInfinity,.375f,false)})
            Run("旧枪非有限意识历史行为保持 "+row,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=row.consciousness;var shot=SharedLauncherShot();
                var def=((PNA_DWExtension)shot.Def.Extension).recordHediff;
                new DamageWorker_LowTier().Apply(shot,p);Near(row.expected,SSCPnaExposureUtility.GetLoad(p));
                Assert(p.health.hediffSet.HasHediff(def)==row.snapshot,"快照创建约定");
            });
        foreach(var row in new (float basis,float expected)[]
            {(0f,0f),(-1f,0f),(float.NaN,0f),(float.PositiveInfinity,.5f)})
            Run("旧枪基础量异常不改变先建快照及物理结果 "+row,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=3;var shot=SharedLauncherShot();
                var ext=(PNA_DWExtension)shot.Def.Extension;ext.baseSeverity=row.basis;
                var w=new DamageWorker_LowTier();w.Apply(shot,p);
                Near(row.expected,SSCPnaExposureUtility.GetLoad(p));Near(3,p.health.hediffSet.GetFirstHediffOfDef(ext.recordHediff).Severity);
                Equal(1,w.InjuryCalls);
            });
        Run("旧枪在物理伤口回调前输入药效的顺序保持",()=>
        {
            var p=CoatedPawn();var w=new DamageWorker_LowTier();bool checkedOrder=false;
            p.health.OnAdded=h=>{if(!SSCPnaExposureUtility.IsBodyLoadDef(h.def))return;Equal(0,w.InjuryCalls);checkedOrder=true;};
            w.Apply(SharedLauncherShot(),p);Assert(checkedOrder,"药效通知先发生");Equal(1,w.InjuryCalls);
        });
    }

    // 单例测试定义不能被其他用例修改阈值，旧枪特征用例各自创建抗性 Def。
    private static DamageInfo SharedLauncherShot()
    {
        var shot=Shot();((PNA_DWExtension)shot.Def.Extension).recordHediff=new HediffDef
            {defName="Hediff_PNA_Consciousness_Record",maxSeverity=100f};
        return shot;
    }
}
