using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在所有 Pawn 引用恢复后依次处理旧指派身份和训导官开关迁移，不在高频入口扫描。</summary>
    public class SSCTrainerIdentityMigration : GameComponent
    {
        /// <summary>由原版创建游戏组件；迁移标记保存在各 Pawn 上，无额外全局状态。</summary>
        public SSCTrainerIdentityMigration(Game game) { }

        /// <summary>覆盖地图、世界、容器和临时 Pawn；两代迁移标记都保存在 Pawn 上。</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Migrate(PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead);
        }

        /// <summary>先完成旧版指派迁移，再一次性关闭旧性奴开关；重复调用不改变新选择。</summary>
        public static void Migrate(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            var all = pawns.Where(p => p != null).Distinct().ToList();
            var assigned = new HashSet<Pawn>(all.Select(p => p.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer)
                .Where(p => p != null && !p.Dead && !p.Destroyed));
            // 间接引用的调教员可能不在本次世界快照中。仍需为其处理两个版本，
            // 否则下一次加载旧身份迁移可能重新打开已经重置的开关。
            foreach (Pawn pawn in all.Concat(assigned).Distinct())
            {
                CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
                if (comp == null) continue;

                // 原迁移先决定旧档中被指派性奴的个人开关。新生成角色和已经
                // 明确保存旧标记的角色跳过这一步，维持原有旧档兼容语义。
                if (!comp.trainerIdentityInitialized)
                {
                    comp.slaveTrainerEnabled = comp.pawnIdentity == PawnIdentity.Slave && assigned.Contains(pawn);
                    comp.trainerIdentityInitialized = true;
                }

                // 阶段 4 不再允许旧性奴仅凭历史开关任职。只在首次升级时
                // 关闭保存选择，不清理指派、主人身份、特化进度或正在执行的任务。
                if (comp.trainerOfficerMigrationVersion < CompSexSlaveTraining.CurrentTrainerOfficerMigrationVersion)
                {
                    if (comp.pawnIdentity == PawnIdentity.Slave) comp.slaveTrainerEnabled = false;
                    comp.trainerOfficerMigrationVersion = CompSexSlaveTraining.CurrentTrainerOfficerMigrationVersion;
                }
            }
        }
    }
}
