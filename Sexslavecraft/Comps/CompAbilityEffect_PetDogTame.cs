using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class CompProperties_AbilityPetDogTame : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityPetDogTame()
        {
            compClass = typeof(CompAbilityEffect_PetDogTame);
        }
    }

    /// <summary>驯服／训练的单一效果；选择资格与实际同格效果分别复查。</summary>
    public class CompAbilityEffect_PetDogTame : CompAbilityEffect
    {
        // 仅用于一次同步 Activate 的结果，不写入存档、不缓存目标资格或驯服分支。
        public bool Succeeded { get; private set; }
        internal void ResetResult() => Succeeded = false;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!PetDogAbilityUtility.CanSelectTarget(parent.pawn, target.Pawn, out string reason))
            {
                if (throwMessages)
                    Messages.Message(reason.Translate(), parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 排队与原版预热共享此入口，允许尚未走近的可达目标；同格由执行层检查。
            return Valid(target);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            if (!Valid(target)) return null;
            TrainableDef training = target.Pawn.Faction == Faction.OfPlayer
                ? PetDogAbilityUtility.GetNextTraining(target.Pawn) : null;
            return training != null
                ? "SSC_PetDogTameTrainingTargetHint".Translate(target.Pawn.LabelShort, training.LabelCap).ToString()
                : "SSC_PetDogTameTargetHint".Translate(target.Pawn.LabelShort).ToString();
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            // 不检查原版 CanCast，因为此时 PreActivate 已扣冷却。资格工具仍检查真实成果和目标。
            Succeeded = PetDogAbilityUtility.Apply(parent.pawn, target.Pawn);
            if (Succeeded) base.Apply(target, dest);
        }
    }
}
