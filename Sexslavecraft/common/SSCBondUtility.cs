using RimWorld;
using System;
using System.Linq;
using Verse;

// EN: This service keeps the slave-side chain, master-side bridle, and assigned trainer consistent.
// CN: 这个服务负责让性奴侧锁链、主人侧缰绳和指定调教师保持一致。
namespace SexSlaveCraft
{
    public static class SSCBondUtility
    {
        /// <summary>读取性奴侧锁链；角色或定义缺失时返回空，不在查询中修复关系。</summary>
        public static Hediff_ChainOfSexSlave GetChain(Pawn sexSlave)
        {
            if (sexSlave?.health?.hediffSet == null || SSCDefOf.ChainOfSexSlave == null) return null;
            return sexSlave.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.ChainOfSexSlave) as Hediff_ChainOfSexSlave;
        }

        /// <summary>读取主人侧缰绳；角色或定义缺失时返回空，不创建健康状态。</summary>
        public static Hediff_BridleOfSexSlave GetBridle(Pawn master)
        {
            if (master?.health?.hediffSet == null || SSCDefOf.BridleOfSexSlave == null) return null;
            return master.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.BridleOfSexSlave) as Hediff_BridleOfSexSlave;
        }

        /// <summary>读取锁链实际绑定的主人，不把指定调教员推断为主人。</summary>
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

        /// <summary>检查目标到主人的有向绑定关系，反向关系不视为同一许可来源。</summary>
        public static bool IsBoundTo(Pawn sexSlave, Pawn master)
        {
            return sexSlave != null && master != null && GetBoundMaster(sexSlave) == master;
        }

        /// <summary>通过菜单资格后原子提交指派及必要的非主人调教授权；强制覆盖拒绝时保留原选择。</summary>
        public static bool TryAssignTrainer(Pawn sexSlave, Pawn trainer)
        {
            CompSexSlaveTraining comp = sexSlave?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;

            if (trainer != null && !TrainerAssignmentUtility.CanAssignTrainerTo(sexSlave, trainer)) return false;

            return SSCRestrictionTrainerAssignment.TryAssign(sexSlave, trainer);
        }

        /// <summary>先确认目标可以成为性奴，再建立双向绑定；身份锁定或归属冲突时不改动已有关系。</summary>
        public static bool Bind(Pawn master, Pawn sexSlave, bool replaceExisting = false)
        {
            if (master == null || sexSlave == null || master == sexSlave) return false;
            if (!SSCIdentityUtility.IsMaster(master)) return false;

            Pawn existingMaster = GetBoundMaster(sexSlave);
            if (existingMaster != null && existingMaster != master && !replaceExisting) return false;
            if (SSCDefOf.ChainOfSexSlave == null || SSCDefOf.BridleOfSexSlave == null) return false;
            using (TrainerSpecializationLifecycle.BeginMutation(sexSlave))
            {
                // 身份、锁链和缰绳分步建立；事务结束前的“暂无绑定主人”
                // 不能让训导官普通培养错误退出。失败路径也由作用域统一检查最终状态。
                if (!SSCIdentityUtility.TrySetIdentity(sexSlave, PawnIdentity.Slave)) return false;
                if (existingMaster != null && existingMaster != master)
                {
                    GetBridle(existingMaster)?.RemoveTarget(sexSlave);
                }

                Hediff_ChainOfSexSlave chain = Hediff_ChainOfSexSlave.AddToPawn(sexSlave, master);
                Hediff_BridleOfSexSlave bridle = Hediff_BridleOfSexSlave.AddToPawn(master, sexSlave);
                if (chain == null || bridle == null) return false;

                // 限制系统沿用原有事务通知；训导官维护在作用域释放时运行。
                SSCRestrictionGameComponent.Notify(sexSlave);
                return true;
            }
        }

        /// <summary>解除性奴锁链及对应缰绳引用；按调用参数清理原主人的指派，保留其他指定者。</summary>
        public static bool Unbind(Pawn sexSlave, bool clearAssignedTrainer = true)
        {
            using (TrainerSpecializationLifecycle.BeginMutation(sexSlave))
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
                        // 这是解绑事务的关系清理，不是玩家重新指派；人格恢复期间也必须清除原主，
                        // 不能被限制编辑的恢复锁挡住。上面的条件继续保留明确指定的第三方。
                        comp.selectedTrainer = null;
                    }
                }

                return changed;
            }
        }

        /// <summary>遍历缰绳目标快照逐个解绑，返回实际解除数量，避免修改正在枚举的集合。</summary>
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

        /// <summary>以性奴锁链为依据补齐主人侧反向引用，不更换主人或依据旧保护字段改写指派。</summary>
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
