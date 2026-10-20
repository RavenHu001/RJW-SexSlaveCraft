// AI 辅助生成：只记录显示请求，不替代药效判断；实际视觉工具由 PnaWeaponEffects 宿主执行。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCPnaWeaponEffectsUtility
    {
        internal static readonly List<Pawn> Absorptions = new List<Pawn>();
        internal static void ShowAbsorption(Pawn pawn) => Absorptions.Add(pawn);
    }
}
