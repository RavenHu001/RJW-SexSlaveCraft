using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    // 一场绑定仪式由多个 Job 组成。这里管理整场仪式的占用、阶段和结算资格，
    // Job 只负责执行当前阶段；结束一个 Job 不代表整场仪式已经结束。
    public static class BindingRitualStateUtility
    {
        public const int PhaseCount = 6;
        private const string BindingBehaviorDefName = "SSC_BindingRitualBehavior";

        /// <summary>按行为定义识别绑定仪式；空引用或其他类型的仪式返回 false。</summary>
        public static bool IsBindingRitual(LordJob_Ritual ritual)
        {
            return ritual?.Ritual?.behavior?.def?.defName == BindingBehaviorDefName;
        }

        /// <summary>确认仪式仍由地图管理，且两名存活参与者仍属于本场并占据指定的主从角色。</summary>
        /// <returns>全部条件满足时返回 true；已移除、角色改变或参与者死亡时返回 false。</returns>
        public static bool IsActiveRitualFor(Pawn master, Pawn slave, Lord ritualLord)
        {
            if (master == null || slave == null || master.Dead || slave.Dead) return false;
            if (!(ritualLord?.LordJob is LordJob_Ritual ritual) || !IsBindingRitual(ritual)) return false;

            // LordManager 在调用 Cleanup 前先移除 Lord。不能只检查 Pawn.GetLord()：
            // 清理过程中 Pawn 暂时还可能指向已结束的 Lord。
            if (ritualLord.Map?.lordManager?.lords.Contains(ritualLord) != true) return false;
            return ritualLord.ownedPawns.Contains(master)
                && ritualLord.ownedPawns.Contains(slave)
                && ritual.PawnWithRole("master") == master
                && ritual.PawnWithRole("slave") == slave;
        }

        /// <summary>为有效仪式认领目标并设置占用；同一场保留阶段，新场清除上一场进度。</summary>
        /// <returns>可以执行当前阶段时返回 true；仪式无效、缺少组件或已完成六阶段时返回 false。</returns>
        public static bool TryBeginPhase(Pawn master, Pawn slave, Lord ritualLord)
        {
            if (!IsActiveRitualFor(master, slave, ritualLord)) return false;
            CompSexSlaveTraining training = slave.TryGetComp<CompSexSlaveTraining>();
            if (training == null) return false;

            // 只在首次运行时迁移旧档。不能在 PostLoadInit 中根据尚未恢复完整的
            // Lord/角色列表判断仪式已失效，也不能把“暂时在等待”当成仪式结束。
            RecoverPawnState(slave);
            if (training.bindingRitualLord != ritualLord)
            {
                ClearRitualState(training);
                training.bindingRitualLord = ritualLord;
            }

            if (training.ritualPhase >= PhaseCount) return false;
            training.isRitualTraining = true;
            training.isBeingTrained = true;
            return true;
        }

        /// <summary>推进属于本场的已完成阶段，保留仪式占用；调用者负责保证每阶段只调用一次。</summary>
        /// <returns>实际增加阶段计数时返回 true；仪式失效、归属不符或已达末阶段时返回 false。</returns>
        public static bool TryCompletePhase(Pawn master, Pawn slave, Lord ritualLord)
        {
            if (!IsActiveRitualFor(master, slave, ritualLord)) return false;
            CompSexSlaveTraining training = slave.TryGetComp<CompSexSlaveTraining>();
            if (training == null || training.bindingRitualLord != ritualLord
                || !training.isRitualTraining || training.ritualPhase >= PhaseCount) return false;

            training.ritualPhase++;
            // 第六阶段也保持占用，直到结果处理与整场 Cleanup 完成。
            // 不调用日常 Notify_TrainingCompleted，避免给中断/收尾附加日常冷却。
            return true;
        }

        /// <summary>为仍有效且完成六阶段的本场仪式消费一次结算资格；本函数不直接派发奖励。</summary>
        /// <returns>首次成功领取返回 true；未完成、已领取或仪式失效时返回 false。</returns>
        public static bool TryClaimOutcome(LordJob_Ritual ritual)
        {
            if (!IsBindingRitual(ritual)) return false;
            Pawn slave = ritual.PawnWithRole("slave");
            CompSexSlaveTraining training = slave?.TryGetComp<CompSexSlaveTraining>();
            if (training == null || training.bindingRitualLord != ritual.lord
                || !training.isRitualTraining || training.ritualPhase != PhaseCount
                || training.bindingRitualOutcomeClaimed) return false;
            if (!IsActiveRitualFor(ritual.PawnWithRole("master"), slave, ritual.lord)) return false;

            // 先消费资格，再派发奖励；即使结算抛异常也不能重复派发已产生的奖励。
            training.bindingRitualOutcomeClaimed = true;
            return true;
        }

        /// <summary>统一解除结束仪式的临时状态，兼顾目标角色和参与者列表；允许重复调用。</summary>
        /// <remarks>不要求 Lord 仍在地图管理器中，也不会清除已归属另一场仪式的状态。</remarks>
        public static void EndRitual(LordJob_Ritual ritual)
        {
            if (!IsBindingRitual(ritual) || ritual.lord == null) return;
            EndPawnRitual(ritual.PawnWithRole("slave"), ritual.lord);

            // 也检查所属 Pawn，覆盖角色替换等情况下角色表与运行状态暂时不一致。
            foreach (Pawn participant in ritual.lord.ownedPawns)
            {
                EndPawnRitual(participant, ritual.lord);
            }
        }

        /// <summary>清理本场参与者的匹配状态，兼容尚无 Lord 引用的旧档标记；保留其他场次的归属。</summary>
        private static void EndPawnRitual(Pawn pawn, Lord endedRitualLord)
        {
            CompSexSlaveTraining training = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (training == null) return;
            if (training.bindingRitualLord == endedRitualLord
                || (training.bindingRitualLord == null && training.isRitualTraining))
            {
                ClearRitualState(training);
            }
        }

        /// <summary>运行时核对仪式归属：保留有效场次、认领有效旧档，或解除失效仪式的残留占用。</summary>
        /// <remarks>应在 Lord 和角色引用恢复后调用；普通日常调教不会被当作仪式认领。</remarks>
        public static void RecoverPawnState(Pawn pawn)
        {
            CompSexSlaveTraining training = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (training == null) return;

            Lord ritualLord = training.bindingRitualLord;
            if (ritualLord != null)
            {
                Pawn master = (ritualLord.LordJob as LordJob_Ritual)?.PawnWithRole("master");
                if (IsActiveRitualFor(master, pawn, ritualLord)) return;
                ClearRitualState(training);
                return;
            }

            // 老存档没有 Lord 引用。只有确实仍在绑定仪式的目标角色中，才认领
            // 原阶段；否则清掉遗留标记。普通调教的接收 Job 不能作为认领依据。
            if (training.isRitualTraining)
            {
                Lord currentLord = pawn.GetLord();
                Pawn master = (currentLord?.LordJob as LordJob_Ritual)?.PawnWithRole("master");
                if (IsActiveRitualFor(master, pawn, currentLord))
                {
                    training.bindingRitualLord = currentLord;
                    training.ritualPhase = System.Math.Max(0, System.Math.Min(PhaseCount, training.ritualPhase));
                    return;
                }
            }

            if (training.isRitualTraining || training.ritualPhase != 0 || training.bindingRitualOutcomeClaimed)
            {
                ClearRitualState(training);
            }
        }

        /// <summary>复位仪式标记、阶段、归属及结算资格，保留调教配置、长期成长和日常冷却。</summary>
        /// <param name="training">调用者已确认非空的训练组件。</param>
        internal static void ClearRitualState(CompSexSlaveTraining training)
        {
            // 只清理仪式临时状态。旧档成功仪式可能只留下 phase=6，此时正在进行
            // 的日常调教不能被误清；长期成长、指定调教师和冷却时间均保持原值。
            if (training.isRitualTraining || training.bindingRitualLord != null)
            {
                training.isBeingTrained = false;
            }
            training.isRitualTraining = false;
            training.ritualPhase = 0;
            training.bindingRitualLord = null;
            training.bindingRitualOutcomeClaimed = false;
        }
    }
}
