// AI 辅助生成
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public sealed class DamageWorker_SSCPnaBlast : DamageWorker
    {
        public override IEnumerable<IntVec3> ExplosionCellsToHit(IntVec3 center, Map map, float radius,
            IntVec3? needLOSToCell1 = null, IntVec3? needLOSToCell2 = null, FloatRange? affectedAngle = null)
        {
            // 炮弹与陷阱使用相同的无方向爆炸。墙内爆心只处理该格，避免向墙两侧穿透。
            if (!center.CanBeSeenOverFast(map)) return new[] { center };
            return base.ExplosionCellsToHit(center, map, radius);
        }

        protected override void ExplosionDamageThing(Explosion explosion, Thing thing, List<Thing> damagedThings,
            List<Thing> ignoredThings, IntVec3 cell)
        {
            if (thing is not Pawn pawn || !pawn.Spawned || pawn.Map != explosion.Map ||
                ignoredThings?.Contains(pawn) == true || damagedThings.Contains(pawn)) return;

            // 原版保存这次爆炸的目标记录，先登记再调用可能触发健康回调的 PNA 入口。
            damagedThings.Add(pawn);
            SSCPnaExposureUtility.ApplyExposure(pawn, .25f, SSCPnaExposureSource.Blast);
        }
    }
}
