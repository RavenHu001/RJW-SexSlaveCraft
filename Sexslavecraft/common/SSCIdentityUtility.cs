using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

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

            return GetSexSlaveStageFromChain(SSCBondUtility.GetChain(pawn));
        }

        /// <summary>从已经读取的锁链判定当前阶段，供同次查询复用；调用方负责角色身份门槛。</summary>
        internal static int GetSexSlaveStageFromChain(Hediff_ChainOfSexSlave chain)
        {
            // 将阶段边界集中保留在身份工具中；持有锁链的查询入口无需
            // 为了取得阶段再扫描一次 Hediff，也不另写一套严重度门槛。
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

        /// <summary>汇总原有绑定锁与1.7分化印记锁；供界面判断，实际提交还会核对目标身份。</summary>
        public static bool IsIdentityLocked(Pawn pawn)
        {
            return
#if SSC_RW17
                   SSCImprintUtility.GetImprintLockedIdentity(pawn) != PawnIdentity.Unset ||
#endif
                   SSCBondUtility.GetChain(pawn) != null ||
                   SSCBondUtility.GetBridle(pawn)?.ValidTargets.Any() == true;
        }

        /// <summary>普通身份选择统一检查主人资格、绑定锁与印记方向；选择初始印记者的身份不使其分化。</summary>
        public static bool TrySetIdentity(Pawn pawn, PawnIdentity identity)
        {
            // 在旧主人首次退出身份前保留其历史资格与基础成长；查询和界面绘制不执行迁移。
            SSCMasterQualificationMigration.MigratePawn(pawn);
            SSCMasterProgressionMigration.MigratePawn(pawn);
            return TrySetIdentityCore(pawn, identity, null);
        }

        /// <summary>仅供人格植入恢复已保存的身份与资格，仍遵守现有锁和仪式保护；旧人格印记由清源步骤先移出。</summary>
        internal static bool TryRestorePersonalityIdentity(Pawn pawn, PawnIdentity identity, bool qualificationUnlocked)
        {
            return TrySetIdentityCore(pawn, identity, qualificationUnlocked);
        }

        /// <summary>执行共同身份提交；恢复参数只替代资格准入，不开放绕过印记方向或绑定锁的通道。</summary>
        private static bool TrySetIdentityCore(Pawn pawn, PawnIdentity identity, bool? restoredQualification)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return false;
            // 身份回调可能原地替换人格且沿用同一 Comp；跨回调同时核对组件和 token。
            object personalityToken = SSCPersonalityRuntimeUtility.Capture(pawn);
            if (identity != PawnIdentity.Unset && identity != PawnIdentity.Slave && identity != PawnIdentity.Master)
                return false;
#if SSC_RW17
            // 必须早于同身份快捷分支，避免坏身份记录借重复提交绕过 Hediff 已保存的定型方向。
            if (!SSCImprintUtility.AllowsIdentity(pawn, identity)) return false;
#endif
            // 重复设置不属于身份切换，绑定结算也会重复写入性奴身份。
#if SSC_RW17
            if (comp.pawnIdentity == PawnIdentity.Master) SSCPersonalityExcretionAdmission.RemoveForbiddenPreparation(pawn);
#endif
            if (comp.pawnIdentity == identity)
            {
                if (restoredQualification.HasValue) comp.ReplaceMasterQualification(restoredQualification.Value);
                // 同身份请求只刷新现有成长，不补零经验。Master→Master 人格植入
                // 仍由恢复核心替换来源记录，不能靠“身份变化”通知完成流转。
                SSCMasterProgressionUtility.RefreshActivation(pawn);
                if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || pawn.TryGetComp<CompSexSlaveTraining>() != comp) return false;
                // 同身份的人格恢复也可能换入带药效的身体，仍须执行主人免疫核对。
                SSCPnaExposureUtility.RefreshBodyState(pawn, true);
                if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || pawn.TryGetComp<CompSexSlaveTraining>() != comp) return false;
#if SSC_RW17
                SSCOrgasmSensitivityUtility.NotifyStateChanged(pawn);
                FertilitySpecializationUtility.Sync(pawn);
#endif
                SSCAbilityToggleUtility.SyncMarkers(pawn);
                return true;
            }
            if (!restoredQualification.HasValue && identity == PawnIdentity.Master &&
                !SSCMasterQualificationUtility.CanSelectMaster(pawn)) return false;
            if (IsIdentityLocked(pawn)) return false;

            PawnIdentity previousIdentity = comp.pawnIdentity;
            bool previousQualification = comp.masterQualificationUnlocked;
            int previousQualificationVersion = comp.masterQualificationVersion;
            // 先取消仍匹配此角色的仪式，再清除组件归属，避免取消回调失去原 Lord。
            if (!CancelBindingRitualBeforeIdentityChange(pawn, comp)) return false;
            // Cancel 可经外部回调替换组件、改身份或建立新绑定；原切换不得覆盖新状态。
            if (pawn.TryGetComp<CompSexSlaveTraining>() != comp || comp.pawnIdentity != previousIdentity ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || IsIdentityLocked(pawn)) return false;
#if SSC_RW17
            // 仪式取消回调可能新增或替换印记；不能只相信点击时的锁定结果。
            if (!SSCImprintUtility.AllowsIdentity(pawn, identity)) return false;
#endif
            // 取消回调可能改变设置或换入另一个人格，旧请求不得覆盖其最新资格。
            if (restoredQualification.HasValue)
            {
                if (comp.masterQualificationUnlocked != previousQualification ||
                    comp.masterQualificationVersion != previousQualificationVersion) return false;
            }
            else if (identity == PawnIdentity.Master && !SSCMasterQualificationUtility.CanSelectMaster(pawn))
                return false;
            comp.pawnIdentity = identity;
#if SSC_RW17
            SSCPersonalityExcretionAdmission.RemoveForbiddenPreparation(pawn);
            // 身份写入后立即观察退出事件，不能等后续资格回调结束才撤销旧命令。
            SSCRomanceLifecycle.Notify(pawn);
            SSCOrgasmSensitivityUtility.NotifyStateChanged(pawn);
#endif
            if (restoredQualification.HasValue) comp.ReplaceMasterQualification(restoredQualification.Value);
            if (identity != PawnIdentity.Slave)
            {
                BindingRitualStateUtility.ClearRitualState(comp);
                comp.mode = TrainingMode.Disabled;
                comp.selectedTrainer = null;
                comp.isBeingTrained = false;
            }

            // 成功写入身份之后再建立或启停成长。普通选择允许首次建立；
            // 人格恢复不补基础阶段，恢复核心随后精确应用来源，包括明确没有记录的主人。
            SSCMasterProgressionUtility.NotifyIdentityChanged(pawn, !restoredQualification.HasValue);
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || pawn.TryGetComp<CompSexSlaveTraining>() != comp) return false;
            // 仅在身份成功写入后协调；成为主人清受药状态，退出主人不恢复已清记录。
            SSCPnaExposureUtility.RefreshBodyState(pawn, true);
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || pawn.TryGetComp<CompSexSlaveTraining>() != comp) return false;
#if SSC_RW17
            FertilitySpecializationUtility.Sync(pawn);
#endif
            // 身份真正变化后通知训导官生命周期。绑定事务会暂缓这次检查，
            // 待主人锁链和身份全部就绪后统一判断持续条件。
            TrainerSpecializationLifecycle.Notify(pawn);

            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, personalityToken) || pawn.TryGetComp<CompSexSlaveTraining>() != comp) return false;
            SSCAbilityToggleUtility.SyncMarkers(pawn);

            return true;
        }

        /// <summary>仅为未选择身份且保留有效历史锁链的角色提供显式恢复，不依据调教配置自动认领。</summary>
        public static bool CanRestoreLegacySlaveIdentity(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded)
                return false;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.pawnIdentity != PawnIdentity.Unset || comp.restrictionRestoreDepth > 0)
                return false;
            Pawn owner = SSCBondUtility.GetChain(pawn)?.LinkedPawn;
            // 死亡不等于解绑；销毁、丢弃、自指和双角色矛盾则不属于可恢复的历史关系。
            return owner != null && owner != pawn && !owner.Destroyed && !owner.Discarded &&
                SSCBondUtility.GetBridle(pawn)?.ValidTargets.Any() != true;
        }

        /// <summary>玩家确认后仅补回 Slave 身份；不重绑、不改模式、指派、恶堕或特化成长。</summary>
        public static bool TryRestoreLegacySlaveIdentity(Pawn pawn)
        {
            if (!CanRestoreLegacySlaveIdentity(pawn)) return false;
#if SSC_RW17
            // 旧锁链的显式身份修复也不能把已定型为主人的人格改成性奴。
            if (!SSCImprintUtility.AllowsIdentity(pawn, PawnIdentity.Slave)) return false;
#endif
            // 普通切换仍被历史锁链锁定。此定向修复只接受 Unset，不泛化为 Master 转换。
            // 不立即运行可能重算成长或协调指派的生命周期；正常资格查询读取恢复后的身份。
            pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Slave;
#if SSC_RW17
            // 显式恢复仍沿用原锁链；浪漫模块只更新观察，不据此补发关系或创建命令。
            SSCRomanceLifecycle.Notify(pawn);
            SSCOrgasmSensitivityUtility.NotifyStateChanged(pawn);
#endif
            SSCAbilityToggleUtility.SyncMarkers(pawn);
            return true;
        }

        private static bool CancelBindingRitualBeforeIdentityChange(Pawn pawn, CompSexSlaveTraining comp)
        {
            Lord savedLord = comp.bindingRitualLord;
            Lord currentLord = pawn.GetLord();
            CancelMatchingBindingRitual(pawn, savedLord);
            // 只取消调用前已有的场次；取消回调新加入的仪式不属于本次切换的旧状态。
            if (currentLord != savedLord) CancelMatchingBindingRitual(pawn, currentLord);
            Lord remainingLord = pawn.GetLord();
            // 原场次正常结束可以变为空；新场次则必须保留其身份与占用，不运行旧切换的清理。
            return (remainingLord == null || remainingLord == currentLord) &&
                (comp.bindingRitualLord == null || comp.bindingRitualLord == savedLord);
        }

        private static void CancelMatchingBindingRitual(Pawn pawn, Lord ritualLord)
        {
            if (!(ritualLord?.LordJob is LordJob_Ritual ritual)) return;
            Pawn actor = ritual.PawnWithRole("master");
            Pawn target = ritual.PawnWithRole("slave");
            if (pawn != actor && pawn != target) return;
            BindingRitualStateUtility.RejectPhase(actor, target, ritualLord,
                "SSC_Identity_RitualRoleChanged".Translate());
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
