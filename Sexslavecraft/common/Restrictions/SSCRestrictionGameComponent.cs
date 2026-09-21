using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>保存本存档旧设置快照，在所有引用恢复后初始化地图、世界、运输及临时角色。</summary>
    public sealed class SSCRestrictionGameComponent : GameComponent
    {
        private SSCRestrictionLegacySettings legacySettings;
        public bool Ready { get; private set; }

        /// <summary>由游戏创建独立组件，状态不跨存档共享。</summary>
        public SSCRestrictionGameComponent(Game game) { }

        /// <summary>保存旧全局快照；缺少快照的旧存档在本次加载时固定当前可取得的旧设置。</summary>
        public override void ExposeData()
        {
            Scribe_Deep.Look(ref legacySettings, "sscRestrictionLegacySettings");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacySettings == null)
                legacySettings = SSCRestrictionLegacySettings.Capture(SSCMod.settings);
        }

        /// <summary>等引用和调教员资格恢复后统一初始化；先迁移既有指派身份，再协调新配置。</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (legacySettings == null) legacySettings = SSCRestrictionLegacySettings.Capture(SSCMod.settings);
            List<Pawn> pawns = AllPawns();
            SSCTrainerIdentityMigration.Migrate(pawns);
            Ready = true;
            foreach (Pawn pawn in pawns) Notify(pawn);
        }

        /// <summary>获得当前存档内地图、世界和临时容器角色的独立快照，避免枚举中关系更新改变集合。</summary>
        public static List<Pawn> AllPawns()
        {
            if (Current.Game == null) return new List<Pawn>();
            // 原版这些属性复用临时列表，先复制再查询其他集合，不能延迟到 Concat 枚举时才读取。
            var pawns = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList();
            pawns.AddRange(PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead);
            return pawns.Where(p => p != null).Distinct().ToList();
        }

        /// <summary>只在完整运行事件处理配置；加载和恢复期间等待，不由界面绘制或规则查询调用。</summary>
        public static void Notify(Pawn pawn)
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
            SSCRestrictionGameComponent game = Current.Game?.GetComponent<SSCRestrictionGameComponent>();
            if (game?.Ready != true) return;
            CompSexSlaveTraining comp = pawn.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || comp.restrictionRestoreDepth > 0) return;
            SSCRestrictionLifecycle.Refresh(pawn, game.legacySettings, out SSCRestrictionResolution error);
            if (error != null)
            {
                string message = error.Rule + "/" + error.Source + "/" + error.SourceDef + "/" + error.Value;
                if (message != comp.restrictionLastError)
                    Log.Error("[SSC] Restriction initialization failed for " + pawn.LabelShort + ": " + message);
                comp.restrictionLastError = message;
            }
            else comp.restrictionLastError = null;
            SSCRestrictionLifecycle.CoordinateTrainer(pawn);
        }

        /// <summary>全局开关改变后立即协调当前存档；关闭时只解除锁定，重新开启时再按当前许可改回主人。</summary>
        public static void SettingsChanged()
        {
            foreach (Pawn pawn in AllPawns()) SSCRestrictionLifecycle.CoordinateTrainer(pawn);
        }
    }
}
