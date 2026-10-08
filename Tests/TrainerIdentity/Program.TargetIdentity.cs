using System;
using System.Linq;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>真实准入入口必须拒绝保留旧配置但没有 SSC 性奴身份的目标，不能依赖宿主属性隐藏缺口。</summary>
    private static void RunTrainingTargetIdentityTests()
    {
        Run("启用属性只表示保存设置，未设置身份仍由实际准入拒绝", () =>
        {
            Pawn target = Pawn();
            Assert(!target.Training.IsEnabled);
            target.Training.mode = TrainingMode.Enabled;
            Assert(target.Training.IsEnabled && !SSCIdentityUtility.IsSexSlave(target));
        });
        Run("自动候选及强制准备拒绝未设置或主人身份，不触发恢复身体检查和任务创建", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
            foreach (bool protection in new[] { false, true })
            foreach (int vanillaStatus in new[] { 0, 1, 2 })
            {
                var f = Setup(); var work = new WorkGiver_Training();
                f.slave.Training.pawnIdentity = identity;
                f.slave.IsColonist = vanillaStatus == 0;
                f.slave.IsPrisonerOfColony = vanillaStatus == 1;
                f.slave.IsSlave = vanillaStatus == 2;
                f.slave.Training.isBeingTrained = true;
                SSCMod.settings.enableSexSlaveProtectionRules = protection;
                BindingRitualStateUtility.RecoveryCalls = TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
                Assert(!work.PotentialWorkThingsGlobal(f.master).Contains(f.slave));
                foreach (bool forced in new[] { false, true })
                {
                    Assert(!TrainerAssignmentUtility.TryPrepareTrainingTarget(f.slave, f.master, forced, out string reason));
                    Assert(reason == "SSC_Training_TargetIdentityRequired".Translate());
                    Assert(!work.HasJobOnThing(f.master, f.slave, forced));
                    Assert(work.JobOnThing(f.master, f.slave, forced) == null);
                }
                Assert(BindingRitualStateUtility.RecoveryCalls == 0 && TrainingJobUtility.ValidationCalls == 0 && JobMaker.CreatedJobs == 0);
                Assert(f.slave.Training.pawnIdentity == identity && f.slave.Training.IsEnabled
                    && f.slave.Training.selectedTrainer == f.master && f.slave.Training.isBeingTrained);
            }
        });
        Run("后台日常仪式和首次建绑请求均要求 SSC 性奴身份，关闭限制不豁免", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
            foreach (bool protection in new[] { false, true })
            foreach (SSCInteractionKind kind in new[] { SSCInteractionKind.DailyTraining, SSCInteractionKind.RitualTraining, SSCInteractionKind.BindingPreparation })
            {
                var f = Setup(); f.slave.Training.pawnIdentity = identity;
                SSCMod.settings.enableSexSlaveProtectionRules = protection;
                foreach (bool assignment in new[] { false, true })
                {
                    var admission = SSCRestrictionTrainingUtility.Evaluate(new SSCRestrictionRequest(f.master, f.slave, kind, true), assignment);
                    Assert(!admission.Allowed && admission.Failure == SSCTrainingFailure.TargetIdentityRequired);
                    Assert(admission.Reason == "SSC_Training_TargetIdentityRequired".Translate());
                }
            }
        });
        Run("绑定主人最高许可不能替没有 SSC 身份的旧目标提供工作资格", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            var request = SSCRestrictionTrainingUtility.CreateRequest(f.master, f.slave, false);
            var admission = SSCRestrictionTrainingUtility.Evaluate(request, false);
            Assert(admission.Permission.Allowed && admission.Permission.Reason == SSCRestrictionReason.BoundOwner);
            Assert(!admission.Allowed && admission.Failure == SSCTrainingFailure.TargetIdentityRequired);
            Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, f.master, true));
        });
        Run("未设身份目标不可新增指派，失败保留保存配置且允许清理未绑定旧指派", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
            foreach (bool protection in new[] { false, true })
            {
                var f = Setup(); Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
                f.slave.Training.pawnIdentity = identity;
                SSCMod.settings.enableSexSlaveProtectionRules = protection;
                var config = f.slave.Training.restrictionConfig;
                Assert(!TrainerAssignmentUtility.GetTrainerCandidates(f.slave).Any());
                Assert(!TrainerAssignmentUtility.CanAssignTrainerTo(f.slave, other));
                Assert(!SSCBondUtility.TryAssignTrainer(f.slave, other));
                Assert(!SSCRestrictionTrainerAssignment.CanAssign(f.slave, other));
                Assert(!SSCRestrictionTrainerAssignment.TryAssign(f.slave, other));
                Assert(f.slave.Training.selectedTrainer == f.master && ReferenceEquals(config, f.slave.Training.restrictionConfig));
                Assert(SSCBondUtility.TryAssignTrainer(f.slave, null) && f.slave.Training.selectedTrainer == null);
            }
        });
        Run("候选选定后身份失格，创建和指派提交重新拒绝陈旧结果", () =>
        {
            var f = Setup(); var work = new WorkGiver_Training(); Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(work.HasJobOnThing(f.master, f.slave));
            Assert(TrainerAssignmentUtility.GetTrainerCandidates(f.slave).Contains(other));
            f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            TrainingJobUtility.ValidationCalls = JobMaker.CreatedJobs = 0;
            Assert(work.JobOnThing(f.master, f.slave, true) == null);
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, other));
            Assert(TrainingJobUtility.ValidationCalls == 0 && JobMaker.CreatedJobs == 0 && f.slave.Training.selectedTrainer == f.master);
        });
        Run("身体兼容校验返回期间身份失格，完整准备拒绝且保留保存设置", () =>
        {
            var f = Setup(); TrainingJobUtility.ValidationCalls = 0;
            TrainingJobUtility.OnValidation = () => f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            try
            {
                Assert(!TrainerAssignmentUtility.TryPrepareTrainingTarget(f.slave, f.master, true, out string reason));
                Assert(reason == "SSC_Training_TargetIdentityRequired".Translate() && TrainingJobUtility.ValidationCalls == 1);
                Assert(f.slave.Training.IsEnabled && f.slave.Training.selectedTrainer == f.master && !SSCIdentityUtility.IsSexSlave(f.slave));
            }
            finally { TrainingJobUtility.OnValidation = null; }
        });
        Run("无锁链 SSC 性奴仍可首次日常建绑，原版身份范围和排班差异保持独立", () =>
        {
            foreach (int vanillaStatus in new[] { 0, 1, 2 })
            {
                var f = Setup(); var work = new WorkGiver_Training();
                f.slave.IsColonist = vanillaStatus == 0;
                f.slave.IsPrisonerOfColony = vanillaStatus == 1;
                f.slave.IsSlave = vanillaStatus == 2;
                Assert(SSCBondUtility.GetChain(f.slave) == null && TrainerAssignmentUtility.IsAllowedTrainer(f.slave, f.master));
                Assert(work.PotentialWorkThingsGlobal(f.master).Contains(f.slave) && work.JobOnThing(f.master, f.slave) != null);
                f.slave.Training.scheduledTrainingEnabled = true; f.slave.Training.IsWithinScheduledTrainingWindow = false;
                Assert(work.JobOnThing(f.master, f.slave) == null && work.JobOnThing(f.master, f.slave, true) != null);
            }
        });
        Run("显式恢复旧锁链身份仅补回 SSC 性奴，保留历史所有权配置与成长", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            var chain = SSCBondUtility.GetChain(f.slave); chain.Severity = 0.9f;
            f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            f.slave.Training.selectedTrainer = other;
            f.slave.Training.scheduledTrainingEnabled = true;
            f.slave.Training.specializationProgress = 0.74f;
            f.slave.needs.Corruption.CurLevel = 0.52f;
            var config = f.slave.Training.restrictionConfig;
            Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Slave));
            Assert(SSCIdentityUtility.CanRestoreLegacySlaveIdentity(f.slave));
            Assert(SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave));
            Assert(SSCIdentityUtility.IsSexSlave(f.slave) && ReferenceEquals(chain, SSCBondUtility.GetChain(f.slave)));
            Assert(chain.LinkedPawn == f.master && chain.Severity == 0.9f && f.slave.Training.selectedTrainer == other);
            Assert(f.slave.Training.IsEnabled && f.slave.Training.scheduledTrainingEnabled && f.slave.Training.specializationProgress == 0.74f);
            Assert(f.slave.needs.Corruption.CurLevel == 0.52f && ReferenceEquals(config, f.slave.Training.restrictionConfig));
            Assert(!SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave));
        });
        Run("旧档恢复不从启用指派恶堕推断身份，拒绝坏关系及双角色矛盾", () =>
        {
            Assert(!SSCIdentityUtility.CanRestoreLegacySlaveIdentity(null));
            var f = Setup(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; f.slave.needs.Corruption.CurLevel = 0.9f;
            Assert(!SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave));
            foreach (int invalid in Enumerable.Range(0, 10))
            {
                f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave)); f.slave.Training.pawnIdentity = PawnIdentity.Unset;
                var chain = SSCBondUtility.GetChain(f.slave);
                switch (invalid)
                {
                    case 0: f.slave.Dead = true; break;
                    case 1: f.slave.Destroyed = true; break;
                    case 2: f.slave.Discarded = true; break;
                    case 3: chain.LinkedPawn = null; break;
                    case 4: chain.LinkedPawn = f.slave; break;
                    case 5: f.master.Destroyed = true; break;
                    case 6: f.master.Discarded = true; break;
                    case 7: f.slave.Training.pawnIdentity = PawnIdentity.Master; break;
                    case 8: f.slave.Training.restrictionRestoreDepth = 1; break;
                    case 9: Hediff_BridleOfSexSlave.AddToPawn(f.slave, Pawn(PawnIdentity.Slave)); break;
                }
                PawnIdentity identity = f.slave.Training.pawnIdentity;
                Assert(!SSCIdentityUtility.CanRestoreLegacySlaveIdentity(f.slave) && !SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave));
                Assert(f.slave.Training.pawnIdentity == identity && f.slave.Training.selectedTrainer == f.master);
            }
        });
        Run("旧档身份恢复等待引用读入完成，死亡主人关系不被当成解绑", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave)); f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            try
            {
                foreach (LoadSaveMode mode in new[] { LoadSaveMode.LoadingVars, LoadSaveMode.PostLoadInit })
                {
                    Scribe.mode = mode;
                    Assert(!SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave) && f.slave.Training.pawnIdentity == PawnIdentity.Unset);
                }
            }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
            f.master.Dead = true;
            Assert(SSCIdentityUtility.TryRestoreLegacySlaveIdentity(f.slave) && SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Run("身份切换只通知自己占据角色的仪式，普通 Lord 或旁观者保持原工作", () =>
        {
            Pawn actor = Pawn(PawnIdentity.Master), target = Pawn(PawnIdentity.Slave);
            var ritual = new RimWorld.LordJob_Ritual { Master = actor, Slave = target };
            var lord = new Verse.AI.Group.Lord { LordJob = ritual };
            actor.lord = target.lord = lord;
            target.Training.bindingRitualLord = lord;
            BindingRitualStateUtility.RejectionCalls = 0;
            Assert(SSCIdentityUtility.TrySetIdentity(target, PawnIdentity.Unset));
            Assert(BindingRitualStateUtility.RejectionCalls == 1);
            Pawn spectator = Pawn(PawnIdentity.Slave); spectator.lord = lord;
            Assert(SSCIdentityUtility.TrySetIdentity(spectator, PawnIdentity.Unset));
            Assert(BindingRitualStateUtility.RejectionCalls == 1);
            Pawn ordinary = Pawn(PawnIdentity.Slave); ordinary.lord = new Verse.AI.Group.Lord { LordJob = new Verse.AI.Group.LordJob() };
            Assert(SSCIdentityUtility.TrySetIdentity(ordinary, PawnIdentity.Unset));
            Assert(BindingRitualStateUtility.RejectionCalls == 1);
        });
        Run("仪式取消回调建立新锁链或缰绳时，原身份切换不得覆盖新绑定", () =>
        {
            foreach (bool masterSide in new[] { false, true })
            {
                var f = Setup(); var comp = f.slave.Training;
                comp.isRitualTraining = comp.isBeingTrained = true;
                var lord = new Verse.AI.Group.Lord { LordJob = new RimWorld.LordJob_Ritual { Master = f.master, Slave = f.slave } };
                f.slave.lord = comp.bindingRitualLord = lord;
                BindingRitualStateUtility.OnRejection = () =>
                {
                    if (masterSide) Hediff_BridleOfSexSlave.AddToPawn(f.slave, Pawn(PawnIdentity.Slave));
                    else Assert(SSCBondUtility.Bind(f.master, f.slave));
                };
                try
                {
                    Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset));
                    Assert(comp.pawnIdentity == PawnIdentity.Slave && comp.IsEnabled && comp.selectedTrainer == f.master);
                    Assert(SSCIdentityUtility.IsIdentityLocked(f.slave));
                }
                finally { BindingRitualStateUtility.OnRejection = null; }
            }
        });
        Run("仪式取消回调替换组件时，原身份切换不得写入新组件", () =>
        {
            var f = Setup(); var oldComp = f.slave.Training;
            var lord = new Verse.AI.Group.Lord { LordJob = new RimWorld.LordJob_Ritual { Master = f.master, Slave = f.slave } };
            f.slave.lord = oldComp.bindingRitualLord = lord;
            var replacement = new CompSexSlaveTraining { pawnIdentity = PawnIdentity.Master, mode = TrainingMode.Enabled, selectedTrainer = f.master };
            BindingRitualStateUtility.OnRejection = () => f.slave.Training = replacement;
            try
            {
                Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset));
                Assert(ReferenceEquals(replacement, f.slave.Training) && replacement.pawnIdentity == PawnIdentity.Master);
                Assert(replacement.IsEnabled && replacement.selectedTrainer == f.master && oldComp.pawnIdentity == PawnIdentity.Slave);
            }
            finally { BindingRitualStateUtility.OnRejection = null; }
        });
        Run("仪式取消回调更换身份时，原选择不得覆盖第三方的新身份", () =>
        {
            var f = Setup(); var lord = new Verse.AI.Group.Lord { LordJob = new RimWorld.LordJob_Ritual { Master = f.master, Slave = f.slave } };
            f.slave.lord = f.slave.Training.bindingRitualLord = lord;
            BindingRitualStateUtility.OnRejection = () => f.slave.Training.pawnIdentity = PawnIdentity.Master;
            try
            {
                Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset));
                Assert(f.slave.Training.pawnIdentity == PawnIdentity.Master && f.slave.Training.selectedTrainer == f.master);
            }
            finally { BindingRitualStateUtility.OnRejection = null; }
        });
        Run("仪式取消回调安装新 Lord 时，保留新场次的身份与占用", () =>
        {
            var f = Setup(); var comp = f.slave.Training;
            var oldLord = new Verse.AI.Group.Lord { LordJob = new RimWorld.LordJob_Ritual { Master = f.master, Slave = f.slave } };
            var newLord = new Verse.AI.Group.Lord { LordJob = new RimWorld.LordJob_Ritual { Master = f.master, Slave = f.slave } };
            f.slave.lord = comp.bindingRitualLord = oldLord;
            BindingRitualStateUtility.RejectionCalls = 0;
            BindingRitualStateUtility.OnRejection = () =>
            {
                f.slave.lord = comp.bindingRitualLord = newLord;
                comp.isBeingTrained = comp.isRitualTraining = true;
            };
            try
            {
                Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset));
                Assert(BindingRitualStateUtility.RejectionCalls == 1 && f.slave.lord == newLord && comp.bindingRitualLord == newLord);
                Assert(comp.pawnIdentity == PawnIdentity.Slave && comp.isBeingTrained && comp.isRitualTraining && comp.IsEnabled);
            }
            finally { BindingRitualStateUtility.OnRejection = null; }
        });
    }
}
