using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>狗终极驯导的实际施放守卫；失败施放不消耗原版技能冷却。</summary>
    public class Ability_PetDogTame : Ability
    {
        public Ability_PetDogTame() { }
        public Ability_PetDogTame(Pawn pawn) : base(pawn) { }
        public Ability_PetDogTame(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                if (!PetDogAbilityUtility.CanUse(pawn, out string reason)) return reason.Translate();
                // 单一直接驯服效果是本技能的配置契约；损坏或重复配置不进入预热与结算。
                if (EffectComps.OfType<CompAbilityEffect_PetDogTame>().Count() != 1)
                    return "SSC_PetDogTameMissingEffect".Translate();
                return base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 原版 PreActivate 会先开始冷却。排队后的成果、对象与同格条件先在此复查，
            // 且拒绝已被撤销或重授予后遗留在旧任务中的 Ability 实例。
            if (!CanCast || pawn.abilities?.GetAbility(def) != this ||
                !PetDogAbilityUtility.CanApply(pawn, target.Pawn, out _) || !CanApplyOn(target)) return false;
            CompAbilityEffect_PetDogTame effect = EffectComps.OfType<CompAbilityEffect_PetDogTame>().FirstOrDefault();
            if (effect == null) return false;
            effect.ResetResult();
            bool activated = base.Activate(target, dest);
            // 原版成功接口为 void；若第三方拦截实际阵营转换，恢复本次新扣的冷却。
            // CanCast 已确保调用前就绪，故这里不会覆盖一次既有有效冷却。
            if (!effect.Succeeded)
            {
                ResetCooldown();
                return false;
            }
            return activated;
        }
    }
}
