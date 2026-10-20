// AI 辅助生成：该原生宿主只隔离 SSC 身份／暴露和地图写入，实际护甲与近战 API 来自游戏 DLL。
using Verse;
namespace SexSlaveCraft
{
    public struct SSCPnaExposureResult { public bool Accepted, Rooted; }
    public enum SSCPnaExposureSource { Weapon }
    public static class SSCIdentityUtility { public static bool IsMaster(Pawn pawn)=>Program.Masters.Contains(pawn); }
    public static class SSCPersonalityRuntimeUtility
    { public static object Capture(Pawn pawn)=>pawn; public static bool IsCurrent(Pawn pawn, object token)=>ReferenceEquals(pawn,token); public static bool IsMutating(Pawn pawn)=>false; }
    // 原生专项仅观察处理器是否正确进入共享输入；完整负荷、成瘾、快照回调和
    // 人格变化由 PnaLoad 链接真实公共工具验证，不能由这里的恒定凭据替身证明。
    public static class SSCPnaExposureUtility
    {
        public static bool IsValidTarget(Pawn pawn)=>pawn!=null&&Program.Valid.Contains(pawn);
        public static bool IsRooted(Pawn pawn)=>Program.Rooted.Contains(pawn);
        public static void RefreshBodyState(Pawn pawn) { }
        public static SSCPnaExposureResult ApplyExposure(Pawn pawn,float dose,SSCPnaExposureSource source,Func<bool> guard)
        { if(!guard())return default;Program.Doses.Add(dose);return new SSCPnaExposureResult{Accepted=true,Rooted=IsRooted(pawn)}; }
    }
    // 不生成地图视觉；原有真实紫心工具继续由武器特效专项验证。
    internal static class SSCPnaWeaponEffectsUtility { public static void ShowAbsorption(Pawn pawn) { } }
    // 仅供真实解析器识别旧入口；真实旧入口及其暴露已由 PnaLoad 宿主执行。
    public class DamageWorker_SSCPnaCoated : DamageWorker_AddInjury { }
    public class SSCPnaCoatingExtension : DefModExtension { public float baseLoad; public HediffDef recordHediff; }
}
