// AI 辅助生成
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>侵染独立反馈：原结算前冻结两项输入，完成认领后替换标准记忆。</summary>
    public static class SSCDesireInfestationFeedbackUtility
    {
        /// <summary>只使用当前恶堕和带正负的当前好感；非法恶堕不推断成低档。</summary>
        internal static bool TryResolveStage(float corruption, int opinion, out int stage)
        {
            stage = 0;
            if (!SSCPnaMath.IsFinite(corruption)) return false;
            stage = corruption >= .30f ? (opinion >= 0 ? 2 : 3) : (opinion >= 0 ? 0 : 1);
            return true;
        }

        /// <summary>本场局部计划不存档；容器引用防止旧场反馈流入后来创建的心情载体。</summary>
        public sealed class Plan
        {
            private readonly Pawn source, target;
            private readonly Need_Corruption need;
            private readonly Need_Mood mood;
            private readonly MemoryThoughtHandler memories;
            private readonly int stage;
            private bool attempted;

            internal Plan(Pawn source, Pawn target, Need_Corruption need, Need_Mood mood,
                MemoryThoughtHandler memories, int stage)
            {
                this.source = source; this.target = target; this.need = need;
                this.mood = mood; this.memories = memories; this.stage = stage;
            }

            /// <summary>每次创建、移除与添加前后复查人格和原载体；已消费的计划不重放。</summary>
            internal void Apply(Func<bool> isCurrent)
            {
                if (attempted) return;
                attempted = true;
                bool Current() => Scribe.mode == LoadSaveMode.Inactive && isCurrent() &&
                    SSCDesireInfestationUtility.CanAffect(source, target) &&
                    target.needs?.TryGetNeed<Need_Corruption>() == need && target.needs?.mood == mood &&
                    mood.thoughts?.memories == memories;

                // 两个记忆分量分别处理；一个定义/第三方记忆回调异常不阻断另一个合法分量。
                ApplyMemory(SSCDefOf.SSC_DesireInfestation_Mood, false, Current);
                if (Current()) ApplyMemory(SSCDefOf.SSC_DesireInfestation_Opinion, true, Current);
            }

            private void ApplyMemory(ThoughtDef def, bool social, Func<bool> current)
            {
                try
                {
                    // 缺失、错误类型或被补丁改坏的定义不能先删除已有反馈；不直接修改记忆列表。
                    if (!current() || def == null || !def.IsMemory || def.stages == null ||
                        stage < 0 || stage >= def.stages.Count || def.stages[stage] == null ||
                        !SSCPnaMath.IsFinite(def.durationDays) || def.durationDays <= 0f ||
                        !typeof(Thought_Memory).IsAssignableFrom(def.ThoughtClass) ||
                        typeof(Thought_MemorySocial).IsAssignableFrom(def.ThoughtClass) != social) return;
                    float offset = social ? def.stages[stage].baseOpinionOffset : def.stages[stage].baseMoodEffect;
                    if (!SSCPnaMath.IsFinite(offset)) return;

                    // 零社交仅清除本施加者的旧记忆。非零记忆先完成原生准入与实例创建，再替换。
                    Thought_Memory next = null;
                    if (!social || offset != 0f)
                    {
                        if (!ThoughtUtility.CanGetThought(target, def) || !current()) return;
                        if (social && !def.socialTargetDevelopmentalStageFilter.Has(source.DevelopmentalStage)) return;
                        next = ThoughtMaker.MakeThought(def, stage);
                        if (next == null || (next is Thought_MemorySocial) != social || !current()) return;
                    }

                    // 跨档显式替换；社交按真实 Pawn 引用筛选，心情则全局只留下最近一次反应。
                    // 使用旧实例快照逐条删除，回调中恢复的新人格记忆不会被后续循环继续清掉。
                    var previous = memories.Memories.Where(m => m.def == def && (!social || m.otherPawn == source)).ToList();
                    foreach (Thought_Memory memory in previous)
                    {
                        if (!current()) return;
                        if (memories.Memories.Contains(memory)) memories.RemoveMemory(memory);
                        if (!current()) return;
                    }

                    // 标准记忆从年龄零开始；三位主人容量淘汰和社交末段衰减交给原版处理。
                    if (next != null && current()) memories.TryGainMemory(next, source);
                }
                catch (Exception ex)
                {
                    Log.Warning("[SSC] 欲望侵染跳过本次" + (social ? "社交" : "心情") + "反馈：" + ex.GetType().Name);
                }
            }
        }

        /// <summary>读取原结算前的 C/O；缺少关系或心情时只跳过反馈，不创建需求或改变其他收益。</summary>
        internal static Plan Capture(Pawn source, Pawn target, Need_Corruption expectedNeed)
        {
            try
            {
                if (Scribe.mode != LoadSaveMode.Inactive || !SSCDesireInfestationUtility.CanAffect(source, target) ||
                    expectedNeed == null || target.needs?.TryGetNeed<Need_Corruption>() != expectedNeed ||
                    target.relations == null) return null;
                Need_Mood mood = target.needs.mood;
                MemoryThoughtHandler memories = mood?.thoughts?.memories;
                if (memories == null) return null;
                float corruption = expectedNeed.CurLevel;
                int opinion = target.relations.OpinionOf(source);
                return TryResolveStage(corruption, opinion, out int stage)
                    ? new Plan(source, target, expectedNeed, mood, memories, stage) : null;
            }
            catch (Exception ex)
            {
                // 可选关系查询可能由第三方实现；失败不让原 ProcessSex 或前三批收益丢失。
                Log.Warning("[SSC] 欲望侵染未捕获反馈：" + ex.GetType().Name);
                return null;
            }
        }
    }
}
