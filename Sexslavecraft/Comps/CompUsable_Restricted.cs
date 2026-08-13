using RimWorld;
using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{

    public class CompUsable_Restricted : CompUsable
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
        {
            // 基础检查
            if (!myPawn.CanReserve(parent)) yield break;

            // 【修改】检查是否有 "人格排泄(完成)" 状态
            // 只有变成了空壳(SSC_PersonalityExcreted_Done)，才能注入新人格
            if (!myPawn.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done))
            {
                yield return new FloatMenuOption(Strings.Message_CannotUseNotHollow, null);
                yield break;
            }

            // 通过检查，返回正常选项
            foreach (var option in base.CompFloatMenuOptions(myPawn))
            {
                yield return option;
            }
        }
    }

    // 2. 使用效果组件 (CompUseEffect 的子类)
    // 负责触发 InheritEverything
    public class CompUseEffect_AbsorbPersonality : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            var storeComp = parent.TryGetComp<CompPersonalityStore>();
            if (storeComp != null)
            {
                bool success = ExcretionUtility.InheritEverything(usedBy, storeComp);
                if (!success)
                {
                    Messages.Message("SSC_Message_PersonalityInsertFailed".Translate(), usedBy, MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            else
            {
                Log.Error("[PersonalityExcretion] Used item has no personality data!");
            }
        }
    }
}
