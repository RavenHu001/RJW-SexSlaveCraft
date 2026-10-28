// AI 辅助生成：档位和心情只读 XML 配置；不保存角色选择。
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>四个稳定档位的参数，XML 是唯一数值来源。</summary>
    public sealed class SSCOrgasmSensitivityExtension : DefModExtension
    {
        // 顺序对应稳定枚举 1～4；关闭不占一项，倍率／偏移／门槛都不在 C# 内另存默认表。
        public List<float> multipliers;
        public List<float> vulnerabilityOffsets;
        public List<int> minChainStages;
    }

    /// <summary>显式映射锁链阶段、实际档位与思想阶段，避免代码内复制心情矩阵。</summary>
    public sealed class SSCOrgasmSensitivityMoodMapping
    {
        public int chainStage, level, thoughtStageIndex;
    }

    /// <summary>持续思想的 XML 映射，与身体参数分别验证、分别失效。</summary>
    public sealed class SSCOrgasmSensitivityMoodExtension : DefModExtension
    {
        public List<SSCOrgasmSensitivityMoodMapping> stageMappings;
    }

    /// <summary>只缓存定义参数和派生阶段，不缓存 Pawn 的身份、锁链或有效档位。</summary>
    internal static class SSCOrgasmSensitivityConfig
    {
        /// <summary>定义级参数快照及派生阶段；仅构造时写入，角色消费者不得修改共享数组／阶段。</summary>
        internal sealed class Body
        {
            internal bool Valid;
            internal readonly float[] Multipliers = new float[4], Offsets = new float[4];
            internal readonly int[] Gates = new int[4];
            internal readonly HediffStage[] Stages = new HediffStage[4];

            /// <summary>从已校验的非递减门槛求最高可用档位，不假定档位等于锁链阶段。</summary>
            internal int Maximum(int stage)
            {
                int level = 0;
                while (level < Gates.Length && stage >= Gates[level]) level++;
                return level;
            }
        }

        /// <summary>与一份身体配置绑定的思想索引缓存，避免跨配置使用旧的可达组合结论。</summary>
        private sealed class Mood
        {
            internal Body Body;
            internal bool Valid;
            internal readonly Dictionary<(int, int), int> Indices = new Dictionary<(int, int), int>();
        }

        private static ConditionalWeakTable<HediffDef, Body> bodies = new ConditionalWeakTable<HediffDef, Body>();
        private static ConditionalWeakTable<ThoughtDef, Mood> moods = new ConditionalWeakTable<ThoughtDef, Mood>();
        private static LoadedLanguage language;

        /// <summary>取得一次验证的身体配置；配置错误时不提供硬编码替代参数。</summary>
        internal static Body Get(HediffDef definition)
        {
            // 派生阶段携带本地化标签。语言切换后重建定义缓存，不在查询中修改共享阶段。
            if (!ReferenceEquals(language, LanguageDatabase.activeLanguage)) Clear();
            if (definition == null) return null;
            Body body = bodies.GetValue(definition, BuildBody);
            // 原版 DefInjected 可能晚于 activeLanguage 赋值。仅比较四个标签，实际注入后再重建一次；
            // 不重新扫描 Def、不逐 Pawn 修改阶段，也不把前一语言标签永久留在派生缓存中。
            if (body.Valid)
                for (int i = 0; i < 4; i++)
                    if (body.Stages[i].label != definition.stages[i].label)
                    { Clear(); return bodies.GetValue(definition, BuildBody); }
            return body;
        }

        /// <summary>为正常 Def 加载／开发期显式重载提供缓存失效入口；不在 tick 中扫描或重建。</summary>
        internal static void Clear()
        {
            bodies = new ConditionalWeakTable<HediffDef, Body>();
            moods = new ConditionalWeakTable<ThoughtDef, Mood>();
            language = LanguageDatabase.activeLanguage;
        }

        /// <summary>复制四档参数并构造只读属性阶段；绝不修改共享 XML 阶段或逐角色修改 Def。</summary>
        private static Body BuildBody(HediffDef def)
        {
            var result = new Body();
            var ext = def.GetModExtension<SSCOrgasmSensitivityExtension>();
            StatDef vulnerability = DefDatabase<StatDef>.GetNamedSilentFail("Vulnerability");
            StatDef frequency = SSCOrgasmFrequencyUtility.Definition;
            // 四项是稳定档位的结构契约，不是平衡参数。结构或引用损坏时整份身体配置停用。
            bool valid = def.hediffClass == typeof(Hediff_SSCOrgasmSensitivity) && def.minSeverity == 0f &&
                def.maxSeverity == 4f && def.initialSeverity == 1f && def.stages?.Count == 4 &&
                ext?.multipliers?.Count == 4 && ext.vulnerabilityOffsets?.Count == 4 &&
                ext.minChainStages?.Count == 4 && vulnerability != null && frequency != null;
            for (int i = 0; valid && i < 4; i++)
            {
                float multiplier = ext.multipliers[i], offset = ext.vulnerabilityOffsets[i];
                int gate = ext.minChainStages[i];
                // 倍率只限制有限且至少一，不设置人为上限；相同解锁门槛允许多档同时开放。
                valid = float.IsFinite(multiplier) && multiplier >= 1f && float.IsFinite(offset) &&
                    gate >= 1 && gate <= 4 && (i == 0 || gate >= ext.minChainStages[i - 1]) &&
                    def.stages[i] != null && (def.stages[i].statOffsets == null || def.stages[i].statOffsets.Count == 0);
                if (!valid) break;
                result.Multipliers[i] = multiplier; result.Offsets[i] = offset; result.Gates[i] = gate;
                // 属性偏移只从扩展复制一次；原 Def 阶段不再保存同一数值，且不按 Pawn 修改阶段。
                result.Stages[i] = new HediffStage
                {
                    label = def.stages[i].label,
                    minSeverity = i + 1,
                    statOffsets = new List<StatModifier> { new StatModifier { stat = vulnerability, value = offset } },
                    // SSC只提供一个原生因子；其他来源由StatWorker合成，推进消费者不再额外乘此倍率。
                    statFactors = new List<StatModifier> { new StatModifier { stat = frequency, value = multiplier } }
                };
            }
            result.Valid = valid;
            if (!valid) Log.Warning("[SSC] Invalid SSC_OrgasmSensitivity XML configuration; body effects and nonzero edits are disabled.");
            return result;
        }

        /// <summary>验证独立思想映射；错误只停用这项心情，不连带关闭合法身体参数。</summary>
        internal static bool TryGetMood(ThoughtDef def, Body body, int chainStage, int level, out int index)
        {
            index = -1;
            if (def == null || body?.Valid != true) return false;
            // 稳态查询不构造捕获 body 的委托；只有首次使用该定义才分配映射缓存。
            if (!moods.TryGetValue(def, out Mood mood))
            {
                mood = BuildMood(def, body);
                moods.Add(def, mood);
            }
            return ReferenceEquals(mood.Body, body) && mood.Valid && mood.Indices.TryGetValue((chainStage, level), out index);
        }

        /// <summary>校验全部可达组合、重复键和索引及数值；允许 XML 调整解锁门槛后显式扩充矩阵。</summary>
        private static Mood BuildMood(ThoughtDef def, Body body)
        {
            var mood = new Mood { Body = body };
            var entries = def.GetModExtension<SSCOrgasmSensitivityMoodExtension>()?.stageMappings;
            bool valid = def.hediff?.defName == SSCOrgasmSensitivityUtility.DefName && def.stages != null && entries != null;
            // 索引合法还不够：同一阶段／档位只能有一个定义，心情值必须来自有限的 XML 阶段。
            if (valid)
                foreach (var entry in entries)
                {
                    if (entry == null || entry.chainStage < 1 || entry.chainStage > 4 || entry.level < 1 || entry.level > 4 ||
                        entry.thoughtStageIndex < 0 || entry.thoughtStageIndex >= def.stages.Count ||
                        def.stages[entry.thoughtStageIndex] == null || !float.IsFinite(def.stages[entry.thoughtStageIndex].baseMoodEffect) ||
                        !mood.Indices.TryAdd((entry.chainStage, entry.level), entry.thoughtStageIndex))
                    { valid = false; break; }
                }
            // 按当前 XML 门槛检查所有可达格；门槛降低而未补齐映射时，只停思想而保留身体效果。
            for (int stage = 1; valid && stage <= 4; stage++)
                for (int level = 1; level <= body.Maximum(stage); level++)
                    if (!mood.Indices.ContainsKey((stage, level))) { valid = false; break; }
            mood.Valid = valid;
            if (!valid) Log.Warning("[SSC] Invalid SSC_OrgasmSensitivity_Thought XML mapping; only the continuous thought is disabled.");
            return mood;
        }
    }
}
