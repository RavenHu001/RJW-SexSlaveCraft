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
        public static bool IsPetSpecialization(SexSlaveSpecializationType type)
        {
            return type == SexSlaveSpecializationType.PetCat ||
                   type == SexSlaveSpecializationType.PetDog ||
                   type == SexSlaveSpecializationType.PetRabbit;
        }

        /// <summary>只阻止跨种终极冲突；同种认领、非宠物方向和明确留空保持可用。</summary>
        public static bool CanChangeDirection(SexSlaveSpecializationType type,
            bool catFinal, bool dogFinal, bool rabbitFinal, out PetSpecializationFailure failure)
        {
            bool conflict = IsPetSpecialization(type) &&
                ((catFinal && type != SexSlaveSpecializationType.PetCat) ||
                 (dogFinal && type != SexSlaveSpecializationType.PetDog) ||
                 (rabbitFinal && type != SexSlaveSpecializationType.PetRabbit));
            failure = conflict ? PetSpecializationFailure.ConflictingFinal : PetSpecializationFailure.None;
            return !conflict;
        }

        /// <summary>历史不授予培养资格，组内任何终极成果均结束宠物普通培养。</summary>
        public static bool CanTrain(SexSlaveSpecializationType type, SexSlaveSpecializationType currentType,
            bool catFinal, bool dogFinal, bool rabbitFinal)
        {
            return IsPetSpecialization(type) && type == currentType &&
                !catFinal && !dogFinal && !rabbitFinal;
        }
    }
}
