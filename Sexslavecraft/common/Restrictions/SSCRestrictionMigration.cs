using Verse;

namespace SexSlaveCraft
{
    /// <summary>固定升级时能够取得的旧全局设置；延迟恢复的角色也使用同一份存档快照。</summary>
    public sealed class SSCRestrictionLegacySettings : IExposable
    {
        public bool ownerOnly = true;
        public bool allowForcedReception;
        public bool protectBusInitiator = true;
        public bool protectOtherInitiator = true;

        /// <summary>复制当前旧设置，不把总开关关闭解释为六项全部允许。</summary>
        public static SSCRestrictionLegacySettings Capture(SSCSettings settings)
        {
            return new SSCRestrictionLegacySettings
            {
                ownerOnly = settings?.protectNonRapeOwnerOnly ?? true,
                allowForcedReception = settings?.allowSexSlaveRape ?? false,
                protectBusInitiator = settings?.protectBusAggressorRape ?? true,
                protectOtherInitiator = settings?.protectChainedAggressorRape ?? true
            };
        }

        /// <summary>按六项约定映射旧选择，不复制旧整次放行，也不写入装备或特化强制结果。</summary>
        public SSCRestrictionConfig Convert(bool open, bool bus)
        {
            bool consensual = !ownerOnly || open || bus;
            return new SSCRestrictionConfig
            {
                busDefaultsApplied = bus,
                rules = new SSCRestrictionRules
                {
                    allowMasturbation = consensual,
                    consensualInitiation = consensual ? SSCRestrictionValue.Allow : SSCRestrictionValue.OwnerOnly,
                    allowForcedInitiation = bus ? !protectBusInitiator : !protectOtherInitiator,
                    receiveConsensual = consensual,
                    receiveForced = allowForcedReception || bus,
                    receiveTraining = open || bus
                }
            };
        }

        /// <summary>将旧设置快照保存在当前存档中，后续改动模组设置不影响尚待迁移的角色。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref ownerOnly, "ownerOnly", true);
            Scribe_Values.Look(ref allowForcedReception, "allowForcedReception", false);
            Scribe_Values.Look(ref protectBusInitiator, "protectBusInitiator", true);
            Scribe_Values.Look(ref protectOtherInitiator, "protectOtherInitiator", true);
        }
    }

    /// <summary>在旧修复执行前保存适用角色的迁移输入；成功迁移或显式重置后清除。</summary>
    public sealed class SSCRestrictionLegacyPawn : IExposable
    {
        public bool open;
        public bool bus;

        /// <summary>保存迁移输入，使延迟初始化或中途存档也不会丢失原有选择。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref open, "open", false);
            Scribe_Values.Look(ref bus, "bus", false);
        }
    }
}
