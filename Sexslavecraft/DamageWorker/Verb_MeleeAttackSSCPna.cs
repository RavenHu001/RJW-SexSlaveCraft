// AI 辅助生成：专用携液刺击命中后启动矛尖泛光，完整保留原版动作与伤害流程。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class Verb_MeleeAttackSSCPna : Verb_MeleeAttackDamage
    {
        protected override bool TryCastShot()
        {
            Pawn pawn = CasterPawn;
            Thing target = currentTarget.Thing;
            // 对应 1.7.4982 原版的两个提前退出条件；false 还可能表示真正刺空／被闪避，
            // 因此不能把父类返回值当作“是否出手”。额外地图检查只限制视觉，不拦截父类。
            bool attempted = pawn != null && pawn.Spawned && !pawn.stances.FullBodyBusy &&
                target != null && target.Spawned && target.Map == pawn.Map;
            Thing weapon = EquipmentSource;
            if (attempted) SSCPnaSpearGlowUtility.Clear(weapon);
            bool result = base.TryCastShot();
            // 原版 true 表示命中，包含护甲完全阻挡；miss／dodge 均不亮，不依赖施药成功。
            if (attempted && result) SSCPnaSpearGlowUtility.Begin(weapon, pawn);
            return result;
        }
    }
}
