// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>同一任务承载接近、RJW场景及完成后技能；取消清理从不提交受孕。</summary>
    public class JobDriver_FertilityConception : JobDriver_SexBaseInitiator
    {
        // 接收任务只认本场安装的实例和loadID；不按JobDef清理另一场或后来的玩家工作。
        private Job receiverJob;
        private int receiverId;
        // 完成凭据只在当前进程内产生。读档取消；FinishAction不能补造正常完成事实。
        private bool sceneStarted, normalCompletion, completionClaimed, outcomeCompleted, cleaned;
        internal bool SubmittingCompletion { get; private set; }
        private FertilityAbilityUtility.Context Context => FertilityAbilityUtility.Request(job);

        /// <summary>原请求、原Job世代和实际发起驱动均需保留；已结束或换人格的任务不能继续结算。</summary>
        internal bool Current => !ended && pawn?.jobs?.curDriver == this && pawn.CurJob == job && FertilityAbilityUtility.IsCurrent(Context);

        /// <summary>归属只用于精确清理；接收驱动是否已结束另在持续资格中判断。</summary>
        internal bool OwnReceiver => receiverJob != null && receiverJob.loadID == receiverId && Partner?.CurJob == receiverJob &&
            Partner.jobs.curDriver is JobDriver_FertilityReceiver r && r.Partner == pawn;

        /// <summary>受方只接受实际安装它的原发起任务；读档或发起者清理后自行退出。</summary>
        internal bool ReceiverValid(Job j) => !cleaned && Current && receiverJob == j && j.loadID == receiverId;

        /// <summary>开始及结算复用实时身体／孕态前置；已结束但尚未换出的受方也视为中断。</summary>
        private bool CanContinue => Current && !pawn.Downed && !pawn.InMentalState && pawn.health.capacities.CanBeAwake &&
            Partner != null && !Partner.Downed && !Partner.InMentalState && Partner.health.capacities.CanBeAwake &&
            FertilityAbilityUtility.CanTarget(pawn, Partner, out _) &&
            (receiverJob == null || (OwnReceiver && !Partner.jobs.curDriver.ended));

        /// <summary>未完成命令仅有进程内原请求；读档不重新Capture，双方任务均安全取消。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            FertilityAbilityUtility.IsCurrent(Context) && FertilityAbilityUtility.CanTarget(pawn, Partner, out _) &&
            (!SSCRestrictionJobGuard.TryReserve(this, out bool allowed) || allowed) &&
            pawn.Reserve(Partner, job, 1, 0, null, errorOnFailed);

        /// <summary>接近、同场行为及成功提交在一个Job内完成；所有退出共用只清理的FinishAction。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 成功受孕会使CanContinue变为false；已认领的结算尾段不能因此被当作未完成性交。
            this.FailOn(() => !Current || (!completionClaimed && !CanContinue));
            AddFinishAction(_ => CleanupScene());
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);
            yield return Toils_General.Do(BeginScene);
            // 不用CastVerb或独立预热；真实SexTick耗尽后才转到最后的Instant结算步骤。
            var scene = ToilMaker.MakeToil("FertilityScene");
            scene.defaultCompleteMode = ToilCompleteMode.Never;
            scene.socialMode = RandomSocialMode.Off;
            scene.handlingFacing = true;
            scene.tickAction = TickScene;
            yield return scene;
            yield return Toils_General.Do(() => { try { CompleteScene(); } finally { CleanupScene(); } });
        }

        /// <summary>原对象可中断时建立专属接收任务；回调后复查原人格与工作，不调用StopAll。</summary>
        internal void BeginScene()
        {
            if (!CanContinue || !FertilityAbilityUtility.CanApply(pawn, Partner) || !TargetAvailable())
            { EndJobWith(JobCondition.Incompletable); return; }
            Job previous = Partner.CurJob; int previousId = previous?.loadID ?? -1;
            try
            {
                setup_ticks();
                beatings = beatonce = neverendingsex = false;
                Sexprops = SexUtility.SelectSextype(pawn, Partner, false, false);
                if (Sexprops == null) throw new InvalidOperationException("No consensual interaction");
                // 预先选好的SexProps会跳过原版Start的选取分支，须同步其原有反向动作标志。
                if (Sexprops.dictionaryKey != null)
                    Sexprops.isRevese = Sexprops.interaction.HasInteractionTag(SexInteractionTag.Reverse);
                FertilitySceneUtility.Protect(Sexprops);
                if (!CanContinue || Partner.CurJob != previous || (Partner.CurJob?.loadID ?? -1) != previousId ||
                    !TargetAvailable() || (SSCRestrictionJobGuard.TryCheck(this, "Start", out bool allowed) && !allowed)) return;
                // 许可查询也可能调用外部代码，不能在回调后替换人格或工作时继续安装接收任务。
                if (!CanContinue || Partner.CurJob != previous || (Partner.CurJob?.loadID ?? -1) != previousId || !TargetAvailable()) return;
                // 先记归属再安装，StartJob／Start回调拒绝或中途换工作时仍可精确释放本场受方。
                var def = DefDatabase<JobDef>.GetNamed("SSC_Job_FertilityReceiver");
                receiverJob = JobMaker.MakeJob(def, pawn); receiverId = receiverJob.loadID;
                Partner.jobs.StartJob(receiverJob, JobCondition.InterruptForced);
                if (!CanContinue || !OwnReceiver) return;
                pawn.pather.StopDead(); Partner.pather.StopDead();
                Start();
                sceneStarted = Current && OwnReceiver && SSCRestrictionJobGuard.HasStartedScene(this, SSCInteractionKind.Consensual);
                if (sceneStarted) FertilitySceneUtility.Protect((Partner.jobs.curDriver as JobDriver_Sex)?.Sexprops);
            }
            catch (Exception ex) { Log.Warning("[SSC] Fertility scene start: " + ex.GetType().Name); }
            finally { if (!sceneStarted && pawn.jobs?.curDriver == this) EndJobWith(JobCondition.Incompletable); }
        }

        /// <summary>手动技能只接管普通可中断工作；保留原互动许可，不叠加自动邀约的性欲／吸引力预检。</summary>
        private bool TargetAvailable() => Partner?.jobs != null && !Partner.Downed && Partner.health.capacities.CanBeAwake &&
            !Partner.InMentalState && Partner.jobs.curDriver is not JobDriver_Sex && !Partner.jobs.jobQueue.Any() &&
            Partner.CurJob?.playerForced != true && Partner.CurJob?.def.forceCompleteBeforeNextJob != true &&
            Partner.jobs.IsCurrentJobPlayerInterruptible();

        /// <summary>持续推进实际RJW计时；仅本Tick观察到正常耗尽才建立完成凭据。</summary>
        internal void TickScene()
        {
            if (!sceneStarted || !CanContinue) { EndJobWith(JobCondition.Incompletable); return; }
            SexTick(pawn, Partner);
            if (!CanContinue) return;
            SexUtility.reduce_rest(Partner); SexUtility.reduce_rest(pawn);
            if (CanContinue && ticks_left <= 0) { normalCompletion = true; ReadyForNextToil(); }
        }

        /// <summary>认领正常完成后先处理RJW结果，再一次性提交技能；失败、重入、重复回调都不补交。</summary>
        internal bool CompleteScene()
        {
            if (cleaned || completionClaimed || !normalCompletion || !sceneStarted || ticks_left > 0 || !CanContinue) return false;
            completionClaimed = true;
            // 先认领后调用外部结算；回调若换人格或工作，后续技能提交必须取消，不能再次领奖。
            SexUtility.ProcessSex(Sexprops);
            if (!CanContinue) return false;
            outcomeCompleted = true;
            SubmittingCompletion = true;
            try { return job.ability is Ability_FertilityConception a && a.Activate(Partner, LocalTargetInfo.Invalid); }
            finally { SubmittingCompletion = false; }
        }

        /// <summary>幂等清理本场；仅正常完成才调用带有结锁等后续行为的原生End，中断只回收现场。</summary>
        internal void CleanupScene()
        {
            if (cleaned) return;
            cleaned = true;
            bool owns = OwnReceiver;
            // 动画或第三方可能在Start后替换SexProps；清理前登记当前引用，迟到受孕仍属于旧场。
            FertilitySceneUtility.Protect(Sexprops);
            if (owns) FertilitySceneUtility.Protect(((JobDriver_Sex)Partner.jobs.curDriver).Sexprops);
            try
            {
                if (sceneStarted && outcomeCompleted && owns && !Partner.jobs.curDriver.ended && pawn.jobs?.curDriver == this)
                    End();
            }
            finally
            {
                // End或外观回收异常也必须释放确切受方；每次回调后复查，不能结束后来换入的任务。
                try { if (pawn.jobs?.curDriver == this) FertilitySceneUtility.ClearAppearance(pawn); }
                finally
                {
                    if (owns && OwnReceiver)
                    {
                        var r = (JobDriver_FertilityReceiver)Partner.jobs.curDriver;
                        r.parteners.Remove(pawn);
                        try { FertilitySceneUtility.ClearAppearance(Partner); }
                        finally
                        {
                            if (OwnReceiver) Partner.jobs.EndCurrentJob(JobCondition.Incompletable, startNewJob: false);
                        }
                    }
                }
            }
        }
    }

    /// <summary>专用受方不依赖原Loved驱动的JobDef白名单，也不把征召施法者判成失效。</summary>
    public class JobDriver_FertilityReceiver : JobDriver_SexBaseReciever
    {
        /// <summary>只维持专属等待；原发起请求失效即退出，不在受方计时或清理时提交技能。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks(); parteners.AddDistinct(Partner);
            this.FailOn(() => Partner?.jobs?.curDriver is not JobDriver_FertilityConception d || !d.ReceiverValid(job));
            AddFinishAction(_ => { if (pawn.jobs?.curDriver == this) FertilitySceneUtility.ClearAppearance(pawn); });
            var wait = ToilMaker.MakeToil("FertilityReceiver");
            wait.defaultCompleteMode = ToilCompleteMode.Never; wait.socialMode = RandomSocialMode.Off;
            yield return wait;
        }
    }

    /// <summary>只供旧存档解析原curDriver类型与JobDef；不恢复独立后续性交，不重交已成功技能。</summary>
    public class JobDriver_FertilityFollowup : JobDriver_SexBaseInitiator
    {
        /// <summary>旧队列命令拒绝启动，旧存档仅保留类型解析能力。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) => false;

        /// <summary>已保存为当前驱动的旧任务立即取消，不调用Start／End或重放已成功建孕。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(_ => { if (pawn.jobs?.curDriver == this) FertilitySceneUtility.ClearAppearance(pawn); });
            yield return Toils_General.Do(() => EndJobWith(JobCondition.Incompletable));
        }
    }
}
