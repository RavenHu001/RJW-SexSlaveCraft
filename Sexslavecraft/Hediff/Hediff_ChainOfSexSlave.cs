using System.Linq;
using Verse;
using UnityEngine;

// EN: This hediff is the sex-slave side of ChainOfSexSlave.
// EN: It stores the linked master, shows the master name in the label, and tracks how far the slave chain has tightened.
// CN: 这个 Hediff 就是 ChainOfSexSlave 里“性奴这一侧”的锁链。
// CN: 它会保存关联主人、在标签里显示主人的名字，并记录锁链束缚已经推进到什么程度。
namespace SexSlaveCraft
{
    public class Hediff_ChainOfSexSlave : HediffWithTarget
    {
        public Pawn LinkedPawn => target as Pawn;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            ModLog.Message($"[ChainOfSexSlave] Added to {pawn}, linked with {LinkedPawn ?? target}");
        }

        public override string LabelBase
        {
            get
            {
                if (LinkedPawn != null)
                {
                    // EN: Use the shared keyed string so the ChainOfSexSlave label keeps the same in-game format everywhere.
                    // CN: 这里调用统一的本地化字符串，保证 ChainOfSexSlave 的标签在所有地方都保持同一种游戏内格式。
                    return Strings.Chain_LabelWithPawn(base.LabelBase, LinkedPawn.LabelShortCap);
                }
                return base.LabelBase;
            }
        }

        public override bool ShouldRemove
        {
            get
            {
                bool remove = false;
                if (target == null || target.Destroyed)
                {
                    ModLog.Message($"[ChainOfSexSlave] Removing from {pawn}: target is null or destroyed.");
                    remove = true;
                }

                if (LinkedPawn != null && (LinkedPawn.DestroyedOrNull() || LinkedPawn.Discarded))
                {
                    ModLog.Message($"[ChainOfSexSlave] Removing from {pawn}: linked pawn {LinkedPawn} invalid.");
                    remove = true;
                }

                return remove;
            }
        }

        public override void PostRemoved()
        {
            Pawn formerMaster = LinkedPawn;
            base.PostRemoved();
            SSCBondUtility.GetBridle(formerMaster)?.RemoveTarget(pawn);
        }

        public static Hediff_ChainOfSexSlave AddToPawn(Pawn SexSlave, Pawn Master)
        {
            if (SexSlave == null || Master == null)
            {
                ModLog.Message("[ChainOfSexSlave] AddToPawn failed: null host or target.");
                return null;
            }

            HediffDef def = SSCDefOf.ChainOfSexSlave;
            if (def == null) return null;
            var hediff = SexSlave.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_ChainOfSexSlave;

            if (hediff == null)
            {
                hediff = (Hediff_ChainOfSexSlave)HediffMaker.MakeHediff(def, SexSlave);
                // EN: The target field is the real master link used by every ChainOfSexSlave check in SSC.
                // CN: 这里的 target 字段就是 SSC 所有 ChainOfSexSlave 判定真正使用的主人引用。
                hediff.target = Master;
                SexSlave.health.AddHediff(hediff);
                ModLog.Message($"[ChainOfSexSlave] Created new link: {SexSlave} ↔ {Master}");
            }
            else
            {
                hediff.target = Master;
                ModLog.Message($"[ChainOfSexSlave] Updated existing link: {SexSlave} ↔ {Master}");
            }

            return hediff;
        }

        public static Hediff_ChainOfSexSlave IncreaseChainSeverity(Pawn pawn, float amount)
        {
            if (pawn == null)
            {
                ModLog.Message("[ChainOfSexSlave] IncreaseChainSeverity failed: pawn null.");
                return null;
            }

            HediffDef def = SSCDefOf.ChainOfSexSlave;
            if (def == null) return null;
            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def) as Hediff_ChainOfSexSlave;

            if (hediff == null)
            {
                ModLog.Message($"[ChainOfSexSlave] IncreaseChainSeverity failed: {pawn} has no Chain Hediff.");
                return null;
            }

            float oldSeverity = hediff.Severity;
            hediff.Severity = Mathf.Clamp(oldSeverity + amount, 0f, hediff.def.maxSeverity);

            ModLog.Message($"[ChainOfSexSlave] Severity for {pawn} increased {oldSeverity:F2} → {hediff.Severity:F2}");

            return hediff;
        }
    }
}
