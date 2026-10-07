using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>复用原版接触技能的目标显示，将地图选取与同格施放分开。</summary>
    public class Verb_PetCatComfort : Verb_CastAbilityTouch
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            // 原版接触技能的选取不检查当前施放距离；本技能额外要求能走至目标同格。
            // 直接检查地图选取资格与效果组件，允许隔墙绕路，不调用原版相邻 Touch 判断。
            if (!PetCatAbilityUtility.CanSelectTarget(ability.pawn, target.Pawn, out string reason))
            {
                if (showMessages)
                    Messages.Message(reason.Translate(), ability.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            foreach (CompAbilityEffect comp in ability.EffectComps)
                if (!comp.Valid(target, showMessages)) return false;
            return true;
        }

        public override bool IsApplicableTo(LocalTargetInfo target, bool throwMessages = false)
        {
            // 原版接触技能高亮只查询 IsApplicableTo，必须接入完整选取资格以排除失格目标。
            // ValidateTarget 直接检查工具和组件，不回调本方法；保留原版目标圈、鼠标附件及提示文字。
            return ValidateTarget(target, throwMessages);
        }

        public override bool CanHitTarget(LocalTargetInfo target)
        {
            // 原版零射程的 CanHitTarget 无距离限制；明确要求同格才能开始预热。
            return PetCatAbilityUtility.CanApply(ability.pawn, target.Pawn, out _);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            // Stance_Warmup 每 tick 使用 From 版本；原版零射程会接受相邻 Touch。
            // 预热期间被搬走或目标离开同格时必须中断，不能降级成隔格释放。
            return PetCatAbilityUtility.CanTarget(ability.pawn, target.Pawn, out _) &&
                root == target.Pawn.Position;
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg,
            bool surpriseAttack = false, bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            // 在调用原版前拒绝失格目标，防止走近期间提前眩晕或唤醒目标。
            // 成功进入原版预热后，stunTargetWhileCasting 自动使用预热时长暂停目标。
            if (!ability.CanCast || ability.pawn.abilities?.GetAbility(ability.def) != ability ||
                !CanHitTarget(castTarg) || !ability.CanApplyOn(castTarg)) return false;
            return base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns,
                preventFriendlyFire, nonInterruptingSelfCast);
        }
    }
}
