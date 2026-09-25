using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>以生产资格、解析器和统一行为许可验证阶段 4 的两条独立生效门槛。</summary>
    private static void RunStage4Tests(string repo)
    {
        Run("训导官定义只强制允许两项主动行为，不写入个人默认", () =>
        {
            // 检查实际随 Mod 加载的 XML，而不只构造测试 Profile；
            // 目标接收项和调教项必须维持未声明状态。
            var xml = XDocument.Load(Path.Combine(repo, "Defs/SSCRestrictionProfiles.xml"));
            var profile = xml.Root.Elements().Single(element =>
                (string)element.Element("defName") == "SSC_Restriction_TrainerOfficer");
            Assert((string)profile.Element("specialization") == "TrainerOfficer");
            Assert(profile.Element("defaults") == null);
            var forced = profile.Element("forced").Elements().ToArray();
            Assert(forced.Length == 2 &&
                (string)profile.Element("forced").Element("consensualInitiation") == "Allow" &&
                (string)profile.Element("forced").Element("forcedInitiation") == "Allow");
        });

        Run("当前选择训导官从零进度获得主动行为强制效果但不能任职", () =>
        {
            Pawn actor = QualifiedTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            actor.Training.specializationProgress = 0f;
            var profile = TrainerProfile();
            var saved = actor.Training.restrictionConfig.rules;
            DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Add(profile);
            try
            {
                // 同一个角色在 20% 之前的两项结果必须不同：
                // 行为强制已生效，调教员资格及手动开启仍被拒绝。
                Assert(TrainerSpecializationUtility.HasActiveRestrictionEffect(actor));
                Assert(SSCRestrictionResolver.HasProfile(actor, profile));
                Assert(!SSCIdentityUtility.IsTrainer(actor));
                Assert(!SSCIdentityUtility.SetTrainerEnabled(actor, true));
                Assert(SSCRestrictionResolver.Resolve(actor, saved,
                    SSCRestrictionRule.ConsensualInitiation).Value == SSCRestrictionValue.Allow);
                Assert(SSCRestrictionResolver.Resolve(actor, saved,
                    SSCRestrictionRule.ForcedInitiation).Value == SSCRestrictionValue.Allow);
                Assert(saved.consensualInitiation == SSCRestrictionValue.OwnerOnly && !saved.allowForcedInitiation);

                // 强制允许只属于发起者。目标若拒绝接收，统一策略仍拒绝双人行为。
                Pawn target = QualifiedTrainerPawn();
                Assert(!SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, target,
                    SSCInteractionKind.Consensual, true)).Allowed);
                target.Training.restrictionConfig.rules.receiveConsensual = true;
                Assert(SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, target,
                    SSCInteractionKind.Consensual, true)).Allowed);
            }
            finally { DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Clear(); }
        });

        Run("强制效果与任职开关独立，装备和总开关沿用既有优先级", () =>
        {
            Pawn actor = QualifiedTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            actor.Training.specializationProgress = 0.2f;
            var profile = TrainerProfile();
            DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Add(profile);
            try
            {
                // 开关关闭时仍取得特化效果；开启任职不改写个人行为保存值。
                Assert(!actor.Training.slaveTrainerEnabled);
                Assert(SSCRestrictionResolver.Resolve(actor, actor.Training.restrictionConfig.rules,
                    SSCRestrictionRule.ForcedInitiation).Source == SSCRestrictionSource.Specialization);
                Assert(SSCIdentityUtility.SetTrainerEnabled(actor, true));
                Assert(SSCIdentityUtility.IsTrainer(actor));
                Assert(SSCIdentityUtility.SetTrainerEnabled(actor, false));
                Assert(TrainerSpecializationUtility.HasActiveRestrictionEffect(actor));

                // 装备层优先于特化层；关闭特化层后恢复本人保存的拒绝值。
                var gear = new Apparel();
                gear.def.modExtensions.Add(new SSCRestrictionEquipmentExtension
                    { forced = new SSCRestrictionOverrides { forcedInitiation = SSCRestrictionValue.Deny } });
                actor.apparel.WornApparel.Add(gear);
                var equipped = SSCRestrictionResolver.Resolve(actor, actor.Training.restrictionConfig.rules,
                    SSCRestrictionRule.ForcedInitiation);
                Assert(equipped.Source == SSCRestrictionSource.Equipment && equipped.Value == SSCRestrictionValue.Deny);
                actor.apparel.WornApparel.Clear();
                SSCMod.settings.enableSpecializationRestrictionOverrides = false;
                var personal = SSCRestrictionResolver.Resolve(actor, actor.Training.restrictionConfig.rules,
                    SSCRestrictionRule.ForcedInitiation);
                Assert(personal.Source == SSCRestrictionSource.Saved && personal.Value == SSCRestrictionValue.Deny);
                SSCMod.settings.enableSexSlaveProtectionRules = false;
                Assert(SSCRestrictionResolver.Resolve(actor, actor.Training.restrictionConfig.rules,
                    SSCRestrictionRule.ForcedInitiation).Source == SSCRestrictionSource.SystemDisabled);
            }
            finally { DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Clear(); }
        });

        Run("有效终极跨方向强制生效，禁用和失格立即停止且查询只读", () =>
        {
            Pawn actor = QualifiedTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            actor.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            var profile = TrainerProfile();
            Assert(!SSCRestrictionResolver.HasProfile(actor, profile));

            // 终极标记与当前方向独立。禁用标记即使与有效标记同时残留
            // 也优先阻止效果，查询不能清理这份待维护的存档状态。
            actor.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            Assert(SSCRestrictionResolver.HasProfile(actor, profile));
            actor.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            int before = actor.health.hediffSet.hediffs.Count;
            Assert(!SSCRestrictionResolver.HasProfile(actor, profile));
            Assert(actor.health.hediffSet.hediffs.Count == before);
            actor.health.RemoveHediff(actor.health.hediffSet.GetFirstHediffOfDef(
                SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled));
            Assert(SSCRestrictionResolver.HasProfile(actor, profile));

            // 低频维护尚未执行时，当前锁链退阶就应立即失效。
            SSCBondUtility.GetChain(actor).Severity = 0.3f;
            Assert(!SSCRestrictionResolver.HasProfile(actor, profile));
            Assert(actor.health.hediffSet.HasHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final));
        });

        Run("当前方向失格和历史进度均不提供强制效果", () =>
        {
            Pawn actor = QualifiedTrainerPawn();
            actor.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            var profile = TrainerProfile();
            Assert(SSCRestrictionResolver.HasProfile(actor, profile));
            SSCBondUtility.GetChain(actor).Severity = 0.3f;
            Assert(!SSCRestrictionResolver.HasProfile(actor, profile));
            SSCBondUtility.GetChain(actor).Severity = 0.5f;
            actor.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            Assert(!SSCRestrictionResolver.HasProfile(actor, profile));
        });

        Run("已开启的任职选择在失格时暂停并可关闭，恢复资格后不自动改写选择", () =>
        {
            Pawn trainer = QualifiedTrainerPawn();
            trainer.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            trainer.Training.specializationProgress = TrainerSpecializationUtility.BasicQualificationProgress;
            Assert(SSCIdentityUtility.SetTrainerEnabled(trainer, true) && SSCIdentityUtility.IsTrainer(trainer));

            // 模拟低频维护尚未处理的退阶：有效状态应立即暂停，保存选择仍在。
            SSCBondUtility.GetChain(trainer).Severity = 0.3f;
            Assert(!SSCIdentityUtility.IsTrainer(trainer) && trainer.Training.slaveTrainerEnabled);
            Assert(!SSCIdentityUtility.SetTrainerEnabled(trainer, true));
            Assert(SSCIdentityUtility.SetTrainerEnabled(trainer, false));
            SSCBondUtility.GetChain(trainer).Severity = 0.5f;
            Assert(!SSCIdentityUtility.IsTrainer(trainer) && !trainer.Training.slaveTrainerEnabled);
            Assert(SSCIdentityUtility.SetTrainerEnabled(trainer, true) && SSCIdentityUtility.IsTrainer(trainer));
        });
    }

    /// <summary>使用与实际 XML 同形的稀疏强制定义，测试生产解析器与覆盖优先级。</summary>
    private static SSCRestrictionProfileDef TrainerProfile()
    {
        return new SSCRestrictionProfileDef
        {
            defName = "SSC_Restriction_TrainerOfficer",
            specialization = SexSlaveSpecializationType.TrainerOfficer,
            forced = new SSCRestrictionOverrides
            {
                consensualInitiation = SSCRestrictionValue.Allow,
                forcedInitiation = SSCRestrictionValue.Allow
            }
        };
    }
}
