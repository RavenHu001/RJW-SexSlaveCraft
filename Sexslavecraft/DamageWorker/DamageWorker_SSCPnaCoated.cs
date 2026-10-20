// AI 辅助生成：通用涂覆伤害，先执行原物理流程，再按本次实际开放伤口投药。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>携液伤害的 XML 扩展；同一处理器通过不同 DamageDef 配置各武器的剂量。</summary>
    public class SSCPnaCoatingExtension : DefModExtension
    {
        /// <summary>归一化基础负荷：0.15 表示 15 个百分点，不是旧枪扩展的原始严重度。</summary>
        public float baseLoad;

        /// <summary>既有高意识抗性快照的定义；严重度保存意识除数，不保存本次药量。</summary>
        public HediffDef recordHediff;
    }

    /// <summary>先完成通用物理伤害，再按本次实际开放伤口输入一次 PNA。</summary>
    /// <remarks>
    /// 三种载体共用此类，伤害、穿甲、伤势及剂量由各自定义配置。
    /// 继承通用 AddInjury，不执行 Stab 工人专有的主动内部命中与内外层伤害分摊，
    /// 也不继承旧 PNA 发射器的固定创伤、有效链无伤与治疗逻辑。
    /// </remarks>
    public class DamageWorker_SSCPnaCoated : DamageWorker_AddInjury
    {
        /// <summary>以一次 Apply 为一次携液命中；多个部位结果只共同决定是否能够投药。</summary>
        /// <remarks>药效配置或目标不合格时仍执行父类，并原样返回其结果，保留原战斗日志与伤势。</remarks>
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            // 转移的是物理损伤，不是一次新的药液接触。仍执行同一 AddInjury 流程，
            // 但在任何意识取样、快照、负荷或受药反馈之前结束携液分支。
            if (SSCPnaDamageShareUtility.IsRedirectedHit(thing)) return base.Apply(dinfo, thing);
            Pawn pawn = thing as Pawn;
            var extension = dinfo.Def?.GetModExtension<SSCPnaCoatingExtension>();
            // 全部为本次调用的局部状态；工人复用、同 tick 与嵌套命中互不认领。
            float dose = 0f;
            SSCPnaWeaponResistanceSample resistance = default;
            HediffDef recordDef = extension?.recordHediff;
            HashSet<BodyPartRecord> organicParts = null;
            HashSet<Hediff> previousRecords = null;
            // 这里只决定是否准备药效，不提前 return：缺少扩展、非 Pawn 或非法药量
            // 均不能吞掉武器本来的物理伤害。null 表示未准备，空集合表示没有合格部位。
            if (Scribe.mode == LoadSaveMode.Inactive && SSCPnaExposureUtility.IsValidTarget(pawn) &&
                recordDef != null && extension != null &&
                SSCPnaMath.IsFinite(extension.baseLoad) && extension.baseLoad > 0f)
            {
                // 只读锁定物理受伤前的意识；共用算法不提前写入快照或改变身体形态。
                resistance = SSCPnaWeaponDoseUtility.Capture(pawn, recordDef);
                dose = SSCPnaWeaponDoseUtility.FromLoad(extension.baseLoad, resistance);
                // 同时保存受伤前的部位性质与健康实例，既防人工部位被摧毁后误判，
                // 也防把命中前的旧伤或旧缺失记录当成本次新开口。
                organicParts = CaptureOrganicParts(pawn);
                previousRecords = new HashSet<Hediff>(pawn.health.hediffSet.hediffs);
            }

            // 不改伤害、穿甲或传播；尤其不执行旧枪的固定创伤、无伤及治疗分支。
            DamageResult result = base.Apply(dinfo, thing);
            if (organicParts == null || !SSCPnaMath.IsFinite(dose) || dose <= 0f ||
                !HasOpenWound(pawn, result, organicParts, previousRecords)) return result;

            // 原版伤势写入和身体协调都可能同步触发回调，因而不能沿用命中前的身份。
            // 先协调遗留状态，再判主人免疫；锁链分流由共享入口按届时的真实关系处理。
            SSCPnaExposureUtility.RefreshBodyState(pawn);
            bool CanContinue() => Scribe.mode == LoadSaveMode.Inactive &&
                SSCPnaExposureUtility.IsValidTarget(pawn) && !SSCIdentityUtility.IsMaster(pawn) &&
                HasOpenWound(pawn, result, organicParts, previousRecords);
            if (!CanContinue()) return result;

            Hediff record = SSCPnaWeaponDoseUtility.StoreSnapshot(pawn, recordDef, resistance);
            if (record != null)
            {
                // 健康回调看到的即为完整快照；不覆盖嵌套独立命中先建立的记录。
                // 剂量仍使用外层命中前锁定的值，不因内层输入后的状态改变而重新计算。
                SSCPnaExposureUtility.RefreshBodyState(pawn);
                if (!CanContinue() || !pawn.health.hediffSet.hediffs.Contains(record))
                {
                    // 只清本次未能使用的新快照，不动既有或嵌套命中的记录。
                    SSCPnaWeaponDoseUtility.RemoveUnusedSnapshot(pawn, record);
                    return result;
                }
            }

            // 成瘾、需求、负荷上限和普通／活化载体全部复用共享规则。
            // 继续条件会在共享工具的回调边界复查；它中止后续写入，不负责回滚已合法
            // 加入的身体负荷。物理伤害已完成，不因药效结果重伤一次或替目标治疗。
            var exposure = SSCPnaExposureUtility.ApplyExposure(pawn, dose, SSCPnaExposureSource.Weapon, CanContinue);
            // 反馈一次已被接受的真实投送；满负荷时 AddedLoad 为零也应显示，传播伤口不逐个冒心。
            if (exposure.Accepted) SSCPnaWeaponEffectsUtility.ShowAbsorption(pawn);
            return result;
        }

        /// <summary>只读保存命中前尚存在的可受药软组织部位，不持久化或修复身体结构。</summary>
        private static HashSet<BodyPartRecord> CaptureOrganicParts(Pawn pawn)
        {
            var parts = new HashSet<BodyPartRecord>();
            var allParts = pawn.RaceProps?.body?.AllParts;
            if (allParts == null) return parts;
            foreach (BodyPartRecord part in allParts)
                if (!pawn.health.hediffSet.PartIsMissing(part) && IsOrganicPart(pawn, part)) parts.Add(part);
            return parts;
        }

        /// <summary>检查当前部位及其父级安装状态，排除实体组织和非有机人工部位。</summary>
        private static bool IsOrganicPart(Pawn pawn, BodyPartRecord part)
        {
            if (part?.def == null || part.def.IsSolid(part, pawn.health.hediffSet.hediffs)) return false;
            // 子部位不一定有直接的安装记录，必须沿父级检查；有机移植物仍可形成肉体伤口。
            for (BodyPartRecord current = part; current != null; current = current.parent)
            {
                Hediff added = pawn.health.hediffSet.GetDirectlyAddedPartFor(current);
                if (added != null && !added.def.organicAddedBodypart) return false;
            }
            return true;
        }

        /// <summary>首批只接原版刺伤与切割伤；弩矢的 Arrow 伤害在肉体上产生 Cut。</summary>
        // 检查实际伤势而非最初 DamageDef，因此护甲转钝伤产生的 Bruise／Crush 不会投药。
        // 不用实时 BleedRate 判断：不流血的种族、基因或止血状态不等于没有开放伤口。
        private static bool IsOpenInjury(HediffDef injury)
            => injury?.defName == "Stab" || injury?.defName == "Cut";

        /// <summary>只承认本次结果中、当前仍实际写入受击身体的合格新伤口。</summary>
        /// <remarks>
        /// 原版 Stab／Cut 不合并，实例身份可作为依据；第三方改写伤口合并时须另行适配。
        /// 本方法只读取状态，可在协调、快照写入及共享药效的回调边界重复调用。
        /// </remarks>
        private static bool HasOpenWound(Pawn pawn, DamageResult result,
            HashSet<BodyPartRecord> organicParts, HashSet<Hediff> previousRecords)
        {
            if (!SSCPnaExposureUtility.IsValidTarget(pawn) || result?.hediffs == null ||
                !result.wounded || result.totalDamageDealt <= 0f) return false;
            // wounded／总伤害只作粗筛，不能单独证明破皮；整次 deflected 也不能直接否决，
            // 因为传播中可能同时存在被挡住的部位和真正形成伤口的部位。
            foreach (Hediff record in result.hediffs)
            {
                // 结果条目可能只是尝试添加的对象，或已被健康回调移除，须核对真实实例。
                if (record == null || record.pawn != pawn || record.Part == null ||
                    previousRecords.Contains(record) || !organicParts.Contains(record.Part) ||
                    !pawn.health.hediffSet.hediffs.Contains(record)) continue;
                // 命中前合格、现在也合格才承认：期间安装人工部位或移除伤口应立即失效。
                if (record is Hediff_Injury injury && IsOpenInjury(injury.def) &&
                    SSCPnaMath.IsFinite(injury.Severity) && injury.Severity > 0f &&
                    !pawn.health.hediffSet.PartIsMissing(record.Part) && IsOrganicPart(pawn, record.Part)) return true;
                // 断肢时原伤口可能已被原版清理，以本次实际留下的新鲜自然缺失确认开口。
                if (record is Hediff_MissingPart missing && missing.IsFresh &&
                    IsOpenInjury(missing.lastInjury) && IsOrganicPart(pawn, record.Part)) return true;
            }
            return false;
        }
    }
}
