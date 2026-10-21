// AI 辅助生成
using System;
using System.Collections.Generic;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>读取主人当前器官的 RJW 产液能力，冻结为单次侵染剂量；不计算实际体液转移。</summary>
    public static class SSCMasterInfestationFluidUtility
    {
        public const float MinimumFluid = 7.5f;
        public const float MaximumFluid = 250f;
        public const float ReferenceFluid = 20f;
        public const float MinimumDose = 0.03f;
        private static bool warnedReadFailure;

        /// <summary>只对总量补下限；在器官读取和每次累加前饱和，巨大输入不参与加法。</summary>
        public static float CaptureDose(Pawn source)
        {
            float total = 0f;
            try
            {
                var health = source?.health?.hediffSet;
                if (health == null) return MinimumDose;
                var organs = source.GetGenitalsList();
                if (organs == null) return MinimumDose;
                var seen = new HashSet<Hediff>();
                foreach (Hediff organ in organs)
                {
                    // RJW 缓存可能重复或遗留旧器官；只认仍附着在本身体的有效产液器官。
                    if (organ == null || !seen.Add(organ) || organ.pawn != source || organ.Part == null ||
                        !health.hediffs.Contains(organ) || health.PartIsMissing(organ.Part)) continue;
                    try
                    {
                        var part = organ as ISexPartHediff;
                        if (part?.Def?.produceFluidOnOrgasm != true) continue;
                        var comp = part.GetPartComp();
                        if (comp?.Fluid == null) continue;
                        float amount = comp.FluidAmount;
                        // NaN、负数和零不贡献产量；正无穷与极大有限值直接饱和，不让异常浮点传播。
                        if (float.IsNaN(amount) || amount <= 0f) continue;
                        float remaining = MaximumFluid - total;
                        if (amount >= remaining) return 1f;
                        total += amount; // 此处 amount 严格小于 remaining，总量始终小于 250。
                    }
                    catch (Exception ex) { WarnReadFailure(ex); }
                }
            }
            // 可选器官扩展读取失败不阻断原结算；已读到的合法产量保留，无可用量时走最低剂量。
            catch (Exception ex) { WarnReadFailure(ex); }
            float effective = Math.Max(MinimumFluid, total);
            return Math.Min(1f, Math.Max(MinimumDose, SSCPnaMath.MasterInfestationDose * (effective / ReferenceFluid)));
        }

        private static void WarnReadFailure(Exception ex)
        {
            // 同类兼容失败每次启动最多提醒一次，不按小人或每次互动重复刷日志。
            if (warnedReadFailure) return;
            warnedReadFailure = true;
            Log.Warning("[SSC InfestationFluid] Cannot read optional RJW fluid output; retained valid amounts and minimum dose: " + ex.GetType().Name);
        }
    }
}
