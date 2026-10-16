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

        public bool Get(string id, bool defaultValue = false)
            => values != null && values.TryGetValue(id, out bool value) ? value : defaultValue;

        public void Set(string id, bool enabled)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (values == null) values = new Dictionary<string, bool>();
            values[id] = enabled;
        }

        // Pawn、凝胶及加工副本分别持有字典，后续切换不能回写另一份人格快照。
        public SSCAbilityToggleState Copy()
        {
            var copy = new SSCAbilityToggleState();
            if (values != null)
                foreach (var pair in values)
                    if (!string.IsNullOrEmpty(pair.Key)) copy.values[pair.Key] = pair.Value;
            return copy;
        }

        public void ExposeData()
        {
            // 旧档缺节点只补空容器；默认值由定义在查询时解释，不写入整个注册表。
            Scribe_Collections.Look(ref values, "enabledById", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && values == null)
                values = new Dictionary<string, bool>();
        }
    }
}
