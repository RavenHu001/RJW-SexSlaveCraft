using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed, failed;
    private sealed class Fixture
    {
        public Pawn Host = new Pawn { Master = true, LabelShort = "Host" };
        public Pawn Target = new Pawn { LabelShort = "Target" };
        public RitualRole_BindingMaster HostRole = new RitualRole_BindingMaster { id = "master" };
        public RitualRole_BindingSlave TargetRole = new RitualRole_BindingSlave { id = "slave", substitutable = true };
        public Precept_Ritual Ritual = new Precept_Ritual();
        public Map Map = new Map();
        public TargetInfo Spot = new TargetInfo { IsValid = true, Key = 1 };
        public Dialog_BeginRitual Window;
        public RitualRoleAssignments A => Window.Assignments;
        public Fixture(int extras = 0, bool binding = true)
        {
            Target.BoundMaster = Host;
            Target.AssignedTrainer = Host;
            Ritual.behavior.def.defName = binding ? "SSC_BindingRitualBehavior" : "OtherRitual";
            Ritual.behavior.def.roles.Add(HostRole);
            Ritual.behavior.def.roles.Add(TargetRole);
            Map.Pawns.Add(Host); Map.Pawns.Add(Target);
            for (int i = 0; i < extras; i++) Map.Pawns.Add(new Pawn { Eligible = i % 2 == 0 });
            Ritual.behavior.AvailabilityPawns = Map.Pawns;
        }
        public void Open(List<string> notes = null, Pawn selected = null, Dialog_BeginRitual.ActionCallback callback = null)
        {
            Ritual.behavior.CanStartRitualNow(Spot, Ritual);
            Window = new Dialog_BeginRitual("test", Ritual, Spot, Map, callback ?? (assignments =>
            { Ritual.behavior.TryExecuteOn(Spot, Host, Ritual, null, assignments, true); return true; }), extraInfos: notes, selected: selected);
            Window.Open();
        }
        public bool HostSlot(Pawn pawn = null) => Window.Select(pawn ?? Host, new RitualRole[] { HostRole });
        public bool TargetSlot(Pawn pawn = null) => Window.Select(pawn ?? Target, new RitualRole[] { TargetRole });
        public void SelectBoth(bool targetFirst = false)
        {
            if (targetFirst) { Assert(TargetSlot()); Assert(HostSlot()); }
            else { Assert(HostSlot()); Assert(TargetSlot()); }
        }
    }

    private static int Main(string[] args)
    {
        new Harmony("ssc.tests.ritual-selection").PatchAll(typeof(Program).Assembly);
        Console.WriteLine("真实 Harmony 补丁 + 生产选角/资格代码；游戏/RJW 边界为计数模型，不代表实机耗时。");
        foreach (bool mode in new[] { true, false })
        foreach (bool eligible in new[] { true, false })
            Run($"轻量身体查询 mode={mode}, eligible={eligible} 只调用 RJW 一次", () =>
            {
                SSCMod.settings.useRJWOriginalEligibility = mode;
                Assert(Trainjudge.TryCanBeFuckedWithReason(new Pawn { Eligible = eligible }, out _) == eligible);
                Assert(Counters.Eligibility == 1 && Counters.Reports == 0 && Counters.Warnings == 0);
            });
        Run("skipReason 不翻译原因也不生成报告", () =>
        {
            Assert(!Trainjudge.TryCanBeFuckedWithReason(new Pawn { Eligible = false }, out string reason, true));
            Assert(reason == null && Counters.Eligibility == 1 && Counters.Translations == 0 && Counters.Reports == 0);
        });
        Run("原设置门槛保持不变且拒绝时无需调用 RJW", () =>
        {
            rjw.RJWSettings.rape_enabled = false;
            Assert(!Trainjudge.SexSlaveTrainJudge(new Pawn()) && Counters.Eligibility == 0);
            SSCMod.settings.useRJWOriginalEligibility = false;
            Assert(Trainjudge.SexSlaveTrainJudge(new Pawn()) && Counters.Eligibility == 1);
        });
        Run("null 查询和诊断安全返回", () =>
        {
            Assert(!Trainjudge.TryCanBeFuckedWithReason(null, out _, out string report));
            Assert(report != null && !Trainjudge.SexSlaveTrainJudge(null) && Counters.Eligibility == 0);
        });
        Run("显式完整诊断保留内容且只求值一次", () =>
        {
            Assert(Trainjudge.TryCanBeFuckedWithReason(new Pawn(), out _, out string report));
            Assert(Counters.Eligibility == 1 && Counters.Reports == 1 && report.Contains("- rjwpe:") && report.Contains("- reason:"));
        });
        Run("布尔包装不构造报告", () =>
        { Assert(Trainjudge.SexSlaveTrainJudge(new Pawn())); Assert(Counters.Eligibility == 1 && Counters.Reports == 0); });

        foreach (int population in new[] { 20, 100, 300, 1000 })
            Run($"{population} 人开窗、自动填充和高亮不做完整执行者检查", () =>
            {
                var f = new Fixture(population - 2); f.Open(selected: f.Host);
                Assert(f.A.FirstAssignedPawn("master") == null && f.A.FirstAssignedPawn("slave") == null);
                Assert(f.A.SpectatorsForReading.Count == population && !f.Window.CanBegin);
                for (int redraw = 0; redraw < 3; redraw++)
                {
                    f.Window.Draw();
                    foreach (var pawn in f.Map.Pawns)
                    {
                        f.HostRole.AppliesToPawn(pawn, out _, f.Spot, assignments: f.A, skipReason: true);
                        f.TargetRole.AppliesToPawn(pawn, out _, f.Spot, assignments: f.A, skipReason: true);
                    }
                }
                Assert(Counters.Eligibility == 0 && Counters.Permissions == 0 && Counters.Reach == 0 && Counters.Reports == 0 && Counters.Warnings == 0);
            });
        Run("未选执行者时保留原版观众拒绝", () =>
        {
            var f = new Fixture(); f.Map.Pawns.Add(new Pawn { IsPrisonerOfColony = true }); f.Open();
            Assert(f.A.SpectatorsForReading.Count == 2);
        });
        foreach (bool targetFirst in new[] { false, true })
            Run($"两种选人顺序均能组成相同合法组合 targetFirst={targetFirst}", () =>
            {
                var f = new Fixture(); f.Open(); f.SelectBoth(targetFirst);
                Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target && f.Window.CanBegin);
                Assert(f.Target.AssignedTrainer == f.Host && f.Target.BoundMaster == f.Host);
                Assert(Counters.Reports == 0 && Counters.Warnings == 0);
            });
        Run("只选目标时个人资格通过，配对等待另一人", () =>
        {
            var f = new Fixture(); f.Target.AssignedTrainer = f.Target.BoundMaster = null; f.Open();
            Assert(f.TargetSlot() && Counters.Eligibility == 1 && Counters.Permissions == 0);
            Assert(!f.HostSlot() && f.A.FirstAssignedPawn("slave") == f.Target && f.A.FirstAssignedPawn("master") == null);
            Assert(!f.Window.Confirm() && f.Ritual.behavior.Executions == 0);
        });
        Run("一次实际选人复用原版重复查询，完整身体检查只有一次", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.HostSlot()); Counters.Reset();
            Assert(f.TargetSlot());
            Assert(Counters.Eligibility == 1 && Counters.Reach == 2 && Counters.Permissions == 1 && Counters.Reports == 0);
        });
        Run("身体不合格仅显示短拒绝，旧选择和观众不变", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            var invalid = new Pawn { Eligible = false, BoundMaster = f.Host };
            f.A.TryAssignSpectate(invalid); Counters.Reset();
            Assert(!f.TargetSlot(invalid));
            Assert(f.A.FirstAssignedPawn("slave") == f.Target && f.A.SpectatorsForReading.Contains(invalid));
            Assert(Counters.Eligibility == 1 && Counters.Messages == 1 && Counters.Warnings == 0 && Counters.Reports == 0);
        });
        Run("拖到已有头像上的失败换人失败后恢复旧人", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            var invalid = new Pawn { Eligible = false, BoundMaster = f.Host };
            Assert(!f.Window.Replace(invalid, new RitualRole[] { f.TargetRole }, f.Target));
            Assert(f.A.FirstAssignedPawn("slave") == f.Target);
        });
        Run("更换主持者按新人加现有目标验证，拒绝不改指派", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth(); var other = new Pawn { Master = true };
            Assert(!f.Window.Replace(other, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.Target.BoundMaster == f.Host);
            f.Target.PermittedHosts.Add(other); f.Target.AssignedTrainer = other;
            Assert(f.Window.Replace(other, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == other && f.A.FirstAssignedPawn("slave") == f.Target && f.Target.BoundMaster == f.Host);
        });
        Run("原版交换两个执行角色时先验证最终交换组合", () =>
        {
            var f = new Fixture(); f.Host.Master = false; f.Host.Trainer = true; f.Target.Trainer = true;
            f.Target.BoundMaster = null; f.Target.AssignedTrainer = f.Host;
            // Both directions are established training relationships, without granting ownership.
            f.Target.BoundMaster = f.Host;
            f.Host.BoundMaster = f.Target;
            f.Open(); f.SelectBoth(); Counters.Reset();
            Assert(f.Window.Replace(f.Target, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Target && f.A.FirstAssignedPawn("slave") == f.Host);
            Assert(Counters.Eligibility == 1 && Counters.Reports == 0);
        });
        Run("交换后目标身份非法时两个原槽位均保持", () =>
        {
            var f = new Fixture(); f.Target.Trainer = true; f.Open(); f.SelectBoth();
            Assert(!f.Window.Replace(f.Target, new RitualRole[] { f.HostRole }, f.Host));
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target);
        });
        Run("直接 TryAssign 不能利用 skipReason=true 绕过完整验证", () =>
        {
            var f = new Fixture(); f.Target.Eligible = false; f.Open();
            Assert(!f.A.TryAssign(f.Target, f.TargetRole, out _)); Assert(f.A.FirstAssignedPawn("slave") == null && Counters.Eligibility == 1);
        });
        Run("原版基础条件拒绝先于 RJW 且保留原选择", () =>
        {
            var f = new Fixture(); f.Open(); var invalid = new Pawn { VanillaBlocked = true }; Counters.Reset();
            Assert(!f.TargetSlot(invalid) && Counters.Eligibility == 0);
        });
        Run("不能到达仪式地点时实际指派拒绝", () =>
        {
            var f = new Fixture(); f.Target.Reachable = false; f.Open(); Assert(!f.TargetSlot());
            Assert(Counters.Reach == 1 && Counters.Eligibility == 0);
        });
        Run("组件和身份排除在昂贵检查之前", () =>
        {
            var f = new Fixture(); f.Open(); Assert(!f.TargetSlot(f.Host));
            Assert(!f.TargetSlot(new Pawn { HasComp = false })); Assert(Counters.Reach == 0 && Counters.Eligibility == 0);
        });
        Run("原版儿童门槛在候选与提交路径均保留", () =>
        {
            var f = new Fixture(); f.Target.Child = true; f.Open();
            Assert(!f.TargetRole.AppliesToPawn(f.Target, out _, f.Spot, assignments: f.A));
            Assert(!f.TargetSlot() && Counters.Eligibility == 0);
        });
        foreach (string change in new[] { "body", "permission", "downed", "reachable", "trainer" })
            Run($"点击开始前重新读取状态：{change}", () =>
            {
                var f = new Fixture(); f.Open(); f.SelectBoth();
                if (change == "body") f.Target.Eligible = false;
                if (change == "permission") f.Target.BoundMaster = null;
                if (change == "downed") f.Host.Downed = true;
                if (change == "reachable") f.Target.Reachable = false;
                if (change == "trainer") { f.Host.Master = false; f.Host.Trainer = false; }
                Counters.Reset(); Assert(!f.Window.Confirm());
                Assert(f.Ritual.behavior.Executions == 0 && !f.Window.Closed && BindingRitualSelectionUtility.IsWindow(f.A));
            });
        Run("合法启动只验证已选两人一次并解除预览状态", () =>
        {
            var f = new Fixture(298); f.Open(); f.SelectBoth(); Counters.Reset();
            Assert(f.Window.Confirm() && f.Window.Closed && f.Ritual.behavior.Executions == 1);
            Assert(Counters.Eligibility == 1 && Counters.Reports == 0 && !BindingRitualSelectionUtility.IsWindow(f.A));
        });
        Run("直接启动同样在结束原任务之前拦截缺失执行者", () =>
        {
            var f = new Fixture(); var a = new RitualRoleAssignments(f.Ritual, f.Spot);
            f.Ritual.behavior.TryExecuteOn(f.Spot, f.Host, f.Ritual, null, a, true);
            Assert(f.Ritual.behavior.Executions == 0 && Counters.Messages == 1);
        });
        Run("强制预置角色无法跳过开始前身体验证", () =>
        {
            var f = new Fixture(); f.Target.Eligible = false;
            var a = new RitualRoleAssignments(f.Ritual, f.Spot);
            a.ForcedRolesForReading["master"] = f.Host; a.ForcedRolesForReading["slave"] = f.Target;
            f.Ritual.behavior.TryExecuteOn(f.Spot, f.Host, f.Ritual, null, a, true);
            Assert(f.Ritual.behavior.Executions == 0 && Counters.Eligibility == 1);
        });
        Run("下一次选人不复用上一次操作的许可或身体结果", () =>
        {
            var f = new Fixture(); f.Open(); Assert(f.TargetSlot());
            f.A.TryUnassignAnyRole(f.Target); f.Target.Eligible = false; Counters.Reset();
            Assert(!f.TargetSlot() && Counters.Eligibility == 1);
        });
        Run("取消和新窗口不保留旧预览状态", () =>
        {
            var f = new Fixture(); f.Open(); var old = f.A; f.Window.Close();
            Assert(!BindingRitualSelectionUtility.IsWindow(old)); f.Open();
            Assert(!ReferenceEquals(old, f.A) && !f.Window.CanBegin && BindingRitualSelectionUtility.IsWindow(f.A));
        });
        Run("创建列表异常会恢复预览作用域", () =>
        {
            var f = new Fixture(); ExpectThrow(() => Dialog_BeginRitual.CreateRitualRoleAssignments(f.Ritual, f.Spot, null, null, null, null, null));
            Assert(!BindingRitualSelectionUtility.IsPreview(null));
        });
        Run("存在性检查异常会恢复预览作用域", () =>
        {
            var f = new Fixture(); f.Ritual.behavior.ThrowAvailability = true;
            ExpectThrow(() => f.Ritual.behavior.CanStartRitualNow(f.Spot, f.Ritual)); Assert(!BindingRitualSelectionUtility.IsPreview(null));
        });
        Run("实际验证异常不污染后续操作或移除原选择", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth();
            var candidate = new Pawn { BoundMaster = f.Host }; rjw.xxx.Throw = true;
            ExpectThrow(() => f.Window.Replace(candidate, new RitualRole[] { f.TargetRole }, f.Target));
            rjw.xxx.Throw = false; candidate.Eligible = false;
            Assert(!f.TargetSlot(candidate) && f.A.FirstAssignedPawn("slave") == f.Target);
            Assert(!BindingRitualSelectionUtility.IsPreview(null));
        });
        Run("非 SSC 仪式仍走原版自动填充和完整角色查询", () =>
        {
            var f = new Fixture(binding: false); f.Open();
            Assert(!BindingRitualSelectionUtility.IsWindow(f.A));
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target && Counters.Eligibility > 0);
        });
        Run("泛型共享补丁不改变无关角色管理器", () =>
        {
            var widget = new PawnRoleSelectionWidgetBase<object>(new object());
            Assert(widget.Select(new Pawn(), new[] { new object() }));
            Assert(widget.Replace(new Pawn(), new[] { new object() }, new Pawn()) && widget.UnrelatedCalls == 2 && Counters.Messages == 0);
        });
        Run("右键菜单延迟执行时重新校验并恢复角色、观众和候选顺序", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth();
            var candidate = f.Map.Pawns[3]; candidate.BoundMaster = f.Host;
            var spectators = f.A.SpectatorsForReading.ToArray(); var candidates = f.A.AllCandidatePawns.ToArray();
            FloatMenu menu = null; Counters.Reset();
            f.Window.Draw(() => menu = new FloatMenu(new List<FloatMenuOption>
            { new FloatMenuOption { action = () => f.Window.Widget.Replace(candidate, new RitualRole[] { f.TargetRole }, f.Target) } }));
            Assert(Counters.Eligibility == 0);
            candidate.Eligible = false; menu.Options[0].action();
            Assert(f.A.FirstAssignedPawn("slave") == f.Target && f.A.FirstAssignedPawn("master") == f.Host);
            Assert(f.A.SpectatorsForReading.SequenceEqual(spectators) && f.A.AllCandidatePawns.SequenceEqual(candidates));
            Assert(Counters.Eligibility == 1 && Counters.Messages == 1);
        });
        Run("右键菜单合法替换正常生效", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var candidate = f.Map.Pawns[2]; candidate.BoundMaster = f.Host;
            FloatMenu menu = null;
            f.Window.Draw(() => menu = new FloatMenu(new List<FloatMenuOption>
            { new FloatMenuOption { action = () => f.Window.Widget.Replace(candidate, new RitualRole[] { f.TargetRole }, f.Target) } }));
            Counters.Reset(); menu.Options[0].action();
            Assert(f.A.FirstAssignedPawn("slave") == candidate && f.A.SpectatorsForReading.Contains(f.Target));
            Assert(Counters.Eligibility == 1 && Counters.Messages == 0);
        });
        Run("原版强制角色拒绝时恢复换人前的完整列表", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var candidate = f.Map.Pawns[2]; candidate.BoundMaster = f.Host;
            f.A.ForcedRolesForReading["other"] = candidate;
            var spectators = f.A.SpectatorsForReading.ToArray(); var candidates = f.A.AllCandidatePawns.ToArray();
            Assert(!f.Window.Replace(candidate, new RitualRole[] { f.TargetRole }, f.Target));
            Assert(f.A.FirstAssignedPawn("slave") == f.Target && f.A.ForcedRolesForReading["other"] == candidate);
            Assert(f.A.SpectatorsForReading.SequenceEqual(spectators) && f.A.AllCandidatePawns.SequenceEqual(candidates));
        });
        Run("只取消角色或移到观众不进行完整执行者检查", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth(); Counters.Reset();
            f.Window.Draw(() => f.A.TryAssignSpectate(f.Target));
            Assert(f.A.FirstAssignedPawn("slave") == null && f.A.SpectatorsForReading.Contains(f.Target));
            Assert(Counters.Eligibility == 0 && Counters.Permissions == 0 && Counters.Reach == 0);
        });
        Run("同一小人移到空槽位时正确清空原槽位", () =>
        {
            var f = new Fixture(); f.Target.Trainer = true; f.Open(); Assert(f.TargetSlot()); Counters.Reset();
            Assert(f.HostSlot(f.Target));
            Assert(f.A.FirstAssignedPawn("master") == f.Target && f.A.FirstAssignedPawn("slave") == null);
            Assert(Counters.Eligibility == 0 && Counters.Permissions == 0 && Counters.Reach == 1);
        });
        Run("普通右键菜单不受 SSC 包装影响", () =>
        {
            Action original = () => { }; var option = new FloatMenuOption { action = original };
            new FloatMenu(new List<FloatMenuOption> { option }); Assert(ReferenceEquals(option.action, original));
        });
        Run("交换第二步被原版拒绝时回滚并刷新品质预览", () =>
        {
            var f = new Fixture(); f.Host.Master = false; f.Host.Trainer = f.Target.Trainer = true;
            f.Host.BoundMaster = f.Target; f.Open(); f.SelectBoth();
            f.A.RejectAssignPawn = f.Host;
            f.Window.Replace(f.Target, new RitualRole[] { f.HostRole }, f.Host);
            Assert(f.A.FirstAssignedPawn("master") == f.Host && f.A.FirstAssignedPawn("slave") == f.Target);
            Assert(f.Window.Widget.CachedTarget == f.Target);
        });
        Run("运行中的 Lord 即使持有准备分配对象仍执行完整检查", () =>
        {
            var f = new Fixture(); f.Open(); f.Target.Eligible = false;
            Assert(!f.TargetRole.AppliesToPawn(f.Target, out _, f.Spot,
                ritual: new LordJob_Ritual { Master = f.Host, Slave = f.Target }, assignments: f.A, skipReason: true));
            Assert(Counters.Eligibility == 1);
        });
        Run("窗口说明不改动调用方共享列表", () =>
        {
            var notes = new List<string> { "original" }; var f = new Fixture(); f.Open(notes);
            Assert(notes.Count == 1 && f.Window.ExtraInfos.Count == 2);
        });
        if (args.Length > 0) Run("四语新提示完整且键一致", () =>
        {
            string[] expected = { "SSC_RitualSelection_ChooseParticipants", "SSC_RitualSelection_MissingRoles", "SSC_RitualSelection_Invalid" };
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                var doc = XDocument.Load(Path.Combine(args[0], "Languages", language, "Keyed", "SSC_RitualSelection.xml"));
                Assert(doc.Root.Elements().Select(x => x.Name.LocalName).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)));
                Assert(doc.Root.Elements().All(x => !string.IsNullOrWhiteSpace(x.Value)));
            }
        });
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        Counters.Reset(); SSCMod.settings = new Settings(); rjw.RJWSettings.rape_enabled = true; rjw.xxx.Throw = false;
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
    private static void ExpectThrow(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected failure"); }
}
