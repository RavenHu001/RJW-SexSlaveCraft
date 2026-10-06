using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

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
                // 单一驯导效果内部分别处理野生驯服和己方训练；损坏或重复配置不进入预热。
                if (EffectComps.OfType<CompAbilityEffect_PetDogTame>().Count() != 1)
                    return "SSC_PetDogTameMissingEffect".Translate();
                return base.CanCast;
            }
        }

        public override Job GetJob(LocalTargetInfo target, LocalTargetInfo destination)
        {
            Job result = base.GetJob(target, destination);
            // 原版此时尚未构建驱动；两份 JobDef 复用同一接近／预热流程。
            // Job 自带的 def 存档固定驯服或训练分支，途中阵营变化不会自动改换效果。
            if (target.Pawn?.Faction == Faction.OfPlayer && SSCDefOf.SSC_Job_PetDogTrain != null)
                result.def = SSCDefOf.SSC_Job_PetDogTrain;
            return result;
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
            // 原版驯服／训练接口为 void；若第三方拦截实际效果，恢复本次新扣的冷却。
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
