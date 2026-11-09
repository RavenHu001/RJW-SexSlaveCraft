using System;
using System.Collections.ObjectModel;

namespace SexSlaveCraft
{
    public enum PetSpecializationFailure
    {
        None,
        MissingRequirements,
        NotImplemented,
        ResearchRequired,
        AlreadyFinalized,
        ConflictingFinal
    }

    /// <summary>宠物组的纯规则。终极事实由角色健康状态或人格标签的调用方提供。</summary>
    public static class PetSpecializationRules
    {
        internal static readonly ReadOnlyCollection<SexSlaveSpecializationType> PetTypes = Array.AsReadOnly(new[]
        {
            SexSlaveSpecializationType.PetCat,
            SexSlaveSpecializationType.PetDog,
            SexSlaveSpecializationType.PetRabbit,
#if SSC_RW17
            SexSlaveSpecializationType.PetFox,
#endif
        });

        public static bool IsPetSpecialization(SexSlaveSpecializationType type)
        {
            return PetTypes.Contains(type);
        }

        /// <summary>只阻止跨种终极冲突；同种认领、非宠物方向和明确留空保持可用。</summary>
        public static bool CanChangeDirection(SexSlaveSpecializationType type,
            bool catFinal, bool dogFinal, bool rabbitFinal, out PetSpecializationFailure failure) =>
            CanChangeDirection(type, catFinal, dogFinal, rabbitFinal, false, out failure);

        internal static bool CanChangeDirection(SexSlaveSpecializationType type,
            bool catFinal, bool dogFinal, bool rabbitFinal, bool foxFinal, out PetSpecializationFailure failure)
        {
            bool conflict = IsPetSpecialization(type) &&
                ((catFinal && type != SexSlaveSpecializationType.PetCat) ||
                 (dogFinal && type != SexSlaveSpecializationType.PetDog) ||
                 (rabbitFinal && type != SexSlaveSpecializationType.PetRabbit) ||
                 (foxFinal && type != SexSlaveSpecializationType.PetFox));
            failure = conflict ? PetSpecializationFailure.ConflictingFinal : PetSpecializationFailure.None;
            return !conflict;
        }

        /// <summary>历史不授予培养资格，组内任何终极成果均结束宠物普通培养。</summary>
        public static bool CanTrain(SexSlaveSpecializationType type, SexSlaveSpecializationType currentType,
            bool catFinal, bool dogFinal, bool rabbitFinal) =>
            CanTrain(type, currentType, catFinal, dogFinal, rabbitFinal, false);

        internal static bool CanTrain(SexSlaveSpecializationType type, SexSlaveSpecializationType currentType,
            bool catFinal, bool dogFinal, bool rabbitFinal, bool foxFinal)
        {
            return IsPetSpecialization(type) && type == currentType &&
                !catFinal && !dogFinal && !rabbitFinal && !foxFinal;
        }

        /// <summary>普通效果只属于当前方向；本种终极成果独立于当前培养方向生效。</summary>
        public static bool HasEffects(SexSlaveSpecializationType type, SexSlaveSpecializationType currentType,
            bool catFinal, bool dogFinal, bool rabbitFinal) =>
            HasEffects(type, currentType, catFinal, dogFinal, rabbitFinal, false);

        internal static bool HasEffects(SexSlaveSpecializationType type, SexSlaveSpecializationType currentType,
            bool catFinal, bool dogFinal, bool rabbitFinal, bool foxFinal)
        {
            if (!IsPetSpecialization(type)) return false;
            bool ownFinal = type == SexSlaveSpecializationType.PetCat ? catFinal :
                type == SexSlaveSpecializationType.PetDog ? dogFinal :
                type == SexSlaveSpecializationType.PetRabbit ? rabbitFinal : foxFinal;
            return ownFinal || CanTrain(type, currentType, catFinal, dogFinal, rabbitFinal, foxFinal);
        }
    }
}
