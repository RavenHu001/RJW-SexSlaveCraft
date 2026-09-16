using RimWorld;
using System.Linq;
using Verse;

// EN: This service keeps the slave-side chain, master-side bridle, and assigned trainer consistent.
// CN: 这个服务负责让性奴侧锁链、主人侧缰绳和指定调教师保持一致。
namespace SexSlaveCraft
{
    public static class SSCBondUtility
    {
        public static Hediff_ChainOfSexSlave GetChain(Pawn sexSlave)
        {
            if (sexSlave?.health?.hediffSet == null || SSCDefOf.ChainOfSexSlave == null) return null;
            return sexSlave.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.ChainOfSexSlave) as Hediff_ChainOfSexSlave;
        }

        public static Hediff_BridleOfSexSlave GetBridle(Pawn master)
        {
            if (master?.health?.hediffSet == null || SSCDefOf.BridleOfSexSlave == null) return null;
            return master.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.BridleOfSexSlave) as Hediff_BridleOfSexSlave;
        }

        public static Pawn GetBoundMaster(Pawn sexSlave)
        {
            return GetChain(sexSlave)?.LinkedPawn;
        }

        /// <summary>优先返回绑定主人；无主时只使用仍具调教员身份的指定对象。</summary>
        public static Pawn GetResolvedMaster(Pawn sexSlave)
        {
            Pawn boundMaster = GetBoundMaster(sexSlave);
            if (boundMaster != null) return boundMaster;
            return TrainerAssignmentUtility.GetActiveAssignedTrainer(sexSlave);
        }

        public static bool IsBoundTo(Pawn sexSlave, Pawn master)
        {
            return sexSlave != null && master != null && GetBoundMaster(sexSlave) == master;
        }

        /// <summary>指派写入沿用菜单资格，允许清空；拒绝时保留原记录，不能由外部调用绕过身份过滤。</summary>
        public static bool TryAssignTrainer(Pawn sexSlave, Pawn trainer)
        {
            CompSexSlaveTraining comp = sexSlave?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;

            if (trainer != null && !TrainerAssignmentUtility.CanAssignTrainerTo(sexSlave, trainer)) return false;

            comp.selectedTrainer = trainer;
            return true;
        }

        /// <summary>先确认目标可以成为性奴，再建立双向绑定；身份锁定或归属冲突时不改动已有关系。</summary>
        public static bool Bind(Pawn master, Pawn sexSlave, bool replaceExisting = false)
        {
            if (master == null || sexSlave == null || master == sexSlave) return false;
            if (!SSCIdentityUtility.IsMaster(master)) return false;

            Pawn existingMaster = GetBoundMaster(sexSlave);
            if (existingMaster != null && existingMaster != master && !replaceExisting) return false;
            if (SSCDefOf.ChainOfSexSlave == null || SSCDefOf.BridleOfSexSlave == null) return false;
            // 必须先完成身份检查，不能添加锁链后才发现目标是已有性奴的主人。
            if (!SSCIdentityUtility.TrySetIdentity(sexSlave, PawnIdentity.Slave)) return false;
            if (existingMaster != null && existingMaster != master)
            {
                GetBridle(existingMaster)?.RemoveTarget(sexSlave);
            }

            Hediff_ChainOfSexSlave chain = Hediff_ChainOfSexSlave.AddToPawn(sexSlave, master);
            Hediff_BridleOfSexSlave bridle = Hediff_BridleOfSexSlave.AddToPawn(master, sexSlave);
            if (chain == null || bridle == null) return false;

            CompSexSlaveTraining comp = sexSlave.TryGetComp<CompSexSlaveTraining>();
            if (comp != null && !comp.AllowsOthersForTrainingOrSex)
            {
                comp.selectedTrainer = master;
            }

            return true;
        }

        public static bool Unbind(Pawn sexSlave, bool clearAssignedTrainer = true)
        {
            Hediff_ChainOfSexSlave chain = GetChain(sexSlave);
            Pawn master = chain?.LinkedPawn;
            bool changed = false;

            if (master != null)
            {
                Hediff_BridleOfSexSlave bridle = GetBridle(master);
                if (bridle != null)
                {
                    bridle.RemoveTarget(sexSlave);
                    changed = true;
                }
            }

            if (chain != null && sexSlave?.health != null)
            {
                sexSlave.health.RemoveHediff(chain);
                changed = true;
            }

            if (clearAssignedTrainer)
            {
                CompSexSlaveTraining comp = sexSlave?.TryGetComp<CompSexSlaveTraining>();
                if (comp != null && (master == null || comp.selectedTrainer == master))
                {
                    TryAssignTrainer(sexSlave, null);
                }
            }

            return changed;
        }

        public static int UnbindAllFromMaster(Pawn master)
        {
            Hediff_BridleOfSexSlave bridle = GetBridle(master);
            if (bridle == null) return 0;

            int count = 0;
            foreach (Pawn sexSlave in bridle.ValidTargets.ToList())
            {
                if (Unbind(sexSlave)) count++;
            }

            return count;
        }

        public static void RepairReciprocalLink(Pawn sexSlave)
        {
            Pawn master = GetBoundMaster(sexSlave);
            if (master == null) return;

            Hediff_BridleOfSexSlave bridle = GetBridle(master);
            if (bridle == null || !bridle.targets.Contains(sexSlave))
            {
                Hediff_BridleOfSexSlave.AddToPawn(master, sexSlave);
            }
        }
    }
}
