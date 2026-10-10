using System.Collections.Generic;
using System.Linq;
using Verse;

// 只复现本功能所需的引擎记忆接口；资格、归属与反馈入口链接生产源码。
namespace RimWorld
{
    public sealed class ThoughtDef
    {
        public string defName;
        public int stackLimit;
        public float durationDays;
        public float moodEffect;
    }

    public sealed class Thought_Memory
    {
        public ThoughtDef def;
        public Pawn otherPawn;
        public int age;
    }

    public sealed class Need_Mood { public ThoughtHandler thoughts = new ThoughtHandler(); }
    public sealed class ThoughtHandler { public MemoryThoughtHandler memories = new MemoryThoughtHandler(); }
    public sealed class MemoryThoughtHandler
    {
        public readonly List<Thought_Memory> Memories = new List<Thought_Memory>();
        public int GainCalls;

        /// <summary>按已核验的原生普通记忆规则，同定义同对象达到上限时 Renew 最老一份。</summary>
        public void TryGainMemory(ThoughtDef def, Pawn otherPawn = null)
        {
            GainCalls++;
            var group = Memories.Where(m => m.def == def && m.otherPawn == otherPawn).ToList();
            if (group.Count >= def.stackLimit && group.Count > 0)
                group.OrderByDescending(m => m.age).First().age = 0;
            else
                Memories.Add(new Thought_Memory { def = def, otherPawn = otherPawn });
        }
    }

    public struct ThoughtState
    {
        public bool Active;
        public int StageIndex;
        public static ThoughtState Inactive => default;
        public static ThoughtState ActiveAtStage(int index) => new ThoughtState { Active = true, StageIndex = index };
    }

    public class ThoughtWorker
    {
        protected virtual ThoughtState CurrentStateInternal(Pawn p) => ThoughtState.Inactive;
        protected virtual ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn) => ThoughtState.Inactive;
        /// <summary>仅暴露原版 protected 查询以运行真实 Worker。</summary>
        public ThoughtState EvaluateForTest(Pawn p, Pawn otherPawn) => CurrentSocialStateInternal(p, otherPawn);
        /// <summary>非社交条件心情的查询入口，对应原版 CurrentState(Pawn)。</summary>
        public ThoughtState EvaluateStateForTest(Pawn p) => CurrentStateInternal(p);
    }
}
