using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private const string HostIconPath = "UI/Icons/RitualRole_Host";
    private const string TargetIconPath = "UI/Icons/RitualRole_Target";

    private static void PaintUi(Fixture f, Action paint)
    {
        f.Window.Widget.UiDraw = () =>
        {
            var scroll = new Vector2();
            Widgets.BeginScrollView(new Rect(0, 0, 300, 400), ref scroll, new Rect(0, 0, 300, 1000));
            try { paint(); }
            finally { Widgets.EndScrollView(); }
        };
        f.Window.Draw();
    }

    private static bool PaintPortrait(Fixture f, Pawn pawn, Rect rect)
    {
        float x = rect.xMax, y = rect.yMax;
        PawnPortraitIconsDrawer.DrawPawnPortraitIcons(rect, pawn, false, false, ref x, ref y, 20f, true, out bool iconTip);
        if (!iconTip && !DragAndDropWidget.Dragging)
            TooltipHandler.TipRegion(rect, f.Window.Widget.ExtraTipContents(pawn));
        return iconTip;
    }

    private static void PaintSlot(RitualRole role, Rect rect)
        => DragAndDropWidget.DropArea(0, rect, _ => { }, new[] { role }.GroupBy(r => r.id).First());

    private static void ResetUi()
    { UiRecorder.Reset(); PawnPortraitIconsDrawer.NativeIconHovered = false; }

    private static void RunVisualTests()
    {
        foreach (string status in new[] { "殖民者", "原版奴隶", "囚犯" })
        foreach (bool sscSlave in new[] { false, true })
            Run($"目标角标按 SSC 性奴身份过滤：{status}, SSC性奴={sscSlave}", () =>
            {
                ResetUi(); var f = new Fixture();
                var candidate = new Pawn { SexSlave = sscSlave, IsColonist = status == "殖民者",
                    IsSlave = status == "原版奴隶", IsPrisonerOfColony = status == "囚犯" };
                f.Map.Pawns.Add(candidate); f.Open();
                var spectators = f.A.SpectatorsForReading.ToArray(); int writes = f.A.Writes;
                Event.current.mousePosition = new Vector2(40, 40); Counters.Reset();
                PaintUi(f, () => PaintPortrait(f, candidate, new Rect(0, 0, 50, 50)));
                Assert(UiRecorder.Marks.Any(m => m.Kind == "role-icon" && m.Text == TargetIconPath) == sscSlave);
                Assert(UiRecorder.Marks.Any(m => m.Kind == "tip" && m.Text.Contains("SSC_RitualSelection_TargetCandidate")) == sscSlave);
                Assert(UiRecorder.Marks.Count(m => m.Kind == "native-icons") == 1 && f.A.Writes == writes);
                Assert(f.A.SpectatorsForReading.SequenceEqual(spectators) && Counters.Messages == 0);
                Assert(Counters.Eligibility == (sscSlave ? 1 : 0) && Counters.Reach == (sscSlave ? 1 : 0));
            });
        Run("SSC 主人仅显示主持角标和主持候选提示", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); Event.current.mousePosition = new Vector2(40, 40);
            PaintUi(f, () => PaintPortrait(f, f.Host, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Count(m => m.Kind == "badge") == 1);
            Assert(UiRecorder.Marks.Any(m => m.Kind == "role-icon" && m.Text == HostIconPath));
            Assert(!UiRecorder.Marks.Any(m => m.Kind == "tip" && m.Text.Contains("SSC_RitualSelection_TargetCandidate")));
        });
        foreach (bool forced in new[] { false, true })
            Run($"已选目标撤销 SSC 性奴身份立即隐藏标签和勾选，保留分配：强制角色={forced}", () =>
            {
                ResetUi(); var f = new Fixture(); f.Open(); f.SelectBoth();
                if (forced) f.A.ForcedRolesForReading["slave"] = f.Target;
                Event.current.mousePosition = new Vector2(40, 40);
                PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
                Assert(UiRecorder.Marks.Any(m => m.Kind == "badge") && UiRecorder.Marks.Any(m => m.Kind == "check"));
                var spectators = f.A.SpectatorsForReading.ToArray(); int writes = f.A.Writes;
                f.Target.SexSlave = false; Time.realtimeSinceStartup = 0.1f; UiRecorder.Marks.Clear(); Counters.Reset();
                PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
                Assert(!UiRecorder.Marks.Any(m => m.Kind == "badge" || m.Kind == "check" || m.Kind == "border"));
                Assert(!UiRecorder.Marks.Any(m => m.Kind == "tip" && m.Text.Contains("SSC_RitualSelection_TargetCandidate")));
                Assert(f.A.FirstAssignedPawn("slave") == f.Target && f.A.FirstAssignedPawn("master") == f.Host);
                Assert(f.A.Writes == writes && f.A.SpectatorsForReading.SequenceEqual(spectators));
                Assert(f.Target.BoundMaster == f.Host && f.Target.AssignedTrainer == f.Host);
                Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Messages == 0);
                f.Target.SexSlave = true; UiRecorder.Marks.Clear();
                PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
                Assert(UiRecorder.Marks.Any(m => m.Kind == "badge") && UiRecorder.Marks.Any(m => m.Kind == "check"));
            });
        Run("没有目标标签的非性奴不能拖放选入或启动仪式", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.SexSlave = false; f.Open(); Assert(f.HostSlot());
            Counters.Reset(); DragAndDropWidget.Dragging = true; DragAndDropWidget.Dragged = f.Target;
            Event.current.mousePosition = new Vector2(80, 30);
            PaintUi(f, () => PaintSlot(f.TargetRole, new Rect(0, 0, 150, 70)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.r > m.Color.g));
            Assert(Counters.Eligibility == 0 && f.A.FirstAssignedPawn("slave") == null);
            DragAndDropWidget.Dragging = false;
            Assert(!f.TargetSlot() && !f.Window.CanBegin);
            Assert(f.A.FirstAssignedPawn("slave") == null && !f.Target.SexSlave);
        });
        Run("实际绘制钩子：1000 人角色角标重绘不调用 RJW 或寻路", () =>
        {
            ResetUi(); var f = new Fixture(998); f.Open(); Counters.Reset();
            for (int frame = 0; frame < 3; frame++)
                PaintUi(f, () =>
                {
                    for (int i = 0; i < f.Map.Pawns.Count; i++)
                        PaintPortrait(f, f.Map.Pawns[i], new Rect(i % 5 * 54, i / 5 * 72, 50, 50));
                });
            Assert(UiRecorder.Marks.Any(m => m.Kind == "badge") && UiRecorder.Marks.Count(m => m.Kind == "native-icons") == 3000);
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Permissions == 0 && Counters.Messages == 0);
            Assert(BindingRitualSelectionVisuals.Active == null);
        });
        Run("常驻角标保留身体未通过的目标候选与普通观众头像", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Eligible = false; f.Open();
            var audience = new Pawn { HasComp = false }; int writes = f.A.Writes;
            PaintUi(f, () => { PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)); PaintPortrait(f, audience, new Rect(60, 0, 50, 50)); });
            Assert(UiRecorder.Marks.Count(m => m.Kind == "badge") == 1 && UiRecorder.Marks.Count(m => m.Kind == "native-icons") == 2);
            Assert(Counters.Eligibility == 0 && f.A.Writes == writes && f.A.SpectatorsForReading.Contains(f.Target));
        });
        Run("鞭子与项圈使用不同资产，双图标同一行并避开勾选和文化图标", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Trainer = true; f.Open();
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            var badges = UiRecorder.Marks.Where(m => m.Kind == "badge").ToArray();
            var icons = UiRecorder.Marks.Where(m => m.Kind == "role-icon").ToArray();
            Assert(badges.Length == 2 && icons.Length == 2);
            Assert(icons[0].Text == HostIconPath && icons[1].Text == TargetIconPath && icons[0].Text != icons[1].Text);
            Assert(badges[0].Rect.y == badges[1].Rect.y && badges[0].Rect.xMax < badges[1].Rect.x);
            Assert(badges.All(m => m.Rect.width <= 16f && m.Rect.height <= 16f && m.Rect.yMax <= 17f && m.Rect.xMax <= 40f));
            Assert(icons.All(m => m.Rect.width <= 14f && m.Rect.height <= 14f));
            Assert(!UiRecorder.Marks.Any(m => m.Kind == "label"));
        });
        Run("仅有目标资格时项圈保留第一角标位置", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open();
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(10, 20, 50, 50)));
            var badge = UiRecorder.Marks.Single(m => m.Kind == "badge");
            var icon = UiRecorder.Marks.Single(m => m.Kind == "role-icon");
            Assert(badge.Rect.x == 12f && badge.Rect.y == 21f && icon.Text == TargetIconPath);
            Assert(icon.Rect.x >= badge.Rect.x && icon.Rect.xMax <= badge.Rect.xMax);
        });
        foreach (float portraitWidth in new[] { 20f, 28f, 40f })
            Run($"窄头像双图标缩小且保持头像与勾选边界：宽{portraitWidth}", () =>
            {
                ResetUi(); var f = new Fixture(); f.Target.Trainer = true; f.Open();
                PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(10, 20, portraitWidth, 50)));
                var badges = UiRecorder.Marks.Where(m => m.Kind == "badge").ToArray();
                var icons = UiRecorder.Marks.Where(m => m.Kind == "role-icon").ToArray();
                Assert(badges.Length == 2 && icons.Length == 2);
                Assert(badges[0].Rect.y == badges[1].Rect.y && badges[0].Rect.xMax < badges[1].Rect.x);
                Assert(badges.All(m => m.Rect.width > 0f && m.Rect.width <= 16f && m.Rect.height <= 16f
                    && m.Rect.x >= 10f && m.Rect.xMax <= 10f + portraitWidth - 10f));
                for (int i = 0; i < icons.Length; i++)
                    Assert(icons[i].Rect.width > 0f && icons[i].Rect.height > 0f && icons[i].Rect.x >= badges[i].Rect.x
                        && icons[i].Rect.xMax <= badges[i].Rect.xMax && icons[i].Rect.y >= badges[i].Rect.y
                        && icons[i].Rect.yMax <= badges[i].Rect.yMax);
            });
        foreach (bool hostHovered in new[] { true, false })
            Run($"双资格图标独立显示对应角色提示：主持={hostHovered}", () =>
            {
                ResetUi(); var f = new Fixture(); f.Target.Trainer = true; f.Open();
                PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
                var badge = UiRecorder.Marks.Where(m => m.Kind == "badge").ElementAt(hostHovered ? 0 : 1).Rect;
                UiRecorder.Marks.Clear(); Counters.Reset();
                Event.current.mousePosition = new Vector2(badge.x + badge.width / 2f, badge.y + badge.height / 2f);
                bool active = false;
                PaintUi(f, () => active = PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
                string expected = hostHovered ? "SSC_RitualSelection_HostCandidate" : "SSC_RitualSelection_TargetCandidate";
                Assert(active && UiRecorder.Marks.Count(m => m.Kind == "tip") == 1);
                Assert(UiRecorder.Marks.Single(m => m.Kind == "tip").Text.StartsWith(expected));
                Assert(!UiRecorder.Marks.Any(m => m.Kind == "label"));
            });
        Run("配对不兼容显示斜线但不移除目标观众", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            var target = new Pawn { SexSlave = true, AssignedTrainer = new Pawn { Master = true } };
            Assert(f.A.TryAssignSpectate(target)); Counters.Reset();
            PaintUi(f, () => PaintPortrait(f, target, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "slash") && f.A.SpectatorsForReading.Contains(target));
            var icon = UiRecorder.Marks.Single(m => m.Kind == "role-icon");
            Assert(icon.Text == TargetIconPath && icon.Color.r == icon.Color.g && icon.Color.g == icon.Color.b);
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && Counters.Messages == 0);
        });
        Run("角标悬停取代原头像提示并显示完整身体拒绝", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Eligible = false; f.Open(); Assert(f.HostSlot()); Counters.Reset();
            Event.current.mousePosition = new Vector2(5, 5);
            bool active = false;
            PaintUi(f, () => active = PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(active && UiRecorder.Marks.Any(m => m.Kind == "tip" && m.Text.StartsWith("SSC_RitualSelection_TargetCandidate")
                && m.Text.Contains("SSC_RitualSelection_CannotAssign")));
            Assert(!UiRecorder.Marks.Any(m => m.Kind == "tip" && m.Text.StartsWith("native")));
            Assert(Counters.Eligibility == 1 && UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.r > m.Color.g));
        });
        Run("双角色悬停按角色缓存完整查询，重绘不重复寻路", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Trainer = true; f.Open(); Assert(f.HostSlot()); Counters.Reset();
            Event.current.mousePosition = new Vector2(40, 40);
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            int eligibility = Counters.Eligibility, reach = Counters.Reach;
            Assert(eligibility == 1 && reach == 3);
            Time.realtimeSinceStartup = 0.1f;
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(Counters.Eligibility == eligibility && Counters.Reach == reach);
            Time.realtimeSinceStartup = 0.4f;
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(Counters.Eligibility == 2 && Counters.Reach == 6);
        });
        Run("尚未选另一主角时，普通头像悬停仍提前检查个人身体条件", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Eligible = false; f.Open(); Counters.Reset();
            Event.current.mousePosition = new Vector2(40, 40);
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.r > m.Color.g));
            Assert(Counters.Eligibility == 1 && Counters.Reach == 1 && Counters.Permissions == 0);
            Assert(f.A.FirstAssignedPawn("master") == null && f.A.FirstAssignedPawn("slave") == null);
        });
        Run("悬停显示缓存不能让实际提交使用过时的身体通过结果", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); Assert(f.HostSlot());
            Event.current.mousePosition = new Vector2(40, 40);
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.g > m.Color.r));
            f.Target.Eligible = false; Time.realtimeSinceStartup = 0.1f; Counters.Reset();
            Assert(!f.TargetSlot() && Counters.Eligibility == 1 && f.A.FirstAssignedPawn("slave") == null);
            Time.realtimeSinceStartup = 0.4f; UiRecorder.Marks.Clear();
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.r > m.Color.g));
        });
        Run("角色改变立即刷新配对角标，未等待显示缓存周期", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open();
            var other = new Pawn { Master = true };
            PaintUi(f, () => PaintPortrait(f, other, new Rect(0, 0, 50, 50)));
            Assert(!UiRecorder.Marks.Any(m => m.Kind == "slash"));
            Assert(f.TargetSlot()); UiRecorder.Marks.Clear();
            PaintUi(f, () => PaintPortrait(f, other, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "slash"));
        });
        Run("拖拽完整拒绝在滚动退出前画红框并更新原版即时浮窗", () =>
        {
            ResetUi(); var f = new Fixture(1); f.Open(); f.SelectBoth();
            var candidate = f.Map.Pawns[2]; candidate.BoundMaster = f.Host; candidate.Eligible = false;
            Counters.Reset(); DragAndDropWidget.Dragging = true; DragAndDropWidget.Dragged = candidate; DragAndDropWidget.HoverPawn = f.Target;
            Event.current.mousePosition = new Vector2(80, 30);
            PaintUi(f, () => { PaintSlot(f.TargetRole, new Rect(0, 0, 150, 70)); UiRecorder.Add("native-slot", default); });
            var order = UiRecorder.Marks.Select(m => m.Kind).ToArray();
            Assert(Array.IndexOf(order, "native-slot") < Array.IndexOf(order, "border") && Array.IndexOf(order, "border") < Array.IndexOf(order, "end"));
            Assert(Counters.Eligibility == 1 && Counters.Messages == 0 && f.A.FirstAssignedPawn("slave") == f.Target);
            Assert(Find.WindowStack.Window != null && BindingRitualSelectionVisuals.Active == null);
            Find.WindowStack.Window();
            Assert(UiRecorder.Marks.Any(m => m.Kind == "label" && m.Text == "SSC_RitualSelection_CannotAssign"));
        });
        Run("普通观众拖拽不执行主角检查且保留原版反馈", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); Counters.Reset();
            DragAndDropWidget.Dragging = true; DragAndDropWidget.Dragged = f.Target; Event.current.mousePosition = new Vector2(80, 30);
            PaintUi(f, () => DragAndDropWidget.DropArea(0, new Rect(0, 0, 150, 70), _ => { }, new object()));
            Assert(Counters.Eligibility == 0 && Counters.Reach == 0 && !UiRecorder.Marks.Any(m => m.Kind == "border"));
        });
        Run("满槽空白拖放显示拒绝，移到现有头像后才预览合法替换", () =>
        {
            ResetUi(); var f = new Fixture(1); f.Open(); f.SelectBoth();
            var candidate = f.Map.Pawns[2]; candidate.BoundMaster = f.Host;
            Counters.Reset(); DragAndDropWidget.Dragging = true; DragAndDropWidget.Dragged = candidate;
            Event.current.mousePosition = new Vector2(80, 30);
            PaintUi(f, () => PaintSlot(f.TargetRole, new Rect(0, 0, 150, 70)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.r > m.Color.g) && Counters.Eligibility == 0);
            DragAndDropWidget.HoverPawn = f.Target; UiRecorder.Marks.Clear();
            PaintUi(f, () => PaintSlot(f.TargetRole, new Rect(0, 0, 150, 70)));
            Assert(UiRecorder.Marks.Any(m => m.Kind == "border" && m.Color.g > m.Color.r) && Counters.Eligibility == 1);
            Assert(f.A.FirstAssignedPawn("slave") == f.Target && Counters.Messages == 0);
        });
        Run("原文化图标的专用悬停提示保持优先，不做完整查询", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); Assert(f.HostSlot()); Counters.Reset();
            PawnPortraitIconsDrawer.NativeIconHovered = true; Event.current.mousePosition = new Vector2(45, 45);
            bool active = false; PaintUi(f, () => active = PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(active && Counters.Eligibility == 0 && Counters.Reach == 0);
        });
        Run("已分配勾选与绘制颜色恢复", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open(); f.SelectBoth();
            GUI.color = new Color(0.2f, 0.3f, 0.4f); Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft;
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            Assert(UiRecorder.Marks.Count(m => m.Kind == "check") == 1 && GUI.color.r == 0.2f && GUI.color.g == 0.3f && GUI.color.b == 0.4f);
            var badge = UiRecorder.Marks.Single(m => m.Kind == "badge").Rect;
            var check = UiRecorder.Marks.Single(m => m.Kind == "check").Rect;
            Assert(badge.xMax <= check.x && check.width == 8f && check.height == 8f);
            Assert(Text.Font == GameFont.Small && Text.Anchor == TextAnchor.UpperLeft);
        });
        Run("主持金色、目标紫色且图标绘制恢复完整 GUI 颜色", () =>
        {
            ResetUi(); var f = new Fixture(); f.Target.Trainer = true; f.Open();
            GUI.color = new Color(0.2f, 0.3f, 0.4f, 0.6f);
            PaintUi(f, () => PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)));
            var host = UiRecorder.Marks.Single(m => m.Kind == "role-icon" && m.Text == HostIconPath).Color;
            var target = UiRecorder.Marks.Single(m => m.Kind == "role-icon" && m.Text == TargetIconPath).Color;
            Assert(host.r > host.g && host.g > host.b && target.b > target.r && target.r > target.g);
            Assert(GUI.color.r == 0.2f && GUI.color.g == 0.3f && GUI.color.b == 0.4f && GUI.color.a == 0.6f);
        });
        Run("嵌套滚动视图不装饰无关头像或角色槽", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open();
            PaintUi(f, () =>
            {
                var scroll = new Vector2(); Widgets.BeginScrollView(default, ref scroll, default);
                PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50)); Widgets.EndScrollView();
                PaintPortrait(f, f.Host, new Rect(60, 0, 50, 50));
            });
            Assert(UiRecorder.Marks.Count(m => m.Kind == "badge") == 1);
        });
        Run("绘制异常、普通仪式和作用域外头像不残留 SSC 装饰", () =>
        {
            ResetUi(); var f = new Fixture(); f.Open();
            ExpectThrow(() => PaintUi(f, () => { throw new InvalidOperationException("draw"); }));
            Assert(BindingRitualSelectionVisuals.Active == null);
            PaintPortrait(f, f.Target, new Rect(0, 0, 50, 50));
            var normal = new Fixture(binding: false); normal.Open();
            PaintUi(normal, () => PaintPortrait(normal, normal.Target, new Rect(0, 0, 50, 50)));
            Assert(!UiRecorder.Marks.Any(m => m.Kind == "badge") && Counters.Messages == 0);
        });
    }
}
