// AI 辅助生成：直接执行生产伤害和药效代码，核对显示请求与真实施药结果的对应关系。
using System;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunWeaponEffectTests()
    {
        foreach (float dose in new[] { .15f, .06f, .10f })
            Run("受药爱心三种涂覆载体 " + dose, () =>
            {
                var p=CoatedPawn();CoatedWorker().Apply(CoatedShot(dose),p);
                Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);
                Assert(SSCPnaWeaponEffectsUtility.Absorptions[0]==p,"对应本次目标");
                Near(dose,SSCPnaExposureUtility.GetLoad(p));
            });
        foreach (bool launcher in new[] {false,true})
        {
            Run("受药满负荷真实输入仍显示 launcher="+launcher,()=>
            {
                var p=CoatedPawn();Input(p,1);
                if(launcher)new DamageWorker_LowTier().Apply(SharedLauncherShot(),p);
                else CoatedWorker().Apply(CoatedShot(),p);
                Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);Near(1,SSCPnaExposureUtility.GetLoad(p));
            });
            Run("受药主人免疫无爱心 launcher="+launcher,()=>
            {
                var p=CoatedPawn();p.IsMaster=true;
                if(launcher)new DamageWorker_LowTier().Apply(SharedLauncherShot(),p);
                else CoatedWorker().Apply(CoatedShot(),p);
                Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
            });
            Run("受药有效锁链活化同样显示 launcher="+launcher,()=>
            {
                var p=CoatedPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
                if(launcher)new DamageWorker_LowTier().Apply(SharedLauncherShot(),p);
                else CoatedWorker().Apply(CoatedShot(),p);
                Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);Assert(RootedRecord(p)!=null,"保留活化入口");
            });
            Run("受药传播只一颗同tick独立命中各一颗 launcher="+launcher,()=>
            {
                var p=CoatedPawn();
                if(launcher)
                {
                    var w=new DamageWorker_LowTier();var shot=SharedLauncherShot();shot.Parts=3;
                    w.Apply(shot,p);Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);
                    w.Apply(shot,p);Equal(2,SSCPnaWeaponEffectsUtility.Absorptions.Count);Equal(6,w.InjuryCalls);
                }
                else
                {
                    var w=CoatedWorker();w.PhysicalApply=(i,t)=>
                    {var r=BoundaryWound(p,3);r.hediffs.AddRange(BoundaryWound(p,2).hediffs);r.totalDamageDealt+=2;return r;};
                    w.Apply(CoatedShot(),p);Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);
                    w.Apply(CoatedShot(),p);Equal(2,SSCPnaWeaponEffectsUtility.Absorptions.Count);
                }
            });
            Run("受药回调拒绝最终结果不显示 launcher="+launcher,()=>
            {
                var p=CoatedPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsBodyLoadDef(h.def))p.health.RemoveHediff(h);};
                if(launcher)new DamageWorker_LowTier().Apply(SharedLauncherShot(),p);
                else CoatedWorker().Apply(CoatedShot(),p);
                Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
            });
            Run("受药嵌套独立命中分别显示 launcher="+launcher,()=>
            {
                var p=CoatedPawn();bool nested=false;
                if(launcher)
                {
                    var w=new DamageWorker_LowTier();w.BeforePart=_=>{if(nested)return;nested=true;w.Apply(SharedLauncherShot(),p);};
                    w.Apply(SharedLauncherShot(),p);
                }
                else
                {
                    var w=CoatedWorker();w.PhysicalApply=(i,t)=>
                    {if(!nested){nested=true;w.Apply(CoatedShot(),p);}return BoundaryWound(p,5);};
                    w.Apply(CoatedShot(),p);
                }
                Equal(2,SSCPnaWeaponEffectsUtility.Absorptions.Count);
            });
        }
        foreach(string kind in new[]{"Bruise","Crush"})
            Run("受药转钝伤不显示 "+kind,()=>
            {CoatedWorker(kind).Apply(CoatedShot(),CoatedPawn());Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);});
        Run("受药完全阻挡无反馈",()=>
        {
            var w=CoatedWorker();w.PhysicalApply=(i,t)=>new DamageWorker.DamageResult{deflected=true};
            w.Apply(CoatedShot(),CoatedPawn());Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
        Run("受药普通非武器输入不显示",()=>
        {Input(NewPawn(),.1f);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);});
    }
}
