using System.Linq;
using Verse;
using UnityEngine;

// EN: This hediff is the sex-slave side of ChainOfSexSlave.
// EN: It stores the linked master, shows the master name in the label, and tracks how far the slave chain has tightened.
// CN: 这个 Hediff 就是 ChainOfSexSlave 里“性奴这一侧”的锁链。
// CN: 它会保存关联主人、在标签里显示主人的名字，并记录锁链束缚已经推进到什么程度。
namespace SexSlaveCraft
{
    public class Hediff_ChainOfSexSlave : HediffWithTarget
    {
        // 仅缓存上次观察的引用，不保存新归属；加载后的真实 target 仍由基类恢复。
        private Pawn observedMaster;
        public Pawn LinkedPawn => target as Pawn;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            observedMaster = LinkedPawn;
#if SSC_RW17
            // 健康列表已持有这条链后再观察，不能把尚未入体的实例当作当前绑定。
            SSCRomanceLifecycle.Notify(pawn);
#endif
            SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);
            ModLog.Message($"[ChainOfSexSlave] Added to {pawn}, linked with {LinkedPawn ?? target}");
        }

        /// <summary>兼容外部直接改写公开 target 的路径，在下一次锁链 Tick 整理旧关系及反向索引。</summary>
        public override void Tick()
        {
            base.Tick();
            // 健康组件回调可能已替换这条锁链，旧实例不能继续整理新绑定或重建缰绳。
            if (pawn?.health?.hediffSet == null || !pawn.health.hediffSet.hediffs.Contains(this)) return;
            Pawn currentMaster = LinkedPawn;
            if (observedMaster == currentMaster) return;
            Pawn previousMaster = observedMaster;
            observedMaster = currentMaster;
#if SSC_RW17
            // 外部改 target 被检测到时立即撤权，后面的反向索引修复不能延续旧主人授权。
            SSCRomanceLifecycle.Notify(pawn);
#endif
            SSCBondUtility.GetBridle(previousMaster)?.RemoveTarget(pawn);
            SSCBondRelationUtility.RemoveInvalidRelations(pawn);
            SSCBondUtility.RepairReciprocalLink(pawn);
        }

        public override string LabelBase
        {
            get
            {
                if (LinkedPawn != null)
                {
                    // EN: Use the shared keyed string so the ChainOfSexSlave label keeps the same in-game format everywhere.
                    // CN: 这里调用统一的本地化字符串，保证 ChainOfSexSlave 的标签在所有地方都保持同一种游戏内格式。
                    return Strings.Chain_LabelWithPawn(base.LabelBase, LinkedPawn.LabelShortCap);
                }
                return base.LabelBase;
            }
        }

        public override bool ShouldRemove
        {
            get
            {
                bool remove = false;
                if (target == null || target.Destroyed)
                {
                    ModLog.Message($"[ChainOfSexSlave] Removing from {pawn}: target is null or destroyed.");
                    remove = true;
                }

                if (LinkedPawn != null && (LinkedPawn.DestroyedOrNull() || LinkedPawn.Discarded))
                {
                    ModLog.Message($"[ChainOfSexSlave] Removing from {pawn}: linked pawn {LinkedPawn} invalid.");
                    remove = true;
                }

                return remove;
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            ReconcileRemoval();
        }

        /// <summary>只在本锁链已离体时维护归属，兼容 CE 跳过 PostRemoved 后发送健康变化通知的删除入口。</summary>
        internal void ReconcileRemoval()
        {
            // 正常严重度变化也会发出通知；仍在身体上的锁链不触发解绑维护。
            if (pawn?.health?.hediffSet == null || pawn.health.hediffSet.hediffs.Contains(this)) return;
#if SSC_RW17
            // 以现在实际持有的链比较；删除旧实例时已存在的新链不能被误认为同一授权。
            SSCRomanceLifecycle.Notify(pawn);
#endif
            Pawn formerMaster = LinkedPawn;
            Pawn currentMaster = SSCBondUtility.GetBoundMaster(pawn);
            // 删除旧实例后可能已经有新锁链；仍由新链指向的主人应保留反向索引和仪式奖励。
            if (formerMaster != currentMaster) SSCBondUtility.GetBridle(formerMaster)?.RemoveTarget(pawn);
            // 外部先改 target 再同帧删除时，两任主人的反向引用都需要整理。
            if (observedMaster != formerMaster && observedMaster != currentMaster)
                SSCBondUtility.GetBridle(observedMaster)?.RemoveTarget(pawn);
            // 覆盖开发模式和外部模组直接删锁链的入口，不依赖它们调用统一 Unbind 服务。
            SSCBondRelationUtility.RemoveInvalidRelations(pawn);
            // 包含外部模组直接移除锁链的路径；Unbind 内的事务会延迟本次维护。
            TrainerSpecializationLifecycle.Notify(pawn);
            SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);
        }

        public static Hediff_ChainOfSexSlave AddToPawn(Pawn SexSlave, Pawn Master)
        {
            if (SexSlave == null || Master == null)
            {
                ModLog.Message("[ChainOfSexSlave] AddToPawn failed: null host or target.");
                return null;
            }

            HediffDef def = SSCDefOf.ChainOfSexSlave;
            if (def == null) return null;
            var hediff = SexSlave.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_ChainOfSexSlave;

            if (hediff == null)
            {
                hediff = (Hediff_ChainOfSexSlave)HediffMaker.MakeHediff(def, SexSlave);
                // EN: The target field is the real master link used by every ChainOfSexSlave check in SSC.
                // CN: 这里的 target 字段就是 SSC 所有 ChainOfSexSlave 判定真正使用的主人引用。
                hediff.target = Master;
                SexSlave.health.AddHediff(hediff);
                ModLog.Message($"[ChainOfSexSlave] Created new link: {SexSlave} ↔ {Master}");
            }
            else
            {
                Pawn previousMaster = hediff.LinkedPawn;
                hediff.target = Master;
                hediff.observedMaster = Master;
#if SSC_RW17
                // 先记录真实换主，再执行可能重入的缰绳／关系清理；回调恢复原主人也不能掩盖这次撤权。
                SSCRomanceLifecycle.Notify(SexSlave);
#endif
                // AddToPawn 也可能被绕过 Bind 直接调用，换主时仍须清除旧主人的反向引用。
                if (previousMaster != Master) SSCBondUtility.GetBridle(previousMaster)?.RemoveTarget(SexSlave);
                ModLog.Message($"[ChainOfSexSlave] Updated existing link: {SexSlave} ↔ {Master}");
            }

            // 新建或改写 target 后，以当前实际锁链清除过期主人关系，不创建新的奖励关系。
            SSCBondRelationUtility.RemoveInvalidRelations(SexSlave);
#if SSC_RW17
            // 覆盖绕过 Bind 直接调用 AddToPawn 的换主路径，不等待下一次锁链 Tick。
            SSCRomanceLifecycle.Notify(SexSlave);
#endif
            return hediff;
        }

        public static Hediff_ChainOfSexSlave IncreaseChainSeverity(Pawn pawn, float amount)
        {
            if (pawn == null)
            {
                ModLog.Message("[ChainOfSexSlave] IncreaseChainSeverity failed: pawn null.");
                return null;
            }

            HediffDef def = SSCDefOf.ChainOfSexSlave;
            if (def == null) return null;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_ChainOfSexSlave;

            if (hediff == null)
            {
                ModLog.Message($"[ChainOfSexSlave] IncreaseChainSeverity failed: {pawn} has no Chain Hediff.");
                return null;
            }

            float oldSeverity = hediff.Severity;
            int oldStage = SSCIdentityUtility.GetSexSlaveStageFromChain(hediff);
            hediff.Severity = Mathf.Clamp(oldSeverity + amount, 0f, hediff.def.maxSeverity);
#if SSC_RW17
            // 原入口也显式通知，避免健康阶段被外部改写后漏掉 SSC 自身的第四阶段退阶。
            if (oldStage != SSCIdentityUtility.GetSexSlaveStageFromChain(hediff)) SSCRomanceLifecycle.Notify(pawn);
#endif
            if (oldStage != SSCIdentityUtility.GetSexSlaveStageFromChain(hediff))
                SSCSelfTrainingFeedback.NotifyStatusChanged(pawn);

            // 只有跨越训导官所需的第 3 阶段边界时才执行状态维护。
            // 普通锁链成长不会因每次严重度写入都检查特化和绑定。
            if ((oldSeverity >= 0.5f) != (hediff.Severity >= 0.5f))
                TrainerSpecializationLifecycle.Notify(pawn);

            ModLog.Message($"[ChainOfSexSlave] Severity for {pawn} increased {oldSeverity:F2} → {hediff.Severity:F2}");

            return hediff;
        }
    }
}
