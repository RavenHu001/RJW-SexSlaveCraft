using System;
using System.Runtime.CompilerServices;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>记录本次游戏中达到满恶堕的原载体，协调首次绑定与失败重试。</summary>
    internal static class SSCFullCorruptionImprint
    {
        // 一次初始尝试，失败后由需求刷新接手，最多再尝试两次。
        private const int MaxAttempts = 3;

        private sealed class Pending
        {
            internal Need_Corruption Need;
            internal Hediff Initial;
            internal Pawn_HealthTracker Health;
            internal CompSexSlaveTraining Comp;
            internal object Token;
            internal int Attempts;

            internal bool SameCarrier(Pawn pawn)
                => !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && pawn.health == Health &&
                    pawn.TryGetComp<CompSexSlaveTraining>() == Comp && SSCPersonalityRuntimeUtility.IsCurrent(pawn, Token);

            internal bool StillFull(Pawn pawn)
                => SameCarrier(pawn) && pawn.needs?.TryGetNeed<Need_Corruption>() == Need && Need.CurLevel == Need.MaxLevel;

            internal bool IsEligible(Pawn pawn) => StillFull(pawn) &&
                IsEligibleIdentity(Comp.pawnIdentity) && SSCImprintUtility.Find(pawn) == Initial;
        }

        private sealed class State
        {
            internal Pending Request;
            internal Pending Applying;
            internal int RitualDepth;
        }

        // 不序列化待办，普通读档只还原既有身份和印记。
        private static readonly ConditionalWeakTable<Pawn, State> states = new();

        internal static void AfterPersonalityRestore(Pawn pawn)
        {
            var need = pawn.needs?.TryGetNeed<Need_Corruption>();
            if (need == null || need.CurLevel != need.MaxLevel) return;
            CaptureCandidate(pawn, need);
            ResolvePending(pawn, need);
        }

        internal static void CaptureCandidate(Pawn pawn, Need_Corruption need)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || pawn == null ||
                pawn.Dead || pawn.Destroyed || pawn.Discarded || SSCPersonalityRuntimeUtility.IsMutating(pawn) ||
                pawn.needs?.TryGetNeed<Need_Corruption>() != need) return;
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || !IsEligibleIdentity(comp.pawnIdentity)) return;
            var initial = SSCImprintUtility.Find(pawn);
            if (SSCImprintUtility.Kind(initial?.def) != SSCImprintKind.Initial) return;
            object token = SSCPersonalityRuntimeUtility.Capture(pawn);
            State state = states.GetOrCreateValue(pawn);
            // 健康回调可以完整植入新人格。旧分化回到调用方前，保留新 token 的满值请求。
            if (state.Applying?.Token == token) return;
            state.Request = new Pending
            {
                Need = need, Initial = initial, Health = pawn.health, Comp = comp, Token = token
            };
        }

        private static bool IsEligibleIdentity(PawnIdentity identity)
            => identity == PawnIdentity.Slave || identity == PawnIdentity.Unset;

        internal static void ResolvePending(Pawn pawn, Need_Corruption need)
        {
            if (states.TryGetValue(pawn, out State state) && state.RitualDepth == 0 && state.Request?.Need == need)
                Resolve(pawn, state);
        }

        // 奴性爆发等待这里退出（包括发信回调与重试）；成功或失败都释放原满值增长。
        internal static bool IsSettling(Pawn pawn, Need_Corruption need)
            => need != null && states.TryGetValue(pawn, out State state) &&
               (state.RitualDepth != 0 || state.Applying?.Need == need ||
                (state.Request is Pending request && request.Need == need && request.SameCarrier(pawn)));

        // 主人豁免立即生效。即使在两次刷新之间退出主人身份，也不恢复旧待办。
        internal static void CancelForMaster(Pawn pawn)
        {
            if (states.TryGetValue(pawn, out State state)) state.Request = null;
        }

        /// <summary>刷新时处理实际重试；返回值表示本轮仍可继续刷新原需求。</summary>
        internal static bool TryRefreshNeed(Pawn pawn, Need_Corruption need, out bool deferDecay)
        {
            deferDecay = false;
            if (!states.TryGetValue(pawn, out State state) || state.RitualDepth != 0 ||
                state.Request is not Pending request || request.Need != need) return true;
            if (state.Applying != null) return false;
            if (!Resolve(pawn, state, allowRetry: true)) return true;

            // 只有这次失败后仍留存的原请求能暂缓衰减。用完次数会在结算退出时清掉。
            deferDecay = state.Request == request;
            return request.SameCarrier(pawn) && pawn.needs?.TryGetNeed<Need_Corruption>() == need;
        }

        // 首次绑定优先消化本场待办，普通首次绑定继续走原有流程。
        internal static bool ResolveDuringBinding(Pawn pawn)
            => states.TryGetValue(pawn, out State state) && Resolve(pawn, state);

        internal static IDisposable BeginRitual(Pawn pawn)
        {
            State state = states.GetOrCreateValue(pawn);
            state.RitualDepth++;
            return new RitualScope(pawn, state);
        }

        private sealed class RitualScope : IDisposable
        {
            private readonly Pawn pawn;
            private readonly State state;
            internal RitualScope(Pawn pawn, State state) { this.pawn = pawn; this.state = state; }
            public void Dispose()
            {
                if (--state.RitualDepth == 0) Resolve(pawn, state);
            }
        }

        private static bool Resolve(Pawn pawn, State state, bool allowRetry = false)
        {
            Pending request = state.Request;
            if (request == null || Scribe.mode != LoadSaveMode.Inactive || SSCPersonalityRuntimeUtility.IsMutating(pawn)) return false;
            // 添加定型印记的回调可能再次进入此处。事务进行时仍由原请求认领，不能用暂态印记取消它。
            if (state.Applying == request) return true;
            if (!request.IsEligible(pawn))
            {
                state.Request = null;
                return false;
            }

            // 同一请求归满值流程。后续增长、绑定回调与仪式收尾可以认领，但不消耗刷新重试次数。
            if (state.Applying != null || (!allowRetry && request.Attempts > 0)) return true;
            state.Applying = request;
            request.Attempts++;
            bool wasUnset = request.Comp.pawnIdentity == PawnIdentity.Unset;
            bool committed = false;
            try
            {
                if ((!wasUnset || SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave)) &&
                    state.Request == request && request.IsEligible(pawn))
                    committed = SSCImprintUtility.TryGrantDifferentiated(pawn, PawnIdentity.Slave,
                        () => state.Request == request && request.StillFull(pawn));
                if (committed)
                {
                    // 先消费本次请求，再进入可重入的信件投递。如果回调已给新人格登记请求，保留那份待办。
                    // 和首次绑定同场时，受方由这里发满值信。主人自己的绑定定型由绑定入口独立通知。
                    if (state.Request == request) state.Request = null;
                    SSCImprintLetters.SendAcquired(pawn, SSCImprintUtility.Find(pawn), SSCImprintAcquisition.FullCorruption);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[SSC] Full-corruption imprint settlement failed for {pawn}: {ex}");
            }
            finally
            {
                // 只回退本次从未选身份写入的身份。外部回调建立的有效绑定或新人格由其所属流程处理。
                try
                {
                    if (!committed && wasUnset && state.Request == request && request.SameCarrier(pawn) &&
                        request.Comp.pawnIdentity == PawnIdentity.Slave &&
                        SSCImprintUtility.Find(pawn) == request.Initial && !SSCIdentityUtility.IsIdentityLocked(pawn))
                        SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SSC] Could not restore the original identity after full-corruption imprint settlement for {pawn}: {ex}");
                }
                finally
                {
                    state.Applying = null;
                    if (state.Request == request && (request.Attempts >= MaxAttempts || !request.IsEligible(pawn)))
                        state.Request = null;
                }
            }
            return true;
        }
    }
}
