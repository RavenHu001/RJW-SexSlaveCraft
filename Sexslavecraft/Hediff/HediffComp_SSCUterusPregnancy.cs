// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>随单次妊娠保存的固定资格，不保存或恢复母体开发进度。</summary>
    public sealed class HediffComp_SSCUterusPregnancy : HediffComp
    {
        private enum Decision { Undecided = 0, Normal = 1, Shortened = 2, FertilityBasic, FertilitySkilled, FertilityExpert, FertilityComplete, FertilityFinal }
        // 新对象等待真实 PostAdd；旧档缺字段的默认值由 CompExposeData 单独指定。
        private Decision decision = Decision.Undecided;

        /// <summary>只读取首次加入时固定的待遇，不重新查询当前母体或开关。</summary>
        public bool IsShortened => DurationFactor < 1f;
        /// <summary>旧Shortened仍为0.8；新增枚举映射已合并子宫的总时长系数，孕中不重判。</summary>
        public float DurationFactor => decision switch
        {
            Decision.Shortened => .8f, Decision.FertilityBasic => .72f, Decision.FertilitySkilled => .60f,
            Decision.FertilityExpert => .48f, Decision.FertilityComplete => .40f, Decision.FertilityFinal => .32f,
            _ => 1f
        };

        /// <summary>仅在妊娠真实加入母体时判定一次，重复 Add 或 RJW Initialize 不补领。</summary>
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || decision != Decision.Undecided ||
                Pawn?.health?.hediffSet?.hediffs.Contains(parent) != true) return;
            // 不从 PostMake 授权；加入时不合格也固定为普通，后续满级或开启不改变结果。
            var record = SSCUterusDevelopmentUtility.Find(Pawn);
            // Stage的0..5与追加待遇一一对应；无特化/失格回落到原子宫Shortened。
            decision = SSCUterusPregnancyUtility.IsSupported(parent) && record?.FertilityEnabled == true
                ? (Decision)(2 + Math.Max(0, FertilitySpecializationUtility.Stage(Pawn))) : Decision.Normal;
        }

        /// <summary>正常存读档保留待遇；旧档、未完成判定及异常枚举均按普通妊娠恢复。</summary>
        public override void CompExposeData()
        {
            Scribe_Values.Look(ref decision, "sscUterusPregnancyDecision", Decision.Normal);
            // LoadingVars 不依赖尚未恢复的母体引用，也不在 PostLoadInit 重新判断资格。
            if (Scribe.mode == LoadSaveMode.LoadingVars && (decision < Decision.Shortened || decision > Decision.FertilityFinal))
                decision = Decision.Normal;
        }
    }
}
