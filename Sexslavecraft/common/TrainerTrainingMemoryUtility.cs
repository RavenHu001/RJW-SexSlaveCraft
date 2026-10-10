using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>本次日常调教完成时应当发放的记忆套别；一次结算只读取一次。</summary>
    public enum TrainingMemorySet
    {
        /// <summary>主人与其余原本合法的执行者，继续使用主人原套 Def。</summary>
        Existing = 0,

        /// <summary>结算时仍满足有效任职的 SSC 训导官，改发独立弱化套。</summary>
        Officer = 1,

        /// <summary>结算时执行者仍是 SSC 性奴但已不满足有效任职；本次不发受训者记忆。</summary>
        None = 2
    }

    /// <summary>
    /// 训导官弱化套的共用换算与套别分流。
    /// 这里只做只读判定和数值换算，不发奖、不改正式收益、不清理历史记忆。
    /// </summary>
    public static class TrainerTrainingMemoryUtility
    {
        /// <summary>维护者确认的弱化系数；XML 中的弱心情整数按此值与主人原值互相校核。</summary>
        public const float WeakFactor = 0.5f;

        /// <summary>
        /// 在玩家可见的情绪／社交层面保留变化：非零基准被舍入成零时至少给出 1 的绝对值。
        /// </summary>
        private const int MinimumVisibleMagnitude = 1;

        /// <summary>
        /// 只读判定本次结算使用的套别。
        /// 必须在正式成长与绑定效果应用之前调用一次，进入效果应用后不再重查任职，
        /// 避免同一次结算前后选择两套记忆。
        /// </summary>
        public static TrainingMemorySet Resolve(Pawn executor)
        {
            if (executor == null) return TrainingMemorySet.None;
            if (TrainerOfficerFeedback.IsActiveOfficer(executor)) return TrainingMemorySet.Officer;

            // 主人与原本合法的其他执行者继续走原套；这里不能按“是否为 SSC 性奴”一刀切，
            // 否则从未担任训导官的普通性奴执行者会被误判成失格者而拿不到原有反馈。
            if (!SSCIdentityUtility.IsSexSlave(executor)) return TrainingMemorySet.Existing;

            // 只有明确开启过训导官身份的个人才属于“失格”边界。
            // 未初始化的旧档性奴与从未选择的性奴仍按原本合法的执行者处理。
            CompSexSlaveTraining comp = executor.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !comp.trainerIdentityInitialized) return TrainingMemorySet.Existing;

            // 承担过训导官职责但结算时已不满足有效任职：本次不发受训者记忆，
            // 也不能通过失格回退到较强原套。这一边界只处理记忆发放，
            // 不自行回滚已发生的正式效果，也不重试结算。
            return TrainingMemorySet.None;
        }

        /// <summary>
        /// 把主人运行基准换算成训导官弱值：乘 K 后四舍五入到整数，
        /// 并对非零基准保底绝对值 1，避免小幅效果被截断成零效果。
        /// </summary>
        public static int WeakValue(int baseline)
        {
            if (baseline == 0) return 0;

            float scaled = baseline * WeakFactor;
            // 使用绝对值四舍五入再补回符号，明确“五入”而不是银行家舍入。
            int rounded = Mathf.RoundToInt(Mathf.Abs(scaled));
            if (rounded < MinimumVisibleMagnitude) rounded = MinimumVisibleMagnitude;
            return baseline < 0 ? -rounded : rounded;
        }

        /// <summary>按新模式本次好感变化 D 计算弱套实际社交偏移；D=0 由调用方提前拦截。</summary>
        public static int WeakOpinionOffset(float opinionDelta)
        {
            // 原算法已经对 D 取整，这里直接对整数基准做换算，避免出现半值。
            int baseline = Mathf.RoundToInt(opinionDelta);
            return WeakValue(baseline);
        }

        /// <summary>
        /// 按本次结算已确定的套别发放新模式受训者记忆。
        /// 主人分支保持原 Def 与原取值；训导官分支使用独立弱套，档位仍沿用未弱化的好感变化。
        /// 这里集中承接发放，使生产结算入口与回归用例共用同一条真实路径。
        /// </summary>
        /// <param name="memorySet">结算入口只读判定一次的套别。</param>
        /// <param name="opinionDelta">本次好感变化 D 的整数基准。</param>
        /// <param name="executor">实际执行者，同时作为社交记忆的对象。</param>
        /// <param name="receiver">领取记忆的受训者。</param>
        public static void ApplyDynamicMemories(TrainingMemorySet memorySet, int opinionDelta, Pawn executor, Pawn receiver)
        {
            // 失格执行者不发本次受训者记忆，也不回退领取较强的原套。
            if (memorySet == TrainingMemorySet.None) return;

            // 零变化不发心情／社交记忆；该判断对两套完全一致。
            if (opinionDelta == 0) return;

            MemoryThoughtHandler memories = receiver?.needs?.mood?.thoughts?.memories;
            if (memories == null) return;

            // 档位一律由未弱化的 D 决定，弱化只改变实际强度，不改变感受分类。
            int displayStage = Thought_MemoryDynamicTraining.GetDisplayStage(opinionDelta);

            if (memorySet == TrainingMemorySet.Officer)
            {
                ApplyOfficerDynamicMemories(opinionDelta, displayStage, executor, memories);
                return;
            }

            if (SSCDefOf.SSC_Training_MoodDynamic != null)
            {
                var moodThought = ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_Training_MoodDynamic, displayStage);
                memories.TryGainMemory(moodThought, executor);
            }

            if (SSCDefOf.SSC_Training_OpinionDynamic != null)
            {
                var socialThought = (Thought_MemoryDynamicTraining)ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_Training_OpinionDynamic);
                socialThought.opinionOffset = opinionDelta;
                memories.TryGainMemory(socialThought, executor);
            }
        }

        /// <summary>
        /// 训导官弱套发放：心情按同一档位取弱心情 Def，社交按保存阶段创建并写入弱化后的整数偏移。
        /// </summary>
        private static void ApplyOfficerDynamicMemories(int opinionDelta, int displayStage, Pawn officer, MemoryThoughtHandler memories)
        {
            // 四档共用同一个弱心情 Def，档位由 stageIndex 表达，与主人动态心情 Def 的结构一致。
            if (SSCDefOf.SSC_TrainerTraining_MoodDynamic != null)
            {
                var moodThought = ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_TrainerTraining_MoodDynamic, displayStage);
                memories.TryGainMemory(moodThought, officer);
            }

            if (SSCDefOf.SSC_TrainerTraining_OpinionDynamic != null)
            {
                var socialThought = (Thought_MemoryTrainerTraining)ThoughtMaker.MakeThought(
                    SSCDefOf.SSC_TrainerTraining_OpinionDynamic, displayStage);
                // Init 只会填入定义基值，实际弱化偏移必须在入组前写到实例上。
                socialThought.opinionOffset = WeakOpinionOffset(opinionDelta);
                memories.TryGainMemory(socialThought, officer);
            }
        }
    }
}
