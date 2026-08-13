using System.Collections.Generic;
using RimWorld;
using Verse;

// EN: This temporary hediff stores the running full-gel state on the pawn.
// EN: It mainly persists adaptation progress and exposes a small dev shortcut
// EN: so testing can force a success outcome.
// CN: 这个临时 Hediff 用来保存 Pawn 身上的完全胶化运行状态。
// CN: 它主要持久化 adaptation 进度，并提供一个开发者快捷入口以便强制测试成功结局。
namespace SexSlaveCraft
{
    public class Hediff_FullGelatinizationTemporary : HediffWithComps
    {
        public float adaptation;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref adaptation, "adaptation", 0f);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (!Prefs.DevMode || pawn?.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: FullGel Success",
                action = delegate
                {
                    adaptation = 1f;
                    // EN: Utility call: force the success-side resolution for debugging.
                    // CN: 工具调用：开发模式下强制触发成功侧结算。
                    FullGelatinizationUtility.CompleteSuccess(pawn);
                }
            };
        }
    }
}
