// AI 辅助生成。专用任务提供允许运行的邀约来源，实际互动仍由 RJW 原驱动处理。
using System.Collections.Generic;
using System.Linq;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>用专用任务标识已经接受的邀请。走位、接收、Start 和 ProcessSex 仍由 RJW 原驱动生成。</summary>
    public sealed class JobDriver_SSCBreakdownInvitedUse : JobDriver_Rape
    {
        internal const string DefName = "SSC_BreakdownInvitedUse";
        internal static JobDef Def => DefDatabase<JobDef>.GetNamedSilentFail(DefName);
        internal static bool IsInvitedJob(Job job) => job?.def?.driverClass == typeof(JobDriver_SSCBreakdownInvitedUse);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Native Partner 是目标 A，即保存发作和接受结果的受方。当前驱动的 pawn 才是受邀后接单的发起方。
            Pawn subject = Partner;
            var state = MentalState_SSCSlaveBreakdown.CurrentFor(subject);
            // StartJob 在原生预约前已安装当前任务。缺少发作授权时直接拒绝预约，避免触碰其他任务或地图资源。
            return state?.InvitationFor(job) is { } invitation &&
                state.CanIssueBehavior && !state.HasApprovedTraining && invitation.MatchesDirection(subject, this) &&
                invitation.ParticipantsAreCurrent(subject) && invitation.CanPrepareRecipient(subject) &&
                SSCBreakdownInvitation.IsAllowed(subject, pawn) &&
                base.TryMakePreToilReservations(errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            IEnumerable<Toil> source = base.MakeNewToils();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && duration > 0)
            {
                // 原版重建 Toil 时，RJW 的 setup_ticks 会重新生成计时。LoadingVars 已读到原进度，先暂存再构造步骤。
                var saved = (ticks_left, duration, sex_ticks, orgasmstick, orgasmStartTick,
                    ticks_between_hearts, ticks_between_hits, ticks_between_thrusts);
                List<Toil> rebuilt = source.ToList();
                int rebuiltDuration = duration;
                (ticks_left, duration, sex_ticks, orgasmstick, orgasmStartTick,
                    ticks_between_hearts, ticks_between_hits, ticks_between_thrusts) = saved;
                foreach (Toil toil in rebuilt)
                    if (toil.defaultCompleteMode == ToilCompleteMode.Never && toil.defaultDuration == rebuiltDuration)
                        toil.defaultDuration = duration;
                source = rebuilt;
            }

            // 原步骤仍由 RJW 提供。保留原角色、Start、End 与 ProcessSex，只加入开始校验和任务归属。
            return SSCRestrictionJobGuard.TrackPreparation(this, source);
        }
    }
}
