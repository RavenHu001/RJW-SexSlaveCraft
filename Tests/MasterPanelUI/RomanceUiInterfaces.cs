// AI 辅助生成：近似字体/Listing 几何模型；业务查询与提交使用生产 Resolver/Preferences，不模拟屏幕像素。
using SexSlaveCraft;
using UnityEngine;

namespace Verse
{
    /// <summary>模拟 Listing.Begin 的相对坐标分组；旧面板无分组时保持原坐标。</summary>
    public static class RomanceUiOrigins
    {
        public static readonly Stack<Rect> Stack = new();
        /// <summary>将局部控件矩形换算为外层坐标，供包含/重叠断言使用。</summary>
        public static Rect Transform(Rect rect)
        { foreach (var origin in Stack) { rect.x += origin.x; rect.y += origin.y; } return rect; }
    }
    /// <summary>只模拟列表推进和分组，实际板块标题、行高及边框由生产绘制器计算。</summary>
    public class Listing_Standard
    {
        public bool maxOneColumn;
        public float CurHeight, ColumnWidth;
        /// <summary>建立当前列表坐标组，等价于本测试所需的原生分组边界。</summary>
        public void Begin(Rect rect) { ColumnWidth = rect.width; CurHeight = 0; RomanceUiOrigins.Stack.Push(rect); }
        /// <summary>成对关闭列表坐标组，便于验证异常释放。</summary>
        public void End() => RomanceUiOrigins.Stack.Pop();
        /// <summary>只推进列表游标，不计算生产内容高度。</summary>
        public Rect GetRect(float height) { var rect = new Rect(0, CurHeight, ColumnWidth, height); CurHeight += height; return rect; }
        /// <summary>通过既有近似字体边界记录标签。</summary>
        public void Label(string text) => Widgets.Label(GetRect(Text.CalcHeight(text, ColumnWidth)), text);
        /// <summary>模拟普通留白的游标推进。</summary>
        public void Gap(float height = 12f) => CurHeight += height;
        /// <summary>分隔线只模拟空间占用，不渲染像素。</summary>
        public void GapLine(float height = 12f) => CurHeight += height;
    }
    public static partial class Widgets
    {
        public static int RomanceCheckboxIndex, RomanceClick = -1;
        public static int RomanceButtonIndex, RomanceButtonClick = -1;
        public static Action BeforeRomanceButton;
        public static int TargetIndex, TargetClick = -1;
        public static Action BeforeTargetButton;
        /// <summary>仅记录生产灰底禁用命令区域；不执行图形渲染或命令点击。</summary>
        public static void DrawBoxSolid(Rect rect, Color color)
            => Items.Add(new DrawItem(color.r == .24f && color.g == .24f && color.b == .24f ? "romance-button" : "romance-surface", RomanceUiOrigins.Transform(rect), "", !GUI.enabled));
        /// <summary>记录生产框线矩形，不修改其尺寸。</summary>
        public static void DrawBox(Rect rect) => Items.Add(new DrawItem("romance-frame", RomanceUiOrigins.Transform(rect)));
        /// <summary>记录按钮几何与外层输入状态，本宿主不打开实际窗口。</summary>
        public static bool ButtonText(Rect rect, string text)
        {
            Items.Add(new DrawItem("romance-button", RomanceUiOrigins.Transform(rect), text, !GUI.enabled));
            var callback = BeforeRomanceButton; BeforeRomanceButton = null; callback?.Invoke();
            return RomanceButtonIndex++ == RomanceButtonClick;
        }
        /// <summary>仅从复选框注入点击，避免标签热区在同一模型事件再次翻转。</summary>
        public static bool ButtonInvisible(Rect rect)
        {
            if (Items.LastOrDefault()?.Kind != "romance-target") return false;
            var callback = BeforeTargetButton; BeforeTargetButton = null; callback?.Invoke();
            return TargetIndex++ == TargetClick;
        }
        /// <summary>按指定行注入点击，生产绘制器负责禁用保护及原上下文提交。</summary>
        public static void Checkbox(float x, float y, ref bool value, float size, bool disabled)
        {
            Items.Add(new DrawItem("romance-checkbox", RomanceUiOrigins.Transform(new Rect(x,y,size,size)), null, disabled));
            BeforeCheckbox?.Invoke();
            if (RomanceCheckboxIndex++ == RomanceClick && !disabled && GUI.enabled) value = !value;
        }
    }
    public static class Find { public static readonly UiWindowStack WindowStack = new(); }
    public sealed class UiWindowStack
    {
        /// <summary>窗口投递属于外部引擎边界，不执行弹窗逻辑。</summary>
        public void Add(object window) { }
    }
}
namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    { public SSCRomanceConfig romanceConfig; public object restrictionConfig; internal string romanceAuthorizationId = Guid.NewGuid().ToString("N"); }
    // 链/身份查询只是健康模型边界；真实阶段解析与链变化回归在原生宿主。
    public sealed class Hediff_ChainOfSexSlave : Verse.Hediff { public Verse.Pawn Master; public Verse.Pawn LinkedPawn => Master; public int Stage; }
    public static partial class SSCIdentityUtility
    {
        /// <summary>为生产解析器提供明确身份查询的模型输入。</summary>
        public static bool IsSexSlave(Verse.Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Slave;
        /// <summary>阶段是当前夹具输入，实际原版健康阶段阈值由原生宿主验证。</summary>
        public static int GetSexSlaveStageFromChain(Hediff_ChainOfSexSlave chain) => chain?.Stage ?? 0;
    }
    public static partial class SSCBondUtility
    {
        /// <summary>返回夹具实际健康列表中的链，不在读取时补链。</summary>
        public static Hediff_ChainOfSexSlave GetChain(Verse.Pawn pawn) => pawn?.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().FirstOrDefault();
    }
    public static class SSCRestrictionResolver
    {
        /// <summary>旧模块只提供本测试需要的外部展示资格。</summary>
        public static bool IsApplicable(Verse.Pawn pawn) => pawn?.BoundMaster != null;
    }
    public static class SSCSelfTrainingEligibility
    {
        /// <summary>此界面夹具不创建自我调教资格。</summary>
        public static bool IsEligible(Verse.Pawn pawn) => false;
    }
    /// <summary>旧性行为面板作为外部内容边界，只记录固定内容；用于验证提前返回不吞掉浪漫板块。</summary>
    public static class SSCRestrictionUI
    {
        /// <summary>旧板块提前返回，检查调用方仍能绘制第二板块。</summary>
        public static void DrawPawn(Verse.Listing_Standard listing, Verse.Pawn pawn, bool compact = false) { listing.Label("SEX-CONTENT"); }
    }
    public static class Dialog_SSCRestrictions
    {
        /// <summary>只记录旧开关属于性行为板块，不模拟全局设置编辑。</summary>
        public static void DrawGlobalSwitches(Verse.Listing_Standard listing) => listing.Label("SEX-GLOBALS");
    }
    public sealed class SSCRomanceCommandContext
    {
        public Verse.Pawn Actor;
        public object Token;
        public SSCRomanceAction Action;
        /// <summary>切换行为保留模型中的原执行者凭据；实际目标授权由原生宿主验证。</summary>
        public SSCRomanceCommandContext ForTarget(Verse.Pawn target, SSCRomanceAction action)
            => new() { Actor = Actor, Token = Token, Action = action };
    }
    public static class SSCRomanceCommandMenu
    {
        public static readonly List<SSCRomanceCommandContext> Opened = new();
        /// <summary>只记录生产 UI 传出的原上下文，不模拟真实菜单或任务。</summary>
        public static void OpenTargets(SSCRomanceCommandContext context) => Opened.Add(context);
    }
    /// <summary>任务服务是 UI 外部边界，真实任务签发/门禁由 RomanceControlNative 验证。</summary>
    public static class SSCRomanceCommandUtility
    {
        /// <summary>模型仅按链阶段决定按钮资格，实际身份／绑定／补丁门禁由原生专项验证。</summary>
        public static string ActorFailure(Verse.Pawn pawn)
            => SSCBondUtility.GetChain(pawn)?.Stage >= 4 ? null : "CommandStage";
        /// <summary>只捕获原人格 token，验证生产按钮不在点击后重新采集。</summary>
        public static SSCRomanceCommandContext Capture(Verse.Pawn pawn)
            => new() { Actor = pawn, Token = SSCPersonalityRuntimeUtility.Capture(pawn) };
        /// <summary>直接读取当前四语文本，不另复制生产原因文案。</summary>
        public static string Explain(string reason) => Verse.Translation.Translate("SSC_Romance_" + reason);
    }
}

namespace SexSlaveCraft
{
    /// <summary>持续思想刷新为界面宿主的外部边界；实际思想及缓存由原生专项验证。</summary>
    public static class SSCRomanceMoodRefresh
    { public static void Notify(Verse.Pawn pawn) { } }
}
