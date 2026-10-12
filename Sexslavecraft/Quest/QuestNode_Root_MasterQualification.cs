using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>固定一个模块的免费领取任务，接受、奖励展示和物品投放复用原版。</summary>
    public sealed class QuestNode_Root_MasterQualification : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            Map map = slate.Get<Map>("map");
            return SSCDefOf.SSC_MasterQualificationItem != null && map != null &&
                SSCMasterEntryUtility.FindDeliveryMap(map, SSCDefOf.SSC_InitialMasterQualification) == map;
        }

        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Map map = QuestGen.slate.Get<Map>("map");
            string deliverSignal = "Quest" + quest.id + ".SSCEntryDeliver";
            string deliveredSignal = "Quest" + quest.id + ".SSCEntryDelivered";
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
            quest.AddPart(new QuestPart_MasterQualificationDelivery
            {
                inSignalEnable = quest.InitiateSignal,
                deliverSignal = deliverSignal,
                deliveredSignal = deliveredSignal,
                module = item
            });
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
