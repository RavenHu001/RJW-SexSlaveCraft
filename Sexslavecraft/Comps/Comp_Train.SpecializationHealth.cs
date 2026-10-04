using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        /// <summary>独立数据模块通过此边界读取真实终极事实，不保存第二份锁定状态。</summary>
        partial void ReadPetFinalStates(ref bool catFinal, ref bool dogFinal, ref bool rabbitFinal)
        {
            Pawn pawn = parent as Pawn;
            catFinal = PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat);
            dogFinal = PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog);
            rabbitFinal = PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit);
        }

        /// <summary>完整切换后立即更新普通战斗员；读档期间由 PostLoadInit 统一恢复。</summary>
        partial void SpecializationHealthChanged()
        {
            if (Scribe.mode == LoadSaveMode.Inactive)
                CombatantSpecializationUtility.Sync(parent as Pawn);
        }

        // 对账特化状态：凝胶注入/旧存档可能只有 hediff 而没有 comp 类型，
        // 这里自动认领并双向同步，让所有经验来源的门控条件恢复生效。
        /// <summary>根据已有健康状态认领特化方向，并同步当前方向的有效进度与基础状态。</summary>
        public static void ReconcileSpecialization(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            // 终极训导官记录可在其他当前方向上存在，必须在通用认领的
            // “无方向”提前返回之前维护；无关角色在该入口中快速退出。
            TrainerSpecializationLifecycle.Maintain(pawn);
            // 同步当前战斗员方向及明确留空后的普通状态。
            CombatantSpecializationUtility.Sync(pawn);
            if (comp.pawnIdentity == PawnIdentity.Master) return;

            if (comp.specializationType == SexSlaveSpecializationType.None)
            {
                // 旧档缺少组件方向时仍可从健康状态恢复；玩家明确留空，或
                // 训导官失格退出后必须保持“无”，不能从终极标记重新选回。
                if (!comp.CanAdoptSpecializationFromHealth) return;
                SexSlaveSpecializationType adopted = DetectAdoptableType(pawn);
                if (adopted == SexSlaveSpecializationType.None) return;

                comp.SetSpecialization(adopted);
            }

            switch (comp.specializationType)
            {
                case SexSlaveSpecializationType.Bus:
                    BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
                    break;
                case SexSlaveSpecializationType.Cow:
                    BusSpecializationUtility.EnsureCowHediffFromSpecialization(pawn);
                    break;
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
                    break;
            }

            // 保持"进行中"特化的互斥性；终极化 hediff 永不删除。
            RemoveInactiveSpecializationStates(pawn, comp, comp.specializationType);
        }

        /// <summary>按既有优先级从健康状态中找出可恢复的特化方向。</summary>
        private static SexSlaveSpecializationType DetectAdoptableType(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return SexSlaveSpecializationType.None;

            if (BusSpecializationUtility.HasFinalBusState(pawn)) return SexSlaveSpecializationType.Bus;
            if (BusSpecializationUtility.HasFinalCowState(pawn)) return SexSlaveSpecializationType.Cow;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog)) return SexSlaveSpecializationType.PetDog;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat)) return SexSlaveSpecializationType.PetCat;
            if (PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit)) return SexSlaveSpecializationType.PetRabbit;

            if (SSCDefOf.SSC_Hediff_Bus != null && pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Bus)) return SexSlaveSpecializationType.Bus;
            if (SSCDefOf.SSC_Hediff_Cow != null && pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_Cow)) return SexSlaveSpecializationType.Cow;

            HediffDef dogBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetDog);
            if (dogBase != null && pawn.health.hediffSet.HasHediff(dogBase)) return SexSlaveSpecializationType.PetDog;

            HediffDef catBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetCat);
            if (catBase != null && pawn.health.hediffSet.HasHediff(catBase)) return SexSlaveSpecializationType.PetCat;

            HediffDef rabbitBase = PetSpecializationUtility.GetBaseHediffDef(SexSlaveSpecializationType.PetRabbit);
            if (rabbitBase != null && pawn.health.hediffSet.HasHediff(rabbitBase)) return SexSlaveSpecializationType.PetRabbit;

            return SexSlaveSpecializationType.None;
        }

        /// <summary>移除非当前方向的基础健康状态，保留终极状态与身体已有奶量。</summary>
        private static void RemoveInactiveSpecializationStates(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType typeToKeep)
        {
            if (pawn?.health?.hediffSet == null) return;

            if (typeToKeep != SexSlaveSpecializationType.Combatant)
                CombatantSpecializationUtility.RemoveOrdinaryState(pawn);

            // 切换时只移除未完成的“基础”hediff；终极化状态是永久的，绝不被删除。
            if (typeToKeep != SexSlaveSpecializationType.Bus)
            {
                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_Bus);
            }

            if (typeToKeep != SexSlaveSpecializationType.Cow)
            {
                if (comp != null)
                {
                    Hediff cowHediff = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_Cow);
                    if (cowHediff != null)
                    {
                        comp.savedCowReservoirCharge = cowHediff.TryGetComp<HediffComp_CowMilkReservoir>()?.CurrentCharge ?? comp.savedCowReservoirCharge;
                    }
                }

                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_Cow);
            }

            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetCat);
            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetDog);
            RemovePetStateIfNotKept(pawn, typeToKeep, SexSlaveSpecializationType.PetRabbit);

            // 训导官普通 Hediff 只代表正在培养。方向退出时必须同时清理，
            // 否则下一轮对账会把失格角色从残留标记重新认领回来。
            if (typeToKeep != SexSlaveSpecializationType.TrainerOfficer)
                RemoveSpecializationHediff(pawn, SSCDefOf.SSC_Hediff_TrainerOfficer);
        }

        /// <summary>移除未被保留的宠物方向基础状态。</summary>
        private static void RemovePetStateIfNotKept(Pawn pawn, SexSlaveSpecializationType typeToKeep, SexSlaveSpecializationType petType)
        {
            if (typeToKeep == petType) return;

            RemoveSpecializationHediff(pawn, PetSpecializationUtility.GetBaseHediffDef(petType));
        }

        /// <summary>安全移除指定定义的全部基础健康状态。</summary>
        private static void RemoveSpecializationHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health?.hediffSet == null || def == null) return;

            Hediff hediff;
            while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def)) != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

    }
}
