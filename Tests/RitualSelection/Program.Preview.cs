using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunPreviewTests()
    {
        Run("1000 人常驻角色预览无 RJW、寻路、报告、消息或角色写入", () =>
        {
            var f = new Fixture(998); f.Open();
            var spectators = f.A.SpectatorsForReading.ToArray();
            var candidates = f.A.AllCandidatePawns.ToArray();
            int writes = f.A.Writes;
            Counters.Reset();
            for (int frame = 0; frame < 3; frame++)
                foreach (Pawn pawn in f.Map.Pawns)
                {
                    BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, pawn);
                    BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, pawn);
                }
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0);
            Assert(Counters.Reports == 0 && Counters.Warnings == 0 && Counters.Messages == 0 && f.A.Writes == writes);
            Assert(f.A.SpectatorsForReading.SequenceEqual(spectators) && f.A.AllCandidatePawns.SequenceEqual(candidates));
            Assert(f.Target.BoundMaster == f.Host && f.Target.AssignedTrainer == f.Host);
        });
        Run("主持者选定后 1000 人配对预览仅执行低成本许可查询", () =>
        {
            var f = new Fixture(998); f.Open(); Assert(f.HostSlot());
            int writes = f.A.Writes; Counters.Reset();
            foreach (Pawn pawn in f.Map.Pawns)
                BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, pawn);
            Assert(Counters.Permissions > 0 && Counters.Eligibility == 0 && Counters.Reach == 0);
            Assert(Counters.Messages == 0 && Counters.Reports == 0 && Counters.Warnings == 0 && f.A.Writes == writes);
        });
        Run("双资格调教员具有主持和目标基础标记，SSC 主人仅具有主持标记", () =>
        {
            var f = new Fixture(); f.Open();
            var ownerHost = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host);
            var ownerTarget = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, f.Host);
            Assert(ownerHost.Candidate && ownerHost.PairAllowed && !ownerTarget.Candidate && !ownerTarget.PairAllowed);
            var trainer = new Pawn { SexSlave = true, Trainer = true };
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, trainer).Candidate);
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, trainer).Candidate);
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0);
        });
        Run("基础目标资格要求 SSC 性奴身份，保留原版殖民者、奴隶和囚犯范围", () =>
        {
            var f = new Fixture(); f.Open();
            foreach (Pawn pawn in new[] { new Pawn { SexSlave = true }, new Pawn { SexSlave = true, IsColonist = false, IsSlave = true },
                new Pawn { SexSlave = true, IsColonist = false, IsPrisonerOfColony = true } })
            {
                var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, pawn);
                Assert(preview.Candidate && preview.PairAllowed && !preview.Assigned);
            }
            foreach (Pawn pawn in new[] { new Pawn(), new Pawn { IsSlave = true }, new Pawn { IsPrisonerOfColony = true },
                new Pawn { SexSlave = true, Child = true }, new Pawn { HasComp = false },
                new Pawn { SexSlave = true, IsColonist = false }, new Pawn { Master = true }, new Pawn { SexSlave = true, Dead = true } })
            {
                var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, pawn);
                Assert(!preview.Candidate && !preview.PairAllowed && !string.IsNullOrEmpty(preview.Reason));
            }
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0);
        });
        Run("首次建绑预览保留 SSC 主人及唯一指派要求", () =>
        {
            var f = new Fixture(); f.Host.Master = false; f.Host.Trainer = true;
            f.Target.BoundMaster = null; f.Target.PermittedHosts.Add(f.Host); f.Open(); Assert(f.TargetSlot());
            Counters.Reset();
            var trainer = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host);
            Assert(trainer.Candidate && !trainer.PairAllowed && trainer.Reason == "SSC_Restrictions_BindingMasterRequired");
            f.Host.Master = true;
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host).PairAllowed);
            f.Target.AssignedTrainer = new Pawn { Master = true };
            var unassigned = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host);
            Assert(unassigned.Candidate && !unassigned.PairAllowed && unassigned.Reason == "SSC_Restrictions_TrainingAssignmentRequired");
            Assert(f.A.FirstAssignedPawn("master") == null && Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Messages == 0);
        });
        Run("已有绑定主人不受其他指派排除，第三方需许可和当前有效指派", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.TargetSlot());
            var trainer = new Pawn { Trainer = true }; f.Target.AssignedTrainer = trainer;
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host).PairAllowed);
            Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, trainer).PairAllowed);
            f.Target.PermittedHosts.Add(trainer);
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, trainer).PairAllowed);
            f.Target.AssignedTrainer = f.Host;
            Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, trainer).PairAllowed);
            Assert(f.Target.BoundMaster == f.Host && f.A.FirstAssignedPawn("master") == null);
        });
        Run("配对标记随当前选择变化重新求值，不把基础候选误报为可配对", () =>
        {
            var f = new Fixture(); f.Open();
            var candidate = new Pawn { SexSlave = true };
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, candidate).PairAllowed);
            Assert(f.HostSlot());
            var blocked = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, candidate);
            Assert(blocked.Candidate && !blocked.PairAllowed && !string.IsNullOrEmpty(blocked.Reason));
            candidate.BoundMaster = f.Host;
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, candidate).PairAllowed);
            f.A.TryUnassignAnyRole(f.Host); candidate.BoundMaster = null;
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, candidate).PairAllowed);
        });
        Run("已分配角色失去基础资格时仍标为已选并提供拒绝原因", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth(); f.Target.Downed = true; Counters.Reset();
            var target = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, f.Target);
            Assert(target.Assigned && !target.Candidate && !target.PairAllowed && target.Reason == "SSC_RitualSelection_Invalid");
            f.Host.Master = false; f.Host.Trainer = false;
            var host = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host);
            Assert(host.Assigned && !host.Candidate && host.Reason == "SSC_TrainerIdentity_Required");
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Messages == 0);
        });
        Run("未登记、已关闭、无关仪式和外部角色不会获得 SSC 候选通过标记", () =>
        {
            var f = new Fixture(); f.Open();
            var raw = new RitualRoleAssignments(f.Ritual, f.Spot);
            var other = new Fixture(binding: false); other.Open();
            foreach (RitualRoleAssignments a in new[] { null, raw, other.A })
            {
                var preview = BindingRitualRolePreviewUtility.Inspect(a, f.HostRole, f.Host);
                Assert(!preview.Candidate && !preview.PairAllowed && !string.IsNullOrEmpty(preview.Reason));
                Assert(!BindingRitualSelectionUtility.InspectAttempt(a, f.HostRole, f.Host, null, out string reason) && reason != null);
            }
            foreach (RitualRole role in new RitualRole[] { null, new RitualRole(), new RitualRole { id = "master" }, new RitualRole_BindingMaster { id = "master" } })
                Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, role, f.Host).Candidate);
            Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, null).Candidate);
            f.A.AllRolesForReading.Remove(f.TargetRole);
            Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host).Candidate);
            Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, f.Host, null, out _));
            f.A.AllRolesForReading.Add(f.TargetRole);
            f.Window.Close();
            Assert(!BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host).PairAllowed);
        });
        Run("完整悬停检查读取原版、寻路及身体条件，仍保持所有选择和关系不变", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            int writes = f.A.Writes; var spectators = f.A.SpectatorsForReading.ToArray(); Counters.Reset();
            Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.TargetRole, f.Target, null, out string reason) && reason == null);
            Assert(Counters.Eligibility == 1 && Counters.Reach == 2 && Counters.Permissions == 1);
            Assert(Counters.Messages == 0 && Counters.Reports == 0 && Counters.Warnings == 0 && f.A.Writes == writes);
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == null);
            Assert(f.A.SpectatorsForReading.SequenceEqual(spectators) && f.Target.BoundMaster == f.Host && f.Target.AssignedTrainer == f.Host);
        });
        Run("强制角色来源保留基础候选与已选状态，但显示不可重新分配的锁定原因", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            f.A.ForcedRolesForReading["other"] = f.Target; int writes = f.A.Writes; Counters.Reset();
            var source = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, f.Target);
            Assert(source.Candidate && !source.Assigned && !source.PairAllowed && source.Reason == "RoleIsLocked");
            Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.TargetRole, f.Target, null, out string reason) && reason == "RoleIsLocked");
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0 && Counters.Messages == 0 && f.A.Writes == writes);
            Assert(!f.TargetSlot());
            f.A.ForcedRolesForReading.Clear(); f.A.ForcedRolesForReading["slave"] = f.Target;
            var assigned = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, f.Target);
            Assert(assigned.Candidate && assigned.Assigned && !assigned.PairAllowed && assigned.Reason == "RoleIsLocked");
            // 原版已固定的合法角色仍可启动；只读提示不能成为新的游戏限制。
            Assert(BindingRitualSelectionUtility.ValidateStart(f.A, f.Spot, out _));
        });
        Run("强制角色目的槽位拒绝替换预览，解除锁定后同一配对恢复正常", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            var host = new Pawn { Master = true }; f.Target.PermittedHosts.Add(host); f.Target.AssignedTrainer = host;
            f.A.ForcedRolesForReading["master"] = f.Host; int writes = f.A.Writes; Counters.Reset();
            var locked = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, host, f.Host);
            Assert(locked.Candidate && !locked.Assigned && !locked.PairAllowed && locked.Reason == "RoleIsLocked");
            Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, host, f.Host, out string reason) && reason == "RoleIsLocked");
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0 && Counters.Messages == 0 && f.A.Writes == writes);
            Assert(!f.Window.Replace(host, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target);
            f.A.ForcedRolesForReading.Clear();
            Assert(BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, host, f.Host).PairAllowed);
            Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, host, f.Host, out _));
        });
        Run("满角色栏空白处不能接收新人，明确拖到当前头像上的替换仍允许", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            var host = new Pawn { Master = true }; f.Target.PermittedHosts.Add(host); f.Target.AssignedTrainer = host;
            int writes = f.A.Writes; Counters.Reset();
            var blank = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, host);
            Assert(blank.Candidate && !blank.PairAllowed && blank.Reason == "MaxPawnsPerRole");
            Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, host, null, out string reason) && reason == "MaxPawnsPerRole");
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0 && Counters.Messages == 0 && f.A.Writes == writes);
            Assert(!f.HostSlot(host) && f.A.FirstAssignedPawn("master") == f.Host);
            var own = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Host);
            Assert(own.Candidate && own.Assigned && own.PairAllowed);
            var replacement = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, host, f.Host);
            Assert(replacement.Candidate && replacement.PairAllowed);
            Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, host, f.Host, out _));
            Assert(f.Window.Replace(host, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == host && f.A.FirstAssignedPawn("slave") == f.Target);
        });
        foreach (string failure in new[] { "vanilla", "reachable", "body" })
            Run("完整悬停提前反馈提交拒绝：" + failure, () =>
            {
                var f = new Fixture(); f.Open(); Assert(f.HostSlot());
                if (failure == "vanilla") f.Target.VanillaBlocked = true;
                if (failure == "reachable") f.Target.Reachable = false;
                if (failure == "body") f.Target.Eligible = false;
                var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.TargetRole, f.Target);
                Assert(preview.Candidate && preview.PairAllowed); Counters.Reset();
                Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.TargetRole, f.Target, null, out string reason));
                Assert(!string.IsNullOrEmpty(reason) && Counters.Messages == 0);
                Assert(Counters.Eligibility == (failure == "body" ? 1 : 0));
                Assert(!f.TargetSlot() && f.A.FirstAssignedPawn("slave") == null);
            });
        Run("悬停成功不会写入提交缓存，同一操作中状态变化后提交仍拒绝", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot()); Counters.Reset();
            using (BindingRitualSelectionUtility.BeginOperation(f.A))
            {
                Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.TargetRole, f.Target, null, out _));
                f.Target.Eligible = false;
                Assert(!BindingRitualSelectionUtility.ValidateAttempt(f.A, f.TargetRole, f.Target, null, out _));
            }
            Assert(Counters.Eligibility == 2 && Counters.Messages == 0 && f.A.FirstAssignedPawn("slave") == null);
        });
        Run("悬停检查不复用该操作已有的提交成功缓存", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot()); Counters.Reset();
            using (BindingRitualSelectionUtility.BeginOperation(f.A))
            {
                Assert(BindingRitualSelectionUtility.ValidateAttempt(f.A, f.TargetRole, f.Target, null, out _));
                f.Target.Eligible = false;
                Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.TargetRole, f.Target, null, out _));
            }
            Assert(Counters.Eligibility == 2 && Counters.Messages == 0);
        });
        Run("角色互换的轻量、完整预览及实际提交使用相同最终配对", () =>
        {
            var f = new Fixture(); f.Host.Master = false; f.Host.SexSlave = true; f.Host.Trainer = f.Target.Trainer = true;
            f.Host.BoundMaster = f.Target; f.Open(); f.SelectBoth(); Counters.Reset();
            var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Target, f.Host);
            Assert(preview.Candidate && preview.PairAllowed && !preview.Assigned);
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0);
            Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, f.Target, f.Host, out _));
            Assert(f.Window.Replace(f.Target, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Target && f.A.FirstAssignedPawn("slave") == f.Host);
        });
        Run("换去目标槽位的 SSC 主人使交换预览失效但保留新人主持基础标记", () =>
        {
            var f = new Fixture(); f.Target.Trainer = true; f.Open(); f.SelectBoth(); Counters.Reset();
            var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Target, f.Host);
            Assert(preview.Candidate && !preview.PairAllowed && preview.Reason == "SSC_Training_TargetIdentityRequired");
            Assert(!BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, f.Target, f.Host, out _));
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0);
            Assert(!f.Window.Replace(f.Target, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target);
        });
        Run("移到空主持槽位的目标预览清空旧目标而不是与自己配对", () =>
        {
            var f = new Fixture(); f.Target.Trainer = true; f.Open(); Assert(f.TargetSlot()); Counters.Reset();
            var preview = BindingRitualRolePreviewUtility.Inspect(f.A, f.HostRole, f.Target);
            Assert(preview.Candidate && preview.PairAllowed && !preview.Assigned && Counters.Permissions == 0);
            Assert(BindingRitualSelectionUtility.InspectAttempt(f.A, f.HostRole, f.Target, null, out _));
            Assert(Counters.Eligibility == 0 && Counters.Reach == 1);
            Assert(f.HostSlot(f.Target) && f.A.FirstAssignedPawn("slave") == null);
        });
    }
}
