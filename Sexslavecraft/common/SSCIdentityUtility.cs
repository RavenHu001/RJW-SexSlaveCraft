using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

// EN: This file is the single entry point for SSC role and progression identity checks.
// EN: Vanilla legal status (colonist, prisoner, slave) remains independent from the SSC role.
// CN: 这个文件是 SSC 角色身份与成长身份判断的唯一入口。
// CN: 原版法律身份（殖民者、囚犯、奴隶）与 SSC 角色身份保持相互独立。
namespace SexSlaveCraft
{
    public static partial class SSCIdentityUtility
    {
        public static PawnIdentity GetIdentity(Pawn pawn)
        {
            return pawn?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset;
        }

        public static bool IsMaster(Pawn pawn)
        {
            return GetIdentity(pawn) == PawnIdentity.Master;
        }

        public static bool IsSexSlave(Pawn pawn)
        {
            return GetIdentity(pawn) == PawnIdentity.Slave;
        }

        public static Trait GetSexSlaveTrait(Pawn pawn)
        {
            return pawn?.story?.traits?.GetTrait(SSCDefOf.SexSlaveTrait);
        }

        public static int GetSexSlaveStage(Pawn pawn)
        {
            if (IsMaster(pawn)) return 0;

            Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(pawn);
            if (chain == null) return 0;

            float severity = chain.Severity;
            if (severity >= 0.9f) return 4;
            if (severity >= 0.5f) return 3;
            if (severity >= 0.3f) return 2;
            if (severity >= 0.1f) return 1;
            return 0;
        }

        public static int GetSexSlaveTraitDegreeFromHighestCorruption(Pawn pawn)
        {
            Need_Corruption corruption = pawn?.needs?.TryGetNeed<Need_Corruption>();
            float highest = corruption?.HighestCorruptionLevel ?? 0f;
            if (highest >= 0.9f) return 4;
            if (highest >= 0.5f) return 3;
            if (highest >= 0.3f) return 2;
            if (highest >= 0.1f) return 1;
            return 0;
        }

        public static bool SyncSexSlaveTraitFromHighestCorruption(Pawn pawn)
        {
            if (pawn?.story?.traits?.allTraits == null || SSCDefOf.SexSlaveTrait == null) return false;

            int targetDegree = GetSexSlaveTraitDegreeFromHighestCorruption(pawn);
            if (targetDegree > 0)
            {
                return TraitUtility.AddOrUpdateTrait(pawn, SSCDefOf.SexSlaveTrait, targetDegree);
            }

            List<Trait> staleTraits = pawn.story.traits.allTraits
                .Where(trait => trait?.def == SSCDefOf.SexSlaveTrait)
                .ToList();
            foreach (Trait staleTrait in staleTraits)
            {
                if (!staleTrait.Suppressed && pawn.story.traits.GetTrait(SSCDefOf.SexSlaveTrait) != null)
                {
                    pawn.story.traits.RemoveTrait(staleTrait);
                }
                else
                {
                    pawn.story.traits.allTraits.Remove(staleTrait);
                }
            }

            return true;
        }

        public static bool HasEstablishedSexSlaveState(Pawn pawn)
        {
            if (pawn == null || IsMaster(pawn)) return false;
            if (IsSexSlave(pawn) || GetSexSlaveStage(pawn) > 0) return true;

            HediffSet hediffSet = pawn.health?.hediffSet;
            if (hediffSet != null && SSCDefOf.ChainOfSexSlave != null &&
                hediffSet.HasHediff(SSCDefOf.ChainOfSexSlave))
            {
                return true;
            }

            Need_Corruption corruption = pawn.needs?.TryGetNeed<Need_Corruption>();
            return corruption != null && corruption.CurLevel > 0f;
        }

        /// <summary>已有锁链或仍有有效绑定性奴时锁定身份，避免切换身份删除绑定和成长进度。</summary>
        public static bool IsIdentityLocked(Pawn pawn)
        {
            return SSCBondUtility.GetChain(pawn) != null ||
                   SSCBondUtility.GetBridle(pawn)?.ValidTargets.Any() == true;
        }

        /// <summary>仅允许无绑定角色切换 SSC 身份；拒绝时不修改绑定、仪式或训练配置。</summary>
        /// <returns>完成设置或身份未变化时返回 true；缺少组件或绑定锁定时返回 false。</returns>
        public static bool TrySetIdentity(Pawn pawn, PawnIdentity identity)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;
            // 重复设置不属于身份切换，绑定结算也会重复写入性奴身份。
            if (comp.pawnIdentity == identity) return true;
            if (IsIdentityLocked(pawn)) return false;

            comp.pawnIdentity = identity;
            if (identity != PawnIdentity.Slave)
            {
                BindingRitualStateUtility.ClearRitualState(comp);
                comp.mode = TrainingMode.Disabled;
                comp.selectedTrainer = null;
                comp.isBeingTrained = false;
            }

            // 身份真正变化后通知训导官生命周期。绑定事务会暂缓这次检查，
            // 待主人锁链和身份全部就绪后统一判断持续条件。
            TrainerSpecializationLifecycle.Notify(pawn);

            return true;
        }

        public static bool IsSupportedVanillaStatus(Pawn pawn)
        {
            // EN: Keep the legacy acceptance rule here. Some guest/host and compatibility
            // systems expose a pawn as a vanilla slave without making it a colony slave.
            // Restricting this to IsSlaveOfColony makes those already-configured training
            // targets disappear from the WorkGiver scan before a job can be created.
            // CN: 这里保留旧版的接受范围。部分访客/宿主与兼容系统会让 Pawn 保持原版
            // 奴隶身份，但不会把它标记为殖民地奴隶。若收窄为 IsSlaveOfColony，
            // 已配置好的调教目标会在 WorkGiver 扫描阶段直接消失，无法生成 Job。
            return pawn != null &&
                   (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlave);
        }
    }
}
