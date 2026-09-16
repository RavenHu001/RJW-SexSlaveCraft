using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存本次已经发生的共同睡眠，使先起床的一方不会抹掉另一方的记录。</summary>
    public class SSCSharedSleepRecord : IExposable
    {
        public Building_Bed bed;
        public Pawn partner;
        public int stage = -1;
        public bool withMaster;

        public void ExposeData()
        {
            Scribe_References.Look(ref bed, "bed");
            Scribe_References.Look(ref partner, "partner");
            Scribe_Values.Look(ref stage, "stage", -1);
            Scribe_Values.Look(ref withMaster, "withMaster", false);
        }
    }

    public static class SSCSharedBedMemory
    {
        /// <summary>只记录双方确实同时睡着的情境，不把清醒躺卧当作共同睡眠。</summary>
        public static void RecordSleep(Pawn actor, Building_Bed bed)
        {
            if (actor == null || bed == null || bed.SleepingSlotsCount <= 1 || actor.CurrentBed() != bed
                || actor.Awake() || !SSCSharedBedUtility.HasMeaningfulCorruption(actor)
                || !SSCSharedBedUtility.TryGetPartner(actor, out Pawn partner, out bool withMaster)
                || partner.CurrentBed() != bed || partner.Awake()) return;

            CompSexSlaveTraining comp = actor.TryGetComp<CompSexSlaveTraining>();
            if (comp == null) return;
            if (comp.sharedSleep == null) comp.sharedSleep = new SSCSharedSleepRecord();
            comp.sharedSleep.bed = bed;
            comp.sharedSleep.partner = partner;
            comp.sharedSleep.withMaster = withMaster;
            comp.sharedSleep.stage = SSCSharedBedUtility.GetSharedBedThoughtStage(actor, partner);

            // 主人/调教员只保留原有的负面房间记忆免除，不另加一份心情奖励。
            CompSexSlaveTraining partnerComp = partner.TryGetComp<CompSexSlaveTraining>();
            if (partnerComp != null && (partnerComp.sharedSleep == null || partnerComp.sharedSleep.stage < 0))
            {
                if (partnerComp.sharedSleep == null) partnerComp.sharedSleep = new SSCSharedSleepRecord();
                partnerComp.sharedSleep.bed = bed;
                partnerComp.sharedSleep.partner = actor;
            }
        }

        public static void RemoveNegativeRoomMemories(Pawn actor, Building_Bed bed)
        {
            SSCSharedSleepRecord record = actor?.TryGetComp<CompSexSlaveTraining>()?.sharedSleep;
            if (bed == null || record?.bed != bed) return;
            var memories = actor.needs?.mood?.thoughts?.memories;
            memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBedroom, thought => thought.MoodOffset() < 0f);
            memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBarracks, thought => thought.MoodOffset() < 0f);
        }

        /// <summary>结束躺卧后结算一次；同类记忆互相替换，不因多次休息或换对象叠加。</summary>
        public static void FinishSleep(Pawn actor, Building_Bed bed)
        {
            CompSexSlaveTraining comp = actor?.TryGetComp<CompSexSlaveTraining>();
            SSCSharedSleepRecord record = comp?.sharedSleep;
            if (record == null) return;
            comp.sharedSleep = null;
            if (bed == null || record.bed != bed || record.stage < 0 || record.partner == null) return;

            var memories = actor.needs?.mood?.thoughts?.memories;
            ThoughtDef def = record.withMaster ? SSCDefOf.SSC_SharedBedWithMaster : SSCDefOf.SSC_SharedBedWithTrainer;
            if (memories == null) return;
            if (def == null || !def.IsMemory)
            {
                Log.ErrorOnce("[SexSlaveCraft] 同床记忆定义缺失或仍为旧版情境心情。请一并更新 Assemblies、Defs 和 Languages 后完全重启游戏。", 193764081);
                return;
            }
            // 原版 RemoveMemoriesOfDef 会直接读取 def.IsMemory，不能传入缺失的定义。
            // 先确认本次记忆有效，再清理旧记忆，避免文件不配套时连已有心情也丢失。
            if (SSCDefOf.SSC_SharedBedWithMaster?.IsMemory == true)
                memories.RemoveMemoriesOfDef(SSCDefOf.SSC_SharedBedWithMaster);
            if (SSCDefOf.SSC_SharedBedWithTrainer?.IsMemory == true)
                memories.RemoveMemoriesOfDef(SSCDefOf.SSC_SharedBedWithTrainer);
            memories.TryGainMemory((Thought_Memory)ThoughtMaker.MakeThought(def, record.stage), record.partner);
        }
    }
}
