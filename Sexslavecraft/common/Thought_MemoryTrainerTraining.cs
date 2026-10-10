using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>
    /// 训导官动态社交记忆：按保存的阶段显示与分组，实际好感为弱化后的整数偏移。
    /// 原 <see cref="Thought_MemoryDynamicTraining"/> 按实际 opinionOffset 反推档位，
    /// 弱化后会把沉沦误显示为适应，所以弱套使用本类按阶段索引取值。
    /// </summary>
    public sealed class Thought_MemoryTrainerTraining : Thought_MemorySocial
    {
        /// <summary>四档标签与描述的本地化键后缀，顺序与 SSC_Training_MoodDynamic 阶段一致。</summary>
        private static readonly string[] StageKeys =
        {
            "Resentment",
            "Wavering",
            "Adaptation",
            "Submission"
        };

        /// <summary>当前阶段在合法范围内时取其键后缀；阶段越界时回退到首个阶段。</summary>
        private static string StageKey(int stageIndex)
            => (stageIndex >= 0 && stageIndex < StageKeys.Length) ? StageKeys[stageIndex] : StageKeys[0];

        /// <summary>按保存阶段取标签，不回读 opinionOffset 重判档位。</summary>
        public override string LabelCap
            => ("SSC_TrainerTrainingOpinionMemory_" + StageKey(CurStageIndex))
                .Translate()
                .CapitalizeFirst();

        /// <summary>
        /// 社交栏标签沿用原版写法：把关联角色的短名格式化进标签。
        /// 原版 <c>Thought_Memory.GroupsWith</c> 在人物不同时会退回标签比较，
        /// 若标签不含对象名，同档位但不同训导官的记录会被误判为同组。
        /// </summary>
        public override string LabelCapSocial
            => ("SSC_TrainerTrainingOpinionMemory_" + StageKey(CurStageIndex))
                .Translate()
                .CapitalizeFirst()
                + " (" + (otherPawn?.LabelShort ?? "???") + ")";

        /// <summary>描述展示实际执行者与实际弱化偏移；偏移为整数，不存在半值展示。</summary>
        public override string Description
            => ("SSC_TrainerTrainingOpinionMemory_" + StageKey(CurStageIndex) + "Desc")
                .Translate(otherPawn?.LabelShort ?? "???", opinionOffset.ToStringWithSign());

        /// <summary>
        /// 原版社交记忆只在同定义、同对象时同组，不比较档位；
        /// 本类补上保存阶段的比较，使不同感受档位各自成组、同档位才相互刷新。
        /// 这里显式再比一次定义：原版基类在人物不同时会退回标签比较，
        /// 而标签按阶段取值，弱套与主人原套的同档标签可能相同，不能据此互相合并。
        /// </summary>
        public override bool GroupsWith(Thought other)
        {
            if (!(other is Thought_MemoryTrainerTraining trainerMemory)) return false;
            if (!ReferenceEquals(def, trainerMemory.def)) return false;

            return base.GroupsWith(other)
                && CurStageIndex == trainerMemory.CurStageIndex;
        }
    }
}
