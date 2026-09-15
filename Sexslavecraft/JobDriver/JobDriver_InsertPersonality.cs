using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>
    /// 殖民者拿起人格凝胶，走向空壳Pawn，将凝胶塞入其体内。
    /// TargetA = 人格凝胶 (Thing)
    /// TargetB = 空壳Pawn (Pawn)
    /// </summary>
    public class JobDriver_InsertPersonality : JobDriver
    {
        private const int InsertDuration = 600; // 约10秒

        /// <summary>从任务目标 A 取得本次植入使用的人格凝胶。</summary>
        private Thing Gel => job.GetTarget(TargetIndex.A).Thing;
        /// <summary>从任务目标 B 取得接受人格植入的空壳角色。</summary>
        private Pawn Hollow => (Pawn)job.GetTarget(TargetIndex.B).Thing;

        /// <summary>依次预约人格凝胶和空壳角色，任一预约失败时拒绝启动任务。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Gel, job, 1, -1, null, errorOnFailed))
                return false;
            if (!pawn.Reserve(Hollow, job, 1, -1, null, errorOnFailed))
                return false;
            return true;
        }

        /// <summary>生成取凝胶、接近空壳、双方等待及最终植入步骤，并登记目标有效性检查。</summary>
        /// <remarks>接触检查在双方等待阶段生效，保留前往目标的正常行走流程。</remarks>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // --- 失败条件 ---
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            // 如果目标不再是空壳，中止
            this.FailOn(() =>
            {
                return Hollow == null
                    || !Hollow.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            });

            // --- Toil 1: 走向凝胶 ---
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.A);

            // --- Toil 2: 拾取凝胶 ---
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, putRemainderInQueue: false, subtractNumTakenFromJobCount: false);

            // --- Toil 3: 搬运凝胶走向空壳Pawn ---
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B);

            // --- Toil 4: 双方等待；原版 WaitWith 持续检查目标仍可接触。 ---
            yield return Toils_General.WaitWith(
                TargetIndex.B, InsertDuration, useProgressBar: true,
                maintainPosture: true, maintainSleep: true,
                face: TargetIndex.B, pathEndMode: PathEndMode.Touch);

            // --- Toil 5: 执行塞入 ---
            Toil insertToil = new Toil();
            insertToil.defaultCompleteMode = ToilCompleteMode.Instant;
            // 最终步骤的初始化回调：交由 DoInsert 重新校验并执行人格写入。
            insertToil.initAction = delegate
            {
                DoInsert();
            };
            yield return insertToil;
        }

        /// <summary>
        /// 重新校验同图、接触及空壳状态后执行人格继承；成功时添加昏迷、清除分配并确保凝胶已消耗。
        /// </summary>
        /// <remarks>最终状态校验失败时结束任务，在人格写入和凝胶消耗之前返回。</remarks>
        private void DoInsert()
        {
            Thing gel = Gel;
            Pawn hollow = Hollow;

            if (gel == null || hollow == null)
            {
                Log.Error("[SSC] JobDriver_InsertPersonality.DoInsert: 凝胶或目标为null！");
                return;
            }

            // 写入人格、消耗凝胶前再次校验，防止等待结束后的目标变化绕过接触检查。
            if (!pawn.Spawned || !hollow.Spawned || hollow.Dead || pawn.Map != hollow.Map
                || !hollow.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done)
                || !ReachabilityImmediate.CanReachImmediate(pawn, hollow, PathEndMode.Touch))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            // 1. 获取人格数据
            var storeComp = gel.TryGetComp<CompPersonalityStore>();
            if (storeComp == null)
            {
                Log.Error("[SSC] JobDriver_InsertPersonality.DoInsert: 凝胶缺少 CompPersonalityStore！");
                return;
            }

            // 2. 执行人格继承
            bool success = ExcretionUtility.InheritEverything(hollow, storeComp);
            if (!success)
            {
                Log.Error($"[SSC] JobDriver_InsertPersonality.DoInsert: 人格继承失败，pawn={hollow.LabelShort}");
                Messages.Message("SSC_Message_PersonalityInsertFailed".Translate(), hollow, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // 3. 添加人格植入适应症（昏迷）
            if (SSCDefOf.SSC_PostInsertionComa != null)
            {
                Hediff coma = hollow.health.AddHediff(SSCDefOf.SSC_PostInsertionComa);
                if (coma != null)
                {
                    coma.Severity = 0.5f;
                }
            }

            // 4. 清除分配记录
            var mapComp = pawn.Map?.GetComponent<MapComponent_PersonalityAssignment>();
            mapComp?.Unassign(gel);

            // 4. 销毁凝胶
            if (pawn.carryTracker != null && pawn.carryTracker.CarriedThing == gel)
            {
                pawn.carryTracker.DestroyCarriedThing();
            }
            else if (!gel.Destroyed)
            {
                gel.Destroy();
            }

            // 5. 发送消息
            Messages.Message(
                Strings.Message_PersonalityInserted(hollow.LabelShort),
                hollow,
                MessageTypeDefOf.PositiveEvent);
        }
    }
}
