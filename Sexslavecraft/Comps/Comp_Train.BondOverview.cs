// AI 辅助生成。普通单选入口，不借用研究、训练许可或开发按钮的准入。
using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        internal IEnumerable<Gizmo> BondOverviewGizmos(Pawn pawn)
        {
            if (!SSCBondOverviewAccess.IsCardSource(pawn)) yield break;
            foreach (Gizmo gizmo in Current.Game.GetComponent<SSCBondRelationGameComponent>().GetOverviewGizmos(pawn)) yield return gizmo;
        }

        internal Gizmo BondOverviewGizmo(Pawn pawn)
            => SSCBondOverviewAccess.IsCardSource(pawn)
                ? Current.Game.GetComponent<SSCBondRelationGameComponent>()?.GetOverviewGizmo(pawn) : null;

        internal Gizmo BondOverviewDiagnosticGizmo(Pawn pawn)
        {
            if (!CanDiagnose(pawn)) return null;
            Game game = Current.Game;
            return new Command_Action
            {
                defaultLabel = SSCBondOverviewText.T("DebugQuery"),
                defaultDesc = SSCBondOverviewText.T("DebugQueryDesc"),
                action = () =>
                {
                    // 调试命令可能在关闭开关、切选或读档后迟到；执行时重新检查全部准入。
                    // 只有此显式动作调用完整查询，构造按钮、悬停和普通 Update 都不扫描。
                    if (!ReferenceEquals(game, Current.Game) || !CanDiagnose(pawn)) return;
                    try
                    {
                        var local = SSCBondOverviewUtility.BuildLocal(pawn, Find.CurrentMap);
                        var global = SSCBondOverviewUtility.Build(pawn, Find.CurrentMap);
                        if (local.State != SSCBondOverviewQueryState.Ready || global.State != SSCBondOverviewQueryState.Ready)
                        {
                            Messages.Message(SSCBondOverviewText.T("DebugQueryFailed"), pawn, MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        var indexed = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
                        foreach (var entry in local.Slaves) indexed.Add(entry.Pawn);
                        int missing = 0;
                        foreach (var entry in global.Slaves) if (!indexed.Contains(entry.Pawn)) missing++;
                        // 仅报告反向索引与全局查询的差异；不修复缰绳，也不替换概览快照或保存名单。
                        Messages.Message(SSCBondOverviewText.Format("DebugQueryResult", local.TotalSlaveCount,
                            global.TotalSlaveCount, missing), pawn, MessageTypeDefOf.NeutralEvent, false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("[SSC] Bond overview diagnostic: " + ex);
                        Messages.Message(SSCBondOverviewText.T("DebugQueryFailed"), pawn, MessageTypeDefOf.RejectInput, false);
                    }
                }
            };
        }

        private static bool CanDiagnose(Pawn pawn)
            => Prefs.DevMode && SSCMod.settings?.enableDebugGizmos == true && SSCBondOverviewAccess.IsCardSource(pawn);
    }
}
