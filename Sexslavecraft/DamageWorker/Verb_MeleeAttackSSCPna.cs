// AI 辅助生成：只给专用携液刺击动作增加攻击纹，完整保留原版命中与伤害流程。
using RimWorld;
using UnityEngine;
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
            Map map = attempted ? pawn.Map : null;
            Vector3 from = attempted ? pawn.DrawPos : default;
            Vector3 to = attempted ? target.DrawPos : default;
            bool result = base.TryCastShot();
            if (attempted) SSCPnaWeaponEffectsUtility.ShowStab(map, from, to);
            return result;
        }
    }
}
