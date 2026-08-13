using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SexSlaveCraft_Injector
    {
        static SexSlaveCraft_Injector()
        {
            LongEventHandler.QueueLongEvent(InjectSafe, "Initializing SexSlaveCraft", false, null);
        }

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

                    // B. 注入 ITab
                    if (def.inspectorTabs == null) def.inspectorTabs = new List<Type>();
                    if (!def.inspectorTabs.Contains(typeof(ITab_SexSlaveTraining)))
                    {
                        def.inspectorTabs.Add(typeof(ITab_SexSlaveTraining));
                        def.inspectorTabsResolved = null; // 清除缓存
                    }

                    def.ResolveReferences();
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SexSlaveCraft] 注入失败: {def.defName} - {ex.Message}");
                }
            }

            Log.Message($"[SexSlaveCraft] 注入完成。已覆盖 {successCount} 个种族 (包含白名单)。");
        }
    }
}