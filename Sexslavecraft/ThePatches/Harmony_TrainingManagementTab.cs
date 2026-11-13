// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在原生教程检查之后，仅把SSC调教标签的展开请求转为独立窗口。</summary>
    [HarmonyPatch]
    internal static class Harmony_TrainingManagementTab
    {
        /// <summary>按 1.7 原生签名定位标签切换入口，避免误挂到同名重载。</summary>
        private static MethodBase TargetMethod()
            => AccessTools.DeclaredMethod(typeof(InspectPaneUtility), "ToggleTab",
                new[] { typeof(InspectTabBase), typeof(IInspectPane) });

        /// <summary>仅在原生签名匹配时安装窗口入口。</summary>
        private static bool Prepare()
        {
            if (TargetMethod() is MethodInfo method && method.ReturnType == typeof(void)) return true;
            Log.Warning("[SSC TrainingManagement] ToggleTab signature unavailable; training window routing disabled.");
            return false;
        }

        /// <summary>只接管原生地图检查面板上的 SSC 调教标签。</summary>
        private static bool Prefix(InspectTabBase tab, IInspectPane pane)
        {
            if (tab?.GetType() != typeof(ITab_SexSlaveTraining) ||
                !ReferenceEquals(pane, MainButtonDefOf.Inspect.TabWindow)) return true;

            // 原生点击已经过教程许可检查。清掉可能残留的 SSC 展开状态后直接开窗。
            if (pane.OpenTabType == typeof(ITab_SexSlaveTraining)) pane.CloseOpenTab();
            Dialog_SSCTrainingManagement.OpenSelected();
            return false;
        }
    }

    /// <summary>观察原生选中列表的变更，即使同一帧切走再切回也撤销原角色的编辑弹窗。</summary>
    [HarmonyPatch]
    internal static class Harmony_TrainingOverviewSelection
    {
        // 原版批选和代理选择也经过内部增删入口，清空则直接修改列表。
        private static readonly MethodBase[] selectionWrites =
        {
            AccessTools.DeclaredMethod(typeof(Selector), "SelectInternal", new[] { typeof(object), typeof(bool), typeof(bool) }),
            AccessTools.DeclaredMethod(typeof(Selector), "DeselectInternal", new[] { typeof(object) }),
            AccessTools.DeclaredMethod(typeof(Selector), nameof(Selector.ClearSelection), Type.EmptyTypes)
        };

        /// <summary>签名变动时保留窗口逐帧刷新和提交时校验。</summary>
        private static bool Prepare()
        {
            if (Array.TrueForAll(selectionWrites, method => method is MethodInfo info && info.ReturnType == typeof(void))) return true;
            Log.Warning("[SSC TrainingManagement] Selector write signatures unavailable; using window selection refresh.");
            return false;
        }

        /// <summary>返回已通过签名校验的原生选择写入入口。</summary>
        private static IEnumerable<MethodBase> TargetMethods() => selectionWrites;

        /// <summary>原版完成写入后再比较窗口的编辑对象。</summary>
        private static void Postfix(Selector __instance)
            => Dialog_SSCTrainingManagement.NotifyEditSelection(__instance);
    }
}
