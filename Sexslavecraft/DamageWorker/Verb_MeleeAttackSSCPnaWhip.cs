// AI 辅助生成：保留初版已写入测试存档的 Verb 类型身份，不再覆写出手或伤害。
using RimWorld;

namespace SexSlaveCraft
{
    // VerbTracker 深度保存类型；继续使用同一 XML 类型，避免换型重建旧存档的动作。
    // 出手骨架继承原版，持鞭姿态由 Harmony_SSCPnaWhipAttack 统一开始；
    // 鞭束对非敌对有效链目标的免闪避／不新增威胁由独立 Cooperation 补丁限定处理。
    public class Verb_MeleeAttackSSCPnaWhip : Verb_MeleeAttackDamage { }
}
