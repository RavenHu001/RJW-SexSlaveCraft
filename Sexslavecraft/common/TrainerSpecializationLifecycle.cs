using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在状态变化后维护训导官普通培养与终极记录；资格查询仍由只读工具负责。</summary>
    public static class TrainerSpecializationLifecycle
    {
        private const float InitialOrdinarySeverity = 0.01f;

        /// <summary>由完整的身份、绑定、锁链或特化事件通知状态变化。</summary>
        public static void Notify(Pawn pawn)
        {
            // 读档和人格植入中可能暂时没有主人或锁链。事件入口只在运行状态维护，
            // 事务内的通知交给最外层结束时统一处理。
            if (Scribe.mode != LoadSaveMode.Inactive) return;
            Maintain(pawn);
        }

        /// <summary>把分步绑定或解绑合并为一次完整状态维护。</summary>
        public static IDisposable BeginMutation(Pawn pawn)
        {
            return new MutationScope(pawn);
        }

        /// <summary>低频兜底及显式事件共用的幂等维护入口。</summary>
        public static void Maintain(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0 || comp.trainerMutationDepth > 0 ||
                comp.trainerMaintenanceInProgress) return;

            // 只做一次本方向 Hediff 扫描。普通历史进度和其他方向的终极状态
            // 都不使角色成为持续维护对象；无关角色在检查绑定和锁链前返回。
            Hediff ordinary = null;
            Hediff activeFinal = null;
            Hediff disabledFinal = null;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (SSCDefOf.SSC_Hediff_TrainerOfficer != null &&
                    hediff.def == SSCDefOf.SSC_Hediff_TrainerOfficer) ordinary = hediff;
                else if (SSCDefOf.SSC_Hediff_TrainerOfficer_Final != null &&
                    hediff.def == SSCDefOf.SSC_Hediff_TrainerOfficer_Final) activeFinal = hediff;
                else if (SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled != null &&
                    hediff.def == SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled) disabledFinal = hediff;
            }

            bool trainingThisDirection = comp.specializationType == SexSlaveSpecializationType.TrainerOfficer;
            if (!trainingThisDirection && ordinary == null && activeFinal == null && disabledFinal == null) return;

            comp.trainerMaintenanceInProgress = true;
            try
            {
                bool eligible = TrainerSpecializationUtility.MeetsContinuousConditions(pawn, out _);

                // 普通方向失格时走公共切换入口，先归档当前进度，再清掉普通标记。
                // 持久化的认领防护阻止其他残留终极状态在下一轮低频对账中
                // 把刚退出的“无”方向自动改写；玩家主动选择新方向时解除防护。
                if (trainingThisDirection && !eligible)
                {
                    comp.SetSpecialization(SexSlaveSpecializationType.None);
                    comp.trainerInvalidExitBlocksAdoption = true;
                    trainingThisDirection = false;
                }

                if (activeFinal != null || disabledFinal != null)
                {
                    // 终极记录独立于当前方向，并且取代同系普通 Hediff。
                    // 当前方向为其他方向或“无”时也必须维护禁用/恢复。
                    if (ordinary != null) RemoveAll(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
                    EnsureFinalMarker(pawn, eligible, activeFinal, disabledFinal);
                    return;
                }

                if (trainingThisDirection && eligible)
                {
                    EnsureOrdinaryMarker(pawn, comp, ordinary);
                    return;
                }

                // 旧档或外部恢复可能只留下普通 Hediff 而没有组件方向。
                // 仅在条件满足、且不是本功能主动失格退出后才允许认领。
                if (ordinary != null && eligible && !comp.trainerInvalidExitBlocksAdoption &&
                    comp.specializationType == SexSlaveSpecializationType.None)
                {
                    comp.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
                    EnsureOrdinaryMarker(pawn, comp, ordinary);
                    return;
                }

                // 非当前方向、失格或显式退出后的残留普通标记不能再次授予资格。
                if (ordinary != null) RemoveAll(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
            }
            finally
            {
                comp.trainerMaintenanceInProgress = false;
            }
        }

        /// <summary>仅在状态或进度真正变化时创建或同步普通培养标记。</summary>
        private static void EnsureOrdinaryMarker(Pawn pawn, CompSexSlaveTraining comp, Hediff ordinary)
        {
            bool adding = ordinary == null;
            if (adding && SSCDefOf.SSC_Hediff_TrainerOfficer != null)
                ordinary = pawn.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer);
            if (ordinary == null || (adding &&
                pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_TrainerOfficer) == null)) return;

            // 与已有特化相同，外部增加的 Hediff 严重度可以并入当前方向进度。
            // 坏档数值限制在零至一；稳定值不再重复赋给 Hediff.Severity。
            float merged = Math.Max(NormalizeProgress(comp.specializationProgress), NormalizeProgress(ordinary.Severity));
            if (comp.specializationProgress != merged) comp.specializationProgress = merged;
            float severity = Math.Max(InitialOrdinarySeverity, merged);
            if (ordinary.Severity != severity) ordinary.Severity = severity;
        }

        /// <summary>先确保目标终极标记存在，再移除来源标记，避免添加失败时丢失记录。</summary>
        private static void EnsureFinalMarker(Pawn pawn, bool eligible, Hediff active, Hediff disabled)
        {
            Hediff target = eligible ? active : disabled;
            HediffDef targetDef = eligible
                ? SSCDefOf.SSC_Hediff_TrainerOfficer_Final
                : SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled;
            HediffDef oppositeDef = eligible
                ? SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled
                : SSCDefOf.SSC_Hediff_TrainerOfficer_Final;

            if (target == null && targetDef != null)
            {
                target = pawn.health.AddHediff(targetDef);
                if (target != null && pawn.health.hediffSet.GetFirstHediffOfDef(targetDef) == null)
                    target = null;
            }
            if (target == null) return;

            // 双标记也按当前持续条件归并。目标已存在时无需重新添加，
            // 且不会在每轮低频维护中反复写入健康状态。
            if (eligible ? disabled != null : active != null) RemoveAll(pawn, oppositeDef);
        }

        /// <summary>移除该定义的全部残留实例；缺失定义时不碰其他健康状态。</summary>
        private static void RemoveAll(Pawn pawn, HediffDef def)
        {
            if (def == null) return;
            Hediff hediff;
            while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
                pawn.health.RemoveHediff(hediff);
        }

        /// <summary>将损坏的普通进度规范到有效范围，避免写入 NaN 或无穷值。</summary>
        private static float NormalizeProgress(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Math.Max(0f, Math.Min(1f, value));
        }

        /// <summary>只在最外层事务结束时维护一次，确保 Bind 和 Unbind 的中间状态不被观察。</summary>
        private sealed class MutationScope : IDisposable
        {
            private readonly Pawn pawn;
            private readonly CompSexSlaveTraining comp;
            private bool disposed;

            public MutationScope(Pawn pawn)
            {
                this.pawn = pawn;
                comp = pawn?.TryGetComp<CompSexSlaveTraining>();
                if (comp != null) comp.trainerMutationDepth++;
            }

            public void Dispose()
            {
                if (disposed || comp == null) return;
                disposed = true;
                comp.trainerMutationDepth--;
                if (comp.trainerMutationDepth == 0) Notify(pawn);
            }
        }
    }
}
