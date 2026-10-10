using RimWorld;
using Verse;

// EN: This bootstrap adjusts SSC thought defs after they are loaded.
// EN: It tweaks opinion offsets for training-memory thoughts.
// CN: 这个启动器会在 SSC 想法 Def 载入后做二次调整。
// CN: 它会微调“调教记忆”的好感度偏移。同床效果由睡眠结束时生成的记忆处理。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SSCThoughtBootstrap
    {
        /// <summary>在 Def 加载完成后的启动阶段应用训练记忆的好感度修正。</summary>
        static SSCThoughtBootstrap()
        {
            AdjustExistingThoughtDefs();
        }

        /// <summary>设置两档社交训练记忆的好感偏移；同床记忆由独立的睡眠结算入口生成。</summary>
        private static void AdjustExistingThoughtDefs()
        {
            // EN: SSC's social training memories need stronger opinion offsets than the XML default values.
            // CN: SSC 的“调教记忆”社交想法需要比 XML 默认值更强一点的好感度偏移。
            SetOpinionOffset(SSCDefOf.SSC_Training_Social_Lvl2, 5);
            SetOpinionOffset(SSCDefOf.SSC_Training_Social_Lvl3, 15);

            // 训导官弱套的旧模式社交 Def 已在 XML 中直接写入运行后的整数值（+3／+8），
            // 这里刻意不做二次改写，避免把弱值错误地折算成 +5／+15 或再乘一次系数。
            // 弱值口径见 TrainerTrainingMemoryUtility.WeakFactor 与设计文档第 4.1 节。
        }

        /// <summary>更新指定心情第一阶段的好感效果；定义或阶段缺失时跳过。</summary>
        /// <param name="def">需要调整的训练心情定义。</param>
        /// <param name="opinionOffset">对记忆关联角色的好感偏移值，不是心情增减值。</param>
        private static void SetOpinionOffset(ThoughtDef def, int opinionOffset)
        {
            if (def?.stages == null || def.stages.Count == 0 || def.stages[0] == null) return;
            def.stages[0].baseOpinionOffset = opinionOffset;
        }
    }

}
