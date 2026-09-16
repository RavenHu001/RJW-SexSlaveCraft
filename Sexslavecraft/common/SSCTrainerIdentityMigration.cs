using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>在所有 Pawn 引用恢复后迁移旧调教员配置，不在 tick 或界面绘制中扫描。</summary>
    public class SSCTrainerIdentityMigration : GameComponent
    {
        /// <summary>由原版创建游戏组件；迁移标记保存在各 Pawn 上，无额外全局状态。</summary>
        public SSCTrainerIdentityMigration(Game game) { }

        /// <summary>覆盖地图、世界、容器和临时 Pawn；重复初始化不会覆盖已保存的开关。</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Migrate(PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead);
        }

        /// <summary>只为旧档中已被指定的 SSC 性奴保留调教员资格；未选择不提权，新 Pawn 和显式关闭不变。</summary>
        public static void Migrate(IEnumerable<Pawn> pawns)
        {
            if (pawns == null) return;
            var all = pawns.Where(p => p != null).Distinct().ToList();
            var assigned = new HashSet<Pawn>(all.Select(p => p.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer)
                .Where(p => p != null && !p.Dead && !p.Destroyed));
            // 也覆盖仅由指派记录持有的对象，避免世界 Pawn 枚举遗漏造成资格丢失。
            foreach (Pawn pawn in all.Concat(assigned).Distinct())
            {
                CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
                if (comp == null || comp.trainerIdentityInitialized) continue;
                comp.slaveTrainerEnabled = comp.pawnIdentity == PawnIdentity.Slave && assigned.Contains(pawn);
                comp.trainerIdentityInitialized = true;
            }
        }
    }
}
