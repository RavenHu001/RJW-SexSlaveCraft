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

        /// <summary>优先返回绑定主人；无主时取首位仍具调教员身份的指定对象。</summary>
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
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            if (comp.AssignedTrainerCount > 1) return false;
#endif

            if (trainer != null && !TrainerAssignmentUtility.CanAssignTrainerTo(sexSlave, trainer)) return false;

            return SSCRestrictionTrainerAssignment.TryAssign(sexSlave, trainer);
        }

        /// <summary>建立一次实际绑定；1.7在新的双向关系成功后定型初始印记，同关系重放不重新定型。</summary>
        public static bool Bind(Pawn master, Pawn sexSlave, bool replaceExisting = false)
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            => BindCore(master, sexSlave, replaceExisting, true, out _);

        /// <summary>用本场仪式的结算资格建立或补齐绑定，保留已绑定给其他主人的关系。</summary>
        /// <param name="master">本场主持。新建绑定时，主持须具备 SSC 主人身份。</param>
        /// <param name="sexSlave">本场受训者。</param>
        /// <param name="isCurrent">
        /// 身份刷新后传入 null，校验写链前的原上下文。完成双向写入与限制通知后，
        /// 传入实际创建或复用的链，为后续印记分化复查资格。
        /// </param>
        /// <param name="chain">本场尝试提交的锁链。回调可能在写入后让请求失效，调用方以返回值和实际归属确认成功。</param>
        internal static bool BindForRitual(Pawn master, Pawn sexSlave,
            Func<Hediff_ChainOfSexSlave, bool> isCurrent, out Hediff_ChainOfSexSlave chain)
            => BindCore(master, sexSlave, false, true, out chain, isCurrent);

        /// <summary>仅供人格植入恢复来源已有关系；沿共同绑定逻辑恢复两端，不重放初始印记分化。</summary>
        internal static bool RestorePersonalityBinding(Pawn master, Pawn sexSlave)
            => BindCore(master, sexSlave, true, false, out _);

#if SSC_RW17
        /// <summary>爆发的批准、派发和仪式准入共用新绑定资格；最终写链前再查一次。</summary>
        /// <remarks>解绑冷却接入时在此处补准入条件，让所有调用方读到同一规则。</remarks>
        internal static bool CanBindNewWithProgress(Pawn master, Pawn subject) =>
            master != null && subject != null && master != subject && !master.Dead && !master.Destroyed && !master.Discarded &&
            !subject.Dead && !subject.Destroyed && !subject.Discarded && subject.health?.hediffSet != null &&
            SSCIdentityUtility.IsMaster(master) && !Utils.HasChainHediff(subject);

        /// <summary>用已核验的严重度建立两端关系，确认本次写入后移出来源记录。</summary>
        /// <param name="source">当前体内的无主记录。初次成形时可为空，也可能是零阶记录。</param>
        /// <param name="severity">正式无主沿用来源的原始严重度。初次成形由调用方提供起始严重度。</param>
        /// <param name="isCurrent">每次写入前后复查本场结算资格。归属和来源由本服务复核，回调可在新链挂载后继续验证同一场。</param>
        /// <param name="canPrepare">可选的写链前复查。仪式用它核对仍适用的建绑许可和实际恶堕门槛。</param>
        internal static bool BindNewWithProgress(Pawn master, Pawn subject, Hediff_SSCOwnerlessSlave source,
            float severity, Func<bool> isCurrent, out Hediff_ChainOfSexSlave chain, Func<bool> canPrepare = null)
        {
            chain = null;
            float? sourceSeverity = source?.Severity;
            if (!CanBindNewWithProgress(master, subject) || !float.IsFinite(severity) ||
                SSCDefOf.ChainOfSexSlave == null || severity < .1f || severity > SSCDefOf.ChainOfSexSlave.maxSeverity ||
                (TrainingOutcomeUtility.GetSexSlaveProgressStage(source).Stage > 0 && sourceSeverity != severity) ||
                !isCurrent() || Hediff_SSCOwnerlessSlave.Find(subject) != source || source?.Severity != sourceSeverity) return false;

            var health = subject.health;
            var masterComp = master.TryGetComp<CompSexSlaveTraining>();
            var subjectComp = subject.TryGetComp<CompSexSlaveTraining>();
            object masterToken = SSCPersonalityRuntimeUtility.Capture(master);
            object subjectToken = SSCPersonalityRuntimeUtility.Capture(subject);
            var written = default(Hediff_ChainOfSexSlave);
            bool OwnsWrite(Hediff_ChainOfSexSlave candidate) => candidate != null && subject.health == health &&
                GetChain(subject) == candidate && candidate.LinkedPawn == master;
            // 通知可以更新任一方。捕获的健康列表、组件和人格都仍有效时，交接才属于原请求。
            bool CanFinalize() => isCurrent() && subject.health == health &&
                master.TryGetComp<CompSexSlaveTraining>() == masterComp &&
                subject.TryGetComp<CompSexSlaveTraining>() == subjectComp &&
                SSCPersonalityRuntimeUtility.IsCurrent(master, masterToken) &&
                SSCPersonalityRuntimeUtility.IsCurrent(subject, subjectToken) && SSCIdentityUtility.IsMaster(master) &&
                !master.Dead && !master.Destroyed && !master.Discarded && !subject.Dead && !subject.Destroyed && !subject.Discarded;
            // 写入前检查没有锁链，写入后检查本次实例。两段都要求来源进度保持在提交时捕获的值。
            bool CanWrite(Hediff_ChainOfSexSlave candidate) => (candidate != null || canPrepare?.Invoke() != false) && CanFinalize() &&
                Hediff_SSCOwnerlessSlave.Find(subject) == source && source?.Severity == sourceSeverity &&
                (candidate == null ? CanBindNewWithProgress(master, subject)
                    : OwnsWrite(candidate) && candidate.Severity == severity);

            bool committed = false;
            try
            {
                if (!BindCore(master, subject, false, false, out written, CanWrite, severity) ||
                    !CanWrite(written) || GetBridle(master)?.targets.Contains(subject) != true) return false;
                // 锁链、缰绳和精确进度均已核对，移除旧记录由此成为交接的一部分。
                // 此后的通知可能继续更换归属，后续分化和结果反馈仍须持有本次写入。
                committed = true;
                if (source != null) health.RemoveHediff(source);
                if (!CanFinalize() || !OwnsWrite(written)) return false;
                SSCImprintUtility.DifferentiateAfterBinding(master, subject);
                if (!CanFinalize() || !OwnsWrite(written) || GetBridle(master)?.targets.Contains(subject) != true) return false;
                chain = written;
                return true;
            }
            finally
            {
                // 回调可能在挂载中抛出异常，或先装入另一条链。只清理仍挂在原身体、指向本场主人的具体实例。
                // 锁链的标准移除入口清理缰绳，只要后来建立的关系仍有效，就会保留它。
                if (!committed && written != null && subject.health == health && written.LinkedPawn == master &&
                    health.hediffSet.hediffs.Contains(written) && SSCPersonalityRuntimeUtility.IsCurrent(subject, subjectToken))
                    health.RemoveHediff(written);
            }
        }
#endif

        /// <summary>校验身份与归属、建立双向链并发送原生命周期通知；仅新绑定入口允许末端分化。</summary>
        private static bool BindCore(Pawn master, Pawn sexSlave, bool replaceExisting, bool differentiate,
            out Hediff_ChainOfSexSlave chain, Func<Hediff_ChainOfSexSlave, bool> isCurrent = null, float? initialSeverity = null)
#endif
        {
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            chain = null;
#endif
            if (master == null || sexSlave == null || master == sexSlave) return false;
            if (!SSCIdentityUtility.IsMaster(master)) return false;

            Pawn existingMaster = GetBoundMaster(sexSlave);
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
            // 绑定会触发身份和健康回调，定型前必须仍是本次请求的两个组件与人格。
            object masterToken = SSCPersonalityRuntimeUtility.Capture(master);
            object slaveToken = SSCPersonalityRuntimeUtility.Capture(sexSlave);
            CompSexSlaveTraining masterComp = master.TryGetComp<CompSexSlaveTraining>();
            CompSexSlaveTraining slaveComp = sexSlave.TryGetComp<CompSexSlaveTraining>();
#endif
            if (existingMaster != null && existingMaster != master && !replaceExisting) return false;
            if (SSCDefOf.ChainOfSexSlave == null || SSCDefOf.BridleOfSexSlave == null) return false;
            using (TrainerSpecializationLifecycle.BeginMutation(sexSlave))
            {
                // 身份、锁链和缰绳分步建立；事务结束前的“暂无绑定主人”
                // 不能让训导官普通培养错误退出。失败路径也由作用域统一检查最终状态。
                if (!SSCIdentityUtility.TrySetIdentity(sexSlave, PawnIdentity.Slave)) return false;
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                // 同身份刷新也会触发回调。先核对原链、原需求、参与者的人格凭据和授权，再让 AddToPawn 读取当前健康列表。
                if (isCurrent != null && !isCurrent(null)) return false;
#endif
                if (existingMaster != null && existingMaster != master)
                {
                    GetBridle(existingMaster)?.RemoveTarget(sexSlave);
                }

#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                if (initialSeverity.HasValue)
                    Hediff_ChainOfSexSlave.AddToPawnAtSeverity(sexSlave, master, initialSeverity, out chain);
                else
                    chain = Hediff_ChainOfSexSlave.AddToPawn(sexSlave, master);
                // 锁链是实际归属，缰绳是反向索引。写链后的健康回调可能换链或换主；
                // 只有本次写入的锁链仍在且归主持所有，才为它补缰绳。
                if (isCurrent != null && (chain == null || GetChain(sexSlave) != chain || chain.LinkedPawn != master))
                    return false;
                // 等进度交接的锁链仍是暂存状态。挂链或补缰绳回调可撤销原请求，尽早停止以免关系协调重新写回指派。
                // 返回失败时由 BindNewWithProgress 撤回本次锁链，源记录尚在原处。
                if (initialSeverity.HasValue && isCurrent?.Invoke(chain) == false) return false;
#else
                Hediff_ChainOfSexSlave chain = Hediff_ChainOfSexSlave.AddToPawn(sexSlave, master);
#endif
                Hediff_BridleOfSexSlave bridle = Hediff_BridleOfSexSlave.AddToPawn(master, sexSlave);
                if (chain == null || bridle == null) return false;
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                if (isCurrent != null && (GetChain(sexSlave) != chain || chain.LinkedPawn != master || GetBridle(master) != bridle))
                {
                    // 添加或刷新缰绳也会触发回调。依当前锁链剔除失效引用，保留主人仍有效的其他目标。
                    bridle.PruneInvalidTargets();
                    return false;
                }
                if (initialSeverity.HasValue && isCurrent?.Invoke(chain) == false) return false;
#endif

                // 完成锁链与缰绳写入后清理旧主人关系；换绑不能把原仪式奖励转赠给新主人。
                SSCBondRelationUtility.RemoveInvalidRelations(sexSlave);

                // 限制系统沿用原有事务通知；训导官维护在作用域释放时运行。
                SSCRestrictionGameComponent.Notify(sexSlave);
#if SSC_RW17 || SSC_RITUAL_STAGE_TEST
                // 通知后再校验，只有仍有效的新绑定能触发印记分化。
                // 已有关系重放只补两端，恢复旧关系由调用方关掉分化。
                if (!SSCPersonalityRuntimeUtility.IsCurrent(master, masterToken) ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(sexSlave, slaveToken) ||
                    master.TryGetComp<CompSexSlaveTraining>() != masterComp || sexSlave.TryGetComp<CompSexSlaveTraining>() != slaveComp ||
                    (isCurrent != null && !isCurrent(chain))) return false;
                if (differentiate && existingMaster != master) SSCImprintUtility.DifferentiateAfterBinding(master, sexSlave);
#endif
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
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
                    if (master == null) comp?.ClearAssignedTrainers();
                    else comp?.RemoveAssignedTrainer(master);
#else
                    if (comp != null && (master == null || comp.selectedTrainer == master))
                    {
                        // 这是解绑事务的关系清理，不是玩家重新指派；人格恢复期间也必须清除原主，
                        // 不能被限制编辑的恢复锁挡住。上面的条件继续保留明确指定的第三方。
                        comp.selectedTrainer = null;
                    }
#endif
                }

                // 即使锁链原先已缺失，也清除旧档残留的社交奖励；原生接口同时处理另一端。
                SSCBondRelationUtility.RemoveInvalidRelations(sexSlave);
#if SSC_RW17
                if (master != null)
                {
                    // 解绑后，原主人的手动例外消失，保留的第三方也要重新满足首次建绑资格。
                    SSCDailyTrainingOccupancy.RemoveUnauthorizedQueuedJobs(sexSlave, master);
                    var remaining = sexSlave.TryGetComp<CompSexSlaveTraining>()?.CopyAssignedTrainers();
                    if (remaining != null)
                        foreach (Pawn trainer in remaining)
                            if (trainer != master) SSCDailyTrainingOccupancy.RemoveUnauthorizedQueuedJobs(sexSlave, trainer);
                }
#endif
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
