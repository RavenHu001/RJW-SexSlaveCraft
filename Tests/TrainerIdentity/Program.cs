using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static int passed, failed;

    /// <summary>运行真实身份、指派、工作、仪式及迁移代码；只替代游戏外部接口。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args.Length == 0 ? "../.." : args[0]);
        var xml = XDocument.Load(Path.Combine(repo, "Languages/ChineseSimplified/Keyed/SSC_TrainerIdentity.xml"));
        foreach (var key in xml.Root.Elements()) Verse.Extensions.Translations[key.Name.LocalName] = key.Value;
        RunEducationCompatibilityTests();
        // 训导官只读资格和阶段 2 状态生命周期使用生产源码验证；
        // 下方旧身份用例仍按现行开关规则运行。
        RunTrainerSpecializationTests(repo);
        RunTrainerLifecycleTests();
        RunTrainerProgressTests();
        Run("三种身份的开关真值表与固定身份拒绝写入", () =>
        {
            foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
            foreach (bool saved in new[] { false, true })
            {
                Pawn p = Pawn(identity); p.Training.slaveTrainerEnabled = saved;
                Assert(SSCIdentityUtility.IsTrainer(p) == (identity == PawnIdentity.Master || identity == PawnIdentity.Slave && saved));
                bool changed = SSCIdentityUtility.SetTrainerEnabled(p, !saved);
                Assert(changed == (identity == PawnIdentity.Slave));
                Assert(p.Training.slaveTrainerEnabled == (changed ? !saved : saved));
            }
            Assert(!SSCIdentityUtility.IsTrainer(null));
            Pawn missing = Pawn(); missing.Training = null; Assert(!SSCIdentityUtility.IsTrainer(missing));
        });
        Run("真实身份切换不继承主人强制开启，保留性奴个人选择", () =>
        {
            Pawn p = Pawn(PawnIdentity.Master);
            SSCIdentityUtility.TrySetIdentity(p, PawnIdentity.Slave); Assert(!SSCIdentityUtility.IsTrainer(p));
            SSCIdentityUtility.SetTrainerEnabled(p, true);
            SSCIdentityUtility.TrySetIdentity(p, PawnIdentity.Unset); Assert(!SSCIdentityUtility.IsTrainer(p));
            SSCIdentityUtility.TrySetIdentity(p, PawnIdentity.Slave); Assert(SSCIdentityUtility.IsTrainer(p));
        });
        Run("已绑定性奴拒绝切换且保留锁链进度、恶堕及训练仪式状态", () =>
        {
            foreach (PawnIdentity target in new[] { PawnIdentity.Unset, PawnIdentity.Master })
            {
                var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
                var chain = SSCBondUtility.GetChain(f.slave); chain.Severity = 0.9f;
                f.slave.needs.Corruption.CurLevel = 0.95f;
                f.slave.needs.Corruption.HighestCorruptionLevel = 1f;
                f.slave.Training.isRitualTraining = f.slave.Training.isBeingTrained = true;
                Assert(SSCIdentityUtility.IsIdentityLocked(f.slave));
                Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, target));
                Assert(SSCIdentityUtility.IsSexSlave(f.slave));
                Assert(ReferenceEquals(chain, SSCBondUtility.GetChain(f.slave)) && chain.Severity == 0.9f);
                Assert(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
                Assert(SSCBondUtility.GetBridle(f.master).targets.Contains(f.slave));
                Assert(f.slave.needs.Corruption.CurLevel == 0.95f && f.slave.needs.Corruption.HighestCorruptionLevel == 1f);
                Assert(f.slave.Training.selectedTrainer == f.master && f.slave.Training.mode == TrainingMode.Enabled);
                Assert(f.slave.Training.isRitualTraining && f.slave.Training.isBeingTrained);
            }
        });
        Run("有绑定性奴的主人拒绝两种身份切换且保留全部绑定", () =>
        {
            foreach (PawnIdentity target in new[] { PawnIdentity.Unset, PawnIdentity.Slave })
            {
                var f = Setup(); Pawn second = Pawn(PawnIdentity.Slave, f.slave.Map);
                Assert(SSCBondUtility.Bind(f.master, f.slave) && SSCBondUtility.Bind(f.master, second));
                var bridle = SSCBondUtility.GetBridle(f.master);
                var firstChain = SSCBondUtility.GetChain(f.slave); firstChain.Severity = 0.9f;
                var secondChain = SSCBondUtility.GetChain(second); secondChain.Severity = 0.5f;
                Assert(SSCIdentityUtility.IsIdentityLocked(f.master));
                Assert(!SSCIdentityUtility.TrySetIdentity(f.master, target));
                Assert(SSCIdentityUtility.IsMaster(f.master) && ReferenceEquals(bridle, SSCBondUtility.GetBridle(f.master)));
                Assert(bridle.targets.Count == 2 && bridle.targets.Contains(f.slave) && bridle.targets.Contains(second));
                Assert(ReferenceEquals(firstChain, SSCBondUtility.GetChain(f.slave)) && firstChain.Severity == 0.9f);
                Assert(ReferenceEquals(secondChain, SSCBondUtility.GetChain(second)) && secondChain.Severity == 0.5f);
                Assert(firstChain.LinkedPawn == f.master && secondChain.LinkedPawn == f.master);
            }
        });
        Run("重复设置绑定双方当前身份及重复绑定不清理状态", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            var chain = SSCBondUtility.GetChain(f.slave); chain.Severity = 0.9f;
            foreach (Pawn pawn in new[] { f.master, f.slave })
            {
                pawn.Training.isRitualTraining = pawn.Training.isBeingTrained = true;
                pawn.Training.selectedTrainer = f.master;
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, pawn.Training.pawnIdentity));
                Assert(pawn.Training.isRitualTraining && pawn.Training.isBeingTrained);
                Assert(pawn.Training.mode == TrainingMode.Enabled && pawn.Training.selectedTrainer == f.master);
            }
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Assert(ReferenceEquals(chain, SSCBondUtility.GetChain(f.slave)) && chain.Severity == 0.9f);
            Assert(SSCBondUtility.GetBridle(f.master).targets.Count == 1);
        });
        Run("无绑定身份可自由切换并保留离开性奴时的训练清理", () =>
        {
            foreach (PawnIdentity source in Enum.GetValues<PawnIdentity>())
            foreach (PawnIdentity target in Enum.GetValues<PawnIdentity>())
            {
                Pawn pawn = Pawn(source); pawn.Training.selectedTrainer = Pawn(PawnIdentity.Master);
                pawn.Training.isRitualTraining = pawn.Training.isBeingTrained = true;
                Assert(!SSCIdentityUtility.IsIdentityLocked(pawn));
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, target) && pawn.Training.pawnIdentity == target);
                if (source != target && target != PawnIdentity.Slave)
                    Assert(pawn.Training.mode == TrainingMode.Disabled && pawn.Training.selectedTrainer == null
                        && !pawn.Training.isRitualTraining && !pawn.Training.isBeingTrained);
            }
        });
        Run("明确解绑后解除身份锁定，主人仍有其他绑定时继续锁定", () =>
        {
            var f = Setup(); Pawn second = Pawn(PawnIdentity.Slave, f.slave.Map);
            Assert(SSCBondUtility.Bind(f.master, f.slave) && SSCBondUtility.Bind(f.master, second));
            Assert(SSCBondUtility.Unbind(f.slave));
            Assert(SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Unset));
            Assert(!SSCIdentityUtility.TrySetIdentity(f.master, PawnIdentity.Unset));
            Assert(SSCBondUtility.Unbind(second));
            Assert(!SSCIdentityUtility.IsIdentityLocked(f.master));
            Assert(SSCIdentityUtility.TrySetIdentity(f.master, PawnIdentity.Unset));
        });
        Run("绑定入口不能把已有性奴的主人改为性奴或留下半条绑定", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            Pawn otherMaster = Pawn(PawnIdentity.Master, f.slave.Map);
            foreach (bool replace in new[] { false, true })
            {
                Assert(!SSCBondUtility.Bind(otherMaster, f.master, replace));
                Assert(SSCIdentityUtility.IsMaster(f.master) && SSCBondUtility.GetChain(f.master) == null);
                Assert(SSCBondUtility.GetBridle(otherMaster) == null);
                Assert(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
                Assert(SSCBondUtility.GetBridle(f.master).targets.SequenceEqual(new[] { f.slave }));
            }
        });
        Run("首次绑定未选择或无绑定主人可成功，已有归属冲突仍拒绝", () =>
        {
            foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master })
            {
                Pawn master = Pawn(PawnIdentity.Master), slave = Pawn(identity);
                Assert(SSCBondUtility.Bind(master, slave));
                Assert(SSCIdentityUtility.IsSexSlave(slave) && SSCBondUtility.GetBoundMaster(slave) == master);
                Pawn other = Pawn(PawnIdentity.Master);
                Assert(!SSCBondUtility.Bind(other, slave));
                Assert(SSCBondUtility.GetBoundMaster(slave) == master && SSCBondUtility.GetBridle(other) == null);
            }
            Assert(!SSCIdentityUtility.TrySetIdentity(null, PawnIdentity.Slave));
            Pawn missing = Pawn(); missing.Training = null;
            Assert(!SSCBondUtility.Bind(Pawn(PawnIdentity.Master), missing));
            Assert(SSCBondUtility.GetChain(missing) == null);
        });
        Run("菜单排除未选择、关闭性奴、自身、原版候选外人员及工作关闭", () =>
        {
            var f = Setup(); Pawn on = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            Pawn off = Pawn(PawnIdentity.Slave, f.slave.Map), unset = Pawn(PawnIdentity.Unset, f.slave.Map);
            Pawn paused = Pawn(PawnIdentity.Master, f.slave.Map); paused.workSettings.Active = false;
            Pawn legalSlave = Pawn(PawnIdentity.Slave, f.slave.Map, true); legalSlave.IsSlave = true; legalSlave.IsColonist = false;
            f.slave.Map.mapPawns.FreeColonistsSource.Remove(legalSlave);
            f.slave.Training.slaveTrainerEnabled = true;
            Assert(TrainerAssignmentUtility.GetTrainerCandidates(f.slave).SequenceEqual(new[] { f.master, on }));
            foreach (Pawn invalid in new[] { off, unset, paused, legalSlave, f.slave })
                Assert(!SSCBondUtility.TryAssignTrainer(f.slave, invalid));
        });
        Run("停用旧调教员后生成菜单候选不因原版共享分类列表重建而抛异常", () =>
        {
            var f = Setup(); Pawn inactive = Pawn(PawnIdentity.Unset, f.slave.Map);
            Pawn replacement = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            f.slave.Training.selectedTrainer = inactive;
            using (var candidates = TrainerAssignmentUtility.GetTrainerCandidates(f.slave).GetEnumerator())
            {
                Assert(candidates.MoveNext() && candidates.Current == f.master);
                Assert(TrainerAssignmentUtility.CanAssignTrainerTo(f.slave, replacement));
                Assert(candidates.MoveNext() && candidates.Current == replacement);
                Assert(!candidates.MoveNext());
            }
            Assert(f.slave.Training.selectedTrainer == inactive);
        });
        Run("直接指派校验同样拒绝异图、死亡和倒地", () =>
        {
            var f = Setup(); Pawn foreign = Pawn(PawnIdentity.Master);
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, foreign));
            f.master.Dead = true; Assert(!SSCBondUtility.TryAssignTrainer(f.slave, f.master));
            f.master.Dead = false; f.master.Downed = true; Assert(!SSCBondUtility.TryAssignTrainer(f.slave, f.master));
        });
        Run("重新指派不被原调教员锁定，拒绝写入保留旧指派", () =>
        {
            var f = Setup(); Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, f.master));
            Assert(TrainerAssignmentUtility.GetTrainerCandidates(f.slave).Contains(other));
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, other));
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, Pawn(PawnIdentity.Unset, f.slave.Map)));
            Assert(f.slave.Training.selectedTrainer == other);
        });
        Run("清空始终允许但不开放任意自动接替", () =>
        {
            var f = Setup(); f.slave.Training.selectedTrainer = f.master; f.master.workSettings.Active = false;
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, null) && f.slave.Training.selectedTrainer == null);
            Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, Pawn(PawnIdentity.Master, f.slave.Map)));
        });
        Run("个人禁止调教仍允许菜单选择合格非主人，明确指派才提交授权", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(TrainerAssignmentUtility.GetTrainerCandidates(f.slave).Contains(other));
            Assert(!f.slave.Training.restrictionConfig.rules.receiveTraining);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, other));
            Assert(f.slave.Training.restrictionConfig.rules.receiveTraining && f.slave.Training.selectedTrainer == other);
        });
        Run("主人工作暂停不影响绑定默认指派", () =>
        {
            var f = Setup(); f.master.workSettings.Active = false;
            Assert(SSCBondUtility.Bind(f.master, f.slave));
            Assert(f.slave.Training.selectedTrainer == f.master && SSCBondUtility.IsBoundTo(f.slave, f.master));
            Assert(TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == f.master);
        });
        Run("停用指派不变成未指定，也不允许他人接替", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, trainer));
            SSCIdentityUtility.SetTrainerEnabled(trainer, false);
            Assert(f.slave.Training.selectedTrainer == trainer && TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == null);
            Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, trainer));
            Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, f.master));
            SSCIdentityUtility.SetTrainerEnabled(trainer, true);
            Assert(TrainerAssignmentUtility.IsAllowedTrainer(f.slave, trainer));
        });
        Run("允许其他人和公交车不能越过调教员身份", () =>
        {
            foreach (int kind in new[] { 0, 1, 2 })
            {
                var f = Setup(); f.slave.Training.AllowsOthersForTrainingOrSex = kind == 0;
                f.slave.Training.IsBusSpecialized = kind == 1; f.slave.Training.BusState = kind == 2;
                Pawn unset = Pawn(PawnIdentity.Unset, f.slave.Map), off = Pawn(PawnIdentity.Slave, f.slave.Map);
                f.slave.Training.selectedTrainer = off;
                Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, unset));
                Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, off));
                Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, f.master));
            }
        });
        Run("工作暂停和倒地不抹除有效指派，死亡和自身失效", () =>
        {
            var f = Setup(); f.slave.Training.selectedTrainer = f.master;
            f.master.workSettings.Active = false; f.master.Downed = true;
            Assert(SSCIdentityUtility.IsTrainer(f.master) && TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == f.master);
            f.master.Dead = true; Assert(TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == null);
            f.slave.Training.slaveTrainerEnabled = true; f.slave.Training.selectedTrainer = f.slave;
            Assert(TrainerAssignmentUtility.GetActiveAssignedTrainer(f.slave) == null);
        });
        Run("真实工作入口自动和强制均拒绝无资格者，恢复后允许", () =>
        {
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map);
            var work = new WorkGiver_Training(); TrainingJobUtility.ValidationCalls = 0;
            Assert(work.JobOnThing(trainer, f.slave, false) == null && work.JobOnThing(trainer, f.slave, true) == null);
            Assert(JobFailReason.Last.Contains("调教员身份") && TrainingJobUtility.ValidationCalls == 0);
            SSCBondUtility.Bind(f.master, f.slave);
            f.slave.Training.selectedTrainer = trainer;
            f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            SSCIdentityUtility.SetTrainerEnabled(trainer, true);
            Assert(work.JobOnThing(trainer, f.slave, false)?.target == f.slave);
            Assert(work.PotentialWorkThingsGlobal(trainer).Contains(f.slave));
        });
        Run("身份有效仍保留原有冷却排班和预留限制", () =>
        {
            var f = Setup(); var work = new WorkGiver_Training();
            f.slave.Training.IsOnCooldown = true; Assert(work.JobOnThing(f.master, f.slave, true) == null);
            f.slave.Training.IsOnCooldown = false; f.slave.Training.scheduledTrainingEnabled = true; f.slave.Training.IsWithinScheduledTrainingWindow = false;
            Assert(work.JobOnThing(f.master, f.slave, false) == null);
            Assert(work.JobOnThing(f.master, f.slave, true) != null);
            f.master.Reservable = false; Assert(work.JobOnThing(f.master, f.slave, true) == null);
        });
        Run("关闭身份保留在途状态，禁止下一次启动", () =>
        {
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            SSCBondUtility.Bind(f.master, f.slave); f.slave.Training.restrictionConfig.rules.receiveTraining = true;
            f.slave.Training.selectedTrainer = trainer; f.slave.Training.isBeingTrained = true;
            Assert(TrainerAssignmentUtility.IsAllowedTrainer(f.slave, trainer));
            SSCIdentityUtility.SetTrainerEnabled(trainer, false);
            Assert(f.slave.Training.isBeingTrained && f.slave.Training.selectedTrainer == trainer);
            Assert(!TrainerAssignmentUtility.IsAllowedTrainer(f.slave, trainer));
        });
        Run("无主归属使用有效调教员，有主仍返回实际主人", () =>
        {
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            f.slave.Training.selectedTrainer = trainer; Assert(SSCBondUtility.GetResolvedMaster(f.slave) == trainer);
            SSCIdentityUtility.SetTrainerEnabled(trainer, false); Assert(SSCBondUtility.GetResolvedMaster(f.slave) == null);
            SSCBondUtility.Bind(f.master, f.slave); f.slave.Training.selectedTrainer = trainer;
            Assert(SSCBondUtility.GetResolvedMaster(f.slave) == f.master);
        });
        Run("指派显示区分停用和工作暂停", () =>
        {
            var f = Setup(); f.slave.Training.selectedTrainer = f.master;
            f.master.workSettings.Active = false; Assert(TrainerAssignmentUtility.GetAssignedTrainerLabel(f.slave).Contains("工作或身体"));
            SSCIdentityUtility.TrySetIdentity(f.master, PawnIdentity.Unset);
            Assert(TrainerAssignmentUtility.GetAssignedTrainerLabel(f.slave).Contains("身份停用"));
        });
        Run("新绑定仪式拒绝停用指派，性奴调教员不获得主人角色", () =>
        {
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map, true);
            f.slave.Training.selectedTrainer = trainer;
            var masterRole = new RitualRole_BindingMaster(); var slaveRole = new RitualRole_BindingSlave();
            Assert(!masterRole.AppliesToPawn(trainer, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            Assert(!slaveRole.AppliesToPawn(f.slave, out _, default));
            SSCIdentityUtility.SetTrainerEnabled(trainer, false);
            Assert(!slaveRole.AppliesToPawn(f.slave, out _, default));
            Assert(!masterRole.AppliesToPawn(f.master, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
            f.slave.Training.selectedTrainer = f.master;
            Assert(masterRole.AppliesToPawn(f.master, out _, default, assignments: new RitualRoleAssignments { Slave = f.slave }));
        });
        Run("旧档缺字段区别于显式关闭，新组件默认不迁移", () =>
        {
            Pawn old = Pawn(PawnIdentity.Slave); Scribe_Values.Data.Clear(); Scribe_Values.Loading = true;
            old.Training.ExposeTrainerIdentity(); Assert(!old.Training.trainerIdentityInitialized && !old.Training.slaveTrainerEnabled);
            var fresh = Pawn(PawnIdentity.Slave); Assert(fresh.Training.trainerIdentityInitialized && !SSCIdentityUtility.IsTrainer(fresh));
        });
        Run("旧档迁移只保留已指定性奴资格，未选择不提权", () =>
        {
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave), unset = Pawn(PawnIdentity.Unset), unused = Pawn(PawnIdentity.Slave);
            foreach (Pawn p in new[] { trainer, unset, unused }) p.Training.trainerIdentityInitialized = false;
            f.slave.Training.selectedTrainer = trainer; f.master.Training.selectedTrainer = unset;
            SSCTrainerIdentityMigration.Migrate(new[] { f.slave, f.master, unused });
            Assert(SSCIdentityUtility.IsTrainer(trainer) && trainer.Training.trainerIdentityInitialized);
            Assert(!SSCIdentityUtility.IsTrainer(unset) && unset.Training.pawnIdentity == PawnIdentity.Unset);
            Assert(!SSCIdentityUtility.IsTrainer(unused));
        });
        Run("地图外指派通过游戏初始化迁移，重载不重新打开", () =>
        {
            var f = Setup(); Pawn world = Pawn(PawnIdentity.Slave); world.Map = null;
            world.Training.trainerIdentityInitialized = false; f.slave.Training.selectedTrainer = world;
            PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Clear(); PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Add(f.slave);
            var migration = new SSCTrainerIdentityMigration(new Game()); migration.FinalizeInit(); Assert(SSCIdentityUtility.IsTrainer(world));
            SSCIdentityUtility.SetTrainerEnabled(world, false);
            Scribe_Values.Loading = false; Scribe_Values.Data.Clear(); world.Training.ExposeTrainerIdentity();
            world.Training.slaveTrainerEnabled = true; Scribe_Values.Loading = true; world.Training.ExposeTrainerIdentity();
            migration.FinalizeInit(); Assert(!SSCIdentityUtility.IsTrainer(world) && world.Training.trainerIdentityInitialized);
        });
        Run("迁移不将新生成的被指派性奴或死者自动开启", () =>
        {
            var f = Setup(); Pawn fresh = Pawn(PawnIdentity.Slave), dead = Pawn(PawnIdentity.Slave);
            dead.Dead = true; dead.Training.trainerIdentityInitialized = false;
            f.slave.Training.selectedTrainer = fresh; f.master.Training.selectedTrainer = dead;
            SSCTrainerIdentityMigration.Migrate(new[] { f.slave, f.master, dead });
            Assert(!SSCIdentityUtility.IsTrainer(fresh) && !SSCIdentityUtility.IsTrainer(dead));
        });
        Run("四语新文案键及参数一致", () =>
        {
            var expected = xml.Root.Elements().Select(e => e.Name.LocalName).Order().ToArray();
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                var entries = XDocument.Load(Path.Combine(repo, "Languages", language, "Keyed/SSC_TrainerIdentity.xml")).Root.Elements().ToList();
                Assert(entries.Select(e => e.Name.LocalName).Order().SequenceEqual(expected));
                foreach (var entry in entries) Assert(!string.IsNullOrWhiteSpace(string.Format(entry.Value, "Test")));
                var identityXml = XDocument.Load(Path.Combine(repo, "Languages", language, "Keyed/SSC_Identity.xml"));
                Assert(!string.IsNullOrWhiteSpace(identityXml.Root.Element("SSC_Identity_BoundTip")?.Value));
            }
        });
        Run("绑定不再根据旧开放状态覆盖第三方或空指派", () =>
        {
            // 本套件直接调用生产绑定函数；新限制生命周期协调另由 RestrictionCore 覆盖。
            // 绑定函数不能再制造一次旧规则覆盖，强制指定主人由统一协调入口负责。
            foreach (bool legacy in new[] { false, true })
            foreach (bool assigned in new[] { false, true })
            {
                var f = Setup(); Pawn other = assigned ? Pawn(PawnIdentity.Master, f.slave.Map) : null;
                f.slave.Training.AllowsOthersForTrainingOrSex = legacy;
                f.slave.Training.restrictionConfig.rules.receiveTraining = true;
                f.slave.Training.selectedTrainer = other;
                Assert(SSCBondUtility.Bind(f.master, f.slave));
                Assert(f.slave.Training.selectedTrainer == other);
                Assert(SSCBondUtility.Bind(f.master, f.slave));
                Assert(f.slave.Training.selectedTrainer == other);
            }
        });
        RunStage3BTests();
        RunWorkScanTests();
        Console.WriteLine($"{passed}/{passed + failed} passed (production trainer identity and assignment; game interface model).");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>建立模型并加入原版候选提供器；具体成员由场景配置，不重写完整原版阵营分类。</summary>
    private static Pawn Pawn(PawnIdentity identity = PawnIdentity.Unset, Map map = null, bool enabled = false)
    {
        var pawn = new Pawn { Map = map ?? new Map() }; pawn.Training.pawnIdentity = identity; pawn.Training.slaveTrainerEnabled = enabled;
        pawn.Map.mapPawns.FreeColonistsSource.Add(pawn); pawn.Map.mapPawns.AllPawns.Add(pawn); return pawn;
    }

    /// <summary>准备可接受调教的性奴和同图主人。</summary>
    private static (Pawn slave, Pawn master) Setup()
    {
        Pawn slave = Pawn(PawnIdentity.Slave); Pawn master = Pawn(PawnIdentity.Master, slave.Map);
        slave.Training.selectedTrainer = master; return (slave, master);
    }

    /// <summary>运行独立场景并在失败后继续报告其余场景。</summary>
    private static void Run(string name, Action test)
    {
        SSCMod.settings = new Settings();
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    /// <summary>要求场景符合预期，否则报告调用位置。</summary>
    private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
}
