// AI 辅助生成
#if SSC_RW17
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using rjw;
using rjw.Modules.Attraction;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>从当前身份与真实锁链派生性奴对主人的对象意愿；不保存选择、不修复关系。</summary>
    public static class SSCMasterAttractionUtility
    {
        internal const float MinimumCombinedAttraction = 1f;

        /// <summary>一次评价持有原双方、组件、锁链及人格凭据；偏好回调不能为旧评价重新授权。</summary>
        internal sealed class Context
        {
            internal Pawn Slave, Master;
            internal CompSexSlaveTraining SlaveComp, MasterComp;
            internal Hediff_ChainOfSexSlave Chain;
            internal object SlaveToken, MasterToken;
            internal bool Used;
        }

        private sealed class CachedContexts
        {
            internal Context Observer, Target;
        }
        // AppraisalResult本来已持有双方；弱键仅将原评分凭据限制在这个评价对象的生命周期内。
        private static readonly ConditionalWeakTable<AppraisalResult, CachedContexts> cachedContexts = new ConditionalWeakTable<AppraisalResult, CachedContexts>();

        /// <summary>只读取成年人当前状态；离图本身不改变对象意愿，可执行性仍由原入口判断。</summary>
        private static bool IsAdultParticipant(Pawn pawn)
            => pawn != null && !pawn.Destroyed && !pawn.Discarded && !pawn.Dead &&
               pawn.RaceProps?.Humanlike == true && pawn.ageTracker != null &&
               pawn.ageTracker.AgeBiologicalYears >= 18 && pawn.DevelopmentalStage == DevelopmentalStage.Adult &&
               pawn.GetAgeCategory() == AgeCategory.Adult && !SSCMasterProgressionUtility.IsHollow(pawn) &&
               !SSCPersonalityRuntimeUtility.IsMutating(pawn);

        /// <summary>读取开始时的原载体，并在可能有外部补丁的年龄查询之后确认仍属同一人格与关系。</summary>
        internal static Context CaptureDirectedPair(Pawn observer, Pawn target)
        {
            if (observer == null || target == null || observer == target || Scribe.mode != LoadSaveMode.Inactive) return null;
            CompSexSlaveTraining slaveComp = observer.TryGetComp<CompSexSlaveTraining>();
            CompSexSlaveTraining masterComp = target.TryGetComp<CompSexSlaveTraining>();
            Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(observer);
            // 普通RJW配对在取得人格凭据之前退出；不为无关角色创建进程内状态。
            if (slaveComp?.pawnIdentity != PawnIdentity.Slave || masterComp?.pawnIdentity != PawnIdentity.Master ||
                chain?.LinkedPawn != target) return null;
            var context = new Context
            {
                Slave = observer, Master = target,
                SlaveComp = slaveComp, MasterComp = masterComp, Chain = chain,
                SlaveToken = SSCPersonalityRuntimeUtility.Capture(observer),
                MasterToken = SSCPersonalityRuntimeUtility.Capture(target)
            };
            return IsCurrent(context) ? context : null;
        }

        /// <summary>公开查询始终按性奴→其真实主人判断，指定调教师或缰绳旧条目不能提供资格。</summary>
        public static bool IsEligibleDirectedPair(Pawn observer, Pawn target)
            => CaptureDirectedPair(observer, target) != null;

        /// <summary>核对原身份、原组件、原锁链和两侧人格，不在检查中补建或恢复任何状态。</summary>
        internal static bool IsCurrent(Context context)
        {
            if (context == null || Scribe.mode != LoadSaveMode.Inactive ||
                !IsAdultParticipant(context.Slave) || !IsAdultParticipant(context.Master)) return false;
            Pawn slave = context.Slave, master = context.Master;
            return context.SlaveComp != null && context.MasterComp != null && context.Chain != null &&
                ReferenceEquals(slave.TryGetComp<CompSexSlaveTraining>(), context.SlaveComp) &&
                ReferenceEquals(master.TryGetComp<CompSexSlaveTraining>(), context.MasterComp) &&
                context.SlaveComp.parent == slave && context.MasterComp.parent == master &&
                SSCIdentityUtility.IsSexSlave(slave) && SSCIdentityUtility.IsMaster(master) &&
                ReferenceEquals(SSCBondUtility.GetChain(slave), context.Chain) && context.Chain.pawn == slave &&
                slave.health?.hediffSet?.hediffs.Contains(context.Chain) == true && context.Chain.LinkedPawn == master &&
                SSCPersonalityRuntimeUtility.IsCurrent(slave, context.SlaveToken) &&
                SSCPersonalityRuntimeUtility.IsCurrent(master, context.MasterToken);
        }

        /// <summary>只支持自愿性行为的完整四模式评价，不改变General、ForRape或局部调试评价。</summary>
        internal static bool IsFullSexRequest(ref AttractionRequest request, AttractionMode[] modes)
        {
            if (request.Purpose != AttractionPurpose.ForFucking || modes == null || modes.Length != 4) return false;
            int mask = 0;
            foreach (AttractionMode mode in modes)
            {
                int bit;
                switch (mode)
                {
                    case AttractionMode.Age: bit = 1; break;
                    case AttractionMode.Physical: bit = 2; break;
                    case AttractionMode.Romantic: bit = 4; break;
                    case AttractionMode.Social: bit = 8; break;
                    default: return false;
                }
                if ((mask & bit) != 0) return false;
                mask |= bit;
            }
            return mask == 15;
        }

        /// <summary>原Preflight已经成功后才捕获评价；没有资格时返回空，让全部原分支照常执行。</summary>
        internal static Context CaptureRequest(ref AttractionRequest request, AttractionMode[] modes)
            => SSCMasterAttractionPatches.Ready && IsFullSexRequest(ref request, modes)
                ? CaptureDirectedPair(request.Pawn, request.Target) : null;

        /// <summary>零偏好保留原合成值继续循环；仅原上下文有效时绕过原来的偏好零提前退出。</summary>
        internal static bool ShouldContinueAfterZero(Context context, ref AttractionRequest request, AttractionMode[] modes)
            => SSCMasterAttractionPatches.Ready && IsFullSexRequest(ref request, modes) && IsCurrent(context) &&
               context.Slave == request.Pawn && context.Master == request.Target;

        /// <summary>只给完整原合成分设置下限；高于1的原分保留，之后仍由RJW原Finalize处理需求与预约。</summary>
        internal static float ApplyAttractionFloor(float rawCombined, Context context, ref AttractionRequest request, AttractionMode[] modes)
        {
            if (!ShouldContinueAfterZero(context, ref request, modes) || float.IsNaN(rawCombined)) return rawCombined;
            context.Used = rawCombined < MinimumCombinedAttraction;
            return Math.Max(rawCombined, MinimumCombinedAttraction);
        }

        /// <summary>原Finalize也可能调用外部补丁；只撤销曾实际抬高分数的旧保障，不重放其需求/预约查询。</summary>
        internal static float ValidateFinalizedAttraction(float finalized, Context context)
            => context != null && context.Used && (!SSCMasterAttractionPatches.Ready || !IsCurrent(context)) ? 0f : finalized;

        /// <summary>局部配对门槛沿用RJW原设置，只豁免性奴面向真实主人这一侧的好感和浪漫系数。</summary>
        internal static void GetDirectedHookupSettings(Pawn pawn, out float minimumFuckability,
            out int minimumRelationship, out float minimumAttractiveness, AppraisalResult result, Context context)
        {
            Pawn other = result == null ? null : pawn == result.Observer ? result.Target : pawn == result.Target ? result.Observer : null;
            CasualSex_Helper.GetCasualHookupSettingsForPawn(pawn, out minimumFuckability, out minimumRelationship, out minimumAttractiveness);
            if (!SSCMasterAttractionPatches.Ready || !IsCurrent(context) || context.Slave != pawn || context.Master != other) return;
            context.Used = true;
            minimumRelationship = -100;
            minimumAttractiveness = 0f;
        }

        /// <summary>维持原本即可通过的婚恋意愿；新增豁免仅对给定性奴→主人配对生效。</summary>
        public static bool AllowsCasualPair(Pawn observer, Pawn target)
            => (SSCMasterAttractionPatches.Ready && IsEligibleDirectedPair(observer, target)) || AllowsOriginalCasualPair(observer);

        /// <summary>集中复用RJW原婚恋意愿及既有nympho、beer例外，不修改全局CanFoolAround结果。</summary>
        private static bool AllowsOriginalCasualPair(Pawn pawn)
            => AttractionUtility.CanFoolAround(pawn, true) ||
               (RJWHookupSettings.NymphosCanCheat && xxx.is_nympho(pawn) && xxx.is_frustrated(pawn)) || pawn.HasBeerGoggles();

        /// <summary>目标尚未确定时仅放行候选中的真实主人，不能借主人存在为其他候选取得婚恋豁免。</summary>
        internal static bool CanFoolAroundWithCandidates(Pawn pawn, bool assumeInRelationship, List<Pawn> candidates,
            bool pawnWasNympho, out Context usedContext)
        {
            usedContext = null;
            bool original = AttractionUtility.CanFoolAround(pawn, assumeInRelationship);
            if (!SSCMasterAttractionPatches.Ready || original || candidates == null) return original;
            // FindPartner原本保存过nympho结果；保留同一快照和原fallback，不重复调用CanFoolAround。
            if ((RJWHookupSettings.NymphosCanCheat && pawnWasNympho && xxx.is_frustrated(pawn)) || pawn.HasBeerGoggles()) return true;
            Context context = null;
            foreach (Pawn candidate in candidates)
            {
                context = CaptureDirectedPair(pawn, candidate);
                if (context != null) break;
            }
            if (!IsCurrent(context)) return false;
            // 这是FindPartner自己ToList所得的当次列表；不改Pawn、关系、设置或外部枚举。
            candidates.RemoveAll(candidate => candidate != context.Master);
            usedContext = context;
            return IsCurrent(context);
        }

        /// <summary>仅当本次搜索借用了新增婚恋豁免时复核原主人，后续随机/路径回调不能延续旧绑定。</summary>
        internal static Pawn ValidatePartnerResult(Pawn partner, Context usedContext)
            => usedContext != null && (!SSCMasterAttractionPatches.Ready || !IsCurrent(usedContext) || partner != usedContext.Master)
                ? null : partner;

        /// <summary>右键入口保存开始时的实际有向配对；主人作为发起者时也只保障性奴一侧。</summary>
        internal static Context CaptureEitherDirection(Pawn pawn, Pawn target)
            => SSCMasterAttractionPatches.Ready ? CaptureDirectedPair(pawn, target) ?? CaptureDirectedPair(target, pawn) : null;

        internal static Context CaptureAppraisal(AppraisalResult result)
        {
            if (!SSCMasterAttractionPatches.Ready || result == null) return null;
            Context observer = result.ObserverSettings.Purpose == AttractionPurpose.ForFucking
                ? CaptureDirectedPair(result.Observer, result.Target) : null;
            return observer ?? (result.TargetSettings.Purpose == AttractionPurpose.ForFucking
                ? CaptureDirectedPair(result.Target, result.Observer) : null);
        }

        /// <summary>整次自动意愿检查共用原凭据；后续另一侧回调撤权时，之前放宽的局部阈值不能续权。</summary>
        internal static bool ValidateSettingsResult(bool result, Context context)
            => result && (context == null || !context.Used || SSCMasterAttractionPatches.Ready && IsCurrent(context));

        /// <summary>只改变右键自愿检查读到的局部意见，不写关系，也不影响浪漫、社交或主人反向判断。</summary>
        internal static int AdjustDirectedOpinion(int original, Pawn observer, Pawn target, Context context)
        {
            if (!SSCMasterAttractionPatches.Ready || !IsCurrent(context) || context.Slave != observer || context.Master != target) return original;
            int adjusted = Math.Max(original, RJWHookupSettings.MinimumRelationshipToHookup);
            context.Used |= adjusted != original;
            return adjusted;
        }

        /// <summary>右键评分仍调用RJW原入口，同时记录当前请求曾依赖该关系的意愿保障。</summary>
        internal static float EvaluateForRmb(Pawn observer, Pawn target, AttractionPurpose purpose, Context context)
        {
            if (purpose == AttractionPurpose.ForFucking && SSCMasterAttractionPatches.Ready && IsCurrent(context) &&
                context.Slave == observer && context.Master == target) context.Used = true;
            return AttractionUtility.Evaluate(observer, target, purpose);
        }

        /// <summary>右键原结果保留拒绝理由；只有已用本次关系保障后撤权，才拒绝原先的成功结果。</summary>
        internal static AcceptanceReport ValidateRmbResult(AcceptanceReport result, Context context)
            => result.Accepted && context != null && context.Used &&
               (!SSCMasterAttractionPatches.Ready || !IsCurrent(context))
                ? (AcceptanceReport)"SSC_MasterAttraction_ExplainUnavailable".Translate().ToString() : result;

        /// <summary>载体或人格变化时清除该侧旧float缓存，让RJW原getter重新取得当前真实评分。</summary>
        internal static void PrepareCachedScore(AppraisalResult result, bool observerSide, ref float? cached)
        {
            Pawn observer = observerSide ? result.Observer : result.Target;
            Pawn target = observerSide ? result.Target : result.Observer;
            AttractionPurpose purpose = observerSide ? result.ObserverSettings.Purpose : result.TargetSettings.Purpose;
            Context current = SSCMasterAttractionPatches.Ready && purpose == AttractionPurpose.ForFucking
                ? CaptureDirectedPair(observer, target) : null;
            if (!cachedContexts.TryGetValue(result, out CachedContexts state))
            {
                if (current == null) return;
                state = cachedContexts.GetOrCreateValue(result);
            }
            Context previous = observerSide ? state.Observer : state.Target;
            if (!SameContext(previous, current)) cached = null;
            if (observerSide) state.Observer = current;
            else state.Target = current;
        }

        /// <summary>不将‘现在又绑定到同一人’视为原评价；原链、组件和两侧token都必须相同。</summary>
        private static bool SameContext(Context left, Context right)
            => ReferenceEquals(left, right) || left != null && right != null && left.Slave == right.Slave && left.Master == right.Master &&
               ReferenceEquals(left.Chain, right.Chain) && ReferenceEquals(left.SlaveComp, right.SlaveComp) &&
               ReferenceEquals(left.MasterComp, right.MasterComp) && ReferenceEquals(left.SlaveToken, right.SlaveToken) &&
               ReferenceEquals(left.MasterToken, right.MasterToken);
    }
}
#endif
