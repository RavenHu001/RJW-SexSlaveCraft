// AI 辅助生成
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>集中处理主人记录建立、清空、精确替换和经验增加；只读查询不补建。</summary>
    public static class SSCMasterProgressionUtility
    {
        public const int CurrentVersion = 1;
        public const int DebugExperience = 10;
        public const float NoRecord = -1f;
        public const string DefName = "SSC_Hediff_MasterProgression";

        // 定义引用是正常识别入口，名称检查同时拦截通用标签和克隆复制中的同名记录。
        // 下方查询只读现存 Hediff；缺记录返回 -1，不根据主人身份推断零经验。
        public static bool IsProgressionDef(HediffDef def)
            => def != null && (def == SSCDefOf.SSC_Hediff_MasterProgression || def.defName == DefName);

        public static Hediff_MasterProgression GetRecord(Pawn pawn)
            => SSCDefOf.SSC_Hediff_MasterProgression == null ? null :
                pawn?.health?.hediffSet?.GetFirstHediffOfDef(SSCDefOf.SSC_Hediff_MasterProgression) as Hediff_MasterProgression;

        public static float GetExperience(Pawn pawn) => GetRecord(pawn)?.Severity ?? NoRecord;

        public static bool IsHollow(Pawn pawn)
            => SSCDefOf.SSC_PersonalityExcreted_Done != null &&
               pawn?.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true;

        /// <summary>记录可留在退出身份的人格中；是否提供加成只由当前身份与空壳状态决定。</summary>
        public static bool CanCarryRecord(Pawn pawn)
            => pawn != null && !pawn.Destroyed && !pawn.Discarded && pawn.RaceProps?.Humanlike == true &&
               pawn.health?.hediffSet != null && pawn.TryGetComp<CompSexSlaveTraining>() != null &&
               SSCDefOf.SSC_Hediff_MasterProgression != null;

        // 身份决定是否享有阶段加成，永久资格不参与启停；残留主人身份的空壳也不能生效。
        public static bool IsActive(Pawn pawn) => SSCIdentityUtility.IsMaster(pawn) && !IsHollow(pawn);

        /// <summary>保存不合法数值不能进入阶段计算；经验上限与 XML 定义保持一致。</summary>
        public static float NormalizeExperience(float experience)
        {
            // 异常浮点值无法可靠比较阶段阈值；真实上限从 Def 读取，避免与 XML 双写数值。
            if (float.IsNaN(experience) || float.IsInfinity(experience)) return 0f;
            return System.Math.Max(0f, System.Math.Min(experience, SSCDefOf.SSC_Hediff_MasterProgression?.maxSeverity ?? 0f));
        }

        /// <summary>由首次身份选择、旧档迁移或人格恢复显式建立，不从界面、查询或定时扫描补建。</summary>
        public static bool EnsureInitialRecord(Pawn pawn)
        {
            // 先检查身体，再保留已有实例与经验。重新进入主人身份不会重置历史成长。
            if (!CanCarryRecord(pawn) || IsHollow(pawn)) return false;
            if (GetRecord(pawn) != null) return true;

            // 以零经验创建后交给原版健康添加流程；是否生效由当前身份决定，
            // 人格恢复可在非主人身份下重建暂停记录，随后写入来源的精确经验。
            Hediff record = HediffMaker.MakeHediff(SSCDefOf.SSC_Hediff_MasterProgression, pawn);
            record.Severity = 0f;
            pawn.health.AddHediff(record);
            return GetRecord(pawn) != null;
        }

        /// <summary>普通进入主人可建立基础阶段，人格恢复与重复设置只刷新现有记录。</summary>
        public static void NotifyIdentityChanged(Pawn pawn, bool allowInitialRecord)
        {
            // 只有普通真实身份切换允许首次建立；恢复调用传 false，等待来源快照替换。
            if (allowInitialRecord && IsActive(pawn)) EnsureInitialRecord(pawn);
            RefreshActivation(pawn);
        }

        /// <summary>身份启停即使未改经验也立即重算健康；更新后原版负责倒地等状态变化。</summary>
        public static void RefreshActivation(Pawn pawn)
        {
            Hediff_MasterProgression record = GetRecord(pawn);
            if (record == null) return;

            // 规范化读回的已有经验，不创建缺失记录。Severity 的原生 setter 只在
            // 跨阶段时通知健康，因此身份启停即使经验未变，也须显式刷新能力缓存。
            float normalized = NormalizeExperience(record.Severity);
            if (record.Severity != normalized) record.Severity = normalized;
            pawn.health.Notify_HediffChanged(record);
        }

        /// <summary>提取、植入清理及新克隆清除全部旧记录，并封住后续旧档误补。</summary>
        public static void ClearPersonalityRecord(Pawn pawn)
        {
            if (pawn?.health?.hediffSet != null)
            {
                // 移除全部重复实例；先取列表快照，避免 RemoveHediff 修改正在枚举的集合。
                foreach (Hediff record in pawn.health.hediffSet.hediffs.Where(h => IsProgressionDef(h?.def)).ToList())
                    pawn.health.RemoveHediff(record);
            }
            // 清空是现代人格的明确结果，必须同步写版本，防止下次读档按残留身份补建。
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp != null) comp.masterProgressionVersion = CurrentVersion;
        }

        /// <summary>无记录也清宿主；非空来源精确替换，绝不取宿主较高经验。</summary>
        public static bool RestoreRecord(Pawn pawn, float experience)
        {
            // 快照先规范化，但外部直接调用同样不能让异常值清掉宿主或误建基础阶段。
            if (float.IsNaN(experience) || float.IsInfinity(experience) || experience < 0f && experience != NoRecord) return false;
            if (experience >= 0f && (!CanCarryRecord(pawn) || IsHollow(pawn))) return false;

            // 无记录来源也执行清理；-1 与基础阶段 0 不同，不保留宿主的较高经验。
            ClearPersonalityRecord(pawn);
            if (experience < 0f) return true;

            // 先建立来源人格的独立实例再赋经验。此处不要求当前为主人，
            // 退出身份后保存的成长也能恢复，但健康加成继续保持暂停。
            if (!EnsureInitialRecord(pawn)) return false;
            GetRecord(pawn).Severity = NormalizeExperience(experience);
            RefreshActivation(pawn);
            return true;
        }

        /// <summary>克隆初始化清继承成果及主人身份；成熟复制只过滤来源，不清后来独立取得的成果。</summary>
        public static void InitializeNewClone(Pawn clone)
        {
            // 只在新克隆准备期间清继承记录；成熟阶段仅过滤复制，不调用本方法。
            // 不撤销克隆以后独立取得的主人身份，也不追溯改写旧克隆。
            ClearPersonalityRecord(clone);
            CompSexSlaveTraining comp = clone?.TryGetComp<CompSexSlaveTraining>();
            if (comp?.pawnIdentity == PawnIdentity.Master) comp.pawnIdentity = PawnIdentity.Unset;
        }

        /// <summary>实践与调试共用增加入口；不为现代无记录主人或空壳自动创建阶段。</summary>
        public static float TryAddExperience(Pawn pawn, int amount)
        {
            // 调试和将来的实践经验都只授予存活的有效主人，且必须已有成长记录。
            // 无记录返回零，不把加经验接口变成另一条身份建立或旧档补建入口。
            if (amount <= 0 || pawn == null || pawn.Dead || !CanCarryRecord(pawn) || !IsActive(pawn)) return 0f;
            Hediff_MasterProgression record = GetRecord(pawn);
            if (record == null) return 0f;

            // 原生严重度写入负责跨阶段通知，最大值由 Def 截断；返回实际增量，
            // 满级时为零，调用者不需要再保存或直接修改一份重复等级。
            float previous = NormalizeExperience(record.Severity);
            record.Severity = NormalizeExperience(previous + amount);
            return record.Severity - previous;
        }
    }
}
