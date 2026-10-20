// AI 辅助生成：只提供一次护甲结果／最终伤势写入的边界；真实护甲概率另由原生专项验证。
using System;
using Verse;
namespace Verse
{
    // 两个开关分别隔离最终伤势写入与 IncomingDamageFactor，验证无伤口不等于未穿透。
    public partial class Pawn { public float IncomingFactor=1f; public bool SuppressInjury; }
    public partial class PawnDrawer { public void Notify_DamageApplied(DamageInfo info) { } public void Notify_DamageDeflected(DamageInfo info) { } }
    public static class DebugSettings { public static bool enablePlayerDamage=true; }
    public static class SpacerStatBoundary { public static float GetStatValue(this Pawn pawn, RimWorld.StatDef stat)=>pawn.IncomingFactor; }
    // 委托只给出选定的护甲结果，Calls 观察是否重复判甲；不在替身重写原版随机算法。
    public static class ArmorUtility
    {
        public delegate float ArmorResult(Pawn pawn, float amount, ref DamageDef def);
        public static ArmorResult Boundary;
        public static int Calls;
        public static float GetPostArmorDamage(Pawn pawn,float amount,float penetration,BodyPartRecord part,ref DamageDef def,out bool metalDeflect,out bool metalDiminish)
        { Calls++;metalDeflect=false;metalDiminish=false;return Boundary==null?amount:Boundary(pawn,amount,ref def); }
    }
}
namespace RimWorld { public static class StatDefOf { public static StatDef IncomingDamageFactor=new(); } }
