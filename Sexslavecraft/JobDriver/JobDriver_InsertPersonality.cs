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

        private Thing Gel => job.GetTarget(TargetIndex.A).Thing;
        private Pawn Hollow => (Pawn)job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Gel, job, 1, -1, null, errorOnFailed))
                return false;
            if (!pawn.Reserve(Hollow, job, 1, -1, null, errorOnFailed))
                return false;
            return true;
        }

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

            // --- Toil 4: 等待（进度条，模拟塞入过程） ---
            Toil waitToil = Toils_General.Wait(InsertDuration, TargetIndex.B);
            waitToil.WithProgressBarToilDelay(TargetIndex.B);
            waitToil.handlingFacing = true;
            waitToil.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(Hollow);
            };
            yield return waitToil;

            // --- Toil 5: 执行塞入 ---
            Toil insertToil = new Toil();
            insertToil.defaultCompleteMode = ToilCompleteMode.Instant;
            insertToil.initAction = delegate
            {
                DoInsert();
            };
            yield return insertToil;
        }

        /// <summary>
        /// 核心逻辑：将凝胶中的人格数据继承给空壳Pawn，然后销毁凝胶。
        /// </summary>
        private void DoInsert()
        {
            Thing gel = Gel;
            Pawn hollow = Hollow;

            if (gel == null || hollow == null)
            {
                Log.Error("[SSC] JobDriver_InsertPersonality.DoInsert: 凝胶或目标为null！");
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
