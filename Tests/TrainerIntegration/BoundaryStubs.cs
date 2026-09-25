using System.Collections.Generic;
using Verse;

namespace Verse.AI.Group
{
    // 共用 Pawn 接口引用原版 Lord；本套件没有课堂或仪式组，只提供空的外部对象契约。
    public class LordJob { }
    public class Lord { public LordJob LordJob; }
}

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        // 共用模型未包含旧限制迁移字段；只提供存储槽，迁移及恢复规则链接生产代码。
        public SSCRestrictionLegacyPawn legacyRestrictionInput;
        public bool restrictionLifecycleSeen;
    }

    public class SSCSettings
    {
        // 限制迁移源码的外部设置契约。本套件不触发旧档设置迁移。
        public bool protectNonRapeOwnerOnly = true;
        public bool allowSexSlaveRape;
        public bool protectBusAggressorRape = true;
        public bool protectChainedAggressorRape = true;
    }

    /// <summary>仅承载凝胶数据和字典读写；状态选择、清理与恢复均执行生产 GelUtility。</summary>
    public class CompPersonalityStore
    {
        public SexSlaveSpecializationType specializationType;
        public float specializationProgress;
        public Dictionary<string, float> specializationProgressByType;
        public Dictionary<HediffDef, float> hediffTags = new Dictionary<HediffDef, float>();

        // 这些方法只提供生产标签工具所需的容器接口，不推断普通/终极状态或宿主资格。
        public bool HasTag(HediffDef def) => hediffTags?.ContainsKey(def) == true;
        public float GetTagSeverity(HediffDef def) => hediffTags.TryGetValue(def, out float value) ? value : 0f;
        public void SetTag(HediffDef def, float value) => hediffTags[def] = value;
        public void RemoveTag(HediffDef def) => hediffTags?.Remove(def);
    }
}
