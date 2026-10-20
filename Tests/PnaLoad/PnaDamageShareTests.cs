// AI 辅助生成：执行真实投送／负荷工具；TakeDamage 边界只提供调用帧与已完成的物理结果。
using System;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

namespace Verse
{
    public partial class Thing
    {
        internal static Func<Thing, DamageInfo, DamageWorker.DamageResult> ShareDamageBoundary;
        public DamageWorker.DamageResult TakeDamage(DamageInfo damage)
        {
            using (SSCPnaDamageShareUtility.EnterTakeDamage(this, damage))
                return ShareDamageBoundary(this, damage);
        }
    }
}
internal static partial class Program
{
    private static void ShareRun(string name, Action action) => Run(name, () =>
    { try { action(); } finally { Thing.ShareDamageBoundary = null; } });

    private static DamageInfo CoatedShareShot(float dose=.15f, float amount=5f)
    {
        var info=CoatedShot(dose, amount); info.Def.workerClass=typeof(DamageWorker_SSCPnaCoated); return info;
    }
    private static void RunPnaDamageShareTests()
    {
        // 正式九份 DamageDef 的分类和剂量；三时代分别复用同一策略，不按武器名分支。
        foreach (var row in new[] {
            (File:"Primitive",Def:"SSC_Damage_PnaStaffStab",Dose:.15f,Power:5f,Injury:"Stab"),
            (File:"Primitive",Def:"SSC_Damage_PnaDartStab",Dose:.06f,Power:3f,Injury:"Stab"),
            (File:"Primitive",Def:"SSC_Damage_PnaBolt",Dose:.10f,Power:7f,Injury:"Cut"),
            (File:"Industrial",Def:"SSC_Damage_PnaWhipCut",Dose:.18f,Power:3f,Injury:"Cut"),
            (File:"Industrial",Def:"SSC_Damage_PnaRepeaterNeedle",Dose:.08f,Power:2f,Injury:"Stab"),
            (File:"Industrial",Def:"SSC_Damage_PnaRifleNeedle",Dose:.20f,Power:4f,Injury:"Stab") })
        {
            ShareRun("六涂覆分摊只有伤口、无快照药效反馈 "+row.Def, () =>
            {
                var xml=XDocument.Load(SscTestPaths.Combine(root,"Defs/DemageDefs/SSC_Pna"+row.File+"Damage.xml"))
                    .Root.Elements().Single(e=>(string)e.Element("defName")==row.Def);
                float dose=(float)xml.Element("modExtensions").Elements().Single().Element("baseLoad"); Near(row.Dose,dose);
                var shot=CoatedShareShot(dose,row.Power*.7f);shot.Def.defName=row.Def;shot.ArmorPenetrationInt=.3f;
                var p=SpacerSlave();p.health.capacities.Consciousness=3;var worker=CoatedWorker(row.Injury);DamageInfo received=default;
                Thing.ShareDamageBoundary=(t,i)=>{received=i;return worker.Apply(i,t);};
                var r=SSCPnaDamageShareUtility.ApplyRedirected(p,shot);
                Near(shot.Amount,r.totalDamageDealt);Near(.3,received.ArmorPenetrationInt);Assert(received.Def==shot.Def,"保留原伤害定义");
                Equal(1,worker.ApplyCalls);AssertNoCoatingDrug(p);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
                Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(p),"分摊结束清理");
            });
            ShareRun("六涂覆直接命中仍施药 "+row.Def, () =>
            {
                var p=CoatedPawn();var shot=CoatedShareShot(row.Dose,row.Power);var worker=CoatedWorker(row.Injury);
                Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);p.TakeDamage(shot);
                Near(row.Dose,SSCPnaExposureUtility.GetLoad(p));Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);
            });
        }
        foreach(string name in new[]{"SSC_Weapon_PnaSilkWhip","SSC_Weapon_PnaPulseGun","SSC_Weapon_PnaProjectorGun"})
        {
            ShareRun("太空三武器主人不分摊、直接性奴仍吸收修复 "+name, () =>
            {
                var shot=SpacerAttackFromXml(name).Shot;var master=CoatedPawn();master.IsMaster=true;
                Assert(!SSCPnaDamageShareUtility.CanRedirect(master,shot.Def),"主人不创建纳米分摊");
                var p=SpacerSlave();var injury=SpacerInjury(p,10);var worker=new DamageWorker_SSCPnaSpacer();
                Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);p.TakeDamage(shot);
                float dose=shot.Def.GetModExtension<SSCPnaSpacerExtension>().baseLoad;
                Near(dose,SSCPnaExposureUtility.GetLoad(p));Near(dose*5,injury.Healed);
            });
            ShareRun("太空防御性拒绝被转移的投送及修复 "+name, () =>
            {
                var shot=SpacerAttackFromXml(name).Shot;var p=SpacerSlave();p.health.capacities.Consciousness=3;
                var injury=SpacerInjury(p,10);var worker=new DamageWorker_SSCPnaSpacer();
                Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);var r=SSCPnaDamageShareUtility.ApplyRedirected(p,shot);
                Near(0,r.totalDamageDealt);Near(0,injury.Healed);AssertNoCoatingDrug(p);Equal(0,ArmorUtility.Calls);
                Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
            });
        }
        ShareRun("多个承伤者不复制负荷且旧药效快照保持",()=>
        {
            var shot=CoatedShareShot();var first=CoatedPawn();var second=CoatedPawn();Raw(first,.4f);
            var old=new Hediff{def=CoatingResistance,pawn=first,Severity=3};first.health.hediffSet.hediffs.Add(old);
            var worker=CoatedWorker();Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);
            SSCPnaDamageShareUtility.ApplyRedirected(first,shot);SSCPnaDamageShareUtility.ApplyRedirected(second,shot);
            Near(.2,SSCPnaExposureUtility.GetLoad(first));Assert(first.health.hediffSet.hediffs.Contains(old),"旧快照保留");
            AssertNoCoatingDrug(second);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
        ShareRun("分摊物理回调中同目标同定义的真实命中仍施药",()=>
        {
            var p=CoatedPawn();var shot=CoatedShareShot();var worker=CoatedWorker();bool nested=false;
            worker.PhysicalApply=(i,t)=>
            {
                var r=BoundaryWound(p,i.Amount);
                if(!nested){nested=true;p.TakeDamage(shot);Assert(SSCPnaDamageShareUtility.IsRedirectedHit(p),"恢复外层标记");}
                return r;
            };
            Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);SSCPnaDamageShareUtility.ApplyRedirected(p,shot);
            Near(.15,SSCPnaExposureUtility.GetLoad(p));Equal(1,SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
        ShareRun("嵌套分摊异常退出后下一次真实命中恢复",()=>
        {
            var p=CoatedPawn();var shot=CoatedShareShot();
            Thing.ShareDamageBoundary=(t,i)=>throw new InvalidOperationException("test");
            try{SSCPnaDamageShareUtility.ApplyRedirected(p,shot);throw new Exception("应抛原异常");}catch(InvalidOperationException){}
            Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(p),"异常清理");
            var worker=CoatedWorker();Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);p.TakeDamage(shot);
            Near(.15,SSCPnaExposureUtility.GetLoad(p));
        });
        ShareRun("普通柄击允许分摊且不创建PNA标记",()=>
        {
            var p=CoatedPawn();p.IsMaster=true;var shot=new DamageInfo{Def=new DamageDef(),Amount=9};
            Assert(!SSCPnaDamageShareUtility.IsPnaDamage(shot.Def),"普通物理");Assert(SSCPnaDamageShareUtility.CanRedirect(p,shot.Def),"允许分摊");
            Thing.ShareDamageBoundary=(t,i)=>{Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(t),"无药效标记");return new DamageWorker.DamageResult{totalDamageDealt=i.Amount};};
            Near(9,SSCPnaDamageShareUtility.ApplyRedirected(p,shot).totalDamageDealt);
        });
    }
}
