// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仅操作当前身体；查询不创建、不迁移旧部位进度、不检查身份或绑定。</summary>
    public static partial class SSCUterusDevelopmentUtility
    {
        public const float InitialProgress = 0.01f;

        /// <summary>只在当前身体未缺失的部位中找 Torso；没有适用躯干时返回 null。</summary>
        public static BodyPartRecord FindTorso(Pawn pawn)
            => pawn?.health?.hediffSet?.GetNotMissingParts().FirstOrDefault(p => p.def == BodyPartDefOf.Torso);

        /// <summary>核对定义、进度、当前躯干及健康列表归属；已移除的引用不再生效。</summary>
        public static bool IsCurrentRecord(Hediff_SSCUterusDevelopment record)
            => record != null && record.def?.defName == DefName && float.IsFinite(record.Severity) && record.Severity > 0f &&
                record.Part != null && record.Part == FindTorso(record.pawn) &&
                record.pawn.health.hediffSet.hediffs.Contains(record);

        /// <summary>只读返回首个有效身体记录，无记录时不补建，也不从其他系统恢复。</summary>
        public static Hediff_SSCUterusDevelopment Find(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.OfType<Hediff_SSCUterusDevelopment>().FirstOrDefault(IsCurrentRecord);

        /// <summary>仅为成功手术建立身体记录，拒绝原因使用本地化资源；完整准入仍由手术实现负责。</summary>
        public static bool TryCreate(Pawn pawn, out Hediff_SSCUterusDevelopment record, out string reason)
        {
            record = null;
            reason = null;
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null)
            { reason = "SSC_UterusDevelopment_CannotStart".Translate(); return false; }
            // 明确启动操作才允许先清理异常旧实例，避免重复手术重置现有有效进度。
            Normalize(pawn);
            record = Find(pawn);
            if (record != null) { reason = "SSC_UterusDevelopment_AlreadyStarted".Translate(); return false; }
            // 不回退到全身或生殖器官挂载；缺少定义或正确类时直接拒绝。
            BodyPartRecord torso = FindTorso(pawn);
            if (torso == null) { reason = "SSC_UterusDevelopment_MissingTorso".Translate(); return false; }
            HediffDef definition = DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
            if (definition?.hediffClass != typeof(Hediff_SSCUterusDevelopment))
            { reason = "SSC_UterusDevelopment_ConfigUnavailable".Translate(); return false; }
            // 通过正常原版加入路径落地，并从健康列表复查最终保留的实例。
            var created = (Hediff_SSCUterusDevelopment)HediffMaker.MakeHediff(definition, pawn, torso);
            created.Severity = InitialProgress;
            pawn.health.AddHediff(created, torso);
            record = Find(pawn);
            if (record != null) return true;
            // 最终健康列表未接纳记录时也由同一语言入口报告，不在代码拼接玩家可见文字。
            reason = "SSC_UterusDevelopment_CreateFailed".Translate();
            return false;
        }

        /// <summary>执行玩家生育选择，失败时返回本地化原因；不重判身份、绑定或启动资格。</summary>
        public static bool TrySetFertilityEnabled(Pawn pawn, bool enabled, out string reason)
        {
            if (!FertilityChangeReport(pawn, enabled, out var record, out reason)) return false;
            // 重复请求不写字段，也不再次发送健康变化通知。
            if (record.FertilityEnabled != enabled) record.SetFertilityEnabled(enabled);
            return true;
        }

        /// <summary>为界面提供与实际提交相同的只读检查；不会更改开关、规范化或创建身体记录。</summary>
        public static bool CanSetFertilityEnabled(Pawn pawn, bool enabled, out string reason)
            => FertilityChangeReport(pawn, enabled, out _, out reason);

        /// <summary>统一检查当前身体、培养、妊娠与配置；拒绝原因通过同一语言资源供界面和提交复用。</summary>
        private static bool FertilityChangeReport(Pawn pawn, bool enabled,
            out Hediff_SSCUterusDevelopment record, out string reason)
        {
            record = null;
            reason = null;
            if (Scribe.mode != LoadSaveMode.Inactive)
            { reason = "SSC_BodyManagement_CannotChange".Translate(); return false; }
            record = Find(pawn);
            if (record == null) { reason = "SSC_BodyManagement_NotStarted".Translate(); return false; }
            // 未完成禁止开启；重复请求不再写字段或发送健康变化通知。
            if (enabled && !record.IsComplete) { reason = "SSC_BodyManagement_NotComplete".Translate(); return false; }
            if (record.FertilityEnabled == enabled) return true;
            // false 表示包含尚不可见的妊娠；复用原版及 RJW 已登记妊娠的既有判定。
            if (!enabled && rjw.PawnExtensions.IsPregnant(pawn, false))
            { reason = "SSC_BodyManagement_PregnancyBlocked".Translate(); return false; }
            // 只在切换前检查开启态配置；此处不为已有妊娠创建收益标记或调整进度。
            if (enabled && record.def.GetModExtension<SSCUterusDevelopmentExtension>()?.fertileStage == null)
            { reason = "SSC_BodyManagement_FertilityConfigUnavailable".Translate(); return false; }
            return true;
        }

        /// <summary>新增与读档后归一化；不从普通查询或每 Tick 发起清理。</summary>
        internal static void Normalize(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.PostLoadInit) return;
            // 先拍当前身体记录的快照，后续移除会触发原版健康回调。
            var records = pawn?.health?.hediffSet?.hediffs.OfType<Hediff_SSCUterusDevelopment>()
                .Where(h => h.def?.defName == DefName).ToArray();
            if (records == null || records.Length == 0) return;
            BodyPartRecord torso = FindTorso(pawn);
            Hediff_SSCUterusDevelopment keep = null;
            float progress = 0f;
            bool changed = false;
            // 先规范每份记录，再选首个有效躯干实例；进度可取最大值，开关不从其他份搬运。
            foreach (var candidate in records)
            {
                changed |= candidate.NormalizeState();
                if (torso == null || candidate.Part != torso || candidate.Severity <= 0f) continue;
                keep ??= candidate;
                progress = Math.Max(progress, candidate.Severity);
            }
            // 快照遍历，避免 RemoveHediff 修改活动枚举；开关始终保留首个有效实例的选择。
            foreach (var candidate in records)
                if (candidate != keep) pawn.health.RemoveHediff(candidate);
            // 移除通知可能使保留实例也离体，因此写回前再次确认列表归属。
            if (keep == null || !pawn.health.hediffSet.hediffs.Contains(keep)) return;
            changed |= keep.Severity != progress || records.Length > 1;
            keep.Severity = progress;
            if (changed) pawn.health.Notify_HediffChanged(keep);
        }
    }
}
