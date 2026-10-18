// AI 辅助生成：从武器实际携液攻击只读解析效力，不建立另一份剂量配置或身体状态。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>供原生信息卡使用的涂覆武器基线；不处理目标准入或实际投药。</summary>
    internal static class SSCPnaWeaponPotencyUtility
    {
        /// <summary>成功时返回意识 100%、无快照下的一次基础输入；多个不同剂量不冒充单一效力。</summary>
        internal static bool TryGetBaseline(ThingDef weapon, out float load)
        {
            load = 0f;
            if (weapon == null || (!weapon.IsMeleeWeapon && !weapon.IsRangedWeapon)) return false;

            bool found = false;
            float baseLoad = 0f;
            // 遍历实际投射物引用，不按 DefName、图标或伤害量猜测药效；普通远程武器不显示。
            foreach (VerbProperties verb in weapon.Verbs)
                if (!Collect(verb?.defaultProjectile?.projectile?.damageDef, ref found, ref baseLoad)) return false;

            // 同一工具的动作由原版能力关联决定。普通 Blunt 柄击没有涂覆扩展，不参与基线。
            if (weapon.tools != null)
                foreach (Tool tool in weapon.tools)
                {
                    if (tool?.capacities == null) continue;
                    foreach (ManeuverDef maneuver in tool.Maneuvers)
                        if (!Collect(maneuver?.verb?.meleeDamageDef, ref found, ref baseLoad)) return false;
                }
            if (!found) return false;

            // 只构造固定除数样本，复用真实结算的合法化及单次上限；不调用 Capture/StoreSnapshot。
            load = SSCPnaWeaponDoseUtility.FromLoad(baseLoad,
                new SSCPnaWeaponResistanceSample(1f, 0f, SSCPnaWeaponResistancePolicy.Coating));
            return true;
        }

        /// <summary>无关伤害跳过；涂覆入口配置损坏或多个药量冲突时拒绝展示误导性的数字。</summary>
        private static bool Collect(DamageDef damage, ref bool found, ref float baseLoad)
        {
            if (damage?.workerClass == null ||
                !typeof(DamageWorker_SSCPnaCoated).IsAssignableFrom(damage.workerClass)) return true;
            var coating = damage.GetModExtension<SSCPnaCoatingExtension>();
            if (coating?.recordHediff == null || !SSCPnaMath.IsFinite(coating.baseLoad) || coating.baseLoad <= 0f)
                return false;
            if (found && baseLoad != coating.baseLoad) return false;
            found = true;
            baseLoad = coating.baseLoad;
            return true;
        }
    }
}
