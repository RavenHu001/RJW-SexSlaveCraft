// AI 辅助生成：身体档位的唯一查询／提交入口；本批不写入 RJW 计时。
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>一次只读查询的实际身体状态；无资格时倍率为一、脆弱度偏移为零。</summary>
    public readonly struct SSCOrgasmSensitivityState
    {
        public readonly Hediff_SSCOrgasmSensitivity Record;
        public readonly int SavedLevel, EffectiveLevel, ChainStage, MaximumLevel;
        public readonly float Multiplier, VulnerabilityOffset;
        public readonly string InactiveReason;

        /// <summary>封装同一次有效性查询，不保存结果，也不把实际档位写回身体选择。</summary>
        internal SSCOrgasmSensitivityState(Hediff_SSCOrgasmSensitivity record, int saved, int effective, int stage,
            int maximum, float multiplier, float offset, string reason)
        {
            Record = record; SavedLevel = saved; EffectiveLevel = effective; ChainStage = stage; MaximumLevel = maximum;
            Multiplier = multiplier; VulnerabilityOffset = offset; InactiveReason = reason;
        }
    }

    /// <summary>菜单原凭据；也保存原本没有记录的事实，不向旧请求重新签发 owner。</summary>
    public sealed class SSCOrgasmSensitivityContext
    {
        internal Pawn Pawn, Master;
        internal CompSexSlaveTraining Comp;
        internal object PersonalityToken;
        internal Hediff_ChainOfSexSlave Chain;
        internal Hediff[] Records;
    }

    /// <summary>身体选择与实际效果分离；查询不补建记录，不把枚举接入人格 bool 字典。</summary>
    public static class SSCOrgasmSensitivityUtility
    {
        public const string DefName = "SSC_OrgasmSensitivity";
        public const string ThoughtDefName = "SSC_OrgasmSensitivity_Thought";
        /// <summary>按稳定 ID 读取已加载定义；缺失时保持关闭，不创建替代 Def。</summary>
        internal static HediffDef Definition => DefDatabase<HediffDef>.GetNamedSilentFail(DefName);

        /// <summary>只接受有限的一至四整数；非法输入不能靠取整或原版上限裁剪变成高档。</summary>
        public static bool IsLegalLevel(float value) => float.IsFinite(value) && value >= 1f && value <= 4f && value == (int)value;

        /// <summary>当前健康列表中的首个合法全身记录为唯一 owner；重复记录不会重复提供属性。</summary>
        public static Hediff_SSCOrgasmSensitivity Find(Pawn pawn)
        {
            var records = pawn?.health?.hediffSet?.hediffs;
            if (records == null) return null;
            foreach (var record in records)
                if (record is Hediff_SSCOrgasmSensitivity body && record.def?.defName == DefName &&
                    record.pawn == pawn && record.Part == null && IsLegalLevel(record.Severity)) return body;
            return null;
        }

        /// <summary>从实际身份和锁链计算准入；主人死亡或离图不会撤销已保存的微械指令。</summary>
        public static SSCOrgasmSensitivityState GetState(Pawn pawn)
        {
            // 保存值只来自唯一合法身体记录；未满足当前准入时仍保留这个值供界面说明。
            var record = Find(pawn);
            int saved = record == null ? 0 : (int)record.Severity;
            var config = SSCOrgasmSensitivityConfig.Get(Definition);
            var chain = SSCBondUtility.GetChain(pawn);
            int stage = SSCIdentityUtility.GetSexSlaveStageFromChain(chain);
            string reason = null;
            // 当前身份、真实链和主人引用各自校验；不以历史 Trait／恶堕或指定调教员替代实际绑定。
            // 主人只检查销毁／丢弃，不检查死亡／离图；已保存的微械指令按规划继续有效。
            if (config?.Valid != true) reason = "SSC_Sensitivity_ConfigInvalid";
            else if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded || pawn.RaceProps?.Humanlike != true)
                reason = "SSC_Sensitivity_BodyUnavailable";
            else if (SSCPersonalityRuntimeUtility.IsMutating(pawn) || SSCMasterProgressionUtility.IsHollow(pawn))
                reason = "SSC_Sensitivity_PersonalityUnavailable";
            else if (!SSCIdentityUtility.IsSexSlave(pawn)) reason = "SSC_Sensitivity_NotSlave";
            else if (chain == null || chain.pawn != pawn || !pawn.health.hediffSet.hediffs.Contains(chain) ||
                chain.LinkedPawn == null || chain.LinkedPawn.Destroyed || chain.LinkedPawn.Discarded || stage < 1)
                reason = "SSC_Sensitivity_NoChain";
            int maximum = reason == null ? config.Maximum(stage) : 0;
            // 降阶只裁减这次实际消费，不回写 Severity，因此重新升阶后能恢复原选择。
            int effective = Math.Min(saved, maximum);
            return new SSCOrgasmSensitivityState(record, saved, effective, stage, maximum,
                effective > 0 ? config.Multipliers[effective - 1] : 1f,
                effective > 0 ? config.Offsets[effective - 1] : 0f, reason);
        }

        /// <summary>供后续消费者取得同一有效系数；本函数本身不推进、不采样、不保存余数。</summary>
        public static float GetMultiplier(Pawn pawn) => GetState(pawn).Multiplier;

        /// <summary>捕获整组同 Def 实例，以识别外部增删、替换及原本缺少记录的情况。</summary>
        public static SSCOrgasmSensitivityContext Capture(Pawn pawn)
        {
            var chain = SSCBondUtility.GetChain(pawn);
            // 此数组仅在界面／外部显式请求捕获时分配；推进及属性查询不捕获菜单上下文。
            return new SSCOrgasmSensitivityContext
            {
                Pawn = pawn, Comp = pawn?.TryGetComp<CompSexSlaveTraining>(), PersonalityToken = SSCPersonalityRuntimeUtility.Capture(pawn),
                Chain = chain, Master = chain?.LinkedPawn,
                Records = pawn?.health?.hediffSet?.hediffs.Where(h => h?.def?.defName == DefName).ToArray() ?? Array.Empty<Hediff>()
            };
        }

        /// <summary>旧菜单只比较原凭据；不能通过重新捕获上下文取得新身体或新绑定权限。</summary>
        public static bool IsCurrent(SSCOrgasmSensitivityContext context)
        {
            if (!HasCurrentCredentials(context)) return false;
            int index = 0;
            // 同 Def 的整组原实例及缺失事实都参与比较，不能只检查“当前也有一个同名 Hediff”。
            foreach (var record in context.Pawn.health.hediffSet.hediffs)
                if (record?.def?.defName == DefName && (index >= context.Records.Length || !ReferenceEquals(record, context.Records[index++]))) return false;
            return index == context.Records.Length;
        }

        /// <summary>自有增删后仍比较原组件、人格与锁链；不重新签发任何上下文。</summary>
        private static bool HasCurrentCredentials(SSCOrgasmSensitivityContext context)
        {
            Pawn pawn = context?.Pawn;
            if (pawn?.health?.hediffSet == null || pawn.TryGetComp<CompSexSlaveTraining>() != context.Comp ||
                !SSCPersonalityRuntimeUtility.IsCurrent(pawn, context.PersonalityToken)) return false;
            var chain = SSCBondUtility.GetChain(pawn);
            return ReferenceEquals(chain, context.Chain) && ReferenceEquals(chain?.LinkedPawn, context.Master);
        }

        /// <summary>校验目标档位及原凭据；关闭不依赖效果准入和 XML，但仍须当前身体可编辑。</summary>
        public static bool CanSet(SSCOrgasmSensitivityContext context, int level, out string reason)
            => CheckAdmission(context, level, true, out reason);

        /// <summary>回调后重查实时资格；已知自有增删仅豁免记录集合比较，不豁免人格和阶段门槛。</summary>
        private static bool CheckAdmission(SSCOrgasmSensitivityContext context, int level, bool checkRecords, out string reason)
        {
            string key = null;
            Pawn pawn = context?.Pawn;
            if (!(checkRecords ? IsCurrent(context) : HasCurrentCredentials(context))) key = "SSC_BodyManagement_BodyChanged";
            else if (Scribe.mode != LoadSaveMode.Inactive || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                pawn.RaceProps?.Humanlike != true || context.Comp == null || pawn.Faction != Faction.OfPlayer)
                key = "SSC_Sensitivity_NotEditable";
            else if (SSCPersonalityRuntimeUtility.IsMutating(pawn) || SSCMasterProgressionUtility.IsHollow(pawn))
                key = "SSC_Sensitivity_PersonalityUnavailable";
            else if (level < 0 || level > 4) key = "SSC_Sensitivity_InvalidLevel";
            else if (level != 0)
            {
                // 关闭只检查原凭据和可编辑身体；非零目标额外检查此刻身份、配置与解锁阶段。
                var state = GetState(pawn);
                key = state.InactiveReason;
                if (key == null && level > state.MaximumLevel) key = "SSC_Sensitivity_LevelLocked";
            }
            reason = key == null ? null : key.Translate().ToString();
            return key == null;
        }

        /// <summary>显式写入才规范化、增删或切档；重复选择合法当前值保持幂等。</summary>
        public static bool TrySet(SSCOrgasmSensitivityContext context, int level, out string reason)
        {
            if (!CanSet(context, level, out reason)) return false;
            Pawn pawn = context.Pawn;
            Hediff_SSCOrgasmSensitivity expected = null;
            if (level == 0)
            {
                // 只移除捕获的原记录；每次原生移除都可能触发第三方回调，下一次删除前重查剩余集合。
                var remaining = context.Records.ToList();
                foreach (var record in context.Records)
                {
                    if (!CheckAdmission(context, 0, false, out reason)) return false;
                    if (!pawn.health.hediffSet.hediffs.Where(h => h?.def?.defName == DefName).SequenceEqual(remaining))
                        return RejectChanged(out reason);
                    pawn.health.RemoveHediff(record);
                    remaining.Remove(record);
                }
                if (pawn.health.hediffSet.hediffs.Any(h => h?.def?.defName == DefName)) return RejectChanged(out reason);
            }
            else
            {
                var record = Find(pawn);
                if (context.Records.Length == 1 && record != null && record.Severity == level) return true;
                // 记住清理前的原 owner。规范化后出现的替代实例不能成为这次旧请求的新目标。
                Normalize(pawn);
                // 清理回调可能替换人格／组件／绑定；不能把旧请求用于新的身体上下文。
                if (!CheckAdmission(context, level, false, out reason)) return false;
                if (!HasOnlyRecord(pawn, record)) return RejectChanged(out reason);
                if (record == null)
                {
                    record = HediffMaker.MakeHediff(Definition, pawn) as Hediff_SSCOrgasmSensitivity;
                    if (record == null) { reason = "SSC_Sensitivity_ConfigInvalid".Translate(); return false; }
                    record.Severity = level;
                    // 未入体的实例设置严重度也会刷新缓存；添加前仍须确认身体没有被回调改写。
                    if (!CheckAdmission(context, level, false, out reason)) return false;
                    if (!HasOnlyRecord(pawn, null)) return RejectChanged(out reason);
                    pawn.health.AddHediff(record);
                }
                else record.Severity = level;
                if (!HasOnlyRecord(pawn, record)) return RejectChanged(out reason);
                expected = record;
            }
            NotifyStateChanged(pawn);
            // 原生写入和最终刷新均可能有外部回调。返回成功前核对原凭据、唯一 owner 和最终保存值；
            // 拒绝不回滚第三方的新选择，也不重新捕获上下文来把它覆盖成旧请求值。
            if (!CheckAdmission(context, level, false, out reason)) return false;
            if (!HasOnlyRecord(pawn, expected) || (expected != null && expected.Severity != level)) return RejectChanged(out reason);
            return true;
        }

        /// <summary>只允许自有操作预期的唯一实例或空集合；回调添加的新 owner 不会被旧请求接管。</summary>
        private static bool HasOnlyRecord(Pawn pawn, Hediff expected)
        {
            // 原引用还在列表中也可能已被回调改到其他 Pawn／身体部位；须仍是本身体的合法 owner。
            if (expected != null && !ReferenceEquals(Find(pawn), expected)) return false;
            int count = 0;
            foreach (var record in pawn.health.hediffSet.hediffs)
                if (record?.def?.defName == DefName)
                { if (!ReferenceEquals(record, expected)) return false; count++; }
            return count == (expected == null ? 0 : 1);
        }

        /// <summary>对外统一反馈原载体已经变化，不静默改写替代实例。</summary>
        private static bool RejectChanged(out string reason)
        { reason = "SSC_BodyManagement_BodyChanged".Translate(); return false; }

        /// <summary>仅在加入、加载或自有写入边界修复；保留首个合法实例及其原选择，不合计或取最大档。</summary>
        public static void Normalize(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            var keep = Find(pawn);
            // 先快照待删除项，避免原生 RemoveHediff 改变正在枚举的列表；首个合法档位不参与合计。
            foreach (var record in pawn.health.hediffSet.hediffs.Where(h => h?.def?.defName == DefName && !ReferenceEquals(h, keep)).ToArray())
                pawn.health.RemoveHediff(record);
            NotifyStateChanged(pawn);
        }

        /// <summary>自有身份、锁链、人格及记录变化统一刷新健康／思想缓存，不改保存档位。</summary>
        public static void NotifyStateChanged(Pawn pawn)
        {
            // 直接使派生缓存失效，不再次调用 Notify_HediffChanged，避免健康 Postfix 转发形成递归。
            pawn?.health?.hediffSet?.DirtyCache();
            pawn?.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty();
        }
    }
}
