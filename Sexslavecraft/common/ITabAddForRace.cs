using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SexSlaveCraft_Injector
    {
        /// <summary>在启动长任务队列中安排种族分页注入，避免在静态构造期间立即修改 Def。</summary>
        static SexSlaveCraft_Injector()
        {
            LongEventHandler.QueueLongEvent(InjectSafe, "Initializing SexSlaveCraft", false, null);
        }

        /// <summary>
        /// 为类人及白名单种族补充训练组件和检查分页，保留已有分页实例。
        /// 仅在解析列表为 null 时从原始分页类型恢复列表，不重新解析整个种族 Def；
        /// 单个种族注入失败时记录警告并继续处理其余种族。
        /// </summary>
        private static void InjectSafe()
        {
            var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            int successCount = 0;

            // =========================================================
            // 🔥 定义白名单：把九莲这种特殊种族的 defName 写在这里
            // =========================================================
            HashSet<string> whiteList = new HashSet<string>
            {
                "Ninetailfox",      // 普通九莲
                "Ninetailfoxwt"     // 某种变体 (根据XML)
            };

            foreach (ThingDef def in allDefs)
            {
                try
                {
                    // 只需要检查名字是否为空，以及 race 节点是否存在
                    if (string.IsNullOrEmpty(def.defName) || def.race == null) continue;

                    // 2. 判定逻辑：是类人生物 OR 在白名单里
                    bool isTarget = (def.race.intelligence == Intelligence.Humanlike) || whiteList.Contains(def.defName);

                    // 如果不是目标，直接跳过
                    if (!isTarget) continue;

                    // 3. 排除机械族 (如果不希望给机器人用)
                    // if (def.race.FleshType == FleshTypeDefOf.Mechanoid) continue;

                    // =============================================
                    // 开始注入
                    // =============================================

                    // A. 注入 Comp
                    if (def.comps == null) def.comps = new List<CompProperties>();
                    if (!def.comps.Any(c => c.compClass == typeof(CompSexSlaveTraining)))
                    {
                        def.comps.Add(new CompProperties_SexSlaveTraining());
                    }

                    // B. 只追加 SSC 分页，保留其他模组已解析的分页实例。
                    Type trainingTabType = typeof(ITab_SexSlaveTraining);
                    if (def.inspectorTabs == null) def.inspectorTabs = new List<Type>();
                    if (!def.inspectorTabs.Contains(trainingTabType))
                    {
                        def.inspectorTabs.Add(trainingTabType);
                    }

                    if (def.inspectorTabsResolved == null)
                    {
                        // 尚无解析列表时，只恢复分页，不重跑 HAR 的种族初始化。
                        var resolvedTabs = new List<InspectTabBase>();
                        foreach (Type tabType in def.inspectorTabs)
                        {
                            AddSharedInspectorTab(resolvedTabs, tabType);
                        }
                        def.inspectorTabsResolved = resolvedTabs;
                    }
                    else
                    {
                        AddSharedInspectorTab(def.inspectorTabsResolved, trainingTabType);
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SexSlaveCraft] 注入失败: {def.defName} - {ex.Message}");
                }
            }

            Log.Message($"[SexSlaveCraft] 注入完成。已覆盖 {successCount} 个种族 (包含白名单)。");
        }

        /// <summary>
        /// 在列表缺少指定类型时追加其共享分页实例，保持已有实例及排列顺序。
        /// 按实际类型防重，因此已有非共享实例也会保留；忽略空类型或未取得的共享实例。
        /// </summary>
        /// <param name="tabs">要原位追加的非空分页列表。</param>
        /// <param name="tabType">需要补充的检查分页类型。</param>
        private static void AddSharedInspectorTab(List<InspectTabBase> tabs, Type tabType)
        {
            if (tabType == null || tabs.Any(tab => tab != null && tab.GetType() == tabType))
                return;

            InspectTabBase sharedTab = InspectTabManager.GetSharedInstance(tabType);
            if (sharedTab != null)
                tabs.Add(sharedTab);
        }
    }
}
