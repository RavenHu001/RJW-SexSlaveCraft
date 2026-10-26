// AI 辅助生成：只记录原版已取证调用点的心情改动，不回放互动、不扫描清除历史。
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版心情调用的因果记录；作用域、实例及撤销计划均只活在本次调用栈。</summary>
    internal static class SSCRomanceNativeMood
    {
        [ThreadStatic] private static Site pending;
        [ThreadStatic] private static DefinitionCall definition;
        [ThreadStatic] private static MemoryCall current;
        private static readonly AccessTools.FieldRef<Thought_Memory, int> stageReader = CreateStageReader();
        internal static bool StageReady => stageReader != null;

        /// <summary>本机 CurStageIndex 直接返回 forcedStage；读取保存字段避免观察快照再次进入外部显示查询。</summary>
        private static AccessTools.FieldRef<Thought_Memory, int> CreateStageReader()
        {
            try { return AccessTools.FieldRefAccess<Thought_Memory, int>("forcedStage"); }
            catch (Exception) { return null; } // 新构建字段失配只停用接管，不打断原版获得。
        }

        internal sealed class Site
        {
            internal SSCRomanceMoodFrame Frame;
            internal SSCRomanceMoodFrame.Person Person;
            internal MemoryThoughtHandler Handler;
            internal ThoughtDef Def;
            internal ThoughtDef ChildDef;
            internal Pawn Other;
            internal bool Consumed, Admitted, Completed, Invalid;
        }

        internal sealed class DefinitionCall
        {
            internal DefinitionCall Previous;
            internal Site Site;
            internal Thought_Memory Expected;
            internal bool ObjectClaimed;
        }

        internal sealed class MemoryCall
        {
            internal MemoryCall Previous;
            internal Site Site;
            internal Entry Entry;
            internal Thought_Memory Memory;
            internal bool Ran;
        }

        /// <summary>保存原版实例的原始字段；不使用显示值、当前目录或当前人物状态推断历史。</summary>
        internal sealed class State
        {
            internal readonly Thought_Memory Memory;
            internal readonly ThoughtDef def;
            internal readonly Pawn owner, other;
            internal readonly Precept precept;
            internal readonly int age, duration, stage, offset;
            internal readonly float power;
            internal readonly bool permanent;
            internal State(Thought_Memory memory)
            {
                Memory = memory; def = memory.def; owner = memory.pawn; other = memory.otherPawn;
                precept = memory.sourcePrecept; age = memory.age; duration = memory.durationTicksOverride;
                stage = stageReader(memory); offset = memory.moodOffset; power = memory.moodPowerFactor; permanent = memory.permanent;
            }
            internal bool Matches() => Memory.def == def && Memory.pawn == owner && Memory.otherPawn == other &&
                Memory.sourcePrecept == precept && Memory.age == age && Memory.durationTicksOverride == duration &&
                stageReader(Memory) == stage && Memory.moodOffset == offset && Memory.moodPowerFactor == power && Memory.permanent == permanent;
            internal void Restore()
            {
                Memory.age = age;
                // 原生 Renew 只改变年龄；其余保存字段仅用于复查，不重写身份、Def 或外部背景。
            }
        }

        private sealed class Operation
        {
            internal int Kind, Index; // 0 新增、1 移除、2 续期。
            internal State Before, After;
        }

        internal sealed class Entry
        {
            internal Site Site;
            internal Thought_Memory Candidate;
            internal bool Invalid, Sealed;
            private State[] before, after;
            private readonly List<Operation> operations = new List<Operation>();

            /// <summary>只接管确切普通原版心情；派生类和额外关联效果没有已验证的撤销契约。</summary>
            internal Entry(Site site, Thought_Memory candidate)
            {
                Site = site; Candidate = candidate;
                Invalid = candidate.GetType() != typeof(Thought_Memory) || candidate.def.thoughtToMake != null ||
                    candidate.def.replaceThoughts?.Count > 0;
                before = Snapshot();
                Invalid |= before.Any(s => s.Memory.GetType() != typeof(Thought_Memory));
            }
            /// <summary>分组依据初始来源 Def，不能随回调改写后的候选一起换组。</summary>
            private State[] Snapshot() => Site.Handler.Memories.Where(m => m.def == Site.Def).Select(m => new State(m)).ToArray();

            /// <summary>实际操作逐条记账；外部回调产生的净差异不能借此获得本次事件的因果身份。</summary>
            internal void Added(Thought_Memory memory) => operations.Add(new Operation { Kind = 0, After = new State(memory) });
            internal object BeforeRemove(Thought_Memory memory) => new Operation { Kind = 1, Index = Site.Handler.Memories.IndexOf(memory), Before = new State(memory) };
            internal void Removed(object saved)
            {
                var op = (Operation)saved;
                if (op.Index < 0 || !op.Before.Matches() || Site.Handler.Memories.Contains(op.Before.Memory)) Invalid = true;
                else operations.Add(op);
            }
            internal object BeforeRenew(Thought_Memory memory) => new Operation { Kind = 2, Before = new State(memory) };
            /// <summary>本机原生 Renew 只能把年龄归零；其他字段变化属于未取证外部行为，保留原版但不接管。</summary>
            internal void Renewed(object saved)
            {
                var op = (Operation)saved; op.After = new State(op.Before.Memory);
                Invalid |= op.After.age != 0 || !SameState(op.Before, op.After, true);
                operations.Add(op);
            }

            /// <summary>用已记录操作验证本次完整结果；未记录的新增、删改或后置异常使接管失败。</summary>
            internal void Seal(bool ran, Exception exception)
            {
                Sealed = true; Invalid |= !ran || exception != null;
                after = Snapshot();
                Invalid |= after.Any(s => s.Memory.GetType() != typeof(Thought_Memory));
                var projected = before.ToDictionary(s => s.Memory);
                foreach (var op in operations)
                {
                    if (op.Kind == 0) { if (projected.ContainsKey(op.After.Memory)) Invalid = true; projected[op.After.Memory] = op.After; }
                    else if (op.Kind == 1)
                    {
                        if (!projected.TryGetValue(op.Before.Memory, out var previous) || !SameState(previous, op.Before)) Invalid = true;
                        projected.Remove(op.Before.Memory);
                    }
                    else
                    {
                        if (!projected.TryGetValue(op.Before.Memory, out var previous) || !SameState(previous, op.Before)) Invalid = true;
                        projected[op.After.Memory] = op.After;
                    }
                }
                Invalid |= projected.Count != after.Length || after.Any(s => !projected.TryGetValue(s.Memory, out var expected) || !expected.Matches());
            }

            /// <summary>创建整体逆序计划前检查最终事实；不在验证过程中修改真实列表。</summary>
            internal bool Plan(Dictionary<Thought_Memory, State> projected)
            {
                if (!Sealed || Invalid || after == null || projected.Count != after.Length || after.Any(s =>
                    !projected.TryGetValue(s.Memory, out var state) || !SameState(state, s))) return false;
                projected.Clear(); foreach (var state in before) projected.Add(state.Memory, state);
                return true;
            }
            private static bool SameState(State a, State b, bool ignoreAge = false) => a.Memory == b.Memory && (ignoreAge || a.age == b.age) && a.duration == b.duration &&
                a.def == b.def && a.owner == b.owner && a.other == b.other && a.precept == b.precept && a.stage == b.stage &&
                a.offset == b.offset && a.power == b.power && a.permanent == b.permanent;
            internal void Undo()
            {
                for (int i = operations.Count - 1; i >= 0; i--)
                {
                    var op = operations[i]; var list = Site.Handler.Memories;
                    if (op.Kind == 0) list.Remove(op.After.Memory);
                    else if (op.Kind == 1) list.Insert(Math.Min(op.Index, list.Count), op.Before.Memory);
                    else op.Before.Restore();
                }
            }
        }

        /// <summary>只从原版 worker 的已匹配调用点进入；普通及嵌套调用仍执行原方法一次。</summary>
        internal static void GainFromWorker(MemoryThoughtHandler handler, ThoughtDef def, Pawn other, Precept precept)
        {
            var frame = SSCRomanceMoodEvents.CurrentWorker;
            var person = frame?.ActorState?.Handler == handler ? frame.ActorState : frame?.TargetState?.Handler == handler ? frame.TargetState : null;
            bool controlled = person?.Applicable == true && (person == frame.ActorState ? frame.Command && person.Data.Stage == 4 :
                person.Data.Stage >= 3 && frame.DecisionKnown && frame.OriginalAccepted != frame.Accepted && frame.Rule.HasValue);
            var site = !controlled || other != (person == frame.ActorState ? frame.Target : frame.Actor) || !KnownRoot(def?.defName) ? null :
                new Site { Frame = frame, Person = person, Handler = handler, Def = def, ChildDef = def.thoughtToMake, Other = other };
            if (site != null) frame.NativeSites.Add(site);
            Gain(handler, def, other, precept, site);
        }

        /// <summary>原生 thoughtToMake 只继承这一次精确父调用；不吞掉社交父记忆及其正常刷新。</summary>
        internal static void GainChild(MemoryThoughtHandler handler, ThoughtDef def, Pawn other, Precept precept)
        {
            var parent = current?.Site;
            bool matches = parent != null && parent.Handler == handler && parent.Other == other &&
                parent.ChildDef == def && parent.Def.thoughtToMake == def && ExpectedChild(parent.Def.defName) == def?.defName;
            var site = matches ? new Site { Frame = parent.Frame, Person = parent.Person, Handler = handler, Def = def, ChildDef = def.thoughtToMake, Other = other } : null;
            if (site != null) site.Frame.NativeSites.Add(site);
            Gain(handler, def, other, precept, site);
        }

        private static bool KnownRoot(string name) => name == "RebuffedMyRomanceAttempt" || name == "FailedRomanceAttemptOnMe" ||
            name == "FailedRomanceAttemptOnMeLowOpinionMood" || name == "RejectedMyProposal" || name == "IRejectedTheirProposal" || name == "BrokeUpWithMe";
        private static string ExpectedChild(string name) => name == "RebuffedMyRomanceAttempt" ? "RebuffedMyRomanceAttemptMood" :
            name == "RejectedMyProposal" ? "RejectedMyProposalMood" : name == "BrokeUpWithMe" ? "BrokeUpWithMeMood" : null;
        private static bool IsMood(string name) => name == "RebuffedMyRomanceAttemptMood" || name == "RejectedMyProposalMood" ||
            name == "FailedRomanceAttemptOnMeLowOpinionMood" || name == "BrokeUpWithMeMood" || name == "DivorcedMeMood";

        /// <summary>只转发原 Def 重载，不复制原版构造或准入逻辑；异常仍沿原调用栈传播。</summary>
        private static void Gain(MemoryThoughtHandler handler, ThoughtDef def, Pawn other, Precept precept, Site site)
        {
            var previous = pending; pending = site;
            try { handler.TryGainMemory(def, other, precept); if (site != null) site.Completed = true; }
            finally { pending = previous; }
        }

        /// <summary>Def 重载入口先消费调用点，避免工厂回调中的同名思想冒领外层身份。</summary>
        internal static DefinitionCall EnterDefinition(MemoryThoughtHandler handler, ThoughtDef def, Pawn other)
        {
            var site = pending; pending = null;
            var call = new DefinitionCall { Previous = definition, Site = site?.Handler == handler && site.Def == def && site.Other == other && !site.Consumed ? site : null };
            if (call.Site != null) call.Site.Consumed = true;
            definition = call; return call;
        }
        internal static void ExitDefinition(DefinitionCall call) { if (call != null && definition == call) definition = call.Previous; }

        /// <summary>原 Def 方法实际构造出的唯一实例进入 object 重载；工厂内的其他请求不继承身份。</summary>
        internal static void GainObject(MemoryThoughtHandler handler, Thought_Memory memory, Pawn other)
        {
            var call = definition; if (call != null) call.Expected = memory;
            try { handler.TryGainMemory(memory, other); }
            finally { if (call != null) call.Expected = null; }
        }

        /// <summary>所有 object 调用都压帧；直接调用和重入明确屏蔽外层记录。</summary>
        internal static MemoryCall EnterMemory(MemoryThoughtHandler handler, Thought_Memory memory, Pawn other)
        {
            var site = definition != null && definition.Expected == memory && !definition.ObjectClaimed ? definition.Site : null;
            if (definition?.Expected == memory && definition != null) definition.ObjectClaimed = true;
            if (site?.Handler != handler || site?.Def != memory?.def || site?.Other != other ||
                site?.Frame != SSCRomanceMoodEvents.CurrentWorker) site = null;
            var call = new MemoryCall { Previous = current, Site = site, Memory = memory };
            // 先压帧再读取记录；快照失败只标记技术失效，不从观察 Prefix 阻止原版主体执行。
            current = call;
            if (site != null && IsMood(memory.def.defName))
            {
                try { call.Entry = new Entry(site, memory); site.Frame.NativeEntries.Add(call.Entry); }
                catch (Exception) { site.Invalid = true; call.Entry = null; }
            }
            // 同一处理器／Def 的未取证写入不能被早先原版调用的撤销吞掉。
            var frame = SSCRomanceMoodEvents.CurrentWorker;
            if (frame != null && site == null && memory != null)
                foreach (var entry in frame.NativeEntries.Where(e => e.Site.Handler == handler && e.Candidate.def == memory.def)) entry.Invalid = true;
            return call;
        }
        internal static void ExitMemory(MemoryCall call, bool ran, Exception exception)
        {
            try
            {
                call?.Entry?.Seal(ran, exception);
                if (call?.Site != null && (!ran || exception != null || call.Site.Admitted && !BindingMatches(call))) call.Site.Invalid = true;
            }
            catch (Exception) { if (call?.Site != null) call.Site.Invalid = true; }
            finally { if (call != null && current == call) current = call.Previous; }
        }

        /// <summary>已越过原版准入才登记；虚方法及原生布尔结果逐值透传。</summary>
        internal static bool Merge(Thought_Memory memory, out bool bubble)
        {
            var call = current;
            if (call?.Site != null && definition?.Expected == memory)
            { call.Site.Admitted = true; call.Site.Invalid |= !BindingMatches(call); }
            bool merged = memory.TryMergeWithExistingMemory(out bubble);
            if (call?.Site != null) call.Site.Invalid |= !BindingMatches(call);
            return merged;
        }
        /// <summary>父／子候选都必须保持初始 Def、本人、对象及关联结构；不能把被改写的记录认成本次原版事实。</summary>
        private static bool BindingMatches(MemoryCall call) => call.Memory != null && call.Memory.def == call.Site.Def &&
            call.Memory.pawn == call.Site.Person.Pawn && call.Memory.otherPawn == call.Site.Other && call.Site.Def.thoughtToMake == call.Site.ChildDef;
        /// <summary>仅记录原生 Add 的目标实例，普通思想仍使用原来的列表操作。</summary>
        internal static void Add(List<Thought_Memory> list, Thought_Memory memory)
        {
            list.Add(memory);
            var entry = current?.Entry;
            if (entry?.Candidate == memory && entry.Site.Handler.Memories == list)
                try { entry.Added(memory); } catch (Exception) { entry.Invalid = true; }
        }
        /// <summary>区分原生容量移除与外部主动清理；缺少完整见证时不接管。</summary>
        internal static void Remove(MemoryThoughtHandler handler, Thought_Memory memory)
        {
            var entry = current?.Entry; if (entry?.Site.Handler != handler) entry = null;
            object saved = null;
            try { saved = entry?.BeforeRemove(memory); } catch (Exception) { entry.Invalid = true; }
            handler.RemoveMemory(memory); // 原方法自身的异常照常传播；只隔离观察器异常。
            if (saved != null) try { entry.Removed(saved); } catch (Exception) { entry.Invalid = true; }
        }
        /// <summary>只包装原生基类合并中真实调用的 Renew；不将“年龄为零”当作本次续期证据。</summary>
        internal static void Renew(Thought_Memory memory)
        {
            var entry = current?.Entry; object saved = null;
            try { saved = entry?.BeforeRenew(memory); } catch (Exception) { entry.Invalid = true; }
            memory.Renew();
            if (saved != null) try { entry.Renewed(saved); } catch (Exception) { entry.Invalid = true; }
        }

        /// <summary>替换只针对本人所选原因；先校验整套逆序计划，最后直接提交，无外部回调窗口。</summary>
        internal static bool TryUndo(SSCRomanceMoodFrame frame, SSCRomanceMoodFrame.Person person, bool apply)
        {
            if (!SSCRomanceNativeMoodHooks.Ready || !Thought_MemorySSCCompositeRomance.DefinitionsReady || !SSCRomanceMoodEvents.CurrentPerson(person)) return false;
            var sites = frame.NativeSites.Where(s => s.Person == person).ToArray();
            if (sites.Any(s => !s.Completed || s.Invalid || !s.Consumed || !s.Admitted || s.Def.thoughtToMake != s.ChildDef)) return false;
            // 已知社交父定义缺少对应原生子节点属于结构失配，不能假定原版没有该心情。
            if (sites.Any(s => ExpectedChild(s.Def.defName) is string name && s.Def.thoughtToMake?.defName != name)) return false;
            var entries = frame.NativeEntries.Where(e => e.Site.Person == person).ToArray();
            foreach (var group in entries.GroupBy(e => e.Site.Def))
            {
                var projected = person.Handler.Memories.Where(m => m.def == group.Key).ToDictionary(m => m, m => new State(m));
                foreach (var entry in group.Reverse()) if (!entry.Plan(projected)) return false;
            }
            if (!SSCRomanceMoodEvents.CurrentPerson(person)) return false;
            if (apply) foreach (var entry in entries.Reverse()) entry.Undo();
            return true;
        }
    }
}
