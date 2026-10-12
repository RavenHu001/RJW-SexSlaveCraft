using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>初次获取的只读判定及原版任务登记边界；个人资格和研究状态均不在这里修改。</summary>
    public static class SSCMasterEntryUtility
    {
        public const string QuestDefName = "SSC_InitialMasterQualification";

        /// <summary>只识别己方存活、非空壳的当前 SSC 主人；永久资格及手动赋予设置不代替身份。</summary>
        public static bool HasExistingMaster(IEnumerable<Pawn> pawns)
        {
            if (pawns == null || Faction.OfPlayer == null) return false;
            foreach (Pawn pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer ||
                    pawn.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity != PawnIdentity.Master)
                    continue;
                if (SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                    pawn.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true)
                    continue;
                return true;
            }
            return false;
        }

        public static bool IsEntryQuest(Quest quest)
        {
            return quest?.root?.defName == QuestDefName;
        }

        /// <summary>隐藏提示不结束任务；仅真正进入历史状态的任务才不参与待处理去重。</summary>
        public static Quest FindPendingQuest()
        {
            if (Current.Game == null || Find.QuestManager == null) return null;
            foreach (Quest quest in Find.QuestManager.QuestsListForReading)
                if (IsEntryQuest(quest) && !quest.Historical) return quest;
            return null;
        }

        /// <summary>只在当前游戏中选择原版脚本允许的玩家基地地图，优先保留原投放目标。</summary>
        public static Map FindDeliveryMap(Map preferred, QuestScriptDef root)
        {
            if (Current.Game == null || root == null) return null;
            if (IsDeliveryMap(preferred, root)) return preferred;
            if (IsDeliveryMap(Find.CurrentMap, root)) return Find.CurrentMap;
            foreach (Map map in Find.Maps)
                if (IsDeliveryMap(map, root)) return map;
            return null;
        }

        private static bool IsDeliveryMap(Map map, QuestScriptDef root)
        {
            return map != null && map.IsPlayerHome && map.Parent != null && Find.Maps.Contains(map) &&
                root.IsParentSuitableForQuest(map.Parent);
        }

        /// <summary>生成失败不登记；登记后的通知异常也不能把已经存在的任务当作失败重复创建。</summary>
        public static bool TryCreateQuest(Map map, out Quest created)
        {
            created = null;
            QuestScriptDef root = SSCDefOf.SSC_InitialMasterQualification;
            if (Current.Game == null || Find.QuestManager == null ||
                SSCDefOf.SSC_MasterQualificationItem == null || root == null || !IsDeliveryMap(map, root))
                return false;
            // 直接调用共同后台也要去重，不能只依赖设置界面或存档组件先检查。
            if (FindPendingQuest() != null) return false;

            Quest generated = null;
            try
            {
                Slate slate = new Slate();
                slate.Set("map", map);
                slate.Set("points", 0f);
                if (!root.CanRun(slate, map)) return false;
                generated = QuestGen.Generate(root, slate);
                if (generated == null) return false;
                Find.QuestManager.Add(generated);
            }
            catch (Exception ex)
            {
                Log.Error("[SSC] Initial Master qualification quest creation failed: " + ex);
            }

            // Add 的后半段可能由外部通知抛错，最终以原版管理器是否已登记为准。
            if (generated == null || !Find.QuestManager.Contains(generated)) return false;
            created = generated;
            return true;
        }

        /// <summary>状态提交之后发送领取任务信；提示失败保留已创建任务，防止读档再次补发。</summary>
        public static void NotifyAvailable(Quest quest)
        {
            try { QuestUtility.SendLetterQuestAvailable(quest); }
            catch (Exception ex) { Log.Error("[SSC] Initial Master qualification quest letter failed: " + ex); }
        }
    }
}
