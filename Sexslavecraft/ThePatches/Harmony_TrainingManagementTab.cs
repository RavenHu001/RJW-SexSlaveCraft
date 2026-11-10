// AI 辅助生成
using System;
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

        /// <summary>原生签名不匹配时跳过补丁，由原调教标签继续提供界面。</summary>
        private static bool Prepare()
        {
            if (TargetMethod() is MethodInfo method && method.ReturnType == typeof(void)) return true;
            Log.Warning("[SSC TrainingManagement] ToggleTab signature unavailable; original tab retained.");
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
}
