using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    // ==========================================
    // 专门用于仪式中【主人】位置的检查
    // ==========================================
    public class RitualRole_BindingMaster : RitualRole
    {
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            reason = null;

            // 1. 基础生存状态检查
            if (p == null || p.Dead || p.Downed || !p.Spawned || !p.RaceProps.Humanlike)
            {
                return false;
            }

            // 2. 殖民地检查：必须是本殖民地的人
            if (!p.IsColonist)
            {
                if (!skipReason) reason = Strings.Ritual_MustBeColonist;
                return false;
            }

            // =========================================================
            // 3. 核心修改：面板身份检查 (替代了之前的性别检查)
            // =========================================================
            var comp = p.GetComp<CompSexSlaveTraining>();

            // 防呆：如果没有这个组件（说明 XML 没挂载上或者出错了），自然不能当主人
            if (comp == null)
            {
                if (!skipReason) reason = Strings.Ritual_NoCompData;
                return false;
            }

            // 必须在面板中被明确指定为"主人"
            if (!SSCIdentityUtility.IsMaster(p))
            {
                if (!skipReason) reason = Strings.Ritual_NotDesignatedMaster;
                return false;
            }

            // =========================================================
            // 4. 配对检查：验证此 master 是已选 slave 的指定调教员
            // =========================================================
            if (assignments != null)
            {
                Pawn slave = assignments.FirstAssignedPawn("slave");
                if (slave != null)
                {
                    var slaveComp = slave.GetComp<CompSexSlaveTraining>();
                    if (slaveComp?.selectedTrainer != null && slaveComp.selectedTrainer != p)
                    {
                        if (!skipReason) reason = Strings.RitualRole_SlaveBoundToOther(
                            slaveComp.selectedTrainer.LabelShort);
                        return false;
                    }
                }
            }

            // 5. 物理可达性 (仪式通用需求：必须能走到仪式地点)
            if (selectedTarget.IsValid && !p.CanReach((LocalTargetInfo)selectedTarget, PathEndMode.Touch, Danger.Deadly))
            {
                if (!skipReason) reason = "MessageRitualRoleCannotReach".Translate();
                return false;
            }

            return true;
        }

        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }
    }
}
