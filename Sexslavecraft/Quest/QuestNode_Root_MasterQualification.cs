using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>固定一个模块的免费领取任务，接受、奖励展示和物品投放复用原版。</summary>
    public sealed class QuestNode_Root_MasterQualification : QuestNode
    {
        /// <summary>验证生成器传入的目标确实可用，不在测试阶段创建奖励或改写存档。</summary>
        protected override bool TestRunInt(Slate slate)
        {
            // 查询可能回退另一基地，但生成阶段必须使用 Slate 中同一目标，不能默默换目标。
            Map map = slate.Get<Map>("map");
            return SSCDefOf.SSC_MasterQualificationItem != null && map != null &&
                SSCMasterEntryUtility.FindDeliveryMap(map, SSCDefOf.SSC_InitialMasterQualification) == map;
        }

        /// <summary>构建“接受→投放协调→原版空投成功→任务成功”的信号链及唯一奖励实物。</summary>
        protected override void RunInt()
        {
            // 信号带原版任务 id，避免多个领取任务之间共享投放或完成信号。
            Quest quest = QuestGen.quest;
            Map map = QuestGen.slate.Get<Map>("map");
            string deliverSignal = "Quest" + quest.id + ".SSCEntryDeliver";
            string deliveredSignal = "Quest" + quest.id + ".SSCEntryDelivered";
            // 生成固定一个模块，不走随机奖励预算，也不在任务创建时直接给任何 Pawn 资格。
            Thing item = ThingMaker.MakeThing(SSCDefOf.SSC_MasterQualificationItem);
            item.stackCount = 1;

            // 文案直接写入原版解析槽，仍由 QuestGen 完成任务名称/描述及保存。
            QuestGen.slate.Set("resolvedQuestName", "SSC_MasterEntry_QuestName".Translate().ToString());
            QuestGen.slate.Set("resolvedQuestDescription", "SSC_MasterEntry_QuestDescription".Translate().ToString());

            // 展示和投放共用同一实物；只有空投部分 Deep 保存它，奖励部分保存 Reference。
            QuestPart_Choice choice = new QuestPart_Choice { inSignalChoiceUsed = deliveredSignal };
            QuestPart_Choice.Choice option = new QuestPart_Choice.Choice();
            option.rewards.Add(new Reward_Items { items = new List<Thing> { item } });
            choice.choices.Add(option);
            quest.AddPart(choice);
            // 接受信号只启用协调部分；真正投放要等有效基地，地图变化不会直接提前结束任务。
            quest.AddPart(new QuestPart_MasterQualificationDelivery
            {
                inSignalEnable = quest.InitiateSignal,
                deliverSignal = deliverSignal,
                deliveredSignal = deliveredSignal,
                module = item
            });
            // 原版空投拥有实物并负责保存、清理与运输舱；成功信号也用于记住已派送的奖励展示。
            quest.AddPart(new QuestPart_DropPods
            {
                inSignal = deliverSignal,
                outSignalResult = deliveredSignal,
                mapParent = map.Parent,
                Things = new List<Thing> { item },
                useTradeDropSpot = true
            });
            // 无地图时投放控制部分等待；只有原版确认空投后才以成功结束任务。
            quest.AddPart(new QuestPart_QuestEnd { inSignal = deliveredSignal, outcome = QuestEndOutcome.Success });
        }
    }
}
