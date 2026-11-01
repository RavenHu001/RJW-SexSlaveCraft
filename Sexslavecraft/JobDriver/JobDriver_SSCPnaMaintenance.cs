// AI 辅助生成：服用动作完整沿用原版，只增加原锁链请求的执行与存档校验。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>复用原版完整服用动作，仅追加原请求的生命周期保护和消费完成标志。</summary>
    public sealed class JobDriver_SSCPnaMaintenance : JobDriver_Ingest
    {
        private SSCPnaMaintenanceContext context;
        private bool doseApplied;
        /// <summary>供设置变更精确识别本驱动的原链，避免取消另一条链的任务。</summary>
        internal bool Owns(Hediff_ChainOfSexSlave chain) => context?.Chain == chain;

        /// <summary>保留父类摄入／Toil进度，再保存原请求及消费标志；读档不重新领取候选授权。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref context, "sscPnaMaintenanceContext");
            Scribe_Values.Look(ref doseApplied, "sscPnaMaintenanceDoseApplied", false);
        }

        /// <summary>实际启动时领取附着于Job的原凭据，再由父类判断摄入来源。</summary>
        public override void Notify_Starting()
        {
            context = SSCPnaMaintenanceUtility.Claim(this);
            base.Notify_Starting();
        }

        /// <summary>构建父类动作并包装唯一消费步骤；失格在摄入前拒绝，已消费后正常收尾。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 不调用CanStart：执行自己的补药不是空闲，资格与任务发起守卫必须分开。
            this.FailOn(() => !SSCPnaMaintenanceUtility.CanContinue(context, this, checkNeed: !doseApplied));
            // 保留父类动作与收尾；只在实际消费前复查，并在消费后停止用“已达标”误判本次成功。
            var toils = base.MakeNewToils().ToList();
            // 当前原版恰有一个FinalizeIngest；若后续构建改变此约定，明确失败，避免包装错误动作继续吃药。
            var finalizers = toils.Where(toil => toil.debugName == nameof(Toils_Ingest.FinalizeIngest)).ToList();
            if (finalizers.Count != 1) throw new InvalidOperationException("PNA maintenance requires one vanilla FinalizeIngest toil.");
            var originalIngest = finalizers[0].initAction;
            finalizers[0].initAction = () =>
            {
                if (!SSCPnaMaintenanceUtility.CanContinue(context, this))
                {
                    // 迟到的旧Toil不能结束已经替换上来的其他任务。
                    if (pawn.jobs.curDriver == this && pawn.CurJob == job) EndJobWith(JobCondition.Incompletable);
                    return;
                }
                originalIngest();
                // 父类摄入可能触发健康或外部回调；只标记仍属于本驱动的任务，不向替换任务写完成状态。
                if (pawn.jobs.curDriver == this && pawn.CurJob == job) doseApplied = true;
            };
            foreach (Toil toil in toils) yield return toil;
        }
    }
}
