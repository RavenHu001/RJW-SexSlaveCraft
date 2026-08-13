using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    // EN: A tiny, non-workgiver job for pet affection. The comp schedules it only when the pet is already idle.
    // CN: 宠物亲昵的小型 job。由组件在宠物空闲时定时发起，不走 WorkGiver，避免和工作优先级抢权。
    public class JobDriver_PetAffection : JobDriver
    {
        private const int AffectionDurationTicks = 45;

        private Pawn Master => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // EN: This is only a brief rub/turn-towards action. Reserving the master would interfere with real work.
            // CN: 这里只是短暂蹭一下 / 转向主人，不预定主人，避免影响其他真实工作。
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !PetSpecializationUtility.CanDoPetAffectionNow(pawn, Master));

            Toil affection = Toils_General.Wait(AffectionDurationTicks, TargetIndex.A);
            affection.handlingFacing = true;
            affection.tickAction = delegate
            {
                Pawn master = Master;
                if (master != null)
                {
                    pawn.rotationTracker.FaceTarget(master);
                }
            };
            yield return affection;

            Toil finish = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            finish.initAction = delegate
            {
                PetSpecializationUtility.CompletePetAffection(pawn, Master);
            };
            yield return finish;
        }
    }
}
