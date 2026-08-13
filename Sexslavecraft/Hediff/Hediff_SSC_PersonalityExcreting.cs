using System.Collections.Generic;
using RimWorld;
using Verse;

// EN: This file binds the runtime class for the personality-excreting hediff and
// EN: adds a small dev gizmo for forcing excretion progress during testing.
// CN: 这个文件负责给人格排泄 Hediff 做运行时类绑定，并提供一个开发模式下强制推进排泄进度的按钮。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SSC_RuntimeDefBinding
    {
        static SSC_RuntimeDefBinding()
        {
            // EN: Runtime binding keeps older XML defs working even if the hediffClass was omitted or stale.
            // CN: 运行时绑定能兼容旧 XML，在 hediffClass 缺失或过期时仍然正确挂到这个类上。
            HediffDef peDef = DefDatabase<HediffDef>.GetNamedSilentFail("Hediff_SSC_PersonalityExcreting");
            if (peDef != null)
            {
                peDef.hediffClass = typeof(Hediff_SSC_PersonalityExcreting);
            }
        }
    }

    public class Hediff_SSC_PersonalityExcreting : HediffWithComps
    {
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!Prefs.DevMode || pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: +10% PE",
                defaultDesc = "Increase personality excretion progress by 10%.",
                action = delegate
                {
                    // EN: Dev helper: bump severity directly so PE progression can be tested quickly.
                    // CN: 开发辅助：直接提高严重度，快速测试人格排泄推进流程。
                    Severity += 0.1f;
                    if (Severity > 1f)
                    {
                        Severity = 1f;
                    }
                    pawn.health.Notify_HediffChanged(this);
                }
            };
        }
    }
}
