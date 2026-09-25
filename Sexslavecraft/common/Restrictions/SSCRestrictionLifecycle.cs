using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>集中处理配置写入事件；查询器不调用本类，不在每次许可检查时初始化或修复。</summary>
    public static class SSCRestrictionLifecycle
    {
        /// <summary>读取当前有效公交车条件；历史进度本身不算适用状态。</summary>
        public static bool HasBus(Pawn pawn)
        {
            return pawn?.TryGetComp<CompSexSlaveTraining>()?.specializationType == SexSlaveSpecializationType.Bus ||
                BusSpecializationUtility.HasAnyBusState(pawn);
        }

        /// <summary>在完整生命周期事件后迁移或初始化，并只在首次真正取得公交车时应用一次默认。</summary>
        public static bool Refresh(Pawn pawn, SSCRestrictionLegacySettings legacy, out SSCRestrictionResolution error)
        {
            error = null;
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0) return false;
            // 未绑定时只保留已有数据与待迁移输入，不初始化、不合入新特化默认，也不把旧配置视作异常。
            // 绑定完成后的同一生命周期入口会继续迁移或初始化，首次生效采用那一刻的模板。
            if (!SSCRestrictionResolver.IsApplicable(pawn)) return false;
            if (comp.restrictionConfig == null)
            {
                if (comp.legacyRestrictionInput != null)
                {
                    // 尚未固定本存档快照时必须等待，不能偷用稍后可能改变的全局设置。
                    if (legacy == null) return false;
                    comp.restrictionConfig = legacy.Convert(comp.legacyRestrictionInput.open, comp.legacyRestrictionInput.bus);
                    comp.legacyRestrictionInput = null;
                }
                else
                {
                    if (!SSCRestrictionConfigurationBuilder.TryCreateInitial(pawn, SSCMod.settings?.restrictionDefaults,
                        out SSCRestrictionConfig initial, out error)) return false;
                    comp.restrictionConfig = initial;
                }
                comp.restrictionLifecycleSeen = true;
            }

            SSCRestrictionConfig config = comp.restrictionConfig;
            if (!config.IsValid()) return false;
            if (!config.busDefaultsApplied && HasBus(pawn))
            {
                if (!SSCRestrictionConfigurationBuilder.TryApplyBusDefaults(pawn, config,
                    out SSCRestrictionConfig candidate, out error)) return false;
                comp.restrictionConfig = candidate;
            }
            return true;
        }

        /// <summary>暂缓人格恢复中的中间事件；已有身体配置不接受植入人格的默认写入。</summary>
        public static IDisposable BeginRestore(Pawn pawn)
        {
            return new RestoreScope(pawn);
        }

        /// <summary>清除整只复制带来的配置、迁移与默认标记，让新克隆在自己的绑定生效时按当前模板初始化。</summary>
        public static void ResetNewClone(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;
            comp.restrictionConfig = null;
            comp.legacyRestrictionInput = null;
            comp.restrictionLifecycleSeen = true;
            comp.restrictionRestoreDepth = 0;
            SSCRestrictionGameComponent.Notify(pawn);
        }

        /// <summary>为当前存档目标应用一份模板快照；去重、跳过不适用对象，逐个原子替换并报告结果。</summary>
        public static SSCRestrictionBatchResult ApplyDefaults(IEnumerable<Pawn> pawns, SSCRestrictionRules template)
        {
            var result = new SSCRestrictionBatchResult();
            SSCRestrictionRules snapshot = template?.Copy();
            foreach (Pawn pawn in (pawns ?? Enumerable.Empty<Pawn>()).Where(p => p != null).Distinct().ToList())
            {
                CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
                if (comp == null || !SSCRestrictionResolver.IsApplicable(pawn)) { result.Skipped++; continue; }
                if (comp.restrictionRestoreDepth > 0 || snapshot == null ||
                    !SSCRestrictionConfigurationBuilder.TryCreateInitial(pawn, snapshot, out SSCRestrictionConfig config, out _))
                { result.Failed++; continue; }
                comp.restrictionConfig = config;
                comp.legacyRestrictionInput = null;
                comp.restrictionLifecycleSeen = true;
                CoordinateTrainer(pawn);
                result.Applied++;
            }
            return result;
        }

        /// <summary>读取新配置对指定者选择的锁定；总开关关闭时解除锁定，损坏配置不擅自改关系。</summary>
        public static Pawn GetForcedTrainer(Pawn pawn)
        {
            return SSCRestrictionTrainingUtility.GetForcedTrainer(pawn);
        }

        /// <summary>只清理失效引用；开启限制且禁止非主人调教时改回主人，不恢复隐藏历史指定者。</summary>
        public static void CoordinateTrainer(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0) return;
            if (comp.selectedTrainer != null && (comp.selectedTrainer.Dead || comp.selectedTrainer.Destroyed))
                comp.selectedTrainer = null;
            Pawn forced = GetForcedTrainer(pawn);
            // GetForcedTrainer 表达“条目只准主人”，即使主人死亡仍用于界面锁定。
            // 实际工作指派不得写回死亡/销毁角色；保留锁链和条目，不借清理操作改变所有权或开放第三方。
            if (forced != null && !forced.Dead && !forced.Destroyed) comp.selectedTrainer = forced;
        }

        /// <summary>在最外层恢复完成或异常退出时刷新实际保留状态，确保嵌套操作不会提前写入默认。</summary>
        private sealed class RestoreScope : IDisposable
        {
            private readonly Pawn pawn;
            private readonly CompSexSlaveTraining comp;
            private readonly bool hadConfiguration;
            private bool disposed;

            /// <summary>记录接收身体是否已有配置并递增恢复深度，不复制人格来源的限制数据。</summary>
            public RestoreScope(Pawn pawn)
            {
                this.pawn = pawn;
                comp = pawn?.TryGetComp<CompSexSlaveTraining>();
                if (comp == null) return;
                hadConfiguration = comp.restrictionConfig != null;
                comp.restrictionRestoreDepth++;
            }

            /// <summary>无论成功或异常均结束恢复；已有配置的公交车默认记为已处理，保存选择保持不变。</summary>
            public void Dispose()
            {
                if (disposed || comp == null) return;
                disposed = true;
                comp.restrictionRestoreDepth--;
                if (comp.restrictionRestoreDepth != 0) return;
                if (hadConfiguration && comp.restrictionConfig?.IsValid() == true && HasBus(pawn))
                    comp.restrictionConfig.busDefaultsApplied = true;
                // 人格恢复中的身份、绑定和特化通知均已延迟；最外层结束时
                // 再按接收身体的最终状态退出普通方向或切换终极标记。
                TrainerSpecializationLifecycle.Maintain(pawn);
                SSCRestrictionGameComponent.Notify(pawn);
            }
        }
    }

    /// <summary>批量操作的完整计数，供设置界面报告成功、跳过和失败。</summary>
    public sealed class SSCRestrictionBatchResult
    {
        public int Applied;
        public int Skipped;
        public int Failed;
    }
}
