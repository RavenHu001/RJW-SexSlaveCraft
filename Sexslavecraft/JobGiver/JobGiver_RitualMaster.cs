using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.AI.Group;

// EN: This JobGiver hands the Binding Ritual master their ritual sex job.
// EN: It finds the assigned sex slave, validates that the ritual target is still usable, and starts the ritual-side training job.
// CN: 这个 JobGiver 会把“绑定仪式”主人的核心 Job 发下去。
// CN: 它会找到被分配的性奴、确认仪式目标仍然可用，然后启动仪式侧的训练 Job。
namespace SexSlaveCraft
{
    public class JobGiver_RitualBinding : ThinkNode_JobGiver
    {
        /// <summary>验证仪式目标、可达性和预定条件，认领本场占用后为主人创建当前阶段 Job。</summary>
        /// <returns>满足执行条件时返回 Job；条件不满足或本场已完成时返回 null。</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            SSCLog.Verbose($"[SSC_GIVER] Ritual master evaluation start: pawn={pawn?.LabelShort ?? "null"}, curJob={pawn?.CurJobDef?.defName ?? "null"}");
            // EN: Step 1: fetch the current ritual lord. Without it, this is not an active Binding Ritual context.
            // CN: 步骤 1：先拿当前仪式 Lord。没有它，就说明这里不是正在运行的绑定仪式场景。
            Lord lord = pawn.GetLord();
            if (lord == null) return null;

            // EN: Step 2: confirm the lord really belongs to a ritual job.
            // CN: 步骤 2：确认这个 Lord 确实属于仪式 Job。
            if (!(lord.LordJob is LordJob_Ritual ritualJob)) return null;

            // EN: Step 3: find the pawn assigned to the `slave` ritual role.
            // CN: 步骤 3：找到仪式里被分配到 `slave` 角色的性奴。
            Pawn slave = ritualJob.PawnWithRole("slave");
            if (slave == null)
            {
                SSCLog.WarningImportant($"[SSC_GIVER] 警告: 仪式中找不到 role id 为 'slave' 的角色！");
                return null;
            }

            // EN: Step 4: reject dead or invalid sex-slave targets before the ritual master starts moving.
            // CN: 步骤 4：在主人开始走位前，先剔除死亡或不可用的性奴目标。
            if (slave.Dead) return null;

            // 在目标修复、预约和仪式占用之前复查本阶段；拒绝时取消整场，避免无限等待重试。
            SSCTrainingAdmission admission = SSCRestrictionTrainingUtility.Evaluate(
                SSCRestrictionTrainingUtility.CreateRequest(pawn, slave, true), false);
            if (!admission.Allowed)
            {
                BindingRitualStateUtility.RejectPhase(pawn, slave, lord, admission.Reason);
                return null;
            }

            if (!Trainjudge.TryCanBeFuckedWithReason(slave, out string shortReason))
            {
                SSCLog.WarningImportant($"[SSC_GIVER] 仪式目标不可用: {slave.LabelShort}. {shortReason}");
                return null;
            }

            if (!pawn.CanReach(slave, PathEndMode.Touch, Danger.Deadly))
            {
                SSCLog.Important($"[SSC_GIVER] {pawn.Name} 无法到达性奴身边");
                return null;
            }

            // EN: Step 5: reserve the sex slave even if normal reservations would block it; ritual jobs must take priority here.
            // CN: 步骤 5：强制预定这个性奴；绑定仪式在这里要压过普通预定逻辑。
            if (!pawn.CanReserve(slave, 1, -1, null, true))
            {
                SSCLog.Important($"[SSC_GIVER] {pawn.Name} 无法预定性奴 (占用中)");
                return null;
            }

            // EN: Step 6: create the ritual-side training job. The ritual spot is resolved later from the ritual lord.
            // CN: 步骤 6：创建仪式侧训练 Job。仪式地点稍后会从 ritual lord 那边读取。
            if (SSCDefOf.Training_Ritual == null)
            {
                SSCLog.Error($"[SSC_GIVER] 严重错误: SSCDefOf.Training_Ritual 是 null！");
                return null;
            }

            // 新 Lord 从阶段 0 开始；同一 Lord 换阶段 Job 保留进度。
            // 占用归属于整场仪式，途中取消也由生命周期补丁统一解除。
            if (!BindingRitualStateUtility.TryBeginPhase(pawn, slave, lord)) return null;

            // EN: Only TargetA is filled here with the sex slave. The ritual spot is resolved later from the active ritual lord.
            // CN: 这里只给 TargetA 填入性奴。仪式地点稍后会从当前 ritual lord 那边读取。
            Job job = JobMaker.MakeJob(SSCDefOf.Training_Ritual, slave);

            job.doUntilGatheringEnded = true;
            job.expiryInterval = 12000;
            job.playerForced = true;

            SSCLog.Important($"[SSC_GIVER] Ritual job issued: master={pawn.LabelShort}, slave={slave.LabelShort}, job={job.def.defName}, ritualSpot={ritualJob.selectedTarget.Cell}");

            return job;
        }
    }
}
