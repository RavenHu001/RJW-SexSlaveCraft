// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualBehaviorWorker_Cosmetic : RitualBehaviorWorker
    {
        public RitualBehaviorWorker_Cosmetic() { }
        public RitualBehaviorWorker_Cosmetic(RitualBehaviorDef def) : base(def) { }

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            string reason = base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
            if (reason != null) return reason;
            if (!SSCCosmeticUtility.HasRequiredMeme(ritual)) return "SSC_Cosmetic_MemeRequired".Translate();
            if (!target.IsValid || target.Map == null) return "SSC_Cosmetic_InvalidTarget".Translate();
            Pawn host = null;
            if (forcedForRole != null) forcedForRole.TryGetValue(SSCCosmeticUtility.HostRole, out host);
            if (host != null)
            {
                var hostReport = RitualRole_CosmeticHost.Report(host, target, ritual);
                if (!hostReport.Accepted) return hostReport.Reason;
            }
            // 开窗尚未选主持者时，仅做只读预检；实际开始和消费使用选中的主持者。
            var hosts = (host != null ? new[] { host } : target.Map.mapPawns.FreeColonistsSpawned
                .Where(p => RitualRole_CosmeticHost.Report(p, target, ritual).Accepted)).ToList();
            if (hosts.Count == 0) return "SSC_Cosmetic_RolesRequired".Translate();
            AcceptanceReport report = false;
            foreach (Pawn candidate in hosts)
            {
                report = RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, candidate);
                if (report.Accepted) return null;
            }
            return report.Reason ?? "SSC_Cosmetic_MaterialsRequired".Translate(20, 2);
        }

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string s, TargetInfo ritualTarget)
        {
            if (!base.PawnCanFillRole(pawn, role, out s, ritualTarget)) return false;
            if (role != null) return true;
            var report = SSCCosmeticUtility.ActiveParticipantReport(pawn, ritualTarget);
            if (report.Accepted && !(pawn.IsColonistPlayerControlled || pawn.IsSlaveOfColony))
                report = "SSC_Cosmetic_RitualIdentity".Translate();
            s = report.Reason;
            return report.Accepted;
        }

        public override void TryExecuteOn(TargetInfo target, Pawn organizer, Precept_Ritual ritual,
            RitualObligation obligation, RitualRoleAssignments assignments, bool playerForced = false)
        {
            string reason = null;
            foreach (var role in def.roles.Where(r => r.required))
            {
                Pawn pawn = assignments?.FirstAssignedPawn(role.id);
                if (pawn == null)
                {
                    reason = "SSC_Cosmetic_RolesRequired".Translate();
                    break;
                }
            }
            if (reason == null && assignments != null)
                foreach (Pawn pawn in assignments.Participants)
                {
                    // 原版报告同时复查角色规则、其他活动、伤势、温度及奴隶出席许可。
                    reason = RitualRoleAssignments.PawnNotAssignableReason(pawn, assignments.RoleForPawn(pawn),
                        ritual, assignments, target, out _);
                    if (reason != null) break;
                }
            if (reason == null)
            {
                var report = RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, assignments?.FirstAssignedPawn(SSCCosmeticUtility.HostRole));
                if (!report.Accepted) reason = report.Reason;
            }
            if (reason != null)
            {
                Messages.Message(reason, target, MessageTypeDefOf.RejectInput, false);
                return;
            }
            base.TryExecuteOn(target, organizer, ritual, obligation, assignments, playerForced);
        }
    }
}
