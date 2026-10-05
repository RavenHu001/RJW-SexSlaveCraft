using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>将地图目标选择与同格施放分开，保留原版预热和目标限时暂停。</summary>
    public class Verb_PetCatComfort : Verb_CastAbility
    {
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            // 原版 range=0 会改用 Touch 可达检查。本技能要求 OnCell，并允许隔墙绕路。
            // 这里直接执行地图选取资格，再保留每个效果组件的配置与资格检查。
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
