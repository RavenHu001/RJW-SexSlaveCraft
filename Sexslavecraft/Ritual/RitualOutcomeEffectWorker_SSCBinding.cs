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
        public virtual bool GivesDevelopmentPoints => def.givesDevelopmentPoints;

        // EN: This flag is the ritual-completion gate. Only the ritual JobDriver may open it right before final outcome resolution.
        // CN: 这个标记是“仪式完成通行证”，只有 ritual JobDriver 才能在最后结算前把它打开。
        public static bool IsRitualCompletedSuccessfully = false;

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
                if (comp is RitualOutcomeComp_Quality && comp.Applies(jobRitual) && Mathf.Abs(comp.QualityOffset(jobRitual, DataForComp(comp))) >= float.Epsilon)
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
        public override void Apply(float progress, Dictionary<Pawn, int> totalPresence, LordJob_Ritual jobRitual)
        {
            // 打断拦截 (依靠 JobDriver 传来的通行证)
            if (!IsRitualCompletedSuccessfully)
            {
                SSCLog.Important("[SSC Debug] 仪式未正常完成(中途打断)，拦截结算。");
                return;
            }
            // 验证通过后立刻重置，锁死安全门
            IsRitualCompletedSuccessfully = false;

            Pawn master = jobRitual.PawnWithRole("master");
            Pawn slave = jobRitual.PawnWithRole("slave");

            if (slave == null || master == null)
            {
                Log.Error("[SSC] 仪式结算失败: master 或 slave 为空");
                return;
            }

            SSCLog.Important($"[SSC 仪式结算] 进入结算: master={master.LabelShort}, slave={slave.LabelShort}, prisoner={slave.IsPrisonerOfColony}, colonist={slave.IsColonist}, slaveState={slave.IsSlave}, guestNull={(slave.guest == null)}");

            // 1. 获取纯净质量与结局
            float quality = GetQuality(jobRitual, progress);
            RitualOutcomePossibility outcome = GetOutcome(quality, jobRitual);
            if (outcome == null) return;

            // 2. 派发记忆 (全员包含观众)
            foreach (KeyValuePair<Pawn, int> item in totalPresence)
            {
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
                    if (newThought != null)
                    {
                        p.needs.mood.thoughts.memories.TryGainMemory(newThought);
                    }
                }
            }

            // 3. 运行 SSC 自定义结算
            // EN: Use ConditioningUtility here to apply SSC's Binding Ritual aftermath for the master and sex slave.
            // CN: 这里调用 ConditioningUtility，用来结算主人和性奴在“绑定仪式”中的 SSC 专属后果。
            string sscDetails = ConditioningUtility.ExecuteRitualOutcome(master, slave, quality, outcome.memory);
            bool relationAdded = false;

            // 仅判定性奴对主人的好感 >= 80，添加缺陷恋人关系
            if (slave.relations.OpinionOf(master) >= 80)
            {
                if (!master.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, slave))
                {
                    master.relations.AddDirectRelation(SSCDefOf.SSC_FlawedLovers, slave);
                    relationAdded = true;
                }
            }

            // 4. 处理原版的文化点数和附魔事件
            string extraOutcomeDesc = null;
            LookTargets letterLookTargets = new LookTargets(master, slave);
            if (jobRitual.Ritual?.attachableOutcomeEffect != null && jobRitual.Ritual.attachableOutcomeEffect.AppliesToOutcome(jobRitual.Ritual.outcomeEffect.def, outcome))
            {
                jobRitual.Ritual.attachableOutcomeEffect.Worker.Apply(totalPresence, jobRitual, outcome, out extraOutcomeDesc, ref letterLookTargets);
            }

            string extraDevPointsText = null;
            if (jobRitual.Ritual?.ideo != null && jobRitual.Ritual.ideo.Fluid)
            {
                int num = def.outcomeChances.IndexOf(outcome);
                if (num >= 0 && jobRitual.Ritual.ideo.development.TryGainDevelopmentPointsForRitualOutcome(jobRitual.Ritual, num, out var developmentPoints))
                {
                    if (developmentPoints > 0) extraDevPointsText = "RitualOutcomeExtraDesc_DevelopmentPointsAwarded".Translate(jobRitual.Ritual.ideo.development.Points - developmentPoints, jobRitual.Ritual.ideo.development.Points, developmentPoints.ToStringWithSign());
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
            Find.LetterStack.ReceiveLetter(
                "OutcomeLetterLabel".Translate(outcome.label.Named("OUTCOMELABEL"), jobRitual.Ritual.Label.Named("RITUALLABEL")),
                finalLetterText.ToString(),
                outcome.Positive ? LetterDefOf.RitualOutcomePositive : LetterDefOf.RitualOutcomeNegative,
                letterLookTargets
            );
        }
    }
}
