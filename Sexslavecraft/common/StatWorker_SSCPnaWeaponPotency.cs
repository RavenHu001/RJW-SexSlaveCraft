// AI 辅助生成：涂覆武器的原生只读信息属性；悬浮说明由对应 StatDef 提供。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>显示武器基线药量，不将品质、材质、持有者或目标的状态混入显示数值。</summary>
    public sealed class StatWorker_SSCPnaWeaponPotency : StatWorker
    {
        /// <summary>同时遵守原生显示开关和涂覆入口校验；不为普通武器生成零值条目。</summary>
        public override bool ShouldShowFor(StatRequest req)
            => SSCPnaWeaponPotencyUtility.TryGetBaseline(req.Def as ThingDef, out _) && base.ShouldShowFor(req);

        /// <summary>实体武器和无实体的配方／定义信息卡使用同一 Def 路径。</summary>
        public override float GetValueUnfinalized(StatRequest req, bool applyPostProcess = true)
            => SSCPnaWeaponPotencyUtility.TryGetBaseline(req.Def as ThingDef, out float load) ? load : 0f;

        /// <summary>真实药量不受常规属性修正影响；不运行原版品质／场景等额外修正与舍入。</summary>
        public override void FinalizeValue(StatRequest req, ref float val, bool applyPostProcess) { }

        /// <summary>右侧报告解释当前基线和意识／快照修正；悬浮气泡使用 StatDef.description。</summary>
        public override string GetExplanationUnfinalized(StatRequest req, ToStringNumberSense numberSense)
            => SSCPnaWeaponPotencyUtility.TryGetBaseline(req.Def as ThingDef, out float load)
                ? "SSC_PnaWeaponPotency_Baseline".Translate(load.ToStringPercent()) + "\n\n" +
                    "SSC_PnaWeaponPotency_Calculation".Translate()
                : string.Empty;

        /// <summary>不追加通用属性因子报告，避免声称存在实际药量不使用的修正。</summary>
        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
            => string.Empty;
    }
}
