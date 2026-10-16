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
        // 继续保存既有适应进度；完成与否由临时 Hediff 是否仍附着决定，无须另设存档标记。
        public float adaptation;

        /// <summary>保留原有 adaptation 存档键，兼容此前已经在进行中的过程。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref adaptation, "adaptation", 0f);
        }

        /// <summary>保留继承的按钮，仅向开发模式下的玩家角色提供强制成功入口。</summary>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            // 按钮的可见性只限制调试入口，不改变普通组件扫描或成功概率。
            if (!Prefs.DevMode || pawn?.Faction != Faction.OfPlayer) yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: FullGel Success",
                action = delegate
                {
                    // 界面可能保留先前创建的委托；点击时重新领取终结资格，防止旧按钮再次成功。
                    if (!GelatinizationProcessUtility.TryComplete(this)) return;
                    // 先移除临时过程，再写满进度并调用原成功服务，与自然扫描的结算顺序一致。
                    adaptation = 1f;
                    // EN: Utility call: force the success-side resolution for debugging.
                    // CN: 工具调用：开发模式下强制触发成功侧结算。
                    FullGelatinizationUtility.CompleteSuccess(pawn);
                }
            };
        }
    }
}
