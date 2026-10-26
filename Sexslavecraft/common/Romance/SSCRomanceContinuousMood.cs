// AI 辅助生成：十四个持续分支及局部刷新；不保存事件、不读取恶堕需求。
using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>持续思想阶段的明确映射；同值不同分支也有独立身份和文案。</summary>
    public static class SSCRomanceContinuousMood
    {
        private static readonly string[] ids = { "C-A-S2-N", "C-A-S2-M", "C-A-S3-N", "C-A-S3-M",
            "C-A-S4-N", "C-A-S4-M", "C-B-S3-N", "C-B-S3-M", "C-B-S4-N", "C-B-S4-M",
            "C-AB-S3-N", "C-AB-S3-M", "C-AB-S4-N", "C-AB-S4-M" };

        /// <summary>显示只读思想已缓存的阶段，不在名称和描述 getter 中重新查询角色。</summary>
        public static SSCRomanceMoodBranch BranchAt(int index) => index >= 0 && index < ids.Length &&
            SSCRomanceMoodCatalog.TryGet(ids[index], out var branch) ? branch : null;
        /// <summary>稳定 ID 映射到 XML 索引，未知分支不能降级成零值或另一分支。</summary>
        public static int IndexOf(SSCRomanceMoodBranch branch) => branch == null ? -1 : Array.IndexOf(ids, branch.Id);

        /// <summary>纯当前状态查询；A/B 互斥归并，没有具体对象，不查询好感。</summary>
        public static bool TryResolve(Pawn pawn, out SSCRomanceMoodBranch branch)
            => TryResolveCurrent(pawn, out branch, out _);

        /// <summary>区分合法不生效与回调导致的过期查询；刷新协调器不能把后者记录为恢复自由。</summary>
        internal static bool TryResolveCurrent(Pawn pawn, out SSCRomanceMoodBranch branch, out bool stable)
        {
            branch = null;
            stable = true;
            var token = SSCPersonalityRuntimeUtility.Capture(pawn);
            var traits = pawn?.story?.traits;
            long revision = SSCRomanceMoodRefresh.RevisionOf(pawn);
            var state = SSCRomanceResolver.Resolve(pawn);
            if (!state.Applicable || pawn.Dead || state.Stage < 2) return false;
            int configuration = ConfigurationSignature(state);
            bool romance = state.GetSaved(SSCRomanceSetting.InitiateRomance), proposal = state.GetSaved(SSCRomanceSetting.InitiateProposal);
            // 目标过滤仅在至少一种主动浪漫可用时参与；分手许可不以当前有无伴侣为前提。
            bool active = !romance || !proposal || !state.GetSaved(SSCRomanceSetting.InitiateBreakup) ||
                ((romance || proposal) && (!state.GetSaved(SSCRomanceSetting.InitiateToMaster) ||
                !state.GetSaved(SSCRomanceSetting.InitiateToSexSlave) || !state.GetSaved(SSCRomanceSetting.InitiateToOthers)));
            bool passive = state.Stage >= 3 && (!state.GetSaved(SSCRomanceSetting.AcceptRomance) ||
                !state.GetSaved(SSCRomanceSetting.AcceptProposal) || state.GetSaved(SSCRomanceSetting.AlwaysAcceptMaster) ||
                state.GetSaved(SSCRomanceSetting.AlwaysAcceptSexSlave) || state.GetSaved(SSCRomanceSetting.AlwaysAcceptOthers));
            if (!active && !passive) return false;
            bool found = SSCRomanceMoodCatalog.TryResolveControl(active ? passive ? SSCRomanceControlState.Both : SSCRomanceControlState.Active :
                SSCRomanceControlState.Passive, state.Stage, SSCRomanceMoodDefOf.Masochist != null &&
                pawn.story?.traits?.HasTrait(SSCRomanceMoodDefOf.Masochist) == true, out branch);
            // 有效特质查询可能经过外部回调；分支只属于查询前的原人格、组件和实际锁链。
            var after = SSCRomanceResolver.Resolve(pawn);
            if (!SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || !after.Applicable || pawn.Dead || after.Comp != state.Comp ||
                pawn.story?.traits != traits || SSCRomanceMoodRefresh.RevisionOf(pawn) != revision || ConfigurationSignature(after) != configuration ||
                after.Chain != state.Chain || after.Master != state.Master || after.Stage != state.Stage)
            { branch = null; stable = false; return false; }
            return found;
        }

        /// <summary>比较当前十一个原始选择，覆盖同一配置实例内直接写值；不创建第二份保存状态。</summary>
        private static int ConfigurationSignature(SSCRomanceState state)
        {
            int mask = 0;
            for (int index = (int)SSCRomanceSetting.InitiateRomance; index <= (int)SSCRomanceSetting.AlwaysAcceptOthers; index++)
                if (state.GetSaved((SSCRomanceSetting)index)) mask |= 1 << index;
            return mask;
        }

        /// <summary>阶段与目录不一致时停用本持续思想，不借错误的 XML 阶段显示另一数值。</summary>
        internal static bool ValidCarrier(ThoughtDef def)
        {
            if (def?.thoughtClass != typeof(Thought_SituationalSSCRomanceControl) || def.stages?.Count != ids.Length ||
                def.producesMemoryThought != null) return false;
            for (int i = 0; i < ids.Length; i++) if (def.stages[i] == null || def.stages[i].baseMoodEffect != BranchAt(i).Value) return false;
            return true;
        }
    }

    /// <summary>沿原版准入返回唯一持续阶段；合法零值仍是 Active。</summary>
    public sealed class ThoughtWorker_SSCRomanceControl : ThoughtWorker
    {
        /// <summary>只查询，不刷新缓存或输出日志，避免思想计算重入及每次查询刷屏。</summary>
        protected override ThoughtState CurrentStateInternal(Pawn pawn) => SSCRomanceContinuousMood.ValidCarrier(def) &&
            TryStage(pawn, out int stage) ? ThoughtState.ActiveAtStage(stage) : ThoughtState.Inactive;
        /// <summary>独立数值索引供验证；不再使用心情值加偏移生成阶段。</summary>
        public static bool TryStage(Pawn pawn, out int stage)
        { stage = SSCRomanceContinuousMood.TryResolve(pawn, out var branch) ? SSCRomanceContinuousMood.IndexOf(branch) : -1; return stage >= 0; }
    }

    /// <summary>独立文案入口读取缓存阶段，心情值和原版倍率继续由标准情境思想计算。</summary>
    public sealed class Thought_SituationalSSCRomanceControl : Thought_Situational
    {
        /// <summary>名字与数值使用同一已缓存分支，不动态捕获新人格或缓存翻译文本。</summary>
        public override string LabelCap
        {
            get
            {
                string text = SSCRomanceMoodText.Name(SSCRomanceContinuousMood.BranchAt(CurStageIndex)).CapitalizeFirst();
                return pawn != null && def?.Worker != null ? def.Worker.PostProcessLabel(pawn, text) : text;
            }
        }
        /// <summary>仅替换正文来源，保留人物格式化、文化来源和思想屏蔽说明。</summary>
        public override string Description
        {
            get
            {
                string text = SSCRomanceMoodText.Description(SSCRomanceContinuousMood.BranchAt(CurStageIndex));
                if (pawn == null) return text;
                text = def?.Worker != null ? def.Worker.PostProcessDescription(pawn, text) : text.Formatted(pawn.Named("PAWN")).Resolve();
                if (ModsConfig.IdeologyActive && sourcePrecept != null && !Find.IdeoManager.classicMode) text += CausedByBeliefInPrecept;
                string nullified = ThoughtUtility.ThoughtNullifiedMessage(pawn, def);
                return string.IsNullOrEmpty(nullified) ? text : text + "\n\n(" + nullified + ")";
            }
        }
    }

    /// <summary>弱引用缓存只记录已观察分支与载体；不成为保存源，不操作历史记忆。</summary>
    public static class SSCRomanceMoodRefresh
    {
        private sealed class State
        {
            internal bool Initialized, Busy;
            internal long Revision;
            internal string Branch;
            internal object Token;
            internal CompSexSlaveTraining Comp;
            internal Need_Mood Mood;
            internal SituationalThoughtHandler Handler;
        }
        private static readonly ConditionalWeakTable<Pawn, State> states = new ConditionalWeakTable<Pawn, State>();

        /// <summary>只读通知世代供当前查询复查；不创建缓存、重新捕获人格或授予命令权限。</summary>
        internal static long RevisionOf(Pawn pawn) => pawn != null && states.TryGetValue(pawn, out var state) ? state.Revision : 0;

        /// <summary>人格替换开始立即清掉旧思想缓存，中间态不记录为受限事件或分支变更。</summary>
        public static void Suspend(Pawn pawn)
        { try { pawn?.needs?.mood?.thoughts?.situational?.Notify_SituationalThoughtsDirty(); } catch (Exception) { } }

        /// <summary>在完整生命周期通知后比较分支身份；同值换分支和同分支换处理器也能正确失效。</summary>
        public static void Notify(Pawn pawn, string reason = "Lifecycle")
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive || SSCPersonalityRuntimeUtility.IsMutating(pawn)) return;
            var state = states.GetOrCreateValue(pawn);
            // 忙时也保留新通知世代，外层不得把查询或清缓存之前的旧分支记成最终结果。
            state.Revision++;
            if (state.Busy) return;
            state.Busy = true;
            try
            {
                // 最多协调三轮，处理新通知或过期查询；不无限重试外部持续重入，更不重放互动。
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    long observed = state.Revision;
                    var token = SSCPersonalityRuntimeUtility.Capture(pawn);
                    var comp = pawn.TryGetComp<CompSexSlaveTraining>();
                    var mood = pawn.needs?.mood; var handler = mood?.thoughts?.situational;
                    string id = SSCRomanceContinuousMood.TryResolveCurrent(pawn, out var branch, out bool stable) ? branch.Id : null;
                    if (!stable || state.Revision != observed) continue;
                    // 状态查询允许被外部包装；不能拿旧查询结果清理回调换入的新处理器。
                    if (Scribe.mode != LoadSaveMode.Inactive || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || SSCPersonalityRuntimeUtility.IsMutating(pawn) ||
                        pawn.TryGetComp<CompSexSlaveTraining>() != comp || pawn.needs?.mood != mood || mood?.thoughts?.situational != handler) return;
                    bool changed = state.Branch != id;
                    bool dirty = !state.Initialized || changed || state.Token != token || state.Comp != comp || state.Mood != mood || state.Handler != handler;
                    string old = state.Branch; bool baseline = !state.Initialized;
                    if (dirty) handler?.Notify_SituationalThoughtsDirty();
                    if (state.Revision != observed) continue;
                    // 通知故障或原载体变更不记成功签名，下次仍可重试失效。
                    if (Scribe.mode != LoadSaveMode.Inactive || !SSCPersonalityRuntimeUtility.IsCurrent(pawn, token) || SSCPersonalityRuntimeUtility.IsMutating(pawn) ||
                        pawn.TryGetComp<CompSexSlaveTraining>() != comp || pawn.needs?.mood != mood || mood?.thoughts?.situational != handler) return;
                    state.Initialized = true; state.Branch = id; state.Token = token; state.Comp = comp; state.Mood = mood; state.Handler = handler;
                    if (changed) SSCRomanceMoodDiagnostics.Control(pawn, old, branch, baseline, reason);
                    if (state.Revision != observed) continue;
                    return;
                }
            }
            catch (Exception) { /* 观察器失败不阻断特质、绑定或人格业务；下次通知可重新查询。 */ }
            finally { state.Busy = false; }
        }
    }
}
