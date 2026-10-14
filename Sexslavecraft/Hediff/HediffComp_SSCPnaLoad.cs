// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    public sealed class HediffCompProperties_SSCPnaLoad : HediffCompProperties
    {
        public HediffCompProperties_SSCPnaLoad() { compClass = typeof(HediffComp_SSCPnaLoad); }
    }

    /// <summary>只累计原生健康有效 delta；不以全局时钟补算离图、暂停或冷冻期间的漏 tick。</summary>
    public sealed class HediffComp_SSCPnaLoad : HediffComp
    {
        private int pendingTicks;
        public int PendingTicks => pendingTicks;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref pendingTicks, "sscPnaPendingTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                pendingTicks = System.Math.Max(0, System.Math.Min(SSCPnaMath.UpdateInterval - 1, pendingTicks));
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (delta <= 0 || Pawn == null || Pawn.Dead || Pawn.Suspended) return;
            // 原生 delta 已经代表有效健康时间。输入只结算 pending，不再按 TicksGame 扣同一段。
            long elapsed = (long)pendingTicks + delta;
            if (elapsed < SSCPnaMath.UpdateInterval) { pendingTicks = (int)elapsed; return; }
            pendingTicks = 0;
            SSCPnaExposureUtility.RefreshBodyState(Pawn);
            Advance(elapsed);
        }

        /// <summary>输入前结算不足一批的已累计有效时间，不生成另一份保存时钟。</summary>
        public void SettlePending()
        {
            int elapsed = pendingTicks;
            pendingTicks = 0;
            if (elapsed > 0) Advance(elapsed);
            if (parent.Severity <= SSCPnaMath.RemovalSeverity &&
                Pawn?.health?.hediffSet?.hediffs.Contains(parent) == true)
                Pawn.health.RemoveHediff(parent);
        }

        private void Advance(long ticks)
        {
            if (Pawn?.health?.hediffSet?.hediffs.Contains(parent) != true) return;
            parent.Severity = SSCPnaMath.AdvanceSeverity(parent.Severity, ticks / (double)SSCPnaMath.TicksPerDay);
        }

        public override bool CompShouldRemove => SSCPnaMath.NormalizeSeverity(parent.Severity) <= SSCPnaMath.RemovalSeverity;

        public override string CompTipStringExtra =>
            (SSCPnaExposureUtility.IsRootedLoadDef(parent.def) ? "SSC_Pna_RootedLoadTooltip" : "SSC_Pna_LoadTooltip")
            .Translate((SSCPnaMath.GetLoad(parent.Severity) * 100f).ToString("F1")).ToString();

        public override string CompDebugString() => $"PNA L={SSCPnaMath.GetLoad(parent.Severity):F6}; pending={pendingTicks}";
    }
}
