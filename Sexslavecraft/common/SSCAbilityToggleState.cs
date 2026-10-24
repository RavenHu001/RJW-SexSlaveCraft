// AI 辅助生成
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>随人格保存的能力偏好；未知键和明确关闭值也保留，查询不补写默认值。</summary>
    public sealed class SSCAbilityToggleState : IExposable
    {
        // 稳定 ID 是唯一保存键；不依赖注册顺序，也不把未知能力或显式 false 当成缺省值。
        private Dictionary<string, bool> values = new Dictionary<string, bool>();
        public bool HasEntries => values != null && values.Count > 0;

        /// <summary>只读解释缺键默认，不把默认写入字典或将显式 false 当成缺键。</summary>
        public bool Get(string id, bool defaultValue = false)
            => values != null && values.TryGetValue(id, out bool value) ? value : defaultValue;

        /// <summary>保存有效 ID 的明确选择；未知注册项同样保留，便于存档和后续恢复。</summary>
        public void Set(string id, bool enabled)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (values == null) values = new Dictionary<string, bool>();
            values[id] = enabled;
        }

        // Pawn、凝胶及加工副本分别持有字典，后续切换不能回写另一份人格快照。
        /// <summary>逐项复制独立容器，避免来源、加工凝胶和植入后角色共享可变字典。</summary>
        public SSCAbilityToggleState Copy()
        {
            var copy = new SSCAbilityToggleState();
            if (values != null)
                foreach (var pair in values)
                    if (!string.IsNullOrEmpty(pair.Key)) copy.values[pair.Key] = pair.Value;
            return copy;
        }

        /// <summary>保留原 enabledById 节点；完整加载后只修复空容器，不预填注册表默认。</summary>
        public void ExposeData()
        {
            // 旧档缺节点只补空容器；默认值由定义在查询时解释，不写入整个注册表。
            Scribe_Collections.Look(ref values, "enabledById", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && values == null)
                values = new Dictionary<string, bool>();
        }
    }
}
