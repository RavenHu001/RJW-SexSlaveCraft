using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI.Group;

internal static partial class Program
{
    private static void RunMasterQualificationTests(string repo)
    {
        Run("主人手动赋予默认关闭，明确开启后不写永久资格且关闭不撤销已有身份", () =>
        {
            SSCMod.settings = new Settings();
            Pawn pawn = Pawn();
            Assert(!new SSCSettings().allowDirectMasterSelection && !SSCMod.settings.allowDirectMasterSelection);
            Assert(!SSCMasterQualificationUtility.CanSelectMaster(pawn));
            Assert(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            SSCMod.settings.allowDirectMasterSelection = true;
            Assert(SSCMasterQualificationUtility.CanSelectMaster(pawn));
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Assert(!SSCMasterQualificationUtility.HasQualification(pawn));
            SSCMod.settings.allowDirectMasterSelection = false;
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Assert(pawn.Training.pawnIdentity == PawnIdentity.Master && !pawn.Training.masterQualificationUnlocked);
            Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            Assert(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
        });
        Run("关闭手动赋予后无资格选择被拒，受训及仪式配置保持", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Slave);
            pawn.Training.mode = TrainingMode.Enabled;
            pawn.Training.selectedTrainer = Pawn(PawnIdentity.Master);
            pawn.Training.isBeingTrained = true;
            SSCMod.settings.allowDirectMasterSelection = false;
            Assert(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Assert(pawn.Training.pawnIdentity == PawnIdentity.Slave && pawn.Training.IsEnabled &&
                pawn.Training.selectedTrainer != null && pawn.Training.isBeingTrained);
        });
        Run("已解锁人格跨身份保留资格，性奴角色也可按正常锁定规则重选主人", () =>
        {
            Pawn pawn = Pawn(); pawn.Training.ReplaceMasterQualification(true);
            SSCMod.settings.allowDirectMasterSelection = false;
            foreach (PawnIdentity identity in new[] { PawnIdentity.Master, PawnIdentity.Unset, PawnIdentity.Slave, PawnIdentity.Master })
            {
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, identity));
                Assert(SSCMasterQualificationUtility.HasQualification(pawn));
            }
        });
        Run("主人资格不能绕过绑定锁定，拒绝切换也不清资格", () =>
        {
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            f.slave.Training.ReplaceMasterQualification(true);
            SSCMod.settings.allowDirectMasterSelection = false;
            Assert(!SSCIdentityUtility.TrySetIdentity(f.slave, PawnIdentity.Master));
            Assert(f.slave.Training.masterQualificationUnlocked && SSCBondUtility.IsBoundTo(f.slave, f.master));
        });
        Run("取消仪式回调撤销设置或资格时，最终身份写入重新拒绝", () =>
        {
            foreach (bool byQualification in new[] { false, true })
            {
                Pawn pawn = Pawn(PawnIdentity.Slave);
                pawn.Training.ReplaceMasterQualification(byQualification);
                SSCMod.settings.allowDirectMasterSelection = !byQualification;
                pawn.lord = new Lord { LordJob = new LordJob_Ritual { Slave = pawn } };
                BindingRitualStateUtility.OnRejection = () =>
                {
                    SSCMod.settings.allowDirectMasterSelection = false;
                    pawn.Training.ReplaceMasterQualification(false);
                };
                try { Assert(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master)); }
                finally { BindingRitualStateUtility.OnRejection = null; }
                Assert(pawn.Training.pawnIdentity == PawnIdentity.Slave && !pawn.Training.masterQualificationUnlocked);
            }
        });
        Run("人格恢复可以还原 Master 与明确未解锁组合，整体覆盖宿主资格", () =>
        {
            SSCMod.settings.allowDirectMasterSelection = false;
            foreach (bool saved in new[] { false, true })
            {
                Pawn pawn = Pawn(); pawn.Training.ReplaceMasterQualification(!saved);
                Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, saved));
                Assert(pawn.Training.pawnIdentity == PawnIdentity.Master && pawn.Training.masterQualificationUnlocked == saved);
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
                Assert(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master) == saved);
            }
        });
        Run("人格恢复相同身份只覆盖资格，实际身份切换仍受绑定锁定", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.ReplaceMasterQualification(true);
            Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, false));
            Assert(!pawn.Training.masterQualificationUnlocked);
            var f = Setup(); Assert(SSCBondUtility.Bind(f.master, f.slave));
            f.master.Training.ReplaceMasterQualification(true);
            Assert(!SSCIdentityUtility.TryRestorePersonalityIdentity(f.master, PawnIdentity.Unset, false));
            Assert(f.master.Training.masterQualificationUnlocked && SSCBondUtility.IsBoundTo(f.slave, f.master));
            Assert(SSCIdentityUtility.TryRestorePersonalityIdentity(f.master, PawnIdentity.Master, false));
            Assert(!f.master.Training.masterQualificationUnlocked && SSCBondUtility.IsBoundTo(f.slave, f.master));
        });
        Run("人格恢复取消回调换入新资格时不覆盖新记录", () =>
        {
            Pawn pawn = Pawn(PawnIdentity.Slave);
            pawn.lord = new Lord { LordJob = new LordJob_Ritual { Slave = pawn } };
            BindingRitualStateUtility.OnRejection = () => pawn.Training.ReplaceMasterQualification(true);
            try { Assert(!SSCIdentityUtility.TryRestorePersonalityIdentity(pawn, PawnIdentity.Master, false)); }
            finally { BindingRitualStateUtility.OnRejection = null; }
            Assert(pawn.Training.pawnIdentity == PawnIdentity.Slave && pawn.Training.masterQualificationUnlocked);
        });
        Run("旧身份仅首次迁移，升级后的主动清空或新主人不会被补资格", () =>
        {
            foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
            {
                Pawn pawn = Pawn(identity); pawn.Training.masterQualificationVersion = 0;
                SSCMasterQualificationMigration.MigratePawn(pawn);
                Assert(pawn.Training.masterQualificationUnlocked == (identity == PawnIdentity.Master));
                pawn.Training.ReplaceMasterQualification(false);
                pawn.Training.pawnIdentity = PawnIdentity.Master;
                SSCMasterQualificationMigration.MigratePawn(pawn);
                Assert(!pawn.Training.masterQualificationUnlocked && pawn.Training.masterQualificationVersion == 1);
            }
        });
        Run("旧主人退出前保留历史资格，而克隆和空壳不因旧身份获资格", () =>
        {
            Pawn old = Pawn(PawnIdentity.Master); old.Training.masterQualificationVersion = 0;
            Assert(SSCIdentityUtility.TrySetIdentity(old, PawnIdentity.Unset) && old.Training.masterQualificationUnlocked);
            foreach (bool clone in new[] { false, true })
            {
                Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterQualificationVersion = 0;
                pawn.IsClone = clone;
                if (!clone) pawn.health.AddHediff((HediffDef)SSCDefOf.SSC_PersonalityExcreted_Done);
                SSCMasterQualificationMigration.MigratePawn(pawn);
                Assert(!pawn.Training.masterQualificationUnlocked && pawn.Training.masterQualificationVersion == 1);
            }
        });
        Run("新克隆显式清资格并写当前版本，不因复制当前主人身份而读档补发", () =>
        {
            Pawn source = Pawn(PawnIdentity.Master); source.Training.ReplaceMasterQualification(true);
            Pawn clone = Pawn(PawnIdentity.Master); clone.Training.ReplaceMasterQualification(true);
            clone.Training.masterQualificationVersion = 0;
            SSCMasterQualificationUtility.InitializeNewClone(clone);
            SSCMasterQualificationMigration.MigratePawn(clone);
            Assert(!clone.Training.masterQualificationUnlocked && clone.Training.masterQualificationVersion == 1);
            Assert(source.Training.masterQualificationUnlocked && clone.Training.pawnIdentity == PawnIdentity.Master);
        });
        Run("初始化组件迁移完整游戏快照，重复调用保持新版未解锁", () =>
        {
            var previous = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;
            Pawn first = Pawn(PawnIdentity.Master), dead = Pawn(PawnIdentity.Master);
            first.Training.masterQualificationVersion = dead.Training.masterQualificationVersion = 0;
            dead.Dead = true;
            try
            {
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead = new List<Pawn> { first, dead, null, first };
                var game = new SSCMasterQualificationGameComponent(new Game()); game.FinalizeInit();
                Assert(first.Training.masterQualificationUnlocked && dead.Training.masterQualificationUnlocked);
                first.Training.ReplaceMasterQualification(false); game.FinalizeInit();
                Assert(!first.Training.masterQualificationUnlocked);
            }
            finally { PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead = previous; }
        });
        Run("个人资格字段保存明确未解锁版本，缺失旧字段读零后一次迁移", () =>
        {
            try
            {
                var comp = new CompSexSlaveTraining(); Scribe_Values.Data.Clear(); Scribe_Values.Loading = false;
                comp.ExposeMasterQualification();
                Assert(!(bool)Scribe_Values.Data["masterQualificationUnlocked"] && (int)Scribe_Values.Data["masterQualificationVersion"] == 1);
                comp.ReplaceMasterQualification(true); Scribe_Values.Loading = true; comp.ExposeMasterQualification();
                Assert(!comp.masterQualificationUnlocked && comp.masterQualificationVersion == 1);
                Scribe_Values.Data.Clear(); comp.ExposeMasterQualification();
                Assert(!comp.masterQualificationUnlocked && comp.masterQualificationVersion == 0);
            }
            finally { Scribe_Values.Loading = false; Scribe_Values.Data.Clear(); }
        });
        Run("手动赋予设置保留明确开关值，旧设置缺字段默认关闭", () =>
        {
            try
            {
                foreach (bool enabled in new[] { false, true })
                {
                    var settings = new SSCSettings { allowDirectMasterSelection = enabled };
                    Scribe_Values.Data.Clear(); Scribe_Values.Loading = false; settings.ExposeMasterQualificationSettings();
                    settings.allowDirectMasterSelection = !enabled;
                    Scribe_Values.Loading = true; settings.ExposeMasterQualificationSettings();
                    Assert(settings.allowDirectMasterSelection == enabled);
                    Scribe_Values.Data.Clear(); settings.allowDirectMasterSelection = true;
                    settings.ExposeMasterQualificationSettings(); Assert(!settings.allowDirectMasterSelection);
                }
            }
            finally { Scribe_Values.Loading = false; Scribe_Values.Data.Clear(); }
        });
        Run("缺少设置时仍要求资格，不根据当前身份推导或迁移", () =>
        {
            Assert(!SSCMasterQualificationUtility.HasQualification(null) && !SSCMasterQualificationUtility.CanSelectMaster(null));
            SSCMasterQualificationMigration.Migrate(null); SSCMasterQualificationUtility.InitializeNewClone(null);
            Pawn pawn = Pawn(PawnIdentity.Master); pawn.Training.masterQualificationVersion = 0;
            SSCMod.settings.allowDirectMasterSelection = false;
            for (int i = 0; i < 3; i++) Assert(!SSCMasterQualificationUtility.HasQualification(pawn) && !SSCMasterQualificationUtility.CanSelectMaster(pawn));
            Assert(pawn.Training.masterQualificationVersion == 0);
            var previousSettings = SSCMod.settings;
            try
            {
                SSCMod.settings = null;
                Assert(!SSCMasterQualificationUtility.CanSelectMaster(pawn));
                pawn.Training.ReplaceMasterQualification(true);
                Assert(SSCMasterQualificationUtility.CanSelectMaster(pawn));
            }
            finally { SSCMod.settings = previousSettings; }
        });
        Run("主人准入和设置提示四语完整，根目录与源码镜像一致", () =>
        {
            string[] keys = { "SSC_Identity_MasterQualificationRequired", "SSC_Identity_SelectionUnavailable",
                "SSC_Setting_AllowDirectMasterSelection", "SSC_Setting_AllowDirectMasterSelection_Desc" };
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string relative = $"Languages/{language}/Keyed/SSC_Identity.xml";
                var xml = XDocument.Load(SscTestPaths.Combine(repo, relative));
                foreach (string key in keys) Assert(!string.IsNullOrWhiteSpace(xml.Root.Element(key)?.Value));
                Assert(File.ReadAllText(SscTestPaths.Combine(repo, relative)) == File.ReadAllText(SscTestPaths.Combine(repo, "Sexslavecraft", relative)));
            }
        });
    }
}
