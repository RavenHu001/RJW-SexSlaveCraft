using Verse;

namespace SexSlaveCraft
{
    /// <summary>把明确指派与调教授权作为同一次修改；普通互动规则、绑定主人及工作资格不由本类改变。</summary>
    internal static class SSCRestrictionTrainerAssignment
    {
        /// <summary>只读预检指派是否能完成；候选列表允许用授权后的副本检查强制来源，绝不提前修改个人选择。</summary>
        public static bool CanAssign(Pawn target, Pawn trainer)
        {
            return CanAssign(target, trainer, out _);
        }

        /// <summary>用提交时的同一套规则预检，报告这次指派是否会写入非主人调教许可。</summary>
        internal static bool CanAssign(Pawn target, Pawn trainer, out bool opensNonOwnerTraining)
        {
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            if (target?.TryGetComp<CompSexSlaveTraining>()?.AssignedTrainerCount > 1)
            {
                opensNonOwnerTraining = false;
                return false;
            }
#endif
            bool allowed = TryPrepare(target, trainer, out SSCRestrictionRules replacement);
            // 只有生成了待写入的新规则才提示授权，已经允许的目标无需再次授权。
            opensNonOwnerTraining = allowed && replacement != null;
            return allowed;
        }

        /// <summary>校验完毕后一起提交保存规则与指定者；调用者先检查调教员身份、地图及正常工作资格。</summary>
        public static bool TryAssign(Pawn target, Pawn trainer)
        {
            if (!TryPrepare(target, trainer, out SSCRestrictionRules replacement)) return false;
            CompSexSlaveTraining comp = target.TryGetComp<CompSexSlaveTraining>();
#if SSC_RW17 || SSC_TRAINER_WORK_TEST
            if (comp.AssignedTrainerCount > 1) return false;
            if (replacement != null) comp.restrictionConfig.rules = replacement;
            comp.ReplaceAssignedTrainers(trainer == null ? System.Array.Empty<Pawn>() : new[] { trainer });
#else
            // 在确定不会被装备或特化强制否决后才提交；失败不能留下“授权成功但指派失败”的半成品。
            if (replacement != null) comp.restrictionConfig.rules = replacement;
            comp.selectedTrainer = trainer;
#endif
            return true;
        }

#if SSC_RW17 || SSC_TRAINER_WORK_TEST
        internal static bool CanAdd(Pawn target, Pawn trainer, out bool opensNonOwnerTraining)
        {
            bool allowed = TryPrepareAdd(target, trainer, out SSCRestrictionRules replacement);
            opensNonOwnerTraining = allowed && replacement != null;
            return allowed;
        }

        // 工作事务完成载体复查后提交。规则解析也可能触发外部查询，写入前再次核对。
        internal static bool TryAdd(Pawn target, Pawn trainer, System.Func<bool> isCurrent)
        {
            var comp = target?.TryGetComp<CompSexSlaveTraining>();
            if (!TryPrepareAdd(target, trainer, out SSCRestrictionRules replacement) || !isCurrent()) return false;

            var next = new System.Collections.Generic.List<Pawn>(comp.CopyAssignedTrainers()) { trainer };
            // 解析期间若有其他条目就地变化，只提交本次授权的条目。
            if (replacement != null)
            {
                replacement = comp.restrictionConfig.rules.Copy();
                replacement.Set(SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow);
            }
            comp.ReplaceAssignedTrainers(next);
            if (replacement != null) comp.restrictionConfig.rules = replacement;
            return true;
        }

        private static bool TryPrepareAdd(Pawn target, Pawn trainer, out SSCRestrictionRules replacement)
        {
            replacement = null;
            var comp = target?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && CompSexSlaveTraining.IsRetainableTrainer(target, trainer) &&
                !comp.HasAssignedTrainer(trainer) &&
                (SSCBondUtility.GetBoundMaster(target) != null || SSCIdentityUtility.IsMaster(trainer)) &&
                TryPrepare(target, trainer, out replacement);
        }

        internal static bool CanRemove(Pawn target, Pawn trainer)
        {
            var comp = target?.TryGetComp<CompSexSlaveTraining>();
            return comp != null && comp.restrictionRestoreDepth == 0 && comp.HasAssignedTrainer(trainer) &&
                SSCRestrictionTrainingUtility.GetForcedTrainer(target) != trainer;
        }
#endif

        /// <summary>生成指定非主人所需的调教许可候选；未绑定、指定主人或清空时不隐式改变其他规则。</summary>
        private static bool TryPrepare(Pawn target, Pawn trainer, out SSCRestrictionRules replacement)
        {
            replacement = null;
            CompSexSlaveTraining comp = target?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0) return false;
            // 新增指派不能给未设置 SSC 性奴身份的对象授予调教资格；清理旧指派沿用原规则。
            if (trainer != null && !SSCIdentityUtility.IsSexSlave(target)) return false;
            if (!SSCRestrictionResolver.IsApplicable(target)) return true;

            Pawn owner = SSCBondUtility.GetBoundMaster(target);
            if (trainer == owner) return true;
            // 禁止非主人调教时不能仅清空指定者绕过协调；选定合格的非主人则明确授权该项。
            if (trainer == null) return SSCRestrictionTrainingUtility.GetForcedTrainer(target) == null;
            SSCRestrictionConfig config = comp.restrictionConfig;
            // 总限制关闭时，坏配置不能重新变成指派禁令；只更新指定者，绝不借机修复或覆盖原始数据。
            // 配置有效时仍保存这次明确授权，供之后重新开启限制时使用。
            if (config?.IsValid() != true) return !(SSCMod.settings?.enableSexSlaveProtectionRules ?? true);

            SSCRestrictionRules candidate = config.rules.Copy();
            candidate.Set(SSCRestrictionRule.ReceiveTraining, SSCRestrictionValue.Allow);
            SSCRestrictionResolution effective = SSCRestrictionResolver.Resolve(target, candidate, SSCRestrictionRule.ReceiveTraining);
            // 授权修改的是个人保存值，不是高于装备/特化的白名单；强制禁止仍然有效。
            if (!effective.Valid || effective.Value != SSCRestrictionValue.Allow) return false;
            if (!config.rules.receiveTraining) replacement = candidate;
            return true;
        }
    }
}
