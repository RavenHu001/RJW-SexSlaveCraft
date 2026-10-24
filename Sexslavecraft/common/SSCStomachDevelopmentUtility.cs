// AI 辅助生成
using System;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>胃部开发只操作当前身体；普通查询不创建记录或恢复历史进度。</summary>
    public static class SSCStomachDevelopmentUtility
    {
        public const string DefName = "SSC_StomachDevelopment";
        public const float InitialProgress = 0.01f;

        /// <summary>从未缺失部位定位胃；人工胃不额外排除，无胃不回退到躯干。</summary>
        public static BodyPartRecord FindStomach(Pawn pawn)
            => pawn?.health?.hediffSet?.GetNotMissingParts().FirstOrDefault(p =>
                SSCDefOf.Stomach != null && p.def == SSCDefOf.Stomach);

        /// <summary>核对定义、进度、当前胃部和健康列表，拒绝已移除的旧对象。</summary>
        public static bool IsCurrentRecord(Hediff_SSCStomachDevelopment record)
            => record != null && record.def?.defName == DefName && float.IsFinite(record.Severity) &&
                record.Severity > 0f && record.Part != null && record.Part == FindStomach(record.pawn) &&
                record.pawn.health.hediffSet.hediffs.Contains(record);

        /// <summary>只读取得当前身体有效记录，不检查已完成手术之外的身份资格。</summary>
        public static Hediff_SSCStomachDevelopment Find(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs.OfType<Hediff_SSCStomachDevelopment>().FirstOrDefault(IsCurrentRecord);

        /// <summary>显式成功启动才创建 1% 记录；完整手术资格由调用方负责复查。</summary>
        public static bool TryCreate(Pawn pawn, out Hediff_SSCStomachDevelopment record, out string reason)
        {
            record = null; reason = null;
            if (Scribe.mode != LoadSaveMode.Inactive || pawn?.health?.hediffSet == null)
            { reason = "SSC_StomachDevelopment_CannotStart".Translate(); return false; }
            Normalize(pawn);
            record = Find(pawn);
            if (record != null) { reason = "SSC_StomachDevelopment_AlreadyStarted".Translate(); return false; }
            var stomach = FindStomach(pawn);
            if (stomach == null) { reason = "SSC_StomachDevelopment_MissingStomach".Translate(); return false; }
            var definition = DefDatabase<HediffDef>.GetNamedSilentFail(DefName);
            if (definition?.hediffClass != typeof(Hediff_SSCStomachDevelopment))
            { reason = "SSC_StomachDevelopment_ConfigUnavailable".Translate(); return false; }
            // MakeHediff 与健康通知仍走原版；最终从当前身体重新读取，不返回未被接纳的对象。
            var created = (Hediff_SSCStomachDevelopment)HediffMaker.MakeHediff(definition, pawn, stomach);
            created.Severity = InitialProgress;
            pawn.health.AddHediff(created, stomach);
            record = Find(pawn);
            if (record != null) return true;
            reason = "SSC_StomachDevelopment_CreateFailed".Translate(); return false;
        }

        /// <summary>仅在加入或读档后清理记录；取最大有效进度，不在查询或每 Tick 维护影子状态。</summary>
        internal static void Normalize(Pawn pawn)
        {
            if (Scribe.mode != LoadSaveMode.Inactive && Scribe.mode != LoadSaveMode.PostLoadInit) return;
            var records = pawn?.health?.hediffSet?.hediffs.OfType<Hediff_SSCStomachDevelopment>()
                .Where(h => h.def?.defName == DefName).ToArray();
            if (records == null || records.Length == 0) return;
            var stomach = FindStomach(pawn);
            Hediff_SSCStomachDevelopment keep = null;
            float progress = 0f;
            // 存档直接读取的严重度也经过相同校验；只保留当前胃部上的正进度。
            foreach (var candidate in records)
            {
                candidate.Severity = candidate.Severity;
                if (stomach == null || candidate.Part != stomach || candidate.Severity <= 0f) continue;
                keep ??= candidate;
                progress = Math.Max(progress, candidate.Severity);
            }
            foreach (var candidate in records)
                if (candidate != keep) pawn.health.RemoveHediff(candidate);
            // 移除通知可能改变健康列表；回写前复查保留对象仍属于当前身体。
            if (keep == null || !pawn.health.hediffSet.hediffs.Contains(keep)) return;
            keep.Severity = progress;
        }
    }
}
