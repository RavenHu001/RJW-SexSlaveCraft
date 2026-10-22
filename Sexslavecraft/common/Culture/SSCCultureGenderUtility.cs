// AI 辅助生成：1.7 性奴性别规范的只读查询；实际身份、名册及刷新继续由既有入口负责。
using System;
using System.Collections.Generic;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCCultureGenderPolicy { Unrestricted, FemaleOnly, MaleOnly, FemaleRequired, MaleRequired, NonMasterRequired }

    public static class SSCCultureGenderUtility
    {
        // 固定五个非默认候选，按原版 HasPrecept 的集合查询读取现态；不缓存 Ideo 结果或缺失 Def。
        // 老档未有新 Issue、未启用 DLC 或尚未装载定义时均等价于性别不限，不自动补写文化。
        private static readonly string[] names = {
            "SSC_Precept_SlaveGender_FemaleOnly", "SSC_Precept_SlaveGender_MaleOnly",
            "SSC_Precept_SlaveGender_FemaleRequired", "SSC_Precept_SlaveGender_MaleRequired",
            "SSC_Precept_SlaveGender_NonMasterRequired"
        };

        public static SSCCultureGenderPolicy Policy(Pawn observer)
        {
            if (!ModsConfig.IdeologyActive || observer?.Ideo == null) return SSCCultureGenderPolicy.Unrestricted;
            for (int i = 0; i < names.Length; i++)
            {
                PreceptDef def = DefDatabase<PreceptDef>.GetNamedSilentFail(names[i]);
                if (def != null && observer.Ideo.HasPrecept(def)) return (SSCCultureGenderPolicy)(i + 1);
            }
            return SSCCultureGenderPolicy.Unrestricted;
        }

        public static bool Accepts(SSCCultureGenderPolicy policy, Gender gender)
        {
            // None 是真实的第三种 Pawn.gender；在限定档不被接受，在不限及非主即奴中被接受。
            switch (policy)
            {
                case SSCCultureGenderPolicy.FemaleOnly:
                case SSCCultureGenderPolicy.FemaleRequired: return gender == Gender.Female;
                case SSCCultureGenderPolicy.MaleOnly:
                case SSCCultureGenderPolicy.MaleRequired: return gender == Gender.Male;
                default: return true;
            }
        }

        // 男女必须只对对应性别产生义务；非主即奴包含 None，主人豁免由身份分支负责。
        public static bool Requires(SSCCultureGenderPolicy policy, Gender gender)
            => policy == SSCCultureGenderPolicy.NonMasterRequired
                || policy == SSCCultureGenderPolicy.FemaleRequired && gender == Gender.Female
                || policy == SSCCultureGenderPolicy.MaleRequired && gender == Gender.Male;

        public static bool IsAdult(Pawn pawn)
        {
            // 与已有宠物后续互动的成人子条件一致；不带入清醒、站立、同图或性行为资格。
            // 原版 Adult 阶段也可能覆盖 Teenager，缺少 ageTracker 时该属性还会回退 Adult，不能单独使用。
            return SSCCultureStateUtility.IsLiving(pawn) && pawn.RaceProps?.Humanlike == true
                && pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears >= 18
                && pawn.DevelopmentalStage == DevelopmentalStage.Adult && pawn.GetAgeCategory() == AgeCategory.Adult;
        }

        // 限定不合规只评价实际成年性奴；无链的另一性别普通成员不因其性别受到此项惩罚。
        public static bool IsViolation(Pawn target, SSCCultureGenderPolicy policy)
            => !Accepts(policy, target?.gender ?? Gender.None) && IsAdult(target) && SSCCultureStateUtility.HasChain(target);

        // 先短路当前主人和已有链，避免对已豁免/已履行者重复执行成年查询；不检查途径资格。
        public static bool IsDutyCandidate(Pawn pawn)
            => SSCCultureStateUtility.IsLiving(pawn) && !SSCCultureStateUtility.IsMaster(pawn)
                && !SSCCultureStateUtility.HasChain(pawn) && IsAdult(pawn);

        public static bool HasUnfulfilledDuty(Pawn pawn, SSCCultureGenderPolicy policy)
        {
            // 先短路个人不适用路径，只有实际承担义务的候选才请求名册；预选与主人资格均不豁免。
            if (!Requires(policy, pawn?.gender ?? Gender.None) || !IsDutyCandidate(pawn)) return false;
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            return cache != null && cache.TryIsMember(pawn, out bool member) && member;
        }

        public static bool DutySocial(Pawn observer, Pawn target, SSCCultureGenderPolicy policy)
        {
            // 义务评价才要求双方入册；违规性别评价仍保留一般态度社交的目标范围。
            if (observer == target || !IsAdult(observer) || !HasUnfulfilledDuty(target, policy)) return false;
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            return cache != null && cache.TryIsMember(observer, out bool member) && member;
        }

        public static bool TryEnvironmentCount(Pawn observer, bool duty, out int count)
        {
            count = 0;
            SSCCultureGenderPolicy policy = Policy(observer);
            // 新负向环境允许主人和实际性奴接收；不能复用旧环境的“本人无链”前提。
            if (duty ? !RequiresAny(policy) : (policy == SSCCultureGenderPolicy.Unrestricted
                || policy == SSCCultureGenderPolicy.NonMasterRequired)) return false;
            if (!IsAdult(observer) || observer.Faction?.def?.isPlayer != true) return false;
            var cache = Current.Game?.GetComponent<SSCCultureGameComponent>();
            if (cache == null || !cache.TryIsMember(observer, out bool member) || !member) return false;
            return duty ? cache.TryGetUnfulfilledDutyCount(policy, observer, out count)
                : cache.TryGetGenderViolationCount(policy, observer, out count);
        }

        // 反馈入口在无义务档提前返回，不为不限/限定档请求义务名册与细分。
        public static bool RequiresAny(SSCCultureGenderPolicy policy)
            => policy == SSCCultureGenderPolicy.FemaleRequired || policy == SSCCultureGenderPolicy.MaleRequired
                || policy == SSCCultureGenderPolicy.NonMasterRequired;

        public static bool HasEnvy(Pawn pawn)
        {
            // 用原版戒律集合查询，不推断 thought 是否仍在刷新缓存中，也不缓存按文化的态度结果。
            if (!ModsConfig.IdeologyActive || pawn?.Ideo == null) return false;
            PreceptDef envy = DefDatabase<PreceptDef>.GetNamedSilentFail("SSC_Precept_SexSlave_Envy");
            return envy != null && pawn.Ideo.HasPrecept(envy);
        }
    }

    internal enum SSCCultureGenderFilter { Accepted, Rejected, Required }

    // 每个统计项目各保存三桶与本批贡献；不能在排除本人时混用当前性别或另一批名册的状态。
    internal sealed class SSCCultureGenderBuckets
    {
        private readonly int[] counts = new int[3];
        private readonly Dictionary<Pawn, Gender> contributions = new Dictionary<Pawn, Gender>(SSCBondOverviewReferenceComparer<Pawn>.Instance);

        // 与 GameComponent 的代失效同步清空人数和 Pawn 引用，集合容量留给下一代复用。
        public void Clear() { Array.Clear(counts, 0, counts.Length); contributions.Clear(); }

        public void Add(Pawn pawn)
        {
            // 输入通常已去重，此处仍以引用键防御重复来源；只保存准备当时的实际枚举值。
            if (contributions.ContainsKey(pawn)) return;
            Gender gender = pawn.gender;
            contributions.Add(pawn, gender);
            counts[(int)gender]++;
        }

        private static bool Matches(SSCCultureGenderPolicy policy, SSCCultureGenderFilter filter, Gender gender)
            => filter == SSCCultureGenderFilter.Required ? SSCCultureGenderUtility.Requires(policy, gender)
                : SSCCultureGenderUtility.Accepts(policy, gender) == (filter == SSCCultureGenderFilter.Accepted);

        public int Count(SSCCultureGenderPolicy policy, SSCCultureGenderFilter filter, Pawn except = null)
        {
            // 仅迭代三个桶，不按观察者、文化或档位重扫人口；扣本人在封顶之前完成。
            int count = 0;
            for (int i = 0; i < counts.Length; i++)
                if (Matches(policy, filter, (Gender)i)) count += counts[i];
            if (except != null && contributions.TryGetValue(except, out Gender captured) && Matches(policy, filter, captured)) count--;
            return count;
        }
    }
}
