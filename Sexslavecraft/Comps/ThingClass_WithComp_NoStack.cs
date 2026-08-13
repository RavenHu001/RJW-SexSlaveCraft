using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // 1. 继承 ThingWithComps 以保留组件功能
    public class SSC_PS_NoStack : ThingWithComps
    {
        // 2. 重写 CanStackWith 方法
        public override bool CanStackWith(Thing other)
        {
            return false;
        }
        public override void DrawGUIOverlay()
        {
        }
    }
}