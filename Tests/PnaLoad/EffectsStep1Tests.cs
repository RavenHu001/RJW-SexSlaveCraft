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

        // 两种来源保留统一满值与末期规则；产出者性交另有已确认的每次基础成瘾 +0.01。
        foreach (SSCPnaExposureSource source in new[] { SSCPnaExposureSource.ProducerSex, SSCPnaExposureSource.MasterInfestation })
        {
            float dose = source == SSCPnaExposureSource.ProducerSex ? SSCPnaMath.ProducerSexDose : SSCPnaMath.MasterInfestationDose;
            Run(source + " 正确加负荷并补已有需求", () =>
            {
                var p = NewPawn(); Raw(p, .4f); AddOldAddiction(p, .4f); p.needs.PnaNeed = .2f;
                var need = SSCPnaExposureUtility.GetNeed(p);
                var result = SSCPnaExposureUtility.ApplyExposure(p, dose, source);
                Assert(result.Accepted && !result.AddictionCreated, "有效来源");
                Near(.2 + (source == SSCPnaExposureSource.ProducerSex ? .02 : .08), result.FinalLoad);
                Near(source == SSCPnaExposureSource.ProducerSex ? .3 : 1, need.CurLevel);
                Near(source == SSCPnaExposureSource.ProducerSex ? .41 : .4, SSCPnaExposureUtility.GetAddiction(p).Severity);
                Assert(ReferenceEquals(need, SSCPnaExposureUtility.GetNeed(p)), "保留已有需求实例");
            });
            Run(source + " 首次满值仍建立完整成瘾并初始化需求", () =>
            {
                var p = NewPawn(); Raw(p, 2);
                var result = SSCPnaExposureUtility.ApplyExposure(p, dose, source);
                Assert(result.Accepted && result.AddictionCreated, "首次建立");
                Near(1, SSCPnaExposureUtility.GetAddiction(p).Severity);
                Near(1, SSCPnaExposureUtility.GetNeed(p).CurLevel);
            });
            Run(source + " 满负荷真实输入仍只额外加深一次", () =>
            {
                var p = NewPawn(); Raw(p, 2); AddOldAddiction(p, .4f); p.needs.PnaNeed = .9f;
                var result = SSCPnaExposureUtility.ApplyExposure(p, dose, source);
                Assert(result.Accepted && !result.AddictionCreated, "已有成瘾"); Near(0, result.AddedLoad);
                Near(source == SSCPnaExposureSource.ProducerSex ? .61 : .6, SSCPnaExposureUtility.GetAddiction(p).Severity);
                Near(1, SSCPnaExposureUtility.GetNeed(p).CurLevel);
            });
            Run(source + " 不强行建立不合法的需求", () =>
            {
                var p = NewPawn(); p.needs.AllowPnaNeed = false; Raw(p, 2);
                Assert(SSCPnaExposureUtility.ApplyExposure(p, dose, source).AddictionCreated, "仍可有身体成瘾");
                Assert(SSCPnaExposureUtility.GetNeed(p) == null, "需求准入沿用原版");
            });
            Run(source + " 有效链接收性奴负荷且不建立成瘾需求", () =>
            {
                var p = NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn());
                var result = SSCPnaExposureUtility.ApplyExposure(p, dose, source);
                Assert(result.Accepted && result.Rooted, "链分流"); Near(dose, result.FinalLoad);
                Assert(SSCPnaExposureUtility.GetAddiction(p) == null && SSCPnaExposureUtility.GetNeed(p) == null, "无成瘾需求");
            });
        }

        Run("主人拒绝所有共享来源且不产生通知", () =>
        {
            var p = NewPawn(); p.IsMaster = true; p.NotifyPlayer = true;
            foreach (SSCPnaExposureSource source in Enum.GetValues<SSCPnaExposureSource>())
                Assert(!SSCPnaExposureUtility.ApplyExposure(p, 1, source).Accepted, source.ToString());
            Assert(!p.health.hediffSet.hediffs.Any(hediff => SSCPnaExposureUtility.IsDrugEffectDef(hediff.def)), "无受药状态");
            Assert(SSCPnaExposureUtility.GetNeed(p) == null && Messages.Sent.Count == 0, "无需求和误报");
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
        Run("主人枪伤保留原伤害但不创建抗性快照或负荷", () =>
        {
            var p = NewPawn(); p.IsMaster = true; p.health.capacities.Consciousness = 3;
            var shot = Shot(); shot.Parts = 3;
            ((PNA_DWExtension)shot.Def.Extension).recordHediff.defName = "Hediff_PNA_Consciousness_Record";
            var worker = new DamageWorker_LowTier(); worker.Apply(shot, p);
            Equal(3, worker.InjuryCalls); AssertNoMasterDrugState(p);
        });
        Run("主人有链枪命中保留原治疗但拒绝旧强化", () =>
        {
            var p = NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(p, NewPawn()); p.IsMaster = true;
            var injury = new Hediff_Injury { pawn = p, def = new HediffDef { defName = "Injury" } };
            p.health.hediffSet.hediffs.Add(injury);
            var shot = Shot(); ((PNA_DWExtension)shot.Def.Extension).combatBuff.defName = "Hediff_PNA_Combat_Buff";
            new DamageWorker_LowTier().Apply(shot, p); Near(5, injury.Healed); AssertNoMasterDrugState(p);
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
