// AI 辅助生成：运行实际通用框架及外观 setter，渲染和 Scribe 边界沿用本专项。
using System;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>仅补充第二批接线边界；既有旧档迁移、配色、缓存及线程回归继续独立执行。</summary>
    private static void AbilityToggleFrameworkTests()
    {
        const string id = SSCAbilityToggleUtility.GelRendering;
        Run("渲染注册项沿用外观字段且不写人格键／健康标记", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p);
            Check(SSCAbilityToggleUtility.Definitions.Single(d => d.Group == SSCAbilityToggleGroup.Appearance).Id == id, "注册来源唯一");
            // 字典故意与身体默认冲突；公共入口只能沿原外观字段解释并保存选择。
            p.Training.abilityToggles.Set(id, false);
            Check(SSCAbilityToggleUtility.GetEnabled(p, id), "人格同名键不能覆盖身体默认");
            var view = SSCAbilityToggleUtility.GetView(p, id); int count = p.health.hediffSet.hediffs.Count;
            Check(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && !c.RenderingEnabled, "写原身体组件");
            Check(p.health.hediffSet.hediffs.Count == count && SSCAbilityToggleUtility.Find(id).MarkerDefName == null, "无额外健康记录");
            SSCAbilityToggleUtility.ClearPersonalityState(p); Check(!c.RenderingEnabled, "清人格不清身体外观");
        });
        foreach (PawnIdentity identity in new[] { PawnIdentity.Unset, PawnIdentity.Master, PawnIdentity.Slave })
            Run("渲染不增加身份／空壳／研究门槛 " + identity, () =>
            {
                var p = new Pawn(); p.Training.pawnIdentity = identity; var c = AppearanceOwner(p);
                p.health.hediffSet.hediffs.Add(new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done, pawn = p });
                var view = SSCAbilityToggleUtility.GetView(p, id);
                Check(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && !c.RenderingEnabled, "保留身体控制资格");
            });
        Run("无训练组件的凝胶身体仍能编辑，不补建组件", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); p.Training = null;
            Widgets.Toggle = true; ITab_SexSlaveTraining.DrawGel(p, 320);
            Check(!c.RenderingEnabled && p.Training == null, "原外观操作不要求训练组件");
        });
        Run("缺外观组件仍按原开启回退但不可提交且不补建", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); c.parent.comps.Clear(); var view = SSCAbilityToggleUtility.GetView(p, id);
            Check(view.Visible && view.Enabled && !view.CanChange, "读取与可写分别判断");
            Check(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && c.parent.comps.Count == 0, "不补组件");
        });
        Run("通用渲染提交沿用目标刷新／Revision与同值幂等", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); var other = new Pawn(); AppearanceOwner(other);
            var view = SSCAbilityToggleUtility.GetView(p, id); int dirty = p.Drawer.renderer.Dirty;
            Check(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _), "关闭成功");
            Check(!FullGelAppearanceUtility.ShouldRender(p) && FullGelAppearanceUtility.ShouldRender(other), "原快照准入即时更新");
            // 同值提交允许成功，但必须复用原快照／刷新幂等路径，不能再次刷新本人或波及他人。
            Check(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _), "同值成功");
            Check(p.Drawer.renderer.Dirty == dirty + 1 && other.Drawer.renderer.Dirty == 0, "只刷新目标一次");
            Check(!p.Training.abilityToggles.HasEntries, "不迁移到字典");
        });
        Run("同组件换人格后的旧凝胶UI请求被拒绝", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); var draw = ITab_SexSlaveTraining.CaptureGel(p, 320);
            Check(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope), "进入切换"); using (scope) { }
            Widgets.Toggle = true; draw(); Check(c.RenderingEnabled, "旧人格凭据失效");
        });
        Run("换训练组件后的旧凝胶请求被拒绝", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); var view = SSCAbilityToggleUtility.GetView(p, id);
            p.Training = new CompSexSlaveTraining();
            Check(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && c.RenderingEnabled, "保留原组件凭据");
        });
        Run("通用入口保留凝胶主线程与加载期限制", () =>
        {
            var p = new Pawn(); var c = AppearanceOwner(p); var view = SSCAbilityToggleUtility.GetView(p, id);
            UnityData.IsInMainThread = false;
            try { Check(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && c.RenderingEnabled, "后台拒绝"); }
            finally { UnityData.IsInMainThread = true; }
            Scribe.mode = LoadSaveMode.LoadingVars;
            try { Check(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _) && c.RenderingEnabled, "加载拒绝"); }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
        });
    }
}
