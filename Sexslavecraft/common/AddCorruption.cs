using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public class CorruptionUtility  //一个增加堕落值的组件  A component that increases corruption value
    {
        public static void AddCorruption(Pawn pawn, float amount)
        {
            var need = pawn.needs.TryGetNeed<Need_Corruption>();
            if (need != null)
            {
                need.SetCorruption(need.CurLevel + amount);
            }
        }
    }

}
