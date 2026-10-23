// AI 辅助生成
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    public class RitualBehaviorWorker_Cosmetic : RitualBehaviorWorker
    {
        // 特效仍由原版阶段列表驱动和保存；这里只补挂实机中未创建的本阶段实例。
        private static readonly FieldInfo StageEffectsField = typeof(LordJob_Ritual).GetField(
            "effectWorkersCurrentStage", BindingFlags.Instance | BindingFlags.NonPublic);

        public RitualBehaviorWorker_Cosmetic() { }
        public RitualBehaviorWorker_Cosmetic(RitualBehaviorDef def) : base(def) { }

        public override void Tick(LordJob_Ritual ritual)
        {
            base.Tick(ritual);
            // GotoToil 先进入下一职责、再执行扣料 postAction；只在两个阶段指针一致且正式开始后补挂。
            RitualStage stage = ritual.CurrentStage;
            if (stage == null || stage is RitualStage_CosmeticPreparation || stage.visualEffectDef == null ||
                !(ritual.lord?.CurLordToil is LordToil_Ritual toil) || toil.stage != stage) return;
            var effects = (List<RitualVisualEffect>)StageEffectsField.GetValue(ritual);
            if (effects.Any(effect => effect.def == stage.visualEffectDef)) return;

            // 正常创建或读档已有实例时不重置间隔；缺失时只创建一次，后续 Tick／存档／清理由原版负责。
            RitualVisualEffect effect = stage.visualEffectDef.GetInstance();
            effect.Setup(ritual, false);
            effects.Add(effect);
        }

        public override string CanStartRitualNow(TargetInfo target, Precept_Ritual ritual, Pawn selectedPawn = null, Dictionary<string, Pawn> forcedForRole = null)
        {
            // 开窗和原版启动都会查询这里；保留原版拒绝，再做不消耗物资的文化与供料预检。
            string reason = base.CanStartRitualNow(target, ritual, selectedPawn, forcedForRole);
            if (reason != null) return reason;
            if (!SSCCosmeticUtility.HasRequiredMeme(ritual)) return "SSC_Cosmetic_MemeRequired".Translate();
            if (!target.IsValid || target.Map == null) return "SSC_Cosmetic_InvalidTarget".Translate();
            Pawn host = null;
            if (forcedForRole != null) forcedForRole.TryGetValue(SSCCosmeticUtility.HostRole, out host);
            if (host != null)
            {
                // 已指定主持者时只检查此人，不能借其他人的资格或库存使预检通过。
                var hostReport = RitualRole_CosmeticHost.Report(host, target, ritual);
                if (!hostReport.Accepted) return hostReport.Reason;
                var materialReport = RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, host);
                return materialReport.Accepted ? null : materialReport.Reason;
            }
            // 开窗尚未选主持者时，仅做只读预检；实际开始和消费使用选中的主持者。
            bool hasHost = false;
            foreach (Pawn candidate in target.Map.mapPawns.FreeColonistsSpawned)
            {
                if (!RitualRole_CosmeticHost.Report(candidate, target, ritual).Accepted) continue;
                hasHost = true;
                if (RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, candidate).Accepted) return null;
            }
            return hasHost ? RitualStageAction_ConsumeCosmeticMaterials.MissingMaterialsReason :
                "SSC_Cosmetic_RolesRequired".Translate();
        }

        public override bool PawnCanFillRole(Pawn pawn, RitualRole role, out string s, TargetInfo ritualTarget)
        {
            // 两个专属角色由各自角色类检查；无角色的候选作为观众，不受 Beauty 或同文化限制。
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
            // 点击开始时先检查必需槽位，再让原版统一复查当前实际分配的所有参与者。
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
            // 空间和材料只读复查通过后才交给原版建 Lord，拒绝时尚未发起搬运或扣料。
            if (reason == null)
            {
                if (!RitualPosition_Cosmetic.TryGetPositions(target,
                    assignments?.FirstAssignedPawn(SSCCosmeticUtility.HostRole),
                    assignments?.FirstAssignedPawn(SSCCosmeticUtility.RecipientRole), out _, out _))
                    reason = "SSC_Cosmetic_NoRoleSpace".Translate();
            }
            if (reason == null)
            {
                var report = RitualStageAction_ConsumeCosmeticMaterials.MaterialReport(target, assignments?.FirstAssignedPawn(SSCCosmeticUtility.HostRole));
                if (!report.Accepted) reason = report.Reason;
            }
            if (reason != null)
            {
                // 集中报告首个拒绝原因；不改分配清单，也不替换原版角色选择与刷新流程。
                Messages.Message(reason, target, MessageTypeDefOf.RejectInput, false);
                return;
            }
            // 原版继续负责启动、职责、阶段保存、活动占用和结束释放。
            base.TryExecuteOn(target, organizer, ritual, obligation, assignments, playerForced);
        }
    }
}
