using System;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    // 保留身体状态的旧强化清理回归；旧发射器的治疗与飘字用例已随实现归档。
    private static void RunEffectsStep3Tests()
    {
        // 从存档恢复的旧实例绕过添加补丁，读档收尾清理，不把旧严重度折算成新负荷。
        foreach (bool rooted in new[] { false, true }) Run("第三步只有旧强化的身体读档清理 " + rooted, () =>
        {
            var p = rooted ? WeaponSlave() : NewPawn();
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) AddBodyState(p, name, .7f);
            AddBodyState(p, "SSC_Purple_HyperLactation", .6f); AddBodyState(p, "SSC_Hediff_RabbitCloneLowPNA", .4f);
            SSCPnaGameComponent.NormalizePawn(p); SSCPnaGameComponent.NormalizePawn(p);
            AssertNoLegacyBuff(p); Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null, "不转换成负荷");
            Equal(rooted ? 3 : 2, p.health.hediffSet.hediffs.Count); Near(.6, p.health.hediffSet.hediffs.Single(h => h.def.defName == "SSC_Purple_HyperLactation").Severity);
        });
        Run("第三步旧强化清理保留当前合法负荷成瘾满足度与半批时间", () =>
        {
            var p = NewPawn(); Input(p, .4f); AddOldAddiction(p, .35f); p.needs.PnaNeed = .27f; Tick(p, 50); var load = Record(p);
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) AddBodyState(p, name, .7f);
            SSCPnaGameComponent.NormalizePawn(p); AssertNoLegacyBuff(p); Assert(Record(p) == load, "保留载体");
            Near(.4, SSCPnaExposureUtility.GetLoad(p)); Near(.35, SSCPnaExposureUtility.GetAddiction(p).Severity); Near(.27, p.needs.PnaNeed.Value); Equal(50, BodyComp(p).PendingTicks);
        });
        Run("第三步移除旧强化的回调绑定按最终锁链协调", () =>
        {
            var p = NewPawn(); Input(p, .4f); AddOldAddiction(p, .35f); AddBodyState(p, "Hediff_PNA_Combat_Buff", .7f);
            p.health.OnRemoved = h => { if (SSCPnaExposureUtility.IsLegacyCombatBuffDef(h.def)) Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); };
            SSCPnaExposureUtility.RefreshBodyState(p); AssertNoLegacyBuff(p); Assert(RootedRecord(p) != null, "最终性奴载体");
            Assert(SSCPnaExposureUtility.GetAddiction(p) == null && p.needs.PnaNeed == null, "清除成瘾需求"); Near(.4, SSCPnaExposureUtility.GetLoad(p));
        });
        foreach (bool rooted in new[] { false, true }) Run("第三步直接添加两类旧强化均被现有补丁拒绝 " + rooted, () =>
        {
            var p = rooted ? WeaponSlave() : NewPawn();
            foreach (string name in new[] { "Hediff_PNA_Combat_Buff", "Hediff_PNA_Combat_Buff_Plus" }) p.health.AddHediff(new HediffDef { defName = name });
            AssertNoLegacyBuff(p); Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null, "不创建药效");
        });
    }

    private static Pawn WeaponSlave()
    {
        var p = NewPawn(); p.Spawned = true; p.Map = new Map(); Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); return p;
    }
    private static void AssertNoLegacyBuff(Pawn p) => Assert(!p.health.hediffSet.hediffs.Any(h => SSCPnaExposureUtility.IsLegacyCombatBuffDef(h.def)), "无旧强化");
}
