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

        /// <summary>保存床、对象和已记录的心情阶段，供读档后继续结算这次共同睡眠。</summary>
        /// <remarks>stage 默认为 -1，表示无性奴心情奖励；对象引用由 Scribe 解析。</remarks>
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
        /// <param name="actor">正在获得休息效果的角色，须满足 SSC 身份、对象和恶堕条件。</param>
        /// <param name="bed">双方当前共同使用的多人床。</param>
        /// <remarks>记录此刻的对象与阶段，不提前生成记忆；对象一侧只补充负面房间心情免除记录。</remarks>
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

        /// <summary>已有本床共同睡眠记录时，清理双方各自的负面卧室/营房记忆，保留非负面效果。</summary>
        /// <param name="actor">原版正在结算房间心情的角色，可以是性奴或其主人/调教员。</param>
        /// <param name="bed">本次结束休息的床，必须与记录中的床一致。</param>
        /// <remarks>不重新检查另一方是否仍在睡觉，避免先后起床导致免除失效。</remarks>
        public static void RemoveNegativeRoomMemories(Pawn actor, Building_Bed bed)
        {
            SSCSharedSleepRecord record = actor?.TryGetComp<CompSexSlaveTraining>()?.sharedSleep;
            if (bed == null || record?.bed != bed) return;
            var memories = actor.needs?.mood?.thoughts?.memories;
            memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBedroom, thought => thought.MoodOffset() < 0f);
            memories?.RemoveMemoriesOfDefIf(ThoughtDefOf.SleptInBarracks, thought => thought.MoodOffset() < 0f);
        }

        /// <summary>结束躺卧后结算一次；同类记忆互相替换，不因多次休息或换对象叠加。</summary>
        /// <param name="actor">结束休息的角色；只有记录了有效心情阶段的一方获得记忆。</param>
        /// <param name="bed">刚结束使用的床，必须与共同睡眠记录一致。</param>
        /// <remarks>先消费记录以防重复回调；目标定义缺失或仍为情境心情时保留已有记忆并提示更新文件。</remarks>
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
