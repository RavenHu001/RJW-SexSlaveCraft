using System.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    /// <summary>验证直接关系并集、先分配床伴的身份限制、手动床位优先及多对象睡眠；沿用真实 Harmony 模型入口。</summary>
    private static void RunUpdatedSharedBedRules()
    {
        Run("床伴按主人和调教员去重并排除无效引用", () =>
        {
            var f = Setup(); Pawn master = Master(f.slave.Map);
            f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            Assert(SSCSharedBedUtility.GetAllowedPartners(f.slave).SequenceEqual(new[] { master, f.partner }));
            f.slave.Training.selectedTrainer = master;
            Assert(SSCSharedBedUtility.GetAllowedPartners(f.slave).Single() == master);
            master.Destroyed = true; Assert(!SSCSharedBedUtility.GetAllowedPartners(f.slave).Any());
            f.slave.Training.selectedTrainer = f.slave; Assert(!SSCSharedBedUtility.GetAllowedPartners(f.slave).Any());
        });
        Run("床伴关系双向且不扩展普通奴隶", () =>
        {
            var f = Setup(); Assert(SSCSharedBedUtility.HasSharedBedRelation(f.slave, f.partner));
            Assert(SSCSharedBedUtility.HasSharedBedRelation(f.partner, f.slave));
            f.slave.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(!SSCSharedBedUtility.HasSharedBedRelation(f.slave, f.partner));
        });
        Run("空床候选可显示性奴但不放宽普通奴隶或恶堕门槛", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear();
            Pawn ordinary = NewPawn(f.slave.Map); ordinary.GuestStatus = GuestStatus.Slave;
            f.slave.Map.mapPawns.SlavesOfColonySpawned.AddRange(new[] { f.slave, ordinary });
            f.slave.needs.Corruption.CurLevelPercentage = 0;
            Assert(Comp(f.bed).AssigningCandidates.Contains(f.slave) && !Comp(f.bed).AssigningCandidates.Contains(ordinary));
            Assert(!Comp(f.bed).CanAssignTo(f.slave).Accepted);
            AssertBadges(f.slave, f.bed, "性奴");
        });
        Run("原版奴隶性奴不能先分配空床，先分配主人或调教员后可加入", () =>
        {
            foreach (bool withMaster in new[] { false, true })
            {
                var f = Setup(); f.bed.OwnersForReading.Clear();
                Pawn partner = withMaster ? Master(f.slave.Map) : f.partner;
                if (withMaster) f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = partner };
                Assert(!Comp(f.bed).CanAssignTo(f.slave).Accepted && !Use(f.bed, f.slave));
                Assert(Comp(f.bed).CanAssignTo(partner).Accepted);
                Assign(f.bed, partner);
                Assert(Comp(f.bed).CanAssignTo(f.slave).Accepted && Use(f.bed, f.slave));
                Assign(f.bed, f.slave);
                Assert(Use(f.bed, f.slave) && Use(f.bed, partner));
            }
        });
        Run("殖民者身份的性奴可按原版规则先分配空床", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear(); f.slave.GuestStatus = null;
            f.slave.needs.Corruption.CurLevelPercentage = 0;
            Assert(Comp(f.bed).CanAssignTo(f.slave).Accepted && Use(f.bed, f.slave));
        });
        Run("性奴加入前移除调教员会恢复空床分配限制", () =>
        {
            var f = Setup(); Assert(Comp(f.bed).CanAssignTo(f.slave).Accepted);
            f.bed.OwnersForReading.Clear(); f.partner.ownership.OwnedBed = null;
            Assert(!Comp(f.bed).CanAssignTo(f.slave).Accepted);
        });
        Run("床位身份例外不扩展医疗囚犯或单人床", () =>
        {
            foreach (int kind in new[] { 0, 1, 2 })
            {
                var f = Setup(); f.bed.OwnersForReading.Clear();
                if (kind == 0) f.bed.Medical = true;
                if (kind == 1) f.bed.ForPrisoners = true;
                if (kind == 2) f.bed.SleepingSlotsCount = 1;
                Assert(SSCSharedBedUtility.IsSlaveForBedAssignment(f.slave, Comp(f.bed)));
            }
        });
        Run("担任调教员的普通原版奴隶也不获得身份覆盖", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear(); Assign(f.bed, f.slave);
            f.partner.GuestStatus = GuestStatus.Slave;
            f.partner.Training.pawnIdentity = PawnIdentity.Unset;
            Assert(!Use(f.bed, f.partner) && !Comp(f.bed).CanAssignTo(f.partner).Accepted);
        });
        Run("无关联性奴仍在候选中但不获得别人床位的许可", () =>
        {
            var f = Setup(); f.bed.OwnersForReading[0] = Master(f.slave.Map);
            f.slave.Map.mapPawns.SlavesOfColonySpawned.Add(f.slave);
            Assert(Comp(f.bed).AssigningCandidates.Contains(f.slave) && !Comp(f.bed).CanAssignTo(f.slave).Accepted);
            AssertBadges(f.slave, f.bed, null);
        });
        Run("空床显示主人和性奴但不把普通调教员当作主人", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear();
            AssertBadges(f.slave, f.bed, "性奴"); AssertBadges(Master(f.slave.Map), f.bed, "主人");
            AssertBadges(f.partner, f.bed, "性奴");
        });
        Run("先分配性奴显示其主人和指定调教员且提示本床关联", () =>
        {
            var f = Setup(); Pawn master = Master(f.slave.Map); f.slave.LabelShortCap = "SharedSlave";
            f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            f.bed.OwnersForReading.Clear(); Assign(f.bed, f.slave);
            AssertBadges(f.slave, f.bed, "性奴"); AssertBadges(master, f.bed, "主人"); AssertBadges(f.partner, f.bed, "性奴 · 调教员");
            Assert(Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.partner, f.bed).Contains("SharedSlave"));
            AssertBadges(Master(f.slave.Map), f.bed, null);
        });
        Run("主人已分配时不递归标记未分配性奴的调教员", () =>
        {
            var f = Setup(); Pawn master = Master(f.slave.Map); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            f.bed.OwnersForReading.Clear(); Assign(f.bed, master);
            AssertBadges(master, f.bed, "主人"); AssertBadges(f.slave, f.bed, "性奴"); AssertBadges(f.partner, f.bed, null);
        });
        Run("主人兼任调教员时只显示一个合并标签", () =>
        {
            var f = Setup(); f.partner.Training.pawnIdentity = PawnIdentity.Master;
            f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner };
            Assign(f.bed, f.slave); AssertBadges(f.partner, f.bed, "主人 · 调教员");
            Assert(SSCSharedBedUtility.GetAllowedPartners(f.slave).Count() == 1);
        });
        Run("多人床取直接关系并集且已分配者保留身份标签", () =>
        {
            var f = Setup(); Pawn master1 = Master(f.slave.Map), master2 = Master(f.slave.Map);
            f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master1 };
            Pawn slave2 = NewPawn(f.slave.Map); slave2.Training.pawnIdentity = PawnIdentity.Slave;
            slave2.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master2 };
            f.bed.SleepingSlotsCount = 4; f.bed.OwnersForReading.Clear(); Assign(f.bed, f.slave); Assign(f.bed, slave2);
            AssertBadges(master1, f.bed, "主人"); AssertBadges(master2, f.bed, "主人"); AssertBadges(f.partner, f.bed, "性奴 · 调教员");
            Pawn unrelated = Master(f.slave.Map); AssertBadges(unrelated, f.bed, null);
            Assign(f.bed, unrelated); AssertBadges(unrelated, f.bed, "主人");
        });
        Run("取消分配实时移除调教员标记并恢复空床身份显示", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear(); Assign(f.bed, f.slave);
            AssertBadges(f.partner, f.bed, "性奴 · 调教员");
            f.bed.OwnersForReading.Remove(f.slave); f.slave.ownership.OwnedBed = null;
            AssertBadges(f.partner, f.bed, "性奴"); AssertBadges(Master(f.slave.Map), f.bed, "主人");
        });
        Run("没有 SSC 标签不拒绝原版合法分配或配偶共享", () =>
        {
            var f = Setup(); Pawn colonist = NewPawn(f.slave.Map); colonist.relations.Lovers.Add(f.partner);
            AssertBadges(colonist, f.bed, null);
            Assert(Comp(f.bed).CanAssignTo(colonist).Accepted && Use(f.bed, colonist));
        });
        Run("主人床优先且不可用时改用调教员床", () =>
        {
            var f = WithSeparateMasterBed(); Assert(Find(f.slave) == f.masterBed);
            f.masterBed.Reachable = false; Assert(Find(f.slave) == f.trainerBed);
            f.trainerBed.Reachable = false; Assert(Find(f.slave) == f.slave.VanillaBed);
        });
        Run("手动分配的调教员床优先于主人床并继续遵守原版检查", () =>
        {
            var f = WithSeparateMasterBed(); Assign(f.trainerBed, f.slave);
            Assert(Find(f.slave) == f.trainerBed);
            f.trainerBed.Reachable = false; Assert(Find(f.slave) == f.masterBed);
        });
        Run("已有普通奴隶床归属也不会被自动改派到主人床", () =>
        {
            var f = WithSeparateMasterBed(); Assign(f.slave.VanillaBed, f.slave);
            Assert(Find(f.slave) == f.slave.VanillaBed);
        });
        Run("绑定后只与调教员实际同睡仍获得调教员记忆", () =>
        {
            var f = Sleeping(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = Master(f.slave.Map) };
            Tick(f.slave); Finish(f.slave);
            Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithTrainer && Memories(f.slave)[0].otherPawn == f.partner);
        });
        Run("三人同睡主人记忆优先且主人先起床不被覆盖", () =>
        {
            var f = Sleeping(); Pawn master = Master(f.slave.Map); master.Bed = f.bed; master.Sleeping = true;
            f.bed.SleepingSlotsCount = 3; f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            Tick(f.slave); Finish(master); master.Sleeping = false; master.Bed = null;
            Tick(f.slave); Finish(f.slave);
            Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithMaster && Memories(f.slave)[0].otherPawn == master);
        });
        Run("调教员先睡主人后加入时升级记忆并免除两人的负面房间心情", () =>
        {
            var f = Sleeping(); Pawn master = Master(f.slave.Map); f.bed.SleepingSlotsCount = 3;
            f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            Tick(f.slave); Assert(!f.slave.Training.sharedSleep.withMaster);
            master.Bed = f.bed; master.Sleeping = true; Tick(f.slave);
            foreach (Pawn pawn in new[] { master, f.partner })
            {
                Memories(pawn).Add(ThoughtMaker.MakeThought(ThoughtDefOf.SleptInBedroom, 0));
                Finish(pawn); Assert(Memories(pawn).Count == 0);
            }
            Finish(f.slave); Assert(Memories(f.slave).Single().def == SSCDefOf.SSC_SharedBedWithMaster);
        });
    }

    /// <summary>在原版和 Mint 两条真实补丁路径中检查相同的标签结果；无标签时原姓名与角色行必须保留。</summary>
    private static void AssertBadges(Pawn pawn, Building_Bed bed, string expected)
    {
        foreach (bool mint in new[] { false, true })
        foreach (bool assigned in new[] { false, true })
        {
            Widgets.Labels.Clear(); TooltipHandler.LastTooltip = null;
            if (mint) new DubsMintMenus.Dialog_AssignBuildingOwner(Comp(bed)).DoRow(new Rect(0, 0, 600, 60), pawn, assigned);
            else new Dialog_AssignBuildingOwner(Comp(bed)).Draw(pawn, assigned);
            Assert(Widgets.Labels.Count == (expected == null ? 1 : 2));
            Assert(Widgets.Labels.Last().text == pawn.LabelShortCap);
            if (expected == null) Assert(TooltipHandler.LastTooltip == null);
            else Assert(Widgets.Labels[0].text == expected && TooltipHandler.LastTooltip.StartsWith(expected));
        }
    }

    /// <summary>在模型中同步床的归属名单与角色 OwnedBed；不替代真实原版 ClaimBed 的游戏内测试。</summary>
    private static void Assign(Building_Bed bed, Pawn pawn)
    {
        pawn.ownership.OwnedBed?.OwnersForReading.Remove(pawn);
        pawn.ownership.OwnedBed = bed;
        if (!bed.OwnersForReading.Contains(pawn)) bed.OwnersForReading.Add(pawn);
    }

    /// <summary>创建具有明确 SSC 主人身份的测试角色。</summary>
    private static Pawn Master(Map map)
    {
        Pawn pawn = NewPawn(map); pawn.Training.pawnIdentity = PawnIdentity.Master; return pawn;
    }

    /// <summary>构造主人与调教员各有一张床的场景，以验证找床优先级和回退。</summary>
    private static (Pawn slave, Building_Bed masterBed, Building_Bed trainerBed) WithSeparateMasterBed()
    {
        var f = Setup(); Pawn master = Master(f.slave.Map);
        var masterBed = new Building_Bed { Map = f.slave.Map }; Assign(masterBed, master);
        f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
        return (f.slave, masterBed, f.bed);
    }
}
