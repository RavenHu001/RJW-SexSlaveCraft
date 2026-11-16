using System;
using System.Runtime.CompilerServices;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>记录本次游戏中达到满恶堕的原载体，协调首次绑定与失败重试。</summary>
    internal static class SSCFullCorruptionImprint
    {
        private sealed class Pending
        {
            internal Need_Corruption Need;
            internal Hediff Initial;
            internal Pawn_HealthTracker Health;
            internal CompSexSlaveTraining Comp;
            internal object Token;

            internal bool SameCarrier(Pawn pawn)
                => !pawn.Dead && !pawn.Destroyed && !pawn.Discarded && pawn.health == Health &&
                    pawn.TryGetComp<CompSexSlaveTraining>() == Comp && SSCPersonalityRuntimeUtility.IsCurrent(pawn, Token);

            internal bool StillFull(Pawn pawn)
                => SameCarrier(pawn) && pawn.needs?.TryGetNeed<Need_Corruption>() == Need && Need.CurLevel == Need.MaxLevel;
        }

        private sealed class State
        {
            internal Pending Request;
            internal Pending Applying;
            internal int RitualDepth;
            internal Pending RitualAttempt;
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

        internal static bool ResolvePending(Pawn pawn, Need_Corruption need)
            => pawn != null && states.TryGetValue(pawn, out State state) && state.RitualDepth == 0 &&
                state.Request?.Need == need && Resolve(pawn, state);

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
                if (--state.RitualDepth != 0) return;
                Pending attempted = state.RitualAttempt;
                state.RitualAttempt = null;
                if (state.Request != attempted) Resolve(pawn, state);
            }
        }

        private static bool Resolve(Pawn pawn, State state)
        {
            Pending request = state.Request;
            if (request == null || Scribe.mode != LoadSaveMode.Inactive || SSCPersonalityRuntimeUtility.IsMutating(pawn)) return false;
            if (!request.StillFull(pawn) || !IsEligibleIdentity(request.Comp.pawnIdentity) ||
                SSCImprintUtility.Find(pawn) != request.Initial)
            {
                state.Request = null;
                return false;
            }

            // 有效的待办仍由满值流程负责。此处认领它，阻止同场回调回落到普通绑定分化。
            if (state.Applying != null || state.RitualAttempt == request) return true;
            state.Applying = request;
            if (state.RitualDepth > 0) state.RitualAttempt = request;
            bool wasUnset = request.Comp.pawnIdentity == PawnIdentity.Unset;
            bool committed = false;
            try
            {
                if ((!wasUnset || SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave)) &&
                    request.StillFull(pawn) && SSCImprintUtility.Find(pawn) == request.Initial)
                    committed = SSCImprintUtility.TryGrantDifferentiated(pawn, PawnIdentity.Slave,
                        () => request.StillFull(pawn));
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
                    if (!committed && wasUnset && request.SameCarrier(pawn) && request.Comp.pawnIdentity == PawnIdentity.Slave &&
                        SSCImprintUtility.Find(pawn) == request.Initial && !SSCIdentityUtility.IsIdentityLocked(pawn))
                        SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SSC] Could not restore the original identity after full-corruption imprint settlement for {pawn}: {ex}");
                }
                finally { state.Applying = null; }
            }
            return true;
        }
    }
}
