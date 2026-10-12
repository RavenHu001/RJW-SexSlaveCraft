using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只处理缺字段的旧角色；明确保存的未解锁记录不由当前身份重算。</summary>
    public static class SSCMasterQualificationMigration
    {
        public static void MigratePawn(Pawn pawn)
        {
            CompSexSlaveTraining comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.masterQualificationVersion >= CompSexSlaveTraining.CurrentMasterQualificationVersion)
                return;

            bool hollow = SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                pawn.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true;
            comp.ReplaceMasterQualification(comp.pawnIdentity == PawnIdentity.Master &&
                !hollow && !RabbitCloneUtility.IsRabbitClone(pawn));
        }

        public static void Migrate(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            foreach (Pawn pawn in pawns) MigratePawn(pawn);
        }
    }
}
