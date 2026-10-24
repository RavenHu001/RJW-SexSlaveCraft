// AI 辅助生成
using System;
using System.Runtime.CompilerServices;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>人格归属凭据不存档、不依赖训练组件；同组件原地换人格也使旧请求失效。</summary>
    public static class SSCPersonalityRuntimeUtility
    {
        private sealed class State
        {
            public object Token = new object();
            public MutationScope Mutation;
        }
        // 凭据只服务当前进程的回调归属；弱引用不延长 Pawn 生命周期，也不额外写入存档。
        private static readonly ConditionalWeakTable<Pawn, State> states = new ConditionalWeakTable<Pawn, State>();
        /// <summary>取得当前进程的人格凭据；弱引用状态不存档、不依赖训练组件的存活。</summary>
        public static object Capture(Pawn pawn) => pawn == null ? null : states.GetOrCreateValue(pawn).Token;
        /// <summary>精确比较原 token，防止同一 Comp 原地换人格后继续使用旧点击。</summary>
        public static bool IsCurrent(Pawn pawn, object token)
            => pawn != null && token != null && ReferenceEquals(Capture(pawn), token);
        /// <summary>只读检查是否处于已取得的替换作用域，不统一暂停身体或关系的实际消费。</summary>
        public static bool IsMutating(Pawn pawn)
            => pawn != null && states.TryGetValue(pawn, out State state) && state.Mutation != null;

        /// <summary>拒绝加载期与重入，开始时立即替换人格 token；不承诺对原人格流程做事务回滚。</summary>
        public static bool TryBeginMutation(Pawn pawn, out MutationScope scope)
        {
            // 拒绝加载期和同 Pawn 的嵌套替换；开始时立即换 token，使旧 UI/事件请求失效。
            scope = null;
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return false;
            State state = states.GetOrCreateValue(pawn);
            if (state.Mutation != null) return false;
            state.Token = new object();
            scope = new MutationScope(pawn, state.Token);
            state.Mutation = scope;
            return true;
        }

        public sealed class MutationScope : IDisposable
        {
            private readonly Pawn pawn;
            private readonly object token;
            private bool disposed;
            /// <summary>绑定本次角色和新 token；只有创建它的作用域能够释放该次替换。</summary>
            internal MutationScope(Pawn pawn, object token) { this.pawn = pawn; this.token = token; }
            public bool IsCurrent => !disposed && SSCPersonalityRuntimeUtility.IsCurrent(pawn, token);
            /// <summary>成功、提前退出或异常均释放自身作用域，按最终状态同步派生标记；重复释放无副作用。</summary>
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                // 只释放自己拥有的作用域，能力在全部人格写入结束后才重新协调标记。
                State state = states.GetOrCreateValue(pawn);
                if (!ReferenceEquals(state.Mutation, this)) return;
                state.Mutation = null;
                SSCAbilityToggleUtility.SyncMarkers(pawn);
            }
        }
    }
}
