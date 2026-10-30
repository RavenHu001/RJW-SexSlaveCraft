// AI 辅助生成
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>原版接触技能的接近、预热与目标暂停；只在成功建孕时冷却，未提交的读档任务安全取消。</summary>
    public class JobDriver_FertilityConception : JobDriver_CastAbility
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => FertilityAbilityUtility.IsCurrent(FertilityAbilityUtility.Request(job));

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var c = FertilityAbilityUtility.Request(job);
            this.FailOn(() => !FertilityAbilityUtility.IsCurrent(c));
            AddFinishAction(_ =>
            {
                if (pawn.jobs?.curDriver != this) return;
                if (pawn.stances?.curStance is Stance_Warmup w && w.verb == job.verbToUse) w.Interrupt();
                job.verbToUse?.Reset();
            });
            // 与原版JobDriver_CastAbilityMelee相同，追踪所选Pawn走至Touch；不可达沿寻路失败退出。
            // 不整体调用基类Toils，避免成功孕态使CanCast禁用后阻断本次独立联动交接。
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .FailOn(() => !job.ability.CanCast || !FertilityAbilityUtility.CanTarget(pawn, c?.Target, out _));
            yield return Toils_General.Do(() => pawn.pather.StopDead());
            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, false);
            cast.FailOn(() => job.verbToUse.WarmingUp && !FertilityAbilityUtility.CanApply(pawn, c?.Target));
            cast.WithProgressBar(TargetIndex.A, () => job.verbToUse.WarmupProgress);
            yield return cast;
            yield return Toils_General.Do(() =>
            {
                if (job.ability is Ability_FertilityConception a && a.SuccessfulJobId == job.loadID)
                {
                    a.SuccessfulJobId = -1;
                    if (FertilityAbilityUtility.IsCurrent(c)) FertilityFollowupUtility.TryQueue(pawn, c.Target);
                }
            });
        }
    }
}
