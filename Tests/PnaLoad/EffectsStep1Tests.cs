using System;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接执行生产输入与五个既有补丁，覆盖来源准备、主人清药和同步回调。</summary>
    private static void RunEffectsStep1Tests()
    {
        Run("受药分类不包含生产能力或其他专精", () =>
        {
            foreach (string name in PnaBodyNames.Take(6))
                Assert(SSCPnaExposureUtility.IsDrugEffectDef(new HediffDef { defName = name }), name);
            foreach (string name in PnaBodyNames.Skip(6).Concat(new[] { "Other", "SSC_Hediff_Combatant_Final" }))
                Assert(!SSCPnaExposureUtility.IsDrugEffectDef(new HediffDef { defName = name }), name);
            Assert(!SSCPnaExposureUtility.IsDrugEffectDef(null), "空定义");
        });

        Run("主人清理全部受药状态但保留产出实例与进度", () =>
        {
            var p = CloneSource(); AddAllBodyStates(p, .6f); p.needs.PnaNeed = .3f;
            var production = p.health.hediffSet.hediffs.Where(h => PnaBodyNames.Skip(6).Contains(h.def.defName)).ToArray();
            AddBodyState(p, "Other", .7f);
            var unrelated = p.health.hediffSet.hediffs.First(h => h.def.defName == "Other");
            p.IsMaster = true; SSCPnaExposureUtility.RefreshBodyState(p, true);
            Assert(!p.health.hediffSet.hediffs.Any(h => SSCPnaExposureUtility.IsDrugEffectDef(h.def)), "清受药状态");
            foreach (var record in production) { Assert(p.health.hediffSet.hediffs.Contains(record), "产出原实例"); Near(.6, record.Severity); }
            Assert(p.health.hediffSet.hediffs.Contains(unrelated), "其他状态保留"); Near(.7, unrelated.Severity);
            Assert(SSCPnaExposureUtility.GetNeed(p) == null, "原版撤销需求");
        });
        Run("主人直接 AddHediff 拒绝受药状态而允许产出", () =>
        {
            var p = NewPawn(); p.IsMaster = true;
            foreach (string name in PnaBodyNames) p.health.AddHediff(new HediffDef { defName = name });
            Assert(p.health.hediffSet.hediffs.Select(h => h.def.defName).Order().SequenceEqual(PnaBodyNames.Skip(6).Order()), "仅保留两种产出");
        });
        Run("主人免疫优先于有效链且不生成性奴负荷", () =>
        {
            var p = NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); p.IsMaster = true;
            Assert(!SSCPnaExposureUtility.ApplyExposure(p, 1, SSCPnaExposureSource.Ingestion).Accepted, "拒绝");
            Assert(SSCPnaExposureUtility.IsRooted(p) && SSCPnaExposureUtility.GetBodyRecord(p) == null, "链不被删除或生成载体");
        });
        Run("主人阻断适配及旧 PNA 药物原版旁路", () =>
        {
            foreach (bool adapted in new[] { false, true })
            {
                var p = NewPawn(); AddOldAddiction(p, .4f); p.needs.PnaNeed = .2f; p.IsMaster = true;
                var doer = new IngestionOutcomeDoer_SSCPna { severity = .4f };
                var drug = Drug(adapted ? doer : null); drug.PrePostIngested(p);
                if (adapted) doer.DoIngestionOutcome(p, drug.parent, 1);
                Equal(0, drug.OriginalCalls);
                Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null && SSCPnaExposureUtility.GetAddiction(p) == null &&
                    SSCPnaExposureUtility.GetNeed(p) == null, "没有原版或 Doer 残留");
            }
        });
        Run("主人不阻断其他化学品组件", () =>
        {
            var p = NewPawn(); p.IsMaster = true; var drug = Drug(null);
            drug.Props.chemical = new ChemicalDef { addictionHediff = new HediffDef { defName = "OtherAddiction" } };
            drug.Props.Addictive = false; drug.PrePostIngested(p); Equal(1, drug.OriginalCalls);
        });
        Run("主人没有负荷或成瘾时仍清孤立需求", () =>
        {
            var p = NewPawn(); p.IsMaster = true; p.needs.PnaNeed = .3f;
            SSCPnaExposureUtility.RefreshBodyState(p); Assert(SSCPnaExposureUtility.GetNeed(p) == null, "无负荷早退前刷新");
        });
        Run("主人退出后不恢复旧状态但可以重新接收", () =>
        {
            var p = NewPawn(); Input(p, 1); p.IsMaster = true; SSCPnaExposureUtility.RefreshBodyState(p, true);
            p.IsMaster = false; SSCPnaExposureUtility.RefreshBodyState(p, true);
            Assert(SSCPnaExposureUtility.GetBodyRecord(p) == null && SSCPnaExposureUtility.GetAddiction(p) == null, "没有恢复副本");
            Assert(Input(p, .2f).Accepted, "新输入有效"); Near(.2, SSCPnaExposureUtility.GetLoad(p));
        });
        Run("加量新增负荷回调成为主人后清理并拒绝", () =>
        {
            var p = NewPawn(); p.NotifyPlayer = true;
            p.health.OnAdded = h => { if (SSCPnaExposureUtility.IsBodyLoadDef(h.def)) p.IsMaster = true; };
            Assert(!Input(p, 1).Accepted, "回调失效"); AssertNoMasterDrugState(p);
        });
        Run("已有负荷阶段通知成为主人后不建立成瘾", () =>
        {
            var p = NewPawn(); Input(p, .1f); p.health.OnChanged = h => p.IsMaster = true;
            Assert(!Input(p, 1).Accepted, "回调失效"); AssertNoMasterDrugState(p);
        });
        Run("新建成瘾回调成为主人后撤销负荷需求及通知", () =>
        {
            var p = NewPawn(); p.NotifyPlayer = true;
            p.health.OnAdded = h => { if (SSCPnaExposureUtility.IsAddictionDef(h.def)) p.IsMaster = true; };
            var result = Input(p, 1); Assert(!result.Accepted && !result.AddictionCreated, "不误报成瘾"); AssertNoMasterDrugState(p);
        });
        Run("输入前结算半批回调成为主人不写入新剂量", () =>
        {
            var p = NewPawn(); Input(p, .15f); Tick(p, 50); p.health.OnChanged = h => p.IsMaster = true;
            Assert(!Input(p, .2f).Accepted, "半批回调重新核对"); AssertNoMasterDrugState(p);
        });
        Run("外部 AddHediff 结算半批后切为主人仍拒绝来件", () =>
        {
            var p = NewPawn(); Input(p, .15f); Tick(p, 50); p.health.OnChanged = h => p.IsMaster = true;
            p.health.AddHediff(SSCDefOf.SSC_PnaLoad); AssertNoMasterDrugState(p);
        });
        Run("有链清成瘾回调成为主人且无负荷时仍清旧强化", () =>
        {
            var p = NewPawn(); AddOldAddiction(p, .4f); AddBodyState(p, "Hediff_PNA_Combat_Buff", .5f);
            p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = p, def = ChainDef, LinkedPawn = NewPawn() });
            p.health.OnRemoved = h => { if (SSCPnaExposureUtility.IsAddictionDef(h.def)) p.IsMaster = true; };
            SSCPnaExposureUtility.RefreshBodyState(p, true); AssertNoMasterDrugState(p);
        });
        Run("协调通知重入主人输入仍被拒绝且外层完成清理", () =>
        {
            var p = NewPawn(); Raw(p, .4f); bool entered = false;
            p.health.OnChanged = h =>
            {
                if (entered) return; entered = true; p.IsMaster = true;
                Assert(!Input(p, .2f).Accepted, "重入不突破免疫");
            };
            SSCPnaExposureUtility.RefreshBodyState(p, true); Assert(entered, "回调运行"); AssertNoMasterDrugState(p);
        });
        Run("主人清药回调不能通过 AddHediff 补回受药状态", () =>
        {
            var p = NewPawn(); Raw(p, .6f); p.IsMaster = true;
            p.health.OnRemoved = h => p.health.AddHediff(AddictionDef);
            SSCPnaExposureUtility.RefreshBodyState(p, true); AssertNoMasterDrugState(p);
            p.health.OnRemoved = null; p.IsMaster = false; Assert(Input(p, .1f).Accepted, "重入标记正常释放");
        });
        Run("主人旧档在完整引用恢复后幂等清理", () =>
        {
            var p = CloneSource(); AddAllBodyStates(p, .5f); p.needs.PnaNeed = .2f; p.IsMaster = true;
            Scribe.mode = LoadSaveMode.PostLoadInit; SSCPnaGameComponent.NormalizePawn(p);
            Assert(p.health.hediffSet.hediffs.Any(h => SSCPnaExposureUtility.IsDrugEffectDef(h.def)), "不在加载中改引用");
            Scribe.mode = LoadSaveMode.Inactive; SSCRestrictionGameComponent.Pawns.Add(p);
            new SSCPnaGameComponent(new Game()).FinalizeInit(); AssertNoMasterDrugState(p);
            var production = p.health.hediffSet.hediffs.ToArray(); SSCPnaGameComponent.NormalizePawn(p);
            Assert(production.SequenceEqual(p.health.hediffSet.hediffs), "重复清理不影响产出");
        });
    }

    // 断言最终身体状态，不用宿主重写清药算法；身体生产状态不包含在此禁用清单。
    private static void AssertNoMasterDrugState(Pawn pawn)
    {
        Assert(pawn.IsMaster && !pawn.health.hediffSet.hediffs.Any(h => SSCPnaExposureUtility.IsDrugEffectDef(h.def)), "主人无受药状态");
        Assert(SSCPnaExposureUtility.GetNeed(pawn) == null && Messages.Sent.Count == 0, "无需求或新成瘾通知");
    }
}
