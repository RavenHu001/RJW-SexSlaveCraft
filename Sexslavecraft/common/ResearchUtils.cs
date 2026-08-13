using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

// EN: This file is the tiny helper layer for SSC research gates.
// EN: It lets gameplay code ask whether `调教`, `部位调教`, or any other SSC research project is already finished without repeating lookup code.
// CN: 这个文件是 SSC 研究前置的小型辅助层。
// CN: 它让玩法代码可以直接查询“调教”“部位调教”等研究是否已经完成，而不用反复手写查表逻辑。
namespace SexSlaveCraft
{
    public static class ResearchUtils
    {
        // EN: This overload exists for old hard-coded call sites that still pass the research DefName as a string.
        // CN: 这个重载保留给旧代码使用，适合那些仍然直接传研究 DefName 字符串的场景。
        public static bool IsResearchFinished(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return true;
            ResearchProjectDef def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return IsResearchFinished(def); // 调用下面的方法
        }

        // EN: This is the preferred path for SSCDefOf callers because it skips the extra database lookup.
        // CN: 这是 SSCDefOf 调用方的首选路径，因为它不用再额外去数据库查一次表。
        public static bool IsResearchFinished(ResearchProjectDef def)
        {
            // EN: Missing defs are treated as locked so broken XML never unlocks SSC content by accident.
            // CN: 缺失 Def 一律按“未解锁”处理，避免 XML 出错时把 SSC 内容意外放开。
            if (def == null) return false;
            return def.IsFinished;
        }
    }
}
