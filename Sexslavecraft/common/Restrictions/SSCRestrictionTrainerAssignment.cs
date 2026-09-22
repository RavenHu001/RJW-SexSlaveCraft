using Verse;

namespace SexSlaveCraft
{
    /// <summary>把明确指派与调教授权作为同一次修改；普通互动规则、绑定主人及工作资格不由本类改变。</summary>
    internal static class SSCRestrictionTrainerAssignment
    {
        /// <summary>只读预检指派是否能完成；候选列表允许用授权后的副本检查强制来源，绝不提前修改个人选择。</summary>
        public static bool CanAssign(Pawn target, Pawn trainer)
        {
            return TryPrepare(target, trainer, out _);
        }

        /// <summary>校验完毕后一起提交保存规则与指定者；调用者先检查调教员身份、地图及正常工作资格。</summary>
        public static bool TryAssign(Pawn target, Pawn trainer)
        {
            if (!TryPrepare(target, trainer, out SSCRestrictionRules replacement)) return false;
            CompSexSlaveTraining comp = target.TryGetComp<CompSexSlaveTraining>();
            // 在确定不会被装备或特化强制否决后才提交；失败不能留下“授权成功但指派失败”的半成品。
            if (replacement != null) comp.restrictionConfig.rules = replacement;
            comp.selectedTrainer = trainer;
            return true;
        }

        /// <summary>生成指定非主人所需的调教许可候选；未绑定、指定主人或清空时不隐式改变其他规则。</summary>
        private static bool TryPrepare(Pawn target, Pawn trainer, out SSCRestrictionRules replacement)
        {
            replacement = null;
            CompSexSlaveTraining comp = target?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0) return false;
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
