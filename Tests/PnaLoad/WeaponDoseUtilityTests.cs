// AI 辅助生成：现役涂覆／太空入口共用抗性与快照的回归。
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
            Run("共用归一化剂量 "+row,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=row.consciousness;
                var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
                Near(row.expected,SSCPnaWeaponDoseUtility.FromLoad(.075f,sample));
                Equal(0,p.health.hediffSet.hediffs.Count);
            });
        Run("共用取样只读且高意识候选不等于写入",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
            Near(3,sample.Divisor);Near(3,sample.NewSnapshotSeverity);Equal(0,p.health.Notifications);
            Assert(!p.health.hediffSet.HasHediff(CoatingResistance),"未写入快照");
        });
        Run("共用快照已经存在时不读当前意识或创建第二份",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=9;
            var record=new Hediff{pawn=p,def=CoatingResistance,Severity=3};p.health.hediffSet.hediffs.Add(record);
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
            Near(3,sample.Divisor);Near(0,sample.NewSnapshotSeverity);
            Assert(SSCPnaWeaponDoseUtility.StoreSnapshot(p,CoatingResistance,sample)==null,"没有新记录");
            Assert(p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance)==record,"保留原实例");
        });
        Run("共用归一化极大基数不经乘二溢出",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=float.MaxValue;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
            Near(.5,SSCPnaWeaponDoseUtility.FromLoad(float.MaxValue,sample));
        });
        Run("共用涂覆快照提交不覆盖期间建立的记录",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
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

        foreach(bool spacer in new[]{false,true})
            Run("快照添加回调只观察完整意识值 spacer="+spacer,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=3;bool observed=false;
                p.health.OnAdded=h=>{if(h.def==CoatingResistance){Near(3,h.Severity);observed=true;}};
                if(spacer)new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(.15f),p);
                else CoatedWorker().Apply(CoatedShot(.15f),p);
                Assert(observed,"观察到完整快照");Near(.05,SSCPnaExposureUtility.GetLoad(p));
            });
        Run("涂覆在完整快照回调内嵌套的剂量保持",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var w=CoatedWorker();var shot=CoatedShot();
            p.health.OnAdded=h=>{if(h.def!=CoatingResistance)return;Near(3,h.Severity);p.health.OnAdded=null;w.Apply(shot,p);};
            w.Apply(shot,p);Near(.10,SSCPnaExposureUtility.GetLoad(p));Equal(2,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        foreach(bool spacerFirst in new[]{false,true})
            Run("两代现役入口复用同一意识快照 spacerFirst="+spacerFirst,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=3;
                if(spacerFirst)new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(.15f),p);
                else CoatedWorker().Apply(CoatedShot(.15f),p);
                p.health.capacities.Consciousness=5;
                if(spacerFirst)CoatedWorker().Apply(CoatedShot(.15f),p);
                else new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(.15f),p);
                Near(.10,SSCPnaExposureUtility.GetLoad(p));Near(3,p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance).Severity);
                Equal(1,p.health.hediffSet.hediffs.Count(h=>h.def==CoatingResistance));
            });
        foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Run("共用非法既有快照只读回退正常意识 "+invalid,()=>
            {
                var p=CoatedPawn();var record=new Hediff{pawn=p,def=CoatingResistance,RawSeverity=invalid};
                p.health.hediffSet.hediffs.Add(record);
                var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
                Near(.15,SSCPnaWeaponDoseUtility.FromLoad(.15f,sample));Equal(0,p.health.Notifications);
            });
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Run("共用非有限意识不制造快照 "+invalid,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=invalid;
                var sample=SSCPnaWeaponDoseUtility.Capture(p,CoatingResistance);
                Near(.15,SSCPnaWeaponDoseUtility.FromLoad(.15f,sample));Near(0,sample.NewSnapshotSeverity);
                Assert(SSCPnaWeaponDoseUtility.StoreSnapshot(p,CoatingResistance,sample)==null,"无快照");
            });
    }
}
