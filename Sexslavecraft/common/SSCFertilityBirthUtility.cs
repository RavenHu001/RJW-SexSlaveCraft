// AI 辅助生成：分娩调用范围内汇总活产，不保存孕次、不接管出生或临产。
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>原版递归、月经循环及RJW出生共享的临时汇总；所有退出均释放，不参与Scribe。</summary>
    internal static class SSCFertilityBirthUtility
    {
        [ThreadStatic] private static Scope current;

        internal sealed class Scope
        {
            internal readonly Scope Parent, Root;
            internal readonly Pawn Mother, Father;
            internal readonly bool HasParents;
            private readonly CompSexSlaveTraining comp;
            private readonly object token;
            private bool liveBirth, failed, ended;
            private readonly HashSet<string> warnings = new HashSet<string>();

            /// <summary>固定原人格；同一怀胎者的嵌套出生共用根汇总，其他怀胎者独立处理。</summary>
            internal Scope(Pawn mother, Pawn father, bool hasParents)
            {
                Parent = current; Mother = mother; Father = father; HasParents = hasParents;
                Root = Parent != null && Parent.Mother == mother ? Parent.Root : this;
                comp = mother?.TryGetComp<CompSexSlaveTraining>();
                token = SSCPersonalityRuntimeUtility.Capture(mother);
                current = this;
            }

            /// <summary>只有实际出生结果能标记活产；已有预生成列表不调用，不改变婴儿。</summary>
            internal void Observe(Pawn baby)
            {
                if (!ended && Mother?.RaceProps?.Humanlike == true && baby != null &&
                    baby.RaceProps?.Humanlike == true && !baby.Dead && !baby.Destroyed && !baby.Discarded)
                    Root.liveBirth = true;
            }

            /// <summary>异常不补造完成；最外层结束时才提交一次培养，之后恢复原临时上下文。</summary>
            internal void Finish(Exception exception)
            {
                if (ended) return;
                ended = true;
                if (exception != null) Root.failed = true;
                current = Parent;
                if (!ReferenceEquals(Root, this) || failed || !liveBirth || Mother == null || Mother.Dead ||
                    Mother.Destroyed || Mother.Discarded || comp == null ||
                    Mother.TryGetComp<CompSexSlaveTraining>() != comp ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(Mother, token)) return;
                FertilitySpecializationUtility.TryGainProgress(Mother, .08f);
            }

            /// <summary>同次出生只提示一次相同原因；随调用退出释放，不按Pawn长期缓存。</summary>
            internal bool AddWarning(string key) => Root.warnings.Add(key);
        }

        /// <summary>生成预设不保存在这里；只保留出生期间的真实亲代参数和人格领取凭据。</summary>
        internal static Scope Begin(Pawn mother, Pawn father = null, bool hasParents = false)
            => new Scope(mother, father, hasParents);

        /// <summary>月经回退生成没有father参数，从正在执行的原版出生调用取得，不查绑定／伴侣。</summary>
        internal static Pawn CurrentFather(Pawn mother)
        {
            for (Scope scope = current; scope != null; scope = scope.Parent)
                if (scope.Mother == mother && scope.HasParents) return scope.Father;
            return null;
        }

        /// <summary>RJW放置成功后旁路记录；不把仅有存活预生成个体认作已经分娩。</summary>
        internal static void Observe(Pawn mother, Pawn baby)
        {
            if (current != null && current.Mother == mother) current.Observe(baby);
        }

        /// <summary>分娩外的独立生成仍可提示；分娩内按原因去重，不形成长期去重记录。</summary>
        internal static bool ShouldWarn(Pawn mother, string key)
            => current == null || current.Mother != mother || current.AddWarning(key);
    }
}
