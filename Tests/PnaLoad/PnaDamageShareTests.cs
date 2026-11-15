// AI 辅助生成：执行真实投送／负荷工具；TakeDamage 边界只提供调用帧与已完成的物理结果。
using System;
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
        ShareRun("太空投送被转移时不施药、不修复", () =>
        {
            var shot = SpacerShot();
            var pawn = SpacerSlave();
            var injury = SpacerInjury(pawn, 10);
            var worker = new DamageWorker_SSCPnaSpacer();
            Thing.ShareDamageBoundary = (target, info) => worker.Apply(info, target);
            var result = SSCPnaDamageShareUtility.ApplyRedirected(pawn, shot);
            Near(0, result.totalDamageDealt);
            Near(0, injury.Healed);
            AssertNoCoatingDrug(pawn);
        });
        ShareRun("多个承伤者不复制负荷且旧药效快照保持",()=>
        {
            var shot=CoatedShareShot();var first=CoatedPawn();var second=CoatedPawn();Raw(first,.4f);
            var old=new Hediff{def=CoatingResistance,pawn=first,Severity=3};first.health.hediffSet.hediffs.Add(old);
            var worker=CoatedWorker();Thing.ShareDamageBoundary=(t,i)=>worker.Apply(i,t);
            SSCPnaDamageShareUtility.ApplyRedirected(first,shot);SSCPnaDamageShareUtility.ApplyRedirected(second,shot);
            Near(.2,SSCPnaExposureUtility.GetLoad(first));Assert(first.health.hediffSet.hediffs.Contains(old),"旧快照保留");
            AssertNoCoatingDrug(second);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);
        });
    }
}
