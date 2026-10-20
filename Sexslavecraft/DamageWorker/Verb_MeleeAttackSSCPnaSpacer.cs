// AI 辅助生成：保留原版近战出手流程，只构造一次不会因低伤害改钝的纳米主攻击。
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class Verb_MeleeAttackSSCPnaSpacer : Verb_MeleeAttack
    {
        protected override DamageWorker.DamageResult ApplyMeleeDamageToTarget(LocalTargetInfo target)
        {
            // 原版 TryCastShot 已决定是否命中；这里只接受实际对象和正确配置的纳米端。
            // 不另做一次命中判定，也不在这里施药，实际输入仍只属于伤害处理器。
            if (target.Thing == null || target.ThingDestroyed ||
                !SSCPnaSpacerMeleeUtility.IsWorkingEnd(this)) return new DamageWorker.DamageResult();
            // 复用原版调整后的武器伤害和穿甲，两者正常受品质及其他属性影响。保留随机浮动，
            // 但不执行原版“小于 1 就改为 Blunt”的转换；护甲真实转钝留到处理器中完成。
            Pawn attacker = CasterPawn;
            float amount = verbProps.AdjustedMeleeDamageAmount(this, attacker) * Rand.Range(.8f, 1.2f);
            if (!SSCPnaMath.IsFinite(amount) || amount <= 0f) return new DamageWorker.DamageResult();
            // 保留攻击者、武器、工具、品质和方向供护盾／伤势归因使用；不指定绕甲，
            // 也不提前锁定具体身体部位，让一次实际接触走正常的上游吸收入口。
            var info = new DamageInfo(verbProps.meleeDamageDef, amount,
                verbProps.AdjustedArmorPenetration(this, attacker), instigator: attacker,
                weapon: EquipmentSource.def, instigatorGuilty: !attacker.Drafted);
            info.SetHitPart(null);
            info.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            info.SetAngle((target.Thing.Position - attacker.Position).ToVector3());
            info.SetTool(tool);
            info.SetWeaponBodyPartGroup(verbProps.AdjustedLinkedBodyPartsGroup(tool));
            if (EquipmentSource.TryGetQuality(out var quality)) info.SetWeaponQuality(quality);
            // 工作端只有这一次集群输入，不附带普通柄击、惊袭额外伤害或第二份投送事件。
            return target.Thing.TakeDamage(info);
        }
    }
}
