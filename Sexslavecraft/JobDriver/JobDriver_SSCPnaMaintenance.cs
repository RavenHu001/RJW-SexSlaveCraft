// AI 辅助生成：服用动作完整沿用原版，只增加原锁链请求的执行与存档校验。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    public sealed class JobDriver_SSCPnaMaintenance : JobDriver_Ingest
    {
        private SSCPnaMaintenanceContext context;
        private bool doseApplied;
        internal bool Owns(Hediff_ChainOfSexSlave chain) => context?.Chain == chain;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref context, "sscPnaMaintenanceContext");
            Scribe_Values.Look(ref doseApplied, "sscPnaMaintenanceDoseApplied", false);
        }

        public override void Notify_Starting()
        {
            context = SSCPnaMaintenanceUtility.Claim(this);
            base.Notify_Starting();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 不调用CanStart：执行自己的补药不是空闲，资格与任务发起守卫必须分开。
            this.FailOn(() => !SSCPnaMaintenanceUtility.CanContinue(context, this, checkNeed: !doseApplied));
            // 保留父类动作与收尾；只在实际消费前复查，并在消费后停止用“已达标”误判本次成功。
            var toils = base.MakeNewToils().ToList();
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
                if (pawn.jobs.curDriver == this && pawn.CurJob == job) doseApplied = true;
            };
            foreach (Toil toil in toils) yield return toil;
        }
    }
}
