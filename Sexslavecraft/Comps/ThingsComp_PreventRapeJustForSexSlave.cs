using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
        public class CompSSRapeCheck : ThingComp
        {
            // 这个 comp 用于标记该物品是否使得 pawn 跳过链奴检查
        }

        public class CompProperties_SSRapeCheck : CompProperties
        {
            public CompProperties_SSRapeCheck()
            {
                this.compClass = typeof(CompSSRapeCheck);
            }
        }
}
