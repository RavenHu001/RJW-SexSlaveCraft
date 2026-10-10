using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>读取真实定义并执行生产反馈工具，覆盖资格、无经验场景、记忆刷新与社交方向。</summary>
    private static void RunTrainerOfficerFeedbackTests(string repo)
    {
        string relativeDef = "Defs/ThoughtDefs/SSC_TrainerOfficerThoughts.xml";
        var xml = XDocument.Load(Path.Combine(repo, relativeDef));
        var duty = xml.Root.Elements("ThoughtDef").Single(d => (string)d.Element("defName") == "SSC_TrainerOfficer_DutyFulfilled");
        SSCDefOf.SSC_TrainerOfficer_DutyFulfilled = new ThoughtDef
        {
            defName = "SSC_TrainerOfficer_DutyFulfilled",
            stackLimit = (int)duty.Element("stackLimit"),
            durationDays = (float)duty.Element("durationDays"),
            moodEffect = (float)duty.Element("stages").Element("li").Element("baseMoodEffect")
        };

        Run("训导官反馈真实定义及四语镜像完整，职责心情单份一天且评价为条件好感", () =>
        {
            // 本节两条之外，另有第 10 节的任职信任两条条件性想法，共 4 条。
            Assert(xml.Root.Elements("ThoughtDef").Count() == 4);
            Assert((string)duty.Element("thoughtClass") == "Thought_Memory"
                && SSCDefOf.SSC_TrainerOfficer_DutyFulfilled.stackLimit == 1
                && (int)duty.Element("stackLimitForSameOtherPawn") == 1
                && SSCDefOf.SSC_TrainerOfficer_DutyFulfilled.durationDays == 1f
                && SSCDefOf.SSC_TrainerOfficer_DutyFulfilled.moodEffect == 3f);
            var appraisal = xml.Root.Elements("ThoughtDef").Single(d => (string)d.Element("defName") == "SSC_TrainerOfficer_Appraisal");
            Assert((string)appraisal.Element("thoughtClass") == "Thought_SituationalSocial"
                && (string)appraisal.Element("workerClass") == typeof(ThoughtWorker_TrainerOfficerAppraisal).FullName
                && (int)appraisal.Element("stages").Element("li").Element("baseOpinionOffset") == 5
                && appraisal.Element("durationDays") == null && appraisal.Element("stackLimit") == null);
            Assert(XNode.DeepEquals(xml, XDocument.Load(Path.Combine(repo, "Sexslavecraft", relativeDef))));
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string relativeLanguage = $"Languages/{language}/DefInjected/ThoughtDef/SSC_TrainerOfficerThoughts.xml";
                var translated = XDocument.Load(Path.Combine(repo, relativeLanguage));
                string[] keys = { "SSC_TrainerOfficer_DutyFulfilled.stages.0.label", "SSC_TrainerOfficer_DutyFulfilled.stages.0.description",
                    "SSC_TrainerOfficer_Appraisal.stages.0.label", "SSC_TrainerOfficer_Appraisal.stages.0.description",
                    // 第 10 节新增两条也必须在该文件里有四语文案。
                    "SSC_TrainerOfficer_TrustedByOwnerSocial.stages.0.label", "SSC_TrainerOfficer_TrustedByOwnerSocial.stages.0.description",
                    "SSC_TrainerOfficer_TrustedByOwnerMood.stages.0.label", "SSC_TrainerOfficer_TrustedByOwnerMood.stages.0.description" };
                Assert(translated.Root.Elements().Select(e => e.Name.LocalName).Distinct().Count() == keys.Length
                    && keys.All(key => !string.IsNullOrWhiteSpace((string)translated.Root.Element(key))));
                Assert(XNode.DeepEquals(translated, XDocument.Load(Path.Combine(repo, "Sexslavecraft", relativeLanguage))));
            }
        });

        Run("履行职责只给开启的合格训导官，主人和未选择身份不因调教员身份获奖", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map), receiver = Pawn(PawnIdentity.Slave);
            Assert(TrainerOfficerFeedback.IsActiveOfficer(officer));
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, receiver);
            Assert(officer.needs.mood.thoughts.memories.Memories.Count == 1
                && receiver.needs.mood.thoughts.memories.Memories.Count == 0);
            officer.Training.slaveTrainerEnabled = false;
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, receiver);
            Assert(!TrainerOfficerFeedback.IsActiveOfficer(officer) && officer.needs.mood.thoughts.memories.GainCalls == 1);
            foreach (PawnIdentity identity in new[] { PawnIdentity.Master, PawnIdentity.Unset })
            {
                officer = Pawn(identity, enabled: true);
                Assert(!TrainerOfficerFeedback.IsActiveOfficer(officer));
                TrainerOfficerFeedback.NotifyTrainingCompleted(officer, receiver);
                Assert(officer.needs.mood.thoughts.memories.Memories.Count == 0);
            }
            officer = QualifiedTrainer(Pawn().Map);
            officer.Training.specializationProgress = 0.1999f;
            Assert(!TrainerOfficerFeedback.IsActiveOfficer(officer));
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, receiver);
            Assert(officer.needs.mood.thoughts.memories.Memories.Count == 0);
        });

        Run("普通培养完成和跨方向有效终极即使没有经验增量仍获得职责记忆", () =>
        {
            foreach (bool final in new[] { false, true })
            {
                Pawn officer = QualifiedTrainer(Pawn().Map), receiver = Pawn(PawnIdentity.Slave);
                officer.Training.specializationProgress = 1f;
                if (final)
                {
                    officer.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
                    officer.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
                }
                float before = officer.Training.specializationProgress;
                TrainerSpecializationProgressUtility.NotifyProvidedTrainingCompleted(officer, receiver);
                Assert(officer.Training.specializationProgress == before);
                TrainerOfficerFeedback.NotifyTrainingCompleted(officer, receiver);
                Assert(officer.needs.mood.thoughts.memories.Memories.Count == 1);
            }
        });

        Run("不同受训对象真实完成只刷新单份职责记忆，关闭和失格不删除既有记忆", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map);
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, Pawn(PawnIdentity.Slave));
            var memories = officer.needs.mood.thoughts.memories;
            var original = memories.Memories.Single(); original.age = 42000;
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, Pawn(PawnIdentity.Slave));
            Assert(memories.Memories.Count == 1 && ReferenceEquals(original, memories.Memories.Single())
                && original.age == 0 && original.otherPawn == null && memories.GainCalls == 2);
            original.age = 120;
            officer.Training.slaveTrainerEnabled = false;
            SSCBondUtility.GetChain(officer).Severity = 0.3f;
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, Pawn(PawnIdentity.Slave));
            Assert(memories.Memories.Count == 1 && original.age == 120 && memories.GainCalls == 2);
            SSCBondUtility.GetChain(officer).Severity = 0.5f;
            SSCIdentityUtility.SetTrainerEnabled(officer, true);
            Assert(TrainerOfficerFeedback.IsActiveOfficer(officer) && original.age == 120 && memories.GainCalls == 2);
        });

        Run("职责反馈拒绝自训、无效对象和无心情需求，资格查询不写入状态", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map);
            foreach (Pawn invalid in new[] { null, officer, Pawn(PawnIdentity.Master), Pawn(PawnIdentity.Unset),
                new Pawn { Dead = true }, new Pawn { Destroyed = true }, new Pawn { Discarded = true } })
            {
                if (invalid != null && invalid != officer && (invalid.Dead || invalid.Destroyed || invalid.Discarded))
                    invalid.Training.pawnIdentity = PawnIdentity.Slave;
                TrainerOfficerFeedback.NotifyTrainingCompleted(officer, invalid);
            }
            Assert(officer.needs.mood.thoughts.memories.Memories.Count == 0);
            foreach (Action<Pawn> invalidate in new Action<Pawn>[] { p => p.Dead = true, p => p.Destroyed = true, p => p.Discarded = true, p => p.Training = null })
            {
                Pawn invalid = QualifiedTrainer(Pawn().Map); invalidate(invalid);
                Assert(!TrainerOfficerFeedback.IsActiveOfficer(invalid));
                TrainerOfficerFeedback.NotifyTrainingCompleted(invalid, Pawn(PawnIdentity.Slave));
                Assert(invalid.needs.mood.thoughts.memories.Memories.Count == 0);
            }
            int writes = SSCBondUtility.GetChain(officer).SeverityWrites, states = officer.health.hediffSet.hediffs.Count;
            TrainerOfficerFeedback.IsActiveOfficer(officer);
            Assert(SSCBondUtility.GetChain(officer).SeverityWrites == writes && officer.health.hediffSet.hediffs.Count == states);
            officer.needs.mood = null;
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, Pawn(PawnIdentity.Slave));
            officer.needs = null;
            TrainerOfficerFeedback.NotifyTrainingCompleted(officer, Pawn(PawnIdentity.Slave));
        });

        Run("主人评价仅由实际绑定主人产生，反向关系和跨主人指派均不挪用归属", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map), owner = SSCBondUtility.GetBoundMaster(officer);
            Pawn otherOwner = Pawn(PawnIdentity.Master), receiver = Pawn(PawnIdentity.Slave);
            Assert(SSCBondUtility.Bind(otherOwner, receiver));
            receiver.Training.selectedTrainer = officer;
            var worker = new ThoughtWorker_TrainerOfficerAppraisal();
            Assert(TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer)
                && worker.EvaluateForTest(owner, officer).Active && worker.EvaluateForTest(owner, officer).StageIndex == 0);
            Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(otherOwner, officer)
                && !worker.EvaluateForTest(otherOwner, officer).Active
                && !worker.EvaluateForTest(officer, owner).Active);
            Assert(officer.needs.mood.thoughts.memories.GainCalls == 0
                && owner.needs.mood.thoughts.memories.GainCalls == 0);
        });

        Run("倒地离图和临时工作关闭保留主人评价，个人关闭立即撤销", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map), owner = SSCBondUtility.GetBoundMaster(officer);
            officer.Downed = owner.Downed = true; officer.Map = owner.Map = null;
            officer.Spawned = owner.Spawned = false;
            officer.workSettings.Active = owner.workSettings.Active = false;
            Assert(TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
            officer.Training.slaveTrainerEnabled = false;
            Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
            officer.Training.slaveTrainerEnabled = true;
            Assert(TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
        });

        Run("普通切向退阶解绑及终极禁用即时撤销评价，有效终极跨方向保留", () =>
        {
            foreach (Action<Pawn> revoke in new Action<Pawn>[]
            {
                p => p.Training.SetSpecialization(SexSlaveSpecializationType.Cow),
                p => SSCBondUtility.GetChain(p).Severity = 0.3f,
                p => SSCBondUtility.Unbind(p),
                p => p.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled)
            })
            {
                Pawn officer = QualifiedTrainer(Pawn().Map), owner = SSCBondUtility.GetBoundMaster(officer);
                Assert(TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
                revoke(officer);
                Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
            }
            Pawn finalOfficer = QualifiedTrainer(Pawn().Map), finalOwner = SSCBondUtility.GetBoundMaster(finalOfficer);
            finalOfficer.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_Final);
            finalOfficer.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            Assert(TrainerOfficerFeedback.HasOwnerAppraisal(finalOwner, finalOfficer));
            finalOfficer.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled);
            Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(finalOwner, finalOfficer));
        });

        Run("主人评价拒绝已死亡销毁丢弃或不再为SSC主人的评价者", () =>
        {
            Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(null, null));
            foreach (Action<Pawn> invalidate in new Action<Pawn>[]
            {
                p => p.Dead = true, p => p.Destroyed = true, p => p.Discarded = true,
                p => p.Training.pawnIdentity = PawnIdentity.Slave, p => p.Training.pawnIdentity = PawnIdentity.Unset,
                p => p.Training = null
            })
            {
                Pawn officer = QualifiedTrainer(Pawn().Map), owner = SSCBondUtility.GetBoundMaster(officer);
                invalidate(owner);
                Assert(!TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer));
            }
        });
    }
}
