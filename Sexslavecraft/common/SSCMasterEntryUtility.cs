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
            // 调用者提供地图/世界/商队/运输快照；此处不另起扫描，缺集合或玩家阵营时无匹配。
            if (pawns == null || Faction.OfPlayer == null) return false;
            foreach (Pawn pawn in pawns)
            {
                // 当前 SSC 身份才决定是否已有主人；不要求在地图上，也不要求永久资格为 true。
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer ||
                    pawn.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity != PawnIdentity.Master)
                    continue;
                // 原身体可能仍保留 Master 身份，但已抽空人格的身体不能阻止初次任务。
                if (SSCDefOf.SSC_PersonalityExcreted_Done != null &&
                    pawn.health?.hediffSet?.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) == true)
                    continue;
                // 找到一个有效己方主人即可终止查询；不修改角色身份、资格或迁移版本。
                return true;
            }
            return false;
        }

        /// <summary>按专用任务 Def 的名称识别历史记录，不依赖可失效的组件任务引用。</summary>
        public static bool IsEntryQuest(Quest quest)
        {
            return quest?.root?.defName == QuestDefName;
        }

        /// <summary>隐藏提示不结束任务；仅真正进入历史状态的任务才不参与待处理去重。</summary>
        public static Quest FindPendingQuest()
        {
            // Find 的管理器访问需要当前游戏；主菜单调用必须先短路。
            if (Current.Game == null || Find.QuestManager == null) return null;
            // 原版全集含历史任务；Historical 排除已结束记录，dismissed 不参与判定。
            foreach (Quest quest in Find.QuestManager.QuestsListForReading)
                if (IsEntryQuest(quest) && !quest.Historical) return quest;
            return null;
        }

        /// <summary>只在当前游戏中选择原版脚本允许的玩家基地地图，优先保留原投放目标。</summary>
        public static Map FindDeliveryMap(Map preferred, QuestScriptDef root)
        {
            if (Current.Game == null || root == null) return null;
            // 有效原目标优先，其次当前查看的基地，最后才选其他基地，避免切换视图就换投放地。
            if (IsDeliveryMap(preferred, root)) return preferred;
            if (IsDeliveryMap(Find.CurrentMap, root)) return Find.CurrentMap;
            foreach (Map map in Find.Maps)
                if (IsDeliveryMap(map, root)) return map;
            // 暂时只有商队或不适合该脚本的地图时返回空，由请求/投放组件继续等待。
            return null;
        }

        /// <summary>旧地图对象还存在不代表仍可投放，必须属于当前游戏并通过原版脚本的地图条件。</summary>
        private static bool IsDeliveryMap(Map map, QuestScriptDef root)
        {
            return map != null && map.IsPlayerHome && map.Parent != null && Find.Maps.Contains(map) &&
                root.IsParentSuitableForQuest(map.Parent);
        }

        /// <summary>生成失败不登记；登记后的通知异常也不能把已经存在的任务当作失败重复创建。</summary>
        public static bool TryCreateQuest(Map map, out Quest created)
        {
            // 所有失败路径保持输出为空；后台只检查创建宿主，不在这里重算自动研究/主人条件。
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
                // 固定目标地图与零威胁点数，CanRun 保留原版场景禁用及任务根节点准入。
                Slate slate = new Slate();
                slate.Set("map", map);
                slate.Set("points", 0f);
                if (!root.CanRun(slate, map)) return false;
                // Generate 只创建任务对象；加入管理器后才具有持久任务及信号接收生命周期。
                generated = QuestGen.Generate(root, slate);
                if (generated == null) return false;
                Find.QuestManager.Add(generated);
            }
            catch (Exception ex)
            {
                // 不在异常处理里认定登记结果，统一交给下面的 Contains 检查。
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
            // 任务状态已经由调用者提交；信件是通知，失败不能撤回任务或重新创建奖励。
            try { QuestUtility.SendLetterQuestAvailable(quest); }
            catch (Exception ex) { Log.Error("[SSC] Initial Master qualification quest letter failed: " + ex); }
        }
    }
}
