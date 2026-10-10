using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>
    /// 第 10 节“受到主人信任”的回归：训导官对实际绑定主人的条件性好感与心情。
    /// 两者都不是记忆，用例同时确认它们不写入记忆、不产生历史实例。
    /// </summary>
    private static void RunTrainerOfficerTrustTests(string repo)
    {
        string relativeDef = "Defs/ThoughtDefs/SSC_TrainerOfficerThoughts.xml";
        var xml = XDocument.Load(Path.Combine(repo, relativeDef));

        Run("任职信任定义与四语文案完整：条件性心情显式声明 Thought_Situational", () =>
        {
            var social = xml.Root.Elements("ThoughtDef").Single(d => (string)d.Element("defName") == "SSC_TrainerOfficer_TrustedByOwnerSocial");
            var mood = xml.Root.Elements("ThoughtDef").Single(d => (string)d.Element("defName") == "SSC_TrainerOfficer_TrustedByOwnerMood");

            // 社交条按社交形态解析，固定 +5 好感。
            Assert((string)social.Element("thoughtClass") == "Thought_SituationalSocial"
                && (string)social.Element("workerClass") == typeof(ThoughtWorker_TrainerOfficerTrustedByOwner).FullName
                && (int)social.Element("stages").Element("li").Element("baseOpinionOffset") == 5);

            // 心情条必须显式声明非社交形态，否则带 workerClass 时可能被解析成社交想法而不进心情栏。
            Assert((string)mood.Element("thoughtClass") == "Thought_Situational"
                && (string)mood.Element("workerClass") == typeof(ThoughtWorker_TrainerOfficerTrustedByOwnerMood).FullName
                && (float)mood.Element("stages").Element("li").Element("baseMoodEffect") == 3f);

            // 条件性想法不是记忆：不设时长与容量。
            foreach (var def in new[] { social, mood })
            {
                Assert(def.Element("durationDays") == null && def.Element("stackLimit") == null
                    && def.Element("stackLimitForSameOtherPawn") == null);
            }

            // 源码目录镜像与四语文案键集一致。
            Assert(XNode.DeepEquals(xml, XDocument.Load(Path.Combine(repo, "Sexslavecraft", relativeDef))));
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string relativeLanguage = $"Languages/{language}/DefInjected/ThoughtDef/SSC_TrainerOfficerThoughts.xml";
                var translated = XDocument.Load(Path.Combine(repo, relativeLanguage));
                string[] keys =
                {
                    "SSC_TrainerOfficer_DutyFulfilled.stages.0.label",
                    "SSC_TrainerOfficer_DutyFulfilled.stages.0.description",
                    "SSC_TrainerOfficer_Appraisal.stages.0.label",
                    "SSC_TrainerOfficer_Appraisal.stages.0.description",
                    "SSC_TrainerOfficer_TrustedByOwnerSocial.stages.0.label",
                    "SSC_TrainerOfficer_TrustedByOwnerSocial.stages.0.description",
                    "SSC_TrainerOfficer_TrustedByOwnerMood.stages.0.label",
                    "SSC_TrainerOfficer_TrustedByOwnerMood.stages.0.description"
                };
                Assert(translated.Root.Elements().Select(e => e.Name.LocalName).Distinct().Count() == keys.Length
                    && keys.All(key => !string.IsNullOrWhiteSpace((string)translated.Root.Element(key))));
                Assert(XNode.DeepEquals(translated, XDocument.Load(Path.Combine(repo, "Sexslavecraft", relativeLanguage))));
            }
        });

        Run("有效任职训导官对实际主人获得反向好感与心情，两条同时生效", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map);
            Pawn owner = SSCBondUtility.GetBoundMaster(officer);
            var socialWorker = new ThoughtWorker_TrainerOfficerTrustedByOwner();
            var moodWorker = new ThoughtWorker_TrainerOfficerTrustedByOwnerMood();

            Assert(TrainerOfficerFeedback.HasTrustedOwner(officer, out Pawn resolved) && ReferenceEquals(resolved, owner));
            Assert(socialWorker.EvaluateForTest(officer, owner).Active
                && socialWorker.EvaluateForTest(officer, owner).StageIndex == 0);
            Assert(moodWorker.EvaluateStateForTest(officer).Active
                && moodWorker.EvaluateStateForTest(officer).StageIndex == 0);

            // 条件性想法不进记忆列表：反复查询也不产生实例。
            Assert(officer.needs.mood.thoughts.memories.GainCalls == 0
                && officer.needs.mood.thoughts.memories.Memories.Count == 0
                && owner.needs.mood.thoughts.memories.GainCalls == 0);
        });

        Run("反向信任只指向实际主人，其他主人、受训者与指定调教员都不挪用", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map);
            Pawn owner = SSCBondUtility.GetBoundMaster(officer);
            Pawn otherOwner = Pawn(PawnIdentity.Master);
            Pawn receiver = Pawn(PawnIdentity.Slave);
            Assert(SSCBondUtility.Bind(otherOwner, receiver));
            receiver.Training.selectedTrainer = officer;

            var socialWorker = new ThoughtWorker_TrainerOfficerTrustedByOwner();
            Assert(!socialWorker.EvaluateForTest(officer, otherOwner).Active);
            Assert(!socialWorker.EvaluateForTest(officer, receiver).Active);
            Assert(!socialWorker.EvaluateForTest(officer, officer).Active);
            Assert(!socialWorker.EvaluateForTest(officer, null).Active);

            // 主人对训导官的原评价方向保持不受影响。
            Assert(TrainerOfficerFeedback.HasOwnerAppraisal(owner, officer)
                && !TrainerOfficerFeedback.HasOwnerAppraisal(otherOwner, officer));
        });

        Run("关闭任职、切向、退阶、解绑与终极禁用在两项上同步失效并恢复", () =>
        {
            var moodWorker = new ThoughtWorker_TrainerOfficerTrustedByOwnerMood();
            var socialWorker = new ThoughtWorker_TrainerOfficerTrustedByOwner();

            foreach (Action<Pawn> revoke in new Action<Pawn>[]
            {
                p => p.Training.slaveTrainerEnabled = false,
                p => p.Training.SetSpecialization(SexSlaveSpecializationType.Cow),
                p => SSCBondUtility.GetChain(p).Severity = 0.3f,
                p => SSCBondUtility.Unbind(p),
                p => p.health.AddHediff(SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled)
            })
            {
                Pawn officer = QualifiedTrainer(Pawn().Map);
                Pawn owner = SSCBondUtility.GetBoundMaster(officer);
                Assert(TrainerOfficerFeedback.HasTrustedOwner(officer, out _));
                Assert(socialWorker.EvaluateForTest(officer, owner).Active && moodWorker.EvaluateStateForTest(officer).Active);

                revoke(officer);

                Assert(!TrainerOfficerFeedback.HasTrustedOwner(officer, out Pawn gone) && gone == null);
                Assert(!socialWorker.EvaluateForTest(officer, owner).Active);
                Assert(!moodWorker.EvaluateStateForTest(officer).Active);
            }

            // 重新开启任职后条件项恢复，且不产生任何历史记忆。
            Pawn restored = QualifiedTrainer(Pawn().Map);
            Pawn restoredOwner = SSCBondUtility.GetBoundMaster(restored);
            restored.Training.slaveTrainerEnabled = false;
            Assert(!moodWorker.EvaluateStateForTest(restored).Active);
            SSCIdentityUtility.SetTrainerEnabled(restored, true);
            Assert(TrainerOfficerFeedback.HasTrustedOwner(restored, out _)
                && moodWorker.EvaluateStateForTest(restored).Active
                && socialWorker.EvaluateForTest(restored, restoredOwner).Active);
            Assert(restored.needs.mood.thoughts.memories.GainCalls == 0);
        });

        Run("主人死亡、销毁、丢弃或不再是SSC主人时信任加成立即消失", () =>
        {
            var moodWorker = new ThoughtWorker_TrainerOfficerTrustedByOwnerMood();
            foreach (Action<Pawn> invalidate in new Action<Pawn>[]
            {
                p => p.Dead = true, p => p.Destroyed = true, p => p.Discarded = true,
                p => p.Training.pawnIdentity = PawnIdentity.Slave,
                p => p.Training.pawnIdentity = PawnIdentity.Unset,
                p => p.Training = null
            })
            {
                Pawn officer = QualifiedTrainer(Pawn().Map);
                Pawn owner = SSCBondUtility.GetBoundMaster(officer);
                invalidate(owner);
                Assert(!TrainerOfficerFeedback.HasTrustedOwner(officer, out Pawn gone) && gone == null);
                Assert(!moodWorker.EvaluateStateForTest(officer).Active);
            }

            // 无主、自身与无效输入一律不产生信任加成。
            Assert(!TrainerOfficerFeedback.HasTrustedOwner(null, out _));
            Pawn unbound = Pawn(PawnIdentity.Slave);
            Assert(!TrainerOfficerFeedback.HasTrustedOwner(unbound, out _));
            Pawn selfBound = QualifiedTrainer(Pawn().Map);
            Assert(!TrainerOfficerFeedback.HasTrustedOwner(selfBound, out Pawn self) || !ReferenceEquals(self, selfBound));
            foreach (Action<Pawn> invalidate in new Action<Pawn>[]
            {
                p => p.Dead = true, p => p.Destroyed = true, p => p.Discarded = true, p => p.Training = null
            })
            {
                Pawn invalid = QualifiedTrainer(Pawn().Map);
                invalidate(invalid);
                Assert(!TrainerOfficerFeedback.HasTrustedOwner(invalid, out _));
                Assert(!moodWorker.EvaluateStateForTest(invalid).Active);
            }
        });

        Run("倒地、离图与工作暂停保留任职信任，资格查询不写入状态", () =>
        {
            Pawn officer = QualifiedTrainer(Pawn().Map);
            Pawn owner = SSCBondUtility.GetBoundMaster(officer);
            officer.Downed = owner.Downed = true;
            officer.Map = owner.Map = null;
            officer.Spawned = owner.Spawned = false;
            officer.workSettings.Active = owner.workSettings.Active = false;

            var moodWorker = new ThoughtWorker_TrainerOfficerTrustedByOwnerMood();
            Assert(TrainerOfficerFeedback.HasTrustedOwner(officer, out _) && moodWorker.EvaluateStateForTest(officer).Active);

            int writes = SSCBondUtility.GetChain(officer).SeverityWrites;
            int hediffs = officer.health.hediffSet.hediffs.Count;
            TrainerOfficerFeedback.HasTrustedOwner(officer, out _);
            moodWorker.EvaluateStateForTest(officer);
            Assert(SSCBondUtility.GetChain(officer).SeverityWrites == writes
                && officer.health.hediffSet.hediffs.Count == hediffs);
        });
    }
}
