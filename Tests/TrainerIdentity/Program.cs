using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static class Program
{
    private static int passed, failed;

    /// <summary>运行真实身份、指派、工作、仪式及迁移代码；只替代游戏外部接口。</summary>
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args.Length == 0 ? "../.." : args[0]);
        var xml = XDocument.Load(Path.Combine(repo, "Languages/ChineseSimplified/Keyed/SSC_TrainerIdentity.xml"));
        foreach (var key in xml.Root.Elements()) Verse.Extensions.Translations[key.Name.LocalName] = key.Value;
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
        Run("清空始终允许且恢复原有未指定规则", () =>
        {
            var f = Setup(); f.slave.Training.selectedTrainer = f.master; f.master.workSettings.Active = false;
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, null) && f.slave.Training.selectedTrainer == null);
            Assert(TrainerAssignmentUtility.IsAllowedTrainer(f.slave, Pawn(PawnIdentity.Master, f.slave.Map)));
        });
        Run("实际主人约束在菜单和直接指派一致，开放后允许更换", () =>
        {
            var f = Setup(); SSCBondUtility.Bind(f.master, f.slave); Pawn other = Pawn(PawnIdentity.Master, f.slave.Map);
            Assert(TrainerAssignmentUtility.GetTrainerCandidates(f.slave).Single() == f.master);
            Assert(!SSCBondUtility.TryAssignTrainer(f.slave, other));
            f.slave.Training.AllowsOthersForTrainingOrSex = true;
            Assert(SSCBondUtility.TryAssignTrainer(f.slave, other));
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
            var f = Setup(); Pawn trainer = Pawn(PawnIdentity.Slave, f.slave.Map, true);
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
                Assert(TrainerAssignmentUtility.IsAllowedTrainer(f.slave, f.master));
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
            Assert(!masterRole.AppliesToPawn(trainer, out _, default));
            Assert(slaveRole.AppliesToPawn(f.slave, out _, default));
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
            }
        });
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
        Pawn slave = Pawn(PawnIdentity.Slave); return (slave, Pawn(PawnIdentity.Master, slave.Map));
    }

    /// <summary>运行独立场景并在失败后继续报告其余场景。</summary>
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }

    /// <summary>要求场景符合预期，否则报告调用位置。</summary>
    private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
}
