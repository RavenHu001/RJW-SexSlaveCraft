using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

// EN: This file is the ritual outcome worker for the Binding Ritual.
// EN: It calculates ritual quality, hands out vanilla ritual memories, then runs SSC's own binding aftermath.
// CN: 这个文件是“绑定仪式”的结果处理器。
// CN: 它先计算仪式质量、派发原版仪式记忆，再执行 SSC 自己的绑定后果。
namespace SexSlaveCraft
{
        // EN: SSC bypasses the vanilla `_FromQuality` worker here so the Binding Ritual can own its full quality model.
        // CN: SSC 在这里绕过原版 `_FromQuality` worker，让“绑定仪式”可以完整接管自己的质量模型。
        public class RitualOutcomeEffectWorker_SSCBinding : RitualOutcomeEffectWorker
        {
        // EN: Re-declare the small set of fields / properties the base ritual pipeline still expects from an outcome worker.
        // CN: 这里重新补回原版仪式流水线仍然会用到的一小部分字段 / 属性。
        public static FloatRange ProgressToQualityMapping = new FloatRange(0.25f, 1f);
        public override bool SupportsAttachableOutcomeEffect => def.allowAttachableOutcome;
        // Def 声明该仪式支持文化收益，玩家偏好决定是否启用；初始化期间缺设置时保持关闭。
        public virtual bool GivesDevelopmentPoints => def.givesDevelopmentPoints
            && SSCMod.settings?.enableRitualDevelopmentPoints == true;

        public RitualOutcomeEffectWorker_SSCBinding() { }
        public RitualOutcomeEffectWorker_SSCBinding(RitualOutcomeEffectDef def) : base(def) { }

        // EN: The Binding Ritual does not use the vanilla expectation offset system, so this path is intentionally killed here.
        // CN: 绑定仪式不用原版“期望偏移”系统，所以这里故意把这条路径直接掐掉。
        public static Tuple<ExpectationDef, float> GetExpectationsOffset(Map map, PreceptDef ritual)
        {
            return null;
        }

        // EN: This is the Binding Ritual quality core: only SSC comps count, vanilla expectation is ignored, and SSC's 3-day repeat penalty is applied here.
        // CN: 这里是绑定仪式的算分核心：只遍历 SSC 自己的 Comp，不吃原版期望，并在这里结算 SSC 的 3 天重复惩罚。
        protected float GetQuality(LordJob_Ritual jobRitual, float progress)
        {
            float num = def.startingQuality;
            foreach (RitualOutcomeComp comp in def.comps)
            {
                if (comp is RitualOutcomeComp_Quality && comp.Applies(jobRitual))
                {
                    num += comp.QualityOffset(jobRitual, DataForComp(comp));
                }
            }

            // EN: SSC uses its own 3-day repeat penalty for the Binding Ritual instead of the vanilla ritual expectation penalty.
            // CN: SSC 对绑定仪式使用自己定义的 3 天重复惩罚，而不是原版那套期望惩罚。
            if (jobRitual.repeatPenalty && jobRitual.Ritual != null && jobRitual.Ritual.lastFinishedTick != -1)
            {
                if ((Find.TickManager.TicksGame - jobRitual.Ritual.lastFinishedTick) < 180000)
                {
                    num -= 0.2f; // 扣除20%
                }
            }

            // 限制分数上限
            return Mathf.Clamp(num, def.minQuality, def.maxQuality);
        }

        // EN: After quality is fixed, choose the Binding Ritual outcome from the SSC-weighted outcome table.
        // CN: 当质量确定后，就从 SSC 加权过的结果表里抽出这次绑定仪式结局。
        public virtual RitualOutcomePossibility GetOutcome(float quality, LordJob_Ritual ritual)
        {
            return def.outcomeChances.RandomElementByWeight((RitualOutcomePossibility c) =>
            {
                if (!c.Positive) return c.chance;
                return Mathf.Max(c.chance * quality, 0f);
            });
        }

        // EN: Build the ritual letter's quality breakdown without showing vanilla expectation text.
        // CN: 这里负责拼装仪式信件里的“质量明细”，并去掉原版那段期望文本。
        public virtual string OutcomeQualityBreakdownDesc(float quality, float progress, LordJob_Ritual jobRitual)
        {
            TaggedString taggedString = "RitualOutcomeQualitySpecific".Translate(jobRitual.Ritual.Label, quality.ToStringPercent()).CapitalizeFirst() + ":\n";
            if (def.startingQuality > 0f)
            {
                taggedString += "\n  - " + "StartingRitualQuality".Translate(def.startingQuality.ToStringPercent()) + ".";
            }
            foreach (RitualOutcomeComp comp in def.comps)
            {
                if (comp is RitualOutcomeComp_Quality && comp.Applies(jobRitual) &&
                    (comp is RitualOutcomeComp_BindingSpectatorCount || Mathf.Abs(comp.QualityOffset(jobRitual, DataForComp(comp))) >= float.Epsilon))
                {
                    taggedString += "\n  - " + comp.GetDesc(jobRitual, DataForComp(comp)).CapitalizeFirst();
                }
            }

            // 3天重复惩罚文本
            if (jobRitual.repeatPenalty && jobRitual.Ritual != null && jobRitual.Ritual.lastFinishedTick != -1)
            {
                if ((Find.TickManager.TicksGame - jobRitual.Ritual.lastFinishedTick) < 180000)
                {
                    taggedString += "\n  - " + Strings.Ritual_RepeatPenalty + (-0.2f).ToStringPercent();
                }
            }

            return taggedString;
        }

        public override string ExtraAlertParagraph(Precept_Ritual ritual)
        {
            string text = "";
            foreach (RitualOutcomeComp comp in def.comps)
            {
                if (comp is RitualOutcomeComp_Quality)
                {
                    string desc = comp.GetDesc();
                    if (!desc.NullOrEmpty()) text = text + "\n  - " + desc.CapitalizeFirst();
                }
            }
            return ("RitualOutcomeQualityAbstract".Translate(ritual.Label).Resolve().CapitalizeFirst() + ":").Colorize(ColoredText.TipSectionTitleColor) + text;
        }

        // =========================================================================
        // 🎯 结算执行核心 (Apply)
        // =========================================================================
        /// <summary>领取本场一次性完成资格后执行绑定仪式结算；中断、未完成或重复调用不派发奖励。</summary>
        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            // 只有本场确实完成六阶段且尚未结算，才能领取奖励。全局静态标记会让
            // 同时举办的仪式互相借用完成资格，所以资格随本场 Lord 保存并消费。
            if (!BindingRitualStateUtility.TryClaimOutcome(jobRitual))
            {
                SSCLog.Important("[SSC Debug] 仪式未正常完成(中途打断)，拦截结算。");
                return;
            }
            Pawn master = jobRitual.PawnWithRole("master");
            Pawn slave = jobRitual.PawnWithRole("slave");

            if (slave == null || master == null)
            {
                Log.Error("[SSC] 仪式结算失败: master 或 slave 为空");
                return;
            }

            var phaseContext = new TrainingJobUtility.JobContext(master.jobs?.curDriver);
            bool hasPhaseJob = phaseContext.Driver is JobDriver_RitualTraining;
            Func<bool> canContinue = () =>
                (!hasPhaseJob || phaseContext.IsCurrent && phaseContext.Target.Thing == slave)
                && BindingRitualStateUtility.CanContinueOutcome(jobRitual, master, slave);
            if (!canContinue()) return;

            SSCLog.Important($"[SSC 仪式结算] 进入结算: master={master.LabelShort}, slave={slave.LabelShort}, prisoner={slave.IsPrisonerOfColony}, colonist={slave.IsColonist}, slaveState={slave.IsSlave}, guestNull={(slave.guest == null)}");

            // 在任何结算副作用和原版 ResetCompDatas 之前记录真实出席，便于实机复核零人原因。
            foreach (RitualOutcomeComp comp in def.comps)
            {
                if (!(comp is RitualOutcomeComp_BindingSpectatorCount audience)) continue;
                var presence = DataForComp(comp) as RitualOutcomeComp_DataThingPresence;
                int elapsed = RitualOutcomeComp_BindingSpectatorCount.AttendanceDurationTicks(jobRitual);
                SSCLog.Important($"[SSC_RITUAL_AUDIENCE] progress={jobRitual.TicksPassedWithProgress}, elapsed={elapsed}, minimum={elapsed / 2f}, records={presence?.presentForTicks.Count ?? 0}, counted={audience.Count(jobRitual, presence)}");
                foreach (Pawn spectator in jobRitual.assignments.SpectatorsForReading)
                {
                    if (spectator == null) continue;
                    float ticks = 0f;
                    presence?.presentForTicks.TryGetValue(spectator, out ticks);
                    bool owned = jobRitual.lord.ownedPawns.Contains(spectator);
                    bool inArea = spectator.Spawned && spectator.Map == jobRitual.Map &&
                        GatheringsUtility.InGatheringArea(spectator.Position, jobRitual.Spot, spectator.MapHeld);
                    SSCLog.Important($"[SSC_RITUAL_AUDIENCE] pawn={spectator.LabelShort}, present={ticks}, owned={owned}, inArea={inArea}, position={spectator.Position}, duty={spectator.mindState?.duty?.def?.defName}, focus={spectator.mindState?.duty?.focus}, job={spectator.CurJobDef?.defName}");
                }
            }

            // 1. 获取纯净质量与结局
            float quality = GetQuality(jobRitual, progress);
            if (!canContinue()) return;
            RitualOutcomePossibility outcome = GetOutcome(quality, jobRitual);
            if (outcome == null || !canContinue()) return;

            // 2. 派发记忆 (全员包含观众)
            foreach (KeyValuePair<Pawn, int> item in totalPresence)
            {
                if (!canContinue()) return;
                Pawn p = item.Key;
                if (!outcome.roleIdsNotGainingMemory.NullOrEmpty())
                {
                    RitualRole ritualRole = jobRitual.assignments.RoleForPawn(p);
                    if (ritualRole != null && outcome.roleIdsNotGainingMemory.Contains(ritualRole.id)) continue;
                }

                if (outcome.memory != null && p.needs?.mood != null)
                {
                    // 🔥【核心修复】直接使用基础 Thought_Memory 接收，完美兼容自定义 XML
                    Thought_Memory newThought = MakeMemory(p, jobRitual, outcome.memory);
                    if (!canContinue()) return;
                    if (newThought != null)
                    {
                        p.needs.mood.thoughts.memories.TryGainMemory(newThought);
                    }
                }
            }

            // 3. 运行 SSC 自定义结算
            // EN: Use ConditioningUtility here to apply SSC's Binding Ritual aftermath for the master and sex slave.
            // CN: 这里调用 ConditioningUtility，用来结算主人和性奴在“绑定仪式”中的 SSC 专属后果。
            if (!canContinue()) return;
            string sscDetails = ConditioningUtility.ExecuteRitualOutcome(master, slave, quality, outcome.memory);
            if (!canContinue()) return;

#if SSC_RW17
            // 整场只在原 TryClaimOutcome 内领取一次；六阶段不逐次发放，评分采用 75q。
            var uterus = SSCUterusDevelopmentGrowthUtility.Capture(slave,
                slave.TryGetComp<CompSexSlaveTraining>()?.uterusRitualEvidence,
                SpecializationTrainingProgressUtility.RitualScore(quality));
            uterus?.Apply(canContinue);
            if (!canContinue()) return;
            // 胃部只领取整场一份奖励；证据来自成功完成的阶段，包含末阶段同步通知前的记录。
            var stomach = SSCStomachDevelopmentGrowthUtility.Capture(slave,
                slave.TryGetComp<CompSexSlaveTraining>()?.stomachRitualEvidence,
                SpecializationTrainingProgressUtility.RitualScore(quality));
            stomach?.Apply(canContinue);
            if (!canContinue()) return;
#endif

            // 六个阶段的 ProcessSex 各自只负责 RJW 阶段效果。上方已消费
            // 本场 Lord 的一次性资格，按整场结果只发一份当前方向基础经验。
            SpecializationTrainingProgressUtility.NotifyTrainingCompleted(master, slave,
                SpecializationTrainingProgressUtility.RitualScore(quality));
            if (!canContinue()) return;
            TrainerSpecializationProgressUtility.NotifyProvidedTrainingCompleted(master, slave);
            if (!canContinue()) return;
            // 复用整场一次性认领，不从六个阶段或经验增量派发职责记忆。
            TrainerOfficerFeedback.NotifyTrainingCompleted(master, slave);
            if (!canContinue()) return;

            // 本场六阶段和原有结算已经完成，固定主人经验只领取一次，不依赖质量或对象增量。
            // master 是主持角色名，也可由训导官担任；统一入口核对真实 SSC 主人身份，
            // 不把代劳经验转给绑定主人，也不为现代无成长记录的主持者补建 Hediff。
            SSCMasterProgressionUtility.TryAddExperience(master, SSCMasterProgressionUtility.BindingRitualExperience);
            // 升阶刷新健康可能使任务或仪式被外部替换；后续关系、文化收益和信件仍复查本场归属。
            if (!canContinue()) return;
            // 主持者也可以是第三方调教员；只有锁链实际主人能领取原有的 80 好感关系奖励。
            bool relationAdded = SSCBondRelationUtility.TryCreateAfterRitual(master, slave, canContinue);
            if (!canContinue()) return;

            // 4. 处理原版的文化点数和附魔事件
            string extraOutcomeDesc = null;
            LookTargets letterLookTargets = new LookTargets(master, slave);
            if (jobRitual.Ritual?.attachableOutcomeEffect != null && jobRitual.Ritual.attachableOutcomeEffect.AppliesToOutcome(jobRitual.Ritual.outcomeEffect.def, outcome))
            {
                if (!canContinue()) return;
                jobRitual.Ritual.attachableOutcomeEffect.Worker.Apply(totalPresence, jobRitual, outcome, out extraOutcomeDesc, ref letterLookTargets);
                if (!canContinue()) return;
            }

            string extraDevPointsText = null;
            // 只控制文化点数分支；关闭时不调用原版加点，也不生成“未获得点数”的奖励说明。
            // 仍复用本场已消费的 TryClaimOutcome，不从六个阶段分别发点或新增领取存档字段。
            if (GivesDevelopmentPoints && jobRitual.Ritual?.ideo?.Fluid == true)
            {
                if (!canContinue()) return;
                int num = def.outcomeChances.IndexOf(outcome);
                var development = jobRitual.Ritual.ideo.development;
                int pointsBefore = development.Points;
                if (num >= 0 && development.TryGainDevelopmentPointsForRitualOutcome(jobRitual.Ritual, num, out _))
                {
                    // 原版 out 值是曲线名义奖励，接近改革上限时可能大于实际增量；信件只报告真实前后值。
                    int pointsAfter = development.Points;
                    int gained = pointsAfter - pointsBefore;
                    if (gained > 0) extraDevPointsText = "RitualOutcomeExtraDesc_DevelopmentPointsAwarded".Translate(pointsBefore, pointsAfter, gained.ToStringWithSign());
                    else extraDevPointsText = "RitualOutcomeExtraDesc_NoDevelopmentPointsAwarded".Translate();
                }
            }

            // 5. 拼装信件
            StringBuilder finalLetterText = new StringBuilder();
            finalLetterText.AppendLine(outcome.description.Formatted(jobRitual.Ritual.Label).CapitalizeFirst());
            finalLetterText.AppendLine();

            // 插入 SSC 的详细结算文本
            finalLetterText.AppendLine(sscDetails);

            if (relationAdded)
            {
                finalLetterText.AppendLine(Strings.Ritual_FlawedLoversCreated(slave.LabelShort, master.LabelShort));
            }

            if (!extraOutcomeDesc.NullOrEmpty()) finalLetterText.AppendLine("\n" + extraOutcomeDesc);
            if (!extraDevPointsText.NullOrEmpty()) finalLetterText.AppendLine("\n" + extraDevPointsText);

            finalLetterText.AppendLine();
            finalLetterText.AppendLine(OutcomeQualityBreakdownDesc(quality, progress, jobRitual));

            // 发送弹窗
            if (!canContinue()) return;
            Find.LetterStack.ReceiveLetter(
                "OutcomeLetterLabel".Translate(outcome.label.Named("OUTCOMELABEL"), jobRitual.Ritual.Label.Named("RITUALLABEL")),
                finalLetterText.ToString(),
                outcome.Positive ? LetterDefOf.RitualOutcomePositive : LetterDefOf.RitualOutcomeNegative,
                letterLookTargets
            );
        }
    }
}
