using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class Program
{
    private static int passed, failed;
    private static string repo;

    /// <summary>从 args[0] 读取仓库路径，加载翻译并安装真实 Harmony 补丁，运行模型回归后返回失败状态码。</summary>
    /// <remarks>此入口执行最小生命周期和绘制模型，不启动游戏或验证真实 Unity 布局。</remarks>
    private static int Main(string[] args)
    {
        repo = Path.GetFullPath(args[0]);
        foreach (var key in XDocument.Load(Path.Combine(repo, "Languages/ChineseSimplified/Keyed/SSC_SharedBed.xml")).Root.Elements())
            Translation.Keys[key.Name.LocalName] = key.Value;
        new Harmony("ssc.tests.shared-bed").PatchAll(Assembly.GetExecutingAssembly());

        Run("全局恋人判断未安装 SSC 补丁", () => Assert(Harmony.GetPatchInfo(AccessTools.Method(typeof(LovePartnerRelationUtility), "LovePartnerRelationExists")) == null));
        Run("未绑定性奴可以与指定调教员共享普通床", () => { var f = Setup(); Assert(Use(f.bed, f.slave)); });
        Run("奴隶身份不被床位调用修改", () => { var f = Setup(); Use(f.bed, f.slave); Assert(f.slave.IsSlave); });
        Run("无 SSC 身份的原版奴隶不享有特许", () => { var f = Setup(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; Assert(!Use(f.bed, f.slave)); });
        Run("普通奴隶自己的奴隶床照常使用", () => { var f = Setup(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; f.bed.ForSlaves = true; Assert(Use(f.bed, f.slave)); });
        Run("仅有恶堕和特殊关系不会变成性奴", () => { var f = Setup(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; f.slave.relations.FlawedLovers.Add(f.partner); Assert(!Use(f.bed, f.slave)); });
        Run("绑定后只给主人额外许可", () => { var f = Setup(); Pawn master = NewPawn(f.slave.Map); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master }; Assert(!Use(f.bed, f.slave)); f.bed.OwnersForReading[0] = master; Assert(Use(f.bed, f.slave)); });
        Run("绑定主人死亡不会回退到调教员", () => { var f = Setup(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = new Pawn { Dead = true } }; Assert(!Use(f.bed, f.slave)); });
        Run("损坏的锁链引用不会回退到调教员", () => { var f = Setup(); f.slave.Chain = new Hediff_ChainOfSexSlave(); Assert(!Use(f.bed, f.slave)); });
        Run("没有指定调教员时不扩展普通床", () => { var f = Setup(); f.slave.Training.selectedTrainer = null; Assert(!Use(f.bed, f.slave)); });
        Run("不会扩展陌生人的普通床或无主普通床", () => { var f = Setup(); f.bed.OwnersForReading.Clear(); Assert(!Use(f.bed, f.slave)); f.bed.OwnersForReading.Add(NewPawn(f.slave.Map)); Assert(!Use(f.bed, f.slave)); });
        Run("0.1% 门槛保持严格大于", () => { var f = Setup(); f.slave.needs.Corruption.CurLevelPercentage = .001f; Assert(!Use(f.bed, f.slave)); f.slave.needs.Corruption.CurLevelPercentage = .0011f; Assert(Use(f.bed, f.slave)); });
        Run("自由殖民者中的性奴同样可用指定床", () => { var f = Setup(); f.slave.GuestStatus = null; Assert(Use(f.bed, f.slave)); });
        Run("囚犯不获得 SSC 的身份覆盖", () => { var f = Setup(); f.slave.GuestStatus = GuestStatus.Prisoner; Assert(!Use(f.bed, f.slave)); GuestStatus? status = GuestStatus.Slave; SSCSharedBedUtility.AdjustGuestStatusForPartnerBed(f.bed, f.slave, ref status); Assert(status == GuestStatus.Slave); });
        Run("SSC 特殊关系不冒充原版恋人", () => { var f = Setup(); f.slave.relations.FlawedLovers.Add(f.partner); Assert(!LovePartnerRelationUtility.LovePartnerRelationExists(f.slave, f.partner)); });
        Run("原版恋人与配偶同床保持原结果", () => { var f = Setup(); f.slave.GuestStatus = null; Pawn spouse = NewPawn(f.slave.Map); f.slave.relations.Lovers.Add(spouse); f.bed.OwnersForReading[0] = spouse; Assert(Use(f.bed, f.slave)); Assert(LovePartnerRelationUtility.LovePartnerRelationExists(f.slave, spouse)); });
        Run("满床继续由原版拒绝", () => { var f = Setup(); f.bed.OwnersForReading.Add(NewPawn(f.slave.Map)); Assert(!Use(f.bed, f.slave)); });
        Run("原版床型和环境检查不能被 SSC 放行", () =>
        {
            Action<Building_Bed>[] invalid = { b => b.Vacuum = true, b => b.Burning = true, b => b.Spawned = false,
                b => b.Map = new Map(), b => b.IdeologyForbidden = true, b => b.Forbidden = true,
                b => b.def.maxBodySize = .5f, b => b.AnyUnoccupiedSleepingSlot = false };
            foreach (var change in invalid) { var f = Setup(); change(f.bed); Assert(!Use(f.bed, f.slave)); }
        });
        Run("普通找床优先指定床", () => { var f = Setup(); Assert(Find(f.slave) == f.bed); });
        Run("医疗需求保留原版床，即使原版未找到医疗床", () => { var f = Setup(); f.slave.MedicalRest = true; Assert(Find(f.slave) == f.slave.VanillaBed); f.slave.VanillaBed = null; Assert(Find(f.slave) == null); });
        Run("医疗床结果本身不会被覆盖", () => { var f = Setup(); f.slave.VanillaBed.Medical = true; Assert(Find(f.slave) == f.slave.VanillaBed); });
        Run("死眠中保留原版结果", () => { var f = Setup(); f.slave.Deathresting = true; Assert(Find(f.slave) == f.slave.VanillaBed); });
        Run("死眠棺结果本身不会被覆盖", () => { var f = Setup(); f.slave.VanillaBed.def = ThingDefOf.DeathrestCasket; Assert(Find(f.slave) == f.slave.VanillaBed); });
        Run("普通奴隶的找床结果原样保留", () => { var f = Setup(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; Assert(Find(f.slave) == f.slave.VanillaBed); });
        Run("完整原版检查接收原始寻路与预留参数", () =>
        {
            var f = Setup(); Pawn carrier = NewPawn(f.slave.Map); f.bed.Reservable = false;
            Assert(!SSCSharedBedUtility.TryGetPreferredPartnerBed(f.slave, carrier, false, false, GuestStatus.Slave, out _));
            Assert(SSCSharedBedUtility.TryGetPreferredPartnerBed(f.slave, carrier, false, true, GuestStatus.Slave, out _));
            Assert(RestUtility.LastTraveler == carrier && !RestUtility.LastCheckSocial && RestUtility.LastIgnoreReservations && !RestUtility.LastAllowMed);
            f.bed.Reachable = false; Assert(Find(f.slave) == f.slave.VanillaBed);
        });
        Run("分配 transpiler 仅放宽符合资格的奴隶分类", () => { var f = Setup(); var c = Comp(f.bed); Assert(c.CanAssignTo(f.slave).Accepted); f.slave.Training.pawnIdentity = PawnIdentity.Unset; Assert(!c.CanAssignTo(f.slave).Accepted); });
        Run("分配时保留原版体型拒绝", () => { var f = Setup(); f.slave.BodySize = 3; Assert(Comp(f.bed).CanAssignTo(f.slave).Reason == "TooLargeForBed"); });
        Run("分配候选仅补充合格的性奴", () =>
        {
            var f = Setup(); Pawn ordinary = NewPawn(f.slave.Map); ordinary.GuestStatus = GuestStatus.Slave; ordinary.Training.selectedTrainer = f.partner;
            f.slave.Map.mapPawns.SlavesOfColonySpawned.AddRange(new[] { f.slave, ordinary });
            var candidates = Comp(f.bed).AssigningCandidates.ToList(); Assert(candidates.Contains(f.slave) && !candidates.Contains(ordinary));
        });
        Run("清醒躺卧不生成共同睡眠记忆", () => { var f = Sleeping(); f.partner.Sleeping = false; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Count == 0); });
        Run("只分配同床、未同时在床不生成记忆", () => { var f = Sleeping(); f.partner.Bed = null; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Count == 0); });
        Run("非休息效果不记录共同睡眠", () => { var f = Sleeping(); Toils_LayDown.ApplyBedRelatedEffects(f.slave, f.bed, true, false, 1); Finish(f.slave); Assert(Memories(f.slave).Count == 0); });
        Run("普通奴隶同床不生成 SSC 记忆", () => { var f = Sleeping(); f.slave.Training.pawnIdentity = PawnIdentity.Unset; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Count == 0); });
        Run("未绑定时生成调教员记忆，睡着时不提前结算", () => { var f = Sleeping(); Tick(f.slave); Assert(Memories(f.slave).Count == 0); Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithTrainer); });
        Run("绑定后生成主人记忆", () => { var f = Sleeping(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner }; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithMaster); });
        Run("缺少调教员定义时主人记忆仍正常结算", () =>
        {
            ThoughtDef original = SSCDefOf.SSC_SharedBedWithTrainer;
            try { SSCDefOf.SSC_SharedBedWithTrainer = null; var f = Sleeping(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner }; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithMaster); }
            finally { SSCDefOf.SSC_SharedBedWithTrainer = original; }
        });
        Run("缺少主人定义时调教员记忆仍正常结算", () =>
        {
            ThoughtDef original = SSCDefOf.SSC_SharedBedWithMaster;
            try { SSCDefOf.SSC_SharedBedWithMaster = null; var f = Sleeping(); Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithTrainer); }
            finally { SSCDefOf.SSC_SharedBedWithMaster = original; }
        });
        Run("当前所需定义缺失时保留已有记忆且不抛异常", () =>
        {
            var f = Sleeping(); Tick(f.slave); Finish(f.slave);
            ThoughtDef original = SSCDefOf.SSC_SharedBedWithTrainer;
            try { SSCDefOf.SSC_SharedBedWithTrainer = null; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Single().def == original && f.slave.Training.sharedSleep == null); }
            finally { SSCDefOf.SSC_SharedBedWithTrainer = original; }
        });
        Run("旧版情境心情定义不能用于睡后记忆且保留已有记忆", () =>
        {
            var f = Sleeping(); Tick(f.slave); Finish(f.slave);
            try { SSCDefOf.SSC_SharedBedWithMaster.IsMemory = false; f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner }; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithTrainer && f.slave.Training.sharedSleep == null); }
            finally { SSCDefOf.SSC_SharedBedWithMaster.IsMemory = true; }
        });
        Run("主人先起床不丢失性奴的睡眠记录", () => { var f = Sleeping(); Tick(f.slave); Finish(f.partner); f.partner.Bed = null; Finish(f.slave); Assert(Memories(f.slave).Count == 1 && Memories(f.partner).Count == 0); });
        Run("先起床后双方仍免除本次负面房间记忆", () =>
        {
            var f = Sleeping(); Tick(f.slave);
            foreach (Pawn p in new[] { f.slave, f.partner })
            { Memories(p).Add(ThoughtMaker.MakeThought(ThoughtDefOf.SleptInBedroom, 0)); Memories(p).Add(ThoughtMaker.MakeThought(ThoughtDefOf.SleptInBarracks, 4)); }
            Finish(f.slave); f.slave.Bed = null; Finish(f.partner);
            foreach (Pawn p in new[] { f.slave, f.partner }) Assert(!Memories(p).Any(m => m.def == ThoughtDefOf.SleptInBedroom) && Memories(p).Any(m => m.def == ThoughtDefOf.SleptInBarracks));
        });
        Run("重复结束回调不会重复结算", () => { var f = Sleeping(); Tick(f.slave); Finish(f.slave); Finish(f.slave); Assert(Memories(f.slave).Count == 1); });
        Run("多次睡眠及绑定前后记忆互相替换", () => { var f = Sleeping(); Tick(f.slave); Finish(f.slave); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner }; f.slave.needs.Corruption.CurLevelPercentage = .5f; Tick(f.slave); Finish(f.slave); Assert(Memories(f.slave).Count == 1 && Memories(f.slave)[0].CurStageIndex == 4 && Memories(f.slave)[0].def == SSCDefOf.SSC_SharedBedWithMaster); });
        Run("共同睡眠记录保存恢复后仍可结算", () => { var f = Sleeping(); Tick(f.slave); Scribe.Data.Clear(); Scribe.Loading = false; f.slave.Training.sharedSleep.ExposeData(); f.slave.Training.sharedSleep = new SSCSharedSleepRecord(); Scribe.Loading = true; f.slave.Training.sharedSleep.ExposeData(); Scribe.Loading = false; Finish(f.slave); Assert(Memories(f.slave).Single().otherPawn == f.partner); });
        Run("旧存档缺少记录字段时保持空记录", () => { Scribe.Data.Clear(); Scribe.Loading = true; var record = new SSCSharedSleepRecord(); record.ExposeData(); Scribe.Loading = false; Assert(record.bed == null && record.partner == null && record.stage == -1); });
        Run("六档心情阈值与优先级保留", () =>
        {
            var f = Setup(); var cases = new[] { (.2f, 29, 0), (.2f, 30, 1), (.2f, 50, 2), (.3f, -100, 3), (.5f, -100, 4) };
            foreach (var item in cases) { f.slave.needs.Corruption.CurLevelPercentage = item.Item1; f.slave.relations.Opinion = item.Item2; Assert(SSCSharedBedUtility.GetSharedBedThoughtStage(f.slave, f.partner) == item.Item3); }
            f.slave.relations.FlawedLovers.Add(f.partner); f.slave.needs.Corruption.CurLevelPercentage = .01f;
            Assert(SSCSharedBedUtility.GetSharedBedThoughtStage(f.slave, f.partner) == 5);
        });
        Run("已分配与未分配行均有性奴标记和说明", () =>
        {
            foreach (bool assigned in new[] { true, false })
            {
                var f = Setup(); Widgets.Labels.Clear(); TooltipHandler.LastTooltip = null;
                var font = Text.Font; var anchor = Text.Anchor; var color = GUI.color;
                new Dialog_AssignBuildingOwner(Comp(f.bed)).Draw(f.slave, assigned);
                Assert(Widgets.Labels.Count == 2 && Widgets.Labels[0].text == "性奴" && TooltipHandler.LastTooltip.Contains("指定调教员"));
                Assert(Widgets.Labels[1].rect.x >= Widgets.Labels[0].rect.xMax && Text.Font == font && Text.Anchor == anchor && GUI.color.Equals(color));
            }
        });
        Run("普通奴隶及其他建筑界面不加标记", () =>
        {
            var f = Setup(); Widgets.Labels.Clear(); f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            new Dialog_AssignBuildingOwner(Comp(f.bed)).Draw(f.slave, false); Assert(Widgets.Labels.Count == 1);
            f.slave.Training.pawnIdentity = PawnIdentity.Slave; Widgets.Labels.Clear();
            new Dialog_AssignBuildingOwner(new CompAssignableToPawn()).Draw(f.slave, false); Assert(Widgets.Labels.Count == 1);
        });
        Run("悬停说明区分主人、调教员和门槛", () => { var f = Setup(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner }; f.slave.needs.Corruption.CurLevelPercentage = 0; var tip = Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave); Assert(tip.Contains("已绑定") && tip.Contains("需高于")); });
        Run("Mint 已分配及未分配行显示同一标记、提示且避开按钮", () =>
        {
            foreach (bool assigned in new[] { true, false })
            {
                var f = Setup(); Widgets.Labels.Clear(); TooltipHandler.LastTooltip = null;
                var row = new Rect(20, 10, 600, 60);
                var font = Text.Font; var anchor = Text.Anchor; var color = GUI.color;
                new DubsMintMenus.Dialog_AssignBuildingOwner(Comp(f.bed)).DoRow(row, f.slave, assigned);
                Assert(Widgets.Labels.Count == 2 && Widgets.Labels[0].text == "性奴");
                Assert(TooltipHandler.LastTooltip == Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave));
                Assert(Widgets.Labels[1].rect.x >= Widgets.Labels[0].rect.xMax && Widgets.Labels[1].rect.xMax <= row.xMax - 175f);
                Assert(Text.Font == font && Text.Anchor == anchor && GUI.color.Equals(color));
            }
        });
        Run("Mint 普通奴隶与非床建筑保留原姓名和区域", () =>
        {
            foreach (bool nonBed in new[] { true, false })
            {
                var f = Setup(); Widgets.Labels.Clear(); TooltipHandler.LastTooltip = null;
                if (!nonBed) f.slave.Training.pawnIdentity = PawnIdentity.Unset;
                new DubsMintMenus.Dialog_AssignBuildingOwner(nonBed ? new CompAssignableToPawn() : Comp(f.bed)).DoRow(new Rect(0, 0, 600, 60), f.slave, false);
                Assert(Widgets.Labels.Count == 1 && Widgets.Labels[0].text == f.slave.LabelShortCap && Widgets.Labels[0].rect.xMax == 600 && TooltipHandler.LastTooltip == null);
            }
        });
        Run("Mint 的不可分配原因与意识形态提示保留", () =>
        {
            var f = Setup(); Widgets.Labels.Clear();
            new DubsMintMenus.Dialog_AssignBuildingOwner(Comp(f.bed)) { RejectedReason = "TooLargeForBed", ShowIdeologyInfo = true }.DoRow(new Rect(0, 0, 600, 60), f.slave, false);
            Assert(Widgets.Labels.Count == 3 && Widgets.Labels[1].text.Contains("TooLargeForBed") && Widgets.Labels[2].text == "IdeoligionForbids" && Widgets.Labels[2].rect.x == 430);
        });
        Run("XML 记忆持续一天、不叠加并保留六档数值", CheckDefs);
        Run("四种界面翻译键完整且参数匹配", CheckLanguages);
        Run("共同睡眠记录接入现有 Pawn 存档", () => { string source = File.ReadAllText(Path.Combine(repo, "Sexslavecraft/Comps/Comp_Train.cs")); Assert(source.Contains("Scribe_Deep.Look(ref sharedSleep, \"sharedSleep\")")); });

        Console.WriteLine($"{passed}/{passed + failed} passed (actual Harmony + lifecycle model; no live game).");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>执行命名用例并累计结果；记录单个失败后继续运行其余用例。</summary>
    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    /// <summary>条件不成立时抛出异常，由用例执行器统一记录失败。</summary>
    private static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
    /// <summary>创建属于指定测试地图的角色，保留模型默认组件与需求。</summary>
    private static Pawn NewPawn(Map map) => new Pawn { Map = map };
    /// <summary>创建未绑定性奴、指定调教员及其双人床，并保留原版奴隶床作为找床对照。</summary>
    private static (Pawn slave, Pawn partner, Building_Bed bed) Setup()
    {
        Map map = new Map(); Pawn slave = NewPawn(map), partner = NewPawn(map);
        slave.GuestStatus = GuestStatus.Slave; slave.Training.pawnIdentity = PawnIdentity.Slave; slave.Training.selectedTrainer = partner;
        partner.LabelShortCap = "Trainer";
        Building_Bed bed = new Building_Bed { Map = map }; bed.OwnersForReading.Add(partner); partner.ownership.OwnedBed = bed;
        slave.VanillaBed = new Building_Bed { Map = map, ForSlaves = true };
        return (slave, partner, bed);
    }
    /// <summary>在默认场景上设置双方同床且同时睡着，供共同睡眠结算用例使用。</summary>
    private static (Pawn slave, Pawn partner, Building_Bed bed) Sleeping()
    {
        var f = Setup(); f.slave.Bed = f.partner.Bed = f.bed; f.slave.Sleeping = f.partner.Sleeping = true; return f;
    }
    /// <summary>通过已打补丁的模型入口检查床位使用资格，保持默认社交检查。</summary>
    private static bool Use(Building_Bed bed, Pawn pawn) => RestUtility.CanUseBedNow(bed, pawn, true, false, null);
    /// <summary>通过已打补丁的找床入口，让角色以自身身份和预留规则寻找休息床。</summary>
    private static Building_Bed Find(Pawn pawn) => RestUtility.FindBedFor(pawn, pawn, true, false, pawn.GuestStatus);
    /// <summary>为指定测试床构造分配组件，供候选资格与窗口用例复用。</summary>
    private static CompAssignableToPawn_Bed Comp(Building_Bed bed) => new CompAssignableToPawn_Bed { parent = bed };
    /// <summary>触发一次床上休息效果，使生产补丁按当前双方状态记录共同睡眠。</summary>
    private static void Tick(Pawn pawn) => Toils_LayDown.ApplyBedRelatedEffects(pawn, pawn.Bed, pawn.Sleeping, true, 1);
    /// <summary>模拟普通躺卧结束，依次经过房间心情处理和睡后记忆结算补丁。</summary>
    private static void Finish(Pawn pawn) => Toils_LayDown.FinalizeLayingJob(pawn, pawn.Bed, false);
    /// <summary>取得模型中的可变记忆列表，便于布置旧记忆并检查结算结果。</summary>
    private static System.Collections.Generic.List<Thought_Memory> Memories(Pawn p) => p.needs.mood.thoughts.memories.Items;
    /// <summary>检查两种同床 XML 定义均为一天、不叠加的记忆，并保留约定的六档心情数值。</summary>
    private static void CheckDefs()
    {
        var defs = XDocument.Load(Path.Combine(repo, "Defs/ThoughtDefs/Thought_SharedBed.xml")).Root.Elements().ToList();
        Assert(defs.Count == 2);
        foreach (var def in defs)
        {
            Assert(def.Element("thoughtClass").Value == "Thought_Memory" && def.Element("workerClass") == null);
            Assert(def.Element("durationDays").Value == "1" && def.Element("stackLimit").Value == "1" && def.Element("stackLimitForSameOtherPawn").Value == "1");
            Assert(def.Element("stages").Elements().Select(x => x.Element("baseMoodEffect").Value).SequenceEqual(new[] { "-6", "-3", "2", "5", "9", "13" }));
        }
    }
    /// <summary>检查四语提示键与简中基准一致，并验证文本可以接受对象和门槛格式参数。</summary>
    private static void CheckLanguages()
    {
        string[] expected = Translation.Keys.Keys.OrderBy(x => x).ToArray();
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            var keys = XDocument.Load(Path.Combine(repo, "Languages", language, "Keyed/SSC_SharedBed.xml")).Root.Elements().ToList();
            Assert(keys.Select(x => x.Name.LocalName).OrderBy(x => x).SequenceEqual(expected));
            foreach (var key in keys) string.Format(key.Value, "Partner", "Threshold");
        }
    }
}
