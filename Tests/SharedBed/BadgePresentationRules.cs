using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    /// <summary>验证独立标签布局和 SSC 状态的边界；通过真实 Harmony 接入原版及 Mint 的行模型。</summary>
    private static void RunBadgePresentationRules()
    {
        Run("独立身份色与许可状态色互不混用", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear();
            Pawn master = Master(f.slave.Map);
            Widgets.Boxes.Clear();
            AssertBadges(master, f.bed, "主人 · 调教员");
            Assert(Widgets.Boxes.Count == 12);
            Assert(!Widgets.Boxes[0].color.Equals(Widgets.Boxes[1].color));
            Assert(!Harmony_SSC_SharedBedAssignmentUI.GetBadgeColor(SSCSharedBedBadge.SexSlave)
                .Equals(Harmony_SSC_SharedBedAssignmentUI.GetBadgeColor(SSCSharedBedBadge.Trainer)));
            Assert(Widgets.Boxes[2].rect.width == 7 && Widgets.Boxes[2].color.Equals(
                Harmony_SSC_SharedBedAssignmentUI.GetStatusColor(SSCSharedBedStatus.Waiting)));
        });
        Run("空床达标仍等待床伴且奴隶提示先分配对象", () =>
        {
            var f = Setup(); f.bed.OwnersForReading.Clear();
            AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Waiting, "等待分配有效床伴");
            Assert(Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave, f.bed).Contains("先主人或指定调教员"));
            Assert(!Comp(f.bed).CanAssignTo(f.slave).Accepted);
            AssertStatus(Master(f.slave.Map), f.bed, SSCSharedBedStatus.Waiting, "等待");
        });
        Run("门槛边界显示当前数值与未达标原因", () =>
        {
            var f = Setup(); f.slave.needs.Corruption.CurLevelPercentage = .001f;
            AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Unavailable, "恶堕：0.1%｜未达标");
            f.slave.needs.Corruption.CurLevelPercentage = .0011f;
            AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Permitted, "恶堕：0.11%｜已达标");
        });
        Run("许可只列出达到门槛的实际床伴", () =>
        {
            var f = Setup(); Pawn master = Master(f.slave.Map);
            f.slave.LabelShortCap = "Eligible"; f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            Pawn other = NewPawn(f.slave.Map); other.LabelShortCap = "BelowThreshold";
            other.Training.pawnIdentity = PawnIdentity.Slave; other.needs.Corruption.CurLevelPercentage = 0;
            other.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = master };
            f.bed.OwnersForReading.Clear(); Assign(f.bed, f.slave); Assign(f.bed, other);
            AssertStatus(master, f.bed, SSCSharedBedStatus.Permitted, "SSC同床：可与 Eligible 同床");
            string tip = Harmony_SSC_SharedBedAssignmentUI.GetTooltip(master, f.bed);
            Assert(tip.Contains("BelowThreshold — 恶堕：0%｜未达标"));
            f.slave.needs.Corruption.CurLevelPercentage = 0;
            AssertStatus(master, f.bed, SSCSharedBedStatus.Unavailable, "本床无符合条件的床伴");
        });
        Run("主人自身恶堕不影响由性奴提供的许可", () =>
        {
            var f = Setup(); f.partner.Training.pawnIdentity = PawnIdentity.Master;
            f.partner.needs.Corruption.CurLevelPercentage = 0; Assign(f.bed, f.slave);
            AssertStatus(f.partner, f.bed, SSCSharedBedStatus.Permitted, "可与 Pawn 同床");
            Assert(!Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.partner, f.bed).Contains("恶堕：0%"));
        });
        Run("性奴兼任调教员时不把自身门槛用于对方的许可", () =>
        {
            var f = Setup(); f.partner.needs.Corruption.CurLevelPercentage = 0; Assign(f.bed, f.slave);
            AssertStatus(f.partner, f.bed, SSCSharedBedStatus.Permitted, "可与 Pawn 同床");
            Assert(Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.partner, f.bed).Contains("恶堕：0%｜未达标"));
        });
        Run("既有床位例外不显示为有合法床伴", () =>
        {
            var f = Setup(); Assign(f.bed, f.slave); f.bed.OwnersForReading.Remove(f.partner);
            AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Retained, "保留已有床位");
            Assert(!Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave, f.bed).Contains("可与"));
        });
        Run("床型与囚犯不显示绿色许可", () =>
        {
            foreach (int kind in new[] { 0, 1, 2, 3, 4 })
            {
                var f = Setup();
                if (kind == 0) f.bed.Medical = true;
                if (kind == 1) f.bed.ForPrisoners = true;
                if (kind == 2) f.bed.ForSlaves = true;
                if (kind == 3) f.bed.SleepingSlotsCount = 1;
                if (kind == 4) f.slave.GuestStatus = GuestStatus.Prisoner;
                AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Unavailable, kind == 4 ? "囚犯" : "此床型");
            }
        });
        Run("主人和指定调教员同一人仍分别显示关系字段", () =>
        {
            var f = Setup(); f.slave.Chain = new Hediff_ChainOfSexSlave { LinkedPawn = f.partner };
            string tip = Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave, f.bed);
            Assert(tip.Contains("主人：Trainer\n指定调教员：Trainer"));
            SSCIdentityUtility.SetTrainerEnabled(f.partner, false);
            Assert(Harmony_SSC_SharedBedAssignmentUI.GetTooltip(f.slave, f.bed).Contains("指定调教员：Trainer（身份停用或不可用）"));
            AssertStatus(f.slave, f.bed, SSCSharedBedStatus.Permitted, "可与 Trainer 同床");
        });
        Run("姓名及原版拒绝原因的全文保留在独立提示区域", () =>
        {
            var f = Setup(); TooltipHandler.Tips.Clear(); Widgets.Labels.Clear();
            f.slave.LabelShortCap = new string('X', 100);
            new DubsMintMenus.Dialog_AssignBuildingOwner(Comp(f.bed)) { RejectedReason = "TooLargeForBed" }
                .DoRow(new Rect(20, 0, 600, 60), f.slave, false);
            Assert(TooltipHandler.Tips.Count == 2);
            Assert(TooltipHandler.Tips[0].text == f.slave.LabelShortCap + " (TooLargeForBed)");
            Assert(TooltipHandler.Tips[0].rect.x == 80 && TooltipHandler.Tips[0].rect.xMax < TooltipHandler.Tips[1].rect.x);
        });
        Run("四语双标签均保留姓名起点并避开按钮", () =>
        {
            var original = Translation.Keys.ToDictionary(entry => entry.Key, entry => entry.Value);
            try
            {
                foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
                {
                    foreach (var key in XDocument.Load(Path.Combine(repo, "Languages", language, "Keyed/SSC_SharedBed.xml")).Root.Elements())
                        Translation.Keys[key.Name.LocalName] = key.Value;
                    foreach (bool mint in new[] { false, true })
                    foreach (bool assigned in new[] { false, true })
                    {
                        var f = Setup(); f.bed.OwnersForReading.Clear(); Widgets.Labels.Clear(); Widgets.Boxes.Clear();
                        if (mint) new DubsMintMenus.Dialog_AssignBuildingOwner(Comp(f.bed)).DoRow(new Rect(20, 0, 600, 60), f.partner, assigned);
                        else new Dialog_AssignBuildingOwner(Comp(f.bed)).Draw(f.partner, assigned);
                        Assert(Widgets.Labels.Count == 3 && Widgets.Boxes.Count == 3);
                        Assert(Widgets.Labels[2].rect.x == (mint ? 80 : 0));
                        Assert(Widgets.Labels[2].rect.xMax < Widgets.Labels[0].rect.x);
                        Assert(Widgets.Labels[0].rect.xMax < Widgets.Labels[1].rect.x);
                        Assert(Widgets.Boxes[2].rect.xMax <= (mint ? 445 : 200));
                        Assert(Widgets.Labels.All(item => item.rect.width > 0));
                    }
                }
            }
            finally { Translation.Keys = original; }
        });
    }

    /// <summary>同时核对结构化状态、用户说明和实际 SSC 许可，防止界面与行为发生偏离。</summary>
    private static void AssertStatus(Pawn pawn, Building_Bed bed, SSCSharedBedStatus expected, string text)
    {
        Assert(Harmony_SSC_SharedBedAssignmentUI.GetSharingStatus(pawn, bed) == expected);
        Assert(Harmony_SSC_SharedBedAssignmentUI.GetTooltip(pawn, bed).Contains(text));
        Assert(SSCSharedBedUtility.HasPartnerBedPermission(bed, pawn) == (expected == SSCSharedBedStatus.Permitted));
    }
}
