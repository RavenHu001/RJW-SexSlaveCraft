using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    public class SSC_HediffGiver_Refresh : HediffGiver
    {
        public override void OnIntervalPassed(Pawn pawn, Hediff cause)
        {
            // 检查小人是否已经拥有目标 Hediff
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);

            // 如果还没有，则尝试添加
            if (firstHediffOfDef == null)
            {
                if (TryApply(pawn))
                {
                    // 添加成功后在控制台打印日志，方便你调试
                    Log.Message(pawn.ToString() + " 恶堕程度达标，已获得永久性: " + hediff.defName);
                }
            }
            // 如果已经有了，由于我们要的是“停不下来”，所以不需要再重置 ageTicks。
            // 因为我们在 XML 中会删掉所有让它消失的组件。
        }
    }
}
