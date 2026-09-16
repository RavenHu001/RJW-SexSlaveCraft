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
    // 专门用于仪式中【性奴 / 祭品】位置的检查
    // ==========================================
    public class RitualRole_BindingSlave : RitualRole
    {
        /// <summary>校验仪式性奴角色及有效指定调教员，防止停用记录被当作可用仪式对象。</summary>
        public override bool AppliesToPawn(Pawn p, out string reason, TargetInfo selectedTarget, LordJob_Ritual ritual = null, RitualRoleAssignments assignments = null, Precept_Ritual precept = null, bool skipReason = false)
        {
            reason = null;

            // 1. 基础生存状态检查
            if (p == null || !p.Spawned || !p.RaceProps.Humanlike || p.Dead || p.Downed)
            {
                return false;
            }

            // 2. 身份检查 (殖民者 OR 奴隶 OR 囚犯)
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(p))
            {
                if (!skipReason) reason = Strings.Ritual_MustBeColonistOrSlave;
                return false;
            }

            // 3. 物理可达性检查
            if (selectedTarget.IsValid && !p.CanReach((LocalTargetInfo)selectedTarget, PathEndMode.Touch, Danger.Deadly))
            {
                if (!skipReason) reason = "MessageRitualRoleCannotReach".Translate();
                return false;
            }

            // 4. 年龄检查 (原版逻辑，防止选到婴儿)
            if (!base.AppliesIfChild(p, out reason, skipReason)) return false;

            // 4.1 RJW 可受位检查（详细原因）
            if (!Trainjudge.TryCanBeFuckedWithReason(p, out string shortReason, out string detailedReport))
            {
                if (!skipReason) reason = shortReason;
                Log.Warning($"[SSC_RITUAL_ROLE] Candidate blocked: {p.LabelShort}. {shortReason}\n{detailedReport}");
                return false;
            }

            // =========================================================
            // 5. 核心检查：组件、身份与主人指定
            // =========================================================
            var comp = p.GetComp<CompSexSlaveTraining>();

            // 如果连组件都没有，直接拒绝
            if (comp == null)
            {
                if (!skipReason) reason = Strings.Ritual_MissingTrainingComp;
                return false;
            }

            // A. 身份冲突检查：如果是“主人”身份，不能当祭品
            if (SSCIdentityUtility.IsMaster(p))
            {
                if (!skipReason) reason = Strings.Ritual_CannotBeSlaveAsMaster;
                return false;
            }

            // B. 核心条件：是否在面板指定了调教员
            if (comp.selectedTrainer == null)
            {
                if (!skipReason)
                {
                    reason = Strings.Ritual_NoMasterAssigned;
                }
                return false;
            }
            if (TrainerAssignmentUtility.GetActiveAssignedTrainer(p) == null)
            {
                if (!skipReason) reason = "SSC_TrainerIdentity_Required".Translate();
                return false;
            }

            // =========================================================
            // 6. 锁链一致性检查 (如果已穿戴锁链)
            // =========================================================

            // 🔥 新增：如果带有“公交车”(SSC_Hediff_Bus) 状态，则直接跳过归属检查
            if (!BusSpecializationUtility.HasAnyBusState(p) && !(comp?.AllowsOthersForTrainingOrSex ?? false))
            {
                Hediff_ChainOfSexSlave chainHediff = SSCBondUtility.GetChain(p);

                if (chainHediff != null)
                {
                    // 如果身上有锁链，且锁链的主人 和 面板指定的主人 不一致
                    if (chainHediff.LinkedPawn != null && chainHediff.LinkedPawn != comp.selectedTrainer)
                    {
                        if (!skipReason) reason = Strings.Ritual_ChainConflict(chainHediff.LinkedPawn.LabelShort);
                        return false;
                    }
                }
            }

            return true;
        }

        // 针对“文化职位”的判定逻辑
        public override bool AppliesToRole(Precept_Role role, out string reason, Precept_Ritual ritual = null, Pawn p = null, bool skipReason = false)
        {
            reason = null;
            return true;
        }

        // 不需要 ExposeData，因为没有需要存档的独有数据字段
    }
}
