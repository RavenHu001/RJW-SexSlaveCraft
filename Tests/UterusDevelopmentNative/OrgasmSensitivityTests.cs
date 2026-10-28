// AI 辅助生成：第二批实际生产 DLL／原生 XML、健康、思想和 Scribe，不复制业务算法。
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Collections;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static HediffDef sensitivity;
    private static ThoughtDef sensitivityThought;
    private static StatDef vulnerability;
    private static readonly Type sensitivityConfig = typeof(SSCOrgasmSensitivityUtility).Assembly.GetType("SexSlaveCraft.SSCOrgasmSensitivityConfig");

    /// <summary>实际载入本批资源，执行有效状态、独立配置、原菜单凭据和身体流转回归。</summary>
    private static void OrgasmSensitivityTests(string repo, Harmony harmony)
    {
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
        SSCDefOf.SSC_PersonalityExcreted_Done ??= new HediffDef { defName = "SSC_PersonalityExcreted_Done", hediffClass = typeof(Hediff), initialSeverity = 1 };
        vulnerability = DefDatabase<StatDef>.GetNamedSilentFail("Vulnerability");
        if (vulnerability == null) { vulnerability = new StatDef { defName = "Vulnerability", defaultBaseValue = 1f }; DefDatabase<StatDef>.Add(vulnerability); }
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_OrgasmSensitivity.xml"));
        sensitivity = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.SelectSingleNode("HediffDef"), false);
        DefDatabase<HediffDef>.Add(sensitivity);
        xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_OrgasmSensitivity.xml"));
        sensitivityThought = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement.SelectSingleNode("ThoughtDef"), false);
        DefDatabase<ThoughtDef>.Add(sensitivityThought);
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Require(errors.Count == 0, string.Join('\n', errors));
        // 只把作者 XML 的阶段标签／边界装入既有链夹具，保留实际原版 Severity 通知；
        // 其他锁链能力与渲染仍属于独立套件，不在此伪装成完整运行 Def 快照。
        SSCDefOf.ChainOfSexSlave.stages = XDocument.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/HediffOfSexSlave.xml"))
            .Root.Elements("HediffDef").Single(e => (string)e.Element("defName") == "Hediff_ChainOfSexSlave")
            .Element("stages").Elements("li").Select(e => new HediffStage
                { label = (string)e.Element("label"), minSeverity = (float)e.Element("minSeverity") }).ToList();
        ClearSensitivityConfig();
        // 实际安装健康通知转发，其他初始化／健康状态机仍沿用本宿主的明确边界。
        harmony.Patch(AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged)),
            postfix: new HarmonyMethod(typeof(Harmony_OrgasmSensitivityLifecycle), "Postfix"));
        SensitivityStateTests();
        SensitivityContextTests();
        SensitivitySaveAndTransferTests();
        SensitivityConfigurationTests();
        SensitivityUiTests(harmony);
        SensitivityCallbackTests(harmony);
    }

    /// <summary>为真实性奴附上原类型锁链及主人；不模拟绑定服务，不运行地图仪式。</summary>
    private static Pawn SensitivityPawn(int stage = 4)
    {
        var pawn = SurgeryPawn();
        AccessTools.Field(typeof(Thing), "factionInt").SetValue(pawn, surgeryPlayer);
        var master = SurgeryPawn(PawnIdentity.Master);
        var chain = new Hediff_ChainOfSexSlave { def = SSCDefOf.ChainOfSexSlave, pawn = pawn, target = master };
        pawn.health.hediffSet.hediffs.Add(chain);
        SetChainStage(pawn, stage);
        return pawn;
    }

    /// <summary>独立测试输入使用现行阶段边界，期望值不从被测有效档位反推。</summary>
    private static void SetChainStage(Pawn pawn, int stage)
        => SSCBondUtility.GetChain(pawn).Severity = new[] { 0f, .1f, .3f, .5f, .9f }[stage];

    /// <summary>正常提交入口，不以直接字段写入代替开启与调档。</summary>
    private static Hediff_SSCOrgasmSensitivity SetSensitivity(Pawn pawn, int level)
    {
        Require(SSCOrgasmSensitivityUtility.TrySet(SSCOrgasmSensitivityUtility.Capture(pawn), level, out var reason), reason);
        return SSCOrgasmSensitivityUtility.Find(pawn);
    }

    /// <summary>只隔离思想通用资格条件；实际专用 worker 和 XML 矩阵仍执行生产代码。</summary>
    private static ThoughtState SensitivityMood(Pawn pawn)
        => (ThoughtState)AccessTools.Method(typeof(ThoughtWorker_SSCOrgasmSensitivity), "CurrentStateInternal")
            .Invoke(sensitivityThought.Worker, new object[] { pawn });

    /// <summary>使用实际原版属性工具消费派生阶段，验证只应用一次保存档位对应的有效修正。</summary>
    private static float SensitivityStat(Pawn pawn)
        => pawn.health.hediffSet.hediffs.Sum(h => h.CurStage is HediffStage stage
            ? HediffStatsUtility.GetStatOffsetForSeverity(vulnerability, stage, pawn, h.Severity) : 0f);

    /// <summary>配置热改只用于明确测试边界；生产只在显式定义重载时清缓存。</summary>
    private static void ClearSensitivityConfig() => AccessTools.Method(sensitivityConfig, "Clear").Invoke(null, null);

    /// <summary>缺省、全矩阵、退阶、失格和无心情需求均对照实际消费者。</summary>
    private static void SensitivityStateTests()
    {
        Check("sensitivity native missing body stays off without writes", () =>
        {
            var p = SensitivityPawn(); int count = p.health.hediffSet.hediffs.Count, before = notifications;
            var state = SSCOrgasmSensitivityUtility.GetState(p); Equal(0, state.SavedLevel); Equal(0, state.EffectiveLevel);
            Near(1, state.Multiplier); Near(0, SensitivityStat(p)); Require(!SensitivityMood(p).Active);
            Equal(count, p.health.hediffSet.hediffs.Count); Equal(before, notifications);
        });
        // 只保留低档不升档、最大选择降至最低／中间上限；不逐格快照整个XML参数表。
        foreach (var scenario in new[] { (saved: 1, stage: 4, effective: 1), (saved: 4, stage: 1, effective: 1), (saved: 4, stage: 3, effective: 3) })
            Check($"sensitivity native effective state saved {scenario.saved} stage {scenario.stage}", () =>
            {
                int saved = scenario.saved, stage = scenario.stage;
                var p = SensitivityPawn(); var record = SetSensitivity(p, saved); SetChainStage(p, stage);
                int effective = scenario.effective;
                var state = SSCOrgasmSensitivityUtility.GetState(p);
                Equal(saved, state.SavedLevel); Equal(effective, state.EffectiveLevel); Equal(stage, state.MaximumLevel);
                Near(new[] { 2f, 4f, 8f, 16f }[effective - 1], state.Multiplier);
                Near(new[] { .3f, .75f, 1.5f, 3f }[effective - 1], SensitivityStat(p));
                Near(saved, record.Severity); Equal(effective - 1, record.CurStageIndex);
                SetChainStage(p, 4); Equal(saved, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel);
            });
        // 负面首格和非对角正面映射足以验证消费；阶段切换及最终格由缓存消费回归覆盖。
        foreach (var cell in new[] { (1, 1, -3f), (3, 2, 2f) })
            Check($"sensitivity native continuous mood {cell.Item1}/{cell.Item2}", () =>
            {
                var p = SensitivityPawn(); SetSensitivity(p, cell.Item2); SetChainStage(p, cell.Item1);
                var thought = SensitivityMood(p); Require(thought.Active);
                Near(cell.Item3, sensitivityThought.stages[thought.StageIndex].baseMoodEffect);
                Require(p.needs.mood == null); Near(new[] { .3f, .75f, 1.5f, 3f }[cell.Item2 - 1], SensitivityStat(p));
            });
        foreach (string why in new[] { "identity", "chain", "dead body", "hollow", "mutation" })
            Check("sensitivity native suspension retains body: " + why, () =>
            {
                var p = SensitivityPawn(); var record = SetSensitivity(p, 4); var chain = SSCBondUtility.GetChain(p);
                SSCPersonalityRuntimeUtility.MutationScope scope = null;
                switch (why)
                {
                    case "identity": p.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset; break;
                    case "chain": p.health.hediffSet.hediffs.Remove(chain); break;
                    case "dead body": AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(p.health, PawnHealthState.Dead); break;
                    case "hollow": p.health.hediffSet.hediffs.Add(new Hediff { def = SSCDefOf.SSC_PersonalityExcreted_Done, pawn = p }); break;
                    case "mutation": Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out scope)); break;
                }
                try { Equal(0, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel); Near(0, SensitivityStat(p)); Require(!SensitivityMood(p).Active); Equal(record, SSCOrgasmSensitivityUtility.Find(p)); Near(4, record.Severity); }
                finally { scope?.Dispose(); }
            });
        Check("sensitivity native dead off-map master keeps saved instructions active", () =>
        {
            var p = SensitivityPawn(); SetSensitivity(p, 4);
            AccessTools.Field(typeof(Pawn_HealthTracker), "healthState").SetValue(SSCBondUtility.GetBoundMaster(p).health, PawnHealthState.Dead);
            Equal(4, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel); Near(3, SensitivityStat(p)); Require(SensitivityMood(p).Active);
        });
        Check("sensitivity native cached thought consumption rechecks silent chain changes", () =>
        {
            var p = SensitivityPawn(); SetSensitivity(p, 4);
            var thought = new Thought_SituationalSSCOrgasmSensitivity { pawn = p, def = sensitivityThought };
            Near(6, thought.MoodOffset()); SetChainStage(p, 2); Near(-2, thought.MoodOffset());
            SSCBondUtility.GetChain(p).target = null; Near(0, thought.MoodOffset()); Require(!thought.Active);
        });
        Check("sensitivity native StatWorker applies offsets before factors and preserves bounds", () =>
        {
            var p = SensitivityPawn(); var worker = vulnerability.Worker;
            Near(1, worker.GetValueUnfinalized(StatRequest.For(p), false)); SetSensitivity(p, 1);
            var factorDef = new HediffDef { defName = "SensitivityOtherModFactor", hediffClass = typeof(Hediff), initialSeverity = 1,
                stages = new() { new HediffStage { statFactors = new() { new StatModifier { stat = vulnerability, value = 2 } } } } };
            p.health.AddHediff(factorDef); Near(2.6f, worker.GetValueUnfinalized(StatRequest.For(p), false));
            SetSensitivity(p, 4); Near(8, worker.GetValueUnfinalized(StatRequest.For(p), false));
            // 采用本机 RJW 作者 XML 的上限 3 验证原版终值裁剪，不宣称运行时补丁合并值。
            float previousMax = vulnerability.maxValue;
            try { vulnerability.maxValue = 3; float value = 8; worker.FinalizeValue(StatRequest.For(p), ref value, true); Near(3, value); }
            finally { vulnerability.maxValue = previousMax; }
        });
        Check("sensitivity native chain stage and personality transitions dirty thought cache", () =>
        {
            var p = SensitivityPawn(); SetSensitivity(p, 4);
            p.needs.mood = new Need_Mood(p) { def = new NeedDef { defName = "SensitivityMood" } };
            var handler = p.needs.mood.thoughts.situational;
            var dirty = AccessTools.Field(typeof(SituationalThoughtHandler), "thoughtsDirty");
            var cached = (IList)AccessTools.Field(typeof(SituationalThoughtHandler), "cachedThoughts").GetValue(handler);
            cached.Add(new Thought_SituationalSSCOrgasmSensitivity { pawn = p, def = sensitivityThought }); dirty.SetValue(handler, false);
            SetChainStage(p, 1); Require((bool)dirty.GetValue(handler)); Equal(0, cached.Count);
            dirty.SetValue(handler, false); Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope));
            Require((bool)dirty.GetValue(handler)); Equal(0, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel);
            dirty.SetValue(handler, false); scope.Dispose(); Require((bool)dirty.GetValue(handler)); Equal(1, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel);
        });
        Check("sensitivity native duplicate addition keeps original owner and level", () =>
        {
            var p = SensitivityPawn(); var original = SetSensitivity(p, 1);
            var incoming = HediffMaker.MakeHediff(sensitivity, p); incoming.Severity = 4; p.health.AddHediff(incoming);
            Equal(original, SSCOrgasmSensitivityUtility.Find(p)); Near(1, original.Severity);
            Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == sensitivity)); Near(.3f, SensitivityStat(p));
        });
        Check("sensitivity native raw duplicate getters are readonly and give one offset", () =>
        {
            var p = SensitivityPawn(); var original = SetSensitivity(p, 1); var duplicate = HediffMaker.MakeHediff(sensitivity, p);
            duplicate.Severity = 4; p.health.hediffSet.hediffs.Add(duplicate); int before = notifications;
            Near(.3f, SensitivityStat(p)); Equal(2, p.health.hediffSet.hediffs.Count(h => h.def == sensitivity)); Equal(before, notifications);
            SSCOrgasmSensitivityUtility.Normalize(p); Equal(original, SSCOrgasmSensitivityUtility.Find(p)); Near(1, original.Severity);
            Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == sensitivity));
        });
        // 下界、非整数和非有限值各留一个；上界另由真实setter拒绝回归覆盖。
        foreach (float invalid in new[] { 0f, 1.5f, float.NaN })
            Check("sensitivity native invalid severity remains inactive: " + invalid, () =>
            {
                var p = SensitivityPawn(); var record = SetSensitivity(p, 2);
                AccessTools.Field(typeof(Hediff), "severityInt").SetValue(record, invalid);
                Require(SSCOrgasmSensitivityUtility.Find(p) == null); Near(0, SensitivityStat(p)); Require(!SensitivityMood(p).Active);
                Require(p.health.hediffSet.hediffs.Contains(record)); SSCOrgasmSensitivityUtility.Normalize(p);
                Require(!p.health.hediffSet.hediffs.Contains(record));
            });
        Check("sensitivity native direct setter rejects overflow without clamping to four", () =>
        {
            var p = SensitivityPawn(); var record = SetSensitivity(p, 2); record.Severity = 5;
            Require(SSCOrgasmSensitivityUtility.Find(p) == null); Near(0, SensitivityStat(p));
        });
    }

    /// <summary>菜单捕获不存在事实及全部 owner；重新绑定／换人格不能让旧点击取得新凭据。</summary>
    private static void SensitivityContextTests()
    {
        foreach (string change in new[] { "Comp", "personality", "chain", "master", "missing becomes present", "owner", "duplicate" })
            Check("sensitivity native stale context rejects: " + change, () =>
            {
                var p = SensitivityPawn(); var original = change == "missing becomes present" ? null : SetSensitivity(p, 1);
                var context = SSCOrgasmSensitivityUtility.Capture(p);
                switch (change)
                {
                    case "Comp": AttachToggleTraining(p).pawnIdentity = PawnIdentity.Slave; break;
                    case "personality": Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); scope.Dispose(); break;
                    case "chain": var old = SSCBondUtility.GetChain(p); p.health.hediffSet.hediffs.Remove(old); p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { def = old.def, pawn = p, target = old.target, Severity = old.Severity }); break;
                    case "master": SSCBondUtility.GetChain(p).target = SurgeryPawn(PawnIdentity.Master); break;
                    case "missing becomes present": SetSensitivity(p, 1); break;
                    case "owner": p.health.hediffSet.hediffs.Remove(original); SetSensitivity(p, 1); break;
                    case "duplicate": p.health.hediffSet.hediffs.Add(HediffMaker.MakeHediff(sensitivity, p)); break;
                }
                Require(!SSCOrgasmSensitivityUtility.IsCurrent(context));
                Require(!SSCOrgasmSensitivityUtility.TrySet(context, 4, out var reason)); Require(!string.IsNullOrWhiteSpace(reason));
                Require(!SSCOrgasmSensitivityUtility.TrySet(context, 0, out _));
            });
        Check("sensitivity native menu rechecks stage and current state before write", () =>
        {
            var p = SensitivityPawn(); var record = SetSensitivity(p, 4); var context = SSCOrgasmSensitivityUtility.Capture(p);
            SetChainStage(p, 1); Require(!SSCOrgasmSensitivityUtility.TrySet(context, 3, out _)); Near(4, record.Severity);
            Require(SSCOrgasmSensitivityUtility.TrySet(context, 1, out _)); Near(1, record.Severity);
        });
        Check("sensitivity native off works after role and chain eligibility is lost", () =>
        {
            var p = SensitivityPawn(); SetSensitivity(p, 4); p.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            SSCBondUtility.GetChain(p).target = null; SetSensitivity(p, 0); Require(SSCOrgasmSensitivityUtility.Find(p) == null);
        });
        Check("sensitivity native same level is idempotent and no second persona storage", () =>
        {
            var p = SensitivityPawn(); var record = SetSensitivity(p, 2); int before = notifications;
            Equal(record, SetSensitivity(p, 2)); Equal(before, notifications); Require(!p.GetComp<CompSexSlaveTraining>().abilityToggles.HasEntries);
        });
        Check("sensitivity native non-player and loading body cannot edit", () =>
        {
            var p = SensitivityPawn(); var context = SSCOrgasmSensitivityUtility.Capture(p);
            AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, null); Require(!SSCOrgasmSensitivityUtility.TrySet(context, 1, out _));
            AccessTools.Field(typeof(Thing), "factionInt").SetValue(p, surgeryPlayer);
            try { Scribe.mode = LoadSaveMode.Saving; Require(!SSCOrgasmSensitivityUtility.TrySet(context, 1, out _)); }
            finally { Scribe.mode = LoadSaveMode.Inactive; }
        });
    }

    /// <summary>真实健康节点 Scribe 及成熟复制入口验证身体归属，不声称完整世界跨引用存档。</summary>
    private static void SensitivitySaveAndTransferTests()
    {
        // 只验证旧档无记录和最大档位的保存端点；中间档走同一整数序列化路径。
        foreach (int level in new[] { 0, 4 })
            Check("sensitivity native health Scribe body level " + level, () =>
            {
                var p = NewPawn(); if (level > 0) { var record = HediffMaker.MakeHediff(sensitivity, p); record.Severity = level; p.health.AddHediff(record); }
                // 原生健康外壳不保存 Pawn 的锁链跨引用；只保存本批唯一选择，不保存派生效果。
                var loaded = Load(Save(p)); Near(level, SSCOrgasmSensitivityUtility.Find(loaded)?.Severity ?? 0);
                Require(SSCOrgasmSensitivityUtility.Find(loaded)?.Part == null); Equal(0, SSCOrgasmSensitivityUtility.GetState(loaded).EffectiveLevel);
            });
        Check("sensitivity native load normalizes duplicates retaining first valid level", () =>
        {
            var p = NewPawn(); var first = HediffMaker.MakeHediff(sensitivity, p); first.Severity = 1;
            var duplicate = HediffMaker.MakeHediff(sensitivity, p); duplicate.Severity = 4;
            p.health.hediffSet.hediffs.Add(first); p.health.hediffSet.hediffs.Add(duplicate);
            var loaded = Load(Save(p)); Equal(1, loaded.health.hediffSet.hediffs.Count(h => h.def == sensitivity)); Near(1, SSCOrgasmSensitivityUtility.Find(loaded).Severity);
        });
        Check("sensitivity native invalid saved value is cleared after load without upgrade", () =>
        {
            var p = NewPawn(); var record = HediffMaker.MakeHediff(sensitivity, p); record.Severity = 1; p.health.hediffSet.hediffs.Add(record);
            string path = Save(p); Edit(path, doc => doc.Descendants("severity").Single().Value = "1.5");
            var loaded = Load(path); Require(SSCOrgasmSensitivityUtility.Find(loaded) == null);
        });
        Check("sensitivity native exact persona body filter excludes this config only", () =>
        {
            Require(SSCBodyDevelopmentUtility.IsBodyStateDef(sensitivity));
            Require(!SSCBodyDevelopmentUtility.IsBodyStateDef(SSCDefOf.ChainOfSexSlave));
            Require(!SSCBodyDevelopmentUtility.IsBodyStateDef(new HediffDef { defName = "SSC_Exp_Genitals" }));
        });
        Check("sensitivity native vanilla birth health copying preserves independent body choice", () =>
        {
            var source = SensitivityPawn(); var original = SetSensitivity(source, 4); var clone = SurgeryPawn();
            Hidden(typeof(GameComponent_PawnDuplicator), null, "CopyHediffs", source, clone);
            var copy = SSCOrgasmSensitivityUtility.Find(clone);
            Require(copy != null && copy != original && copy.pawn == clone && copy.Part == null); Near(4, copy.Severity);
            SetSensitivity(source, 1); Near(4, copy.Severity);
        });
        Check("sensitivity native clone maturity preserves changed or disabled birth choice", () =>
        {
            var source = SensitivityPawn(); SetSensitivity(source, 4); var clone = SensitivityPawn(); var own = SetSensitivity(clone, 1);
            Hidden(typeof(RabbitCloneUtility), null, "CopySscHediffsFromSource", source, clone);
            Equal(own, SSCOrgasmSensitivityUtility.Find(clone)); Near(1, own.Severity);
            SetSensitivity(clone, 0); Hidden(typeof(RabbitCloneUtility), null, "CopySscHediffsFromSource", source, clone);
            Require(SSCOrgasmSensitivityUtility.Find(clone) == null);
            Require((bool)Hidden(typeof(RabbitCloneUtility), null, "ShouldKeepSscHediff", sensitivity));
        });
    }

    /// <summary>真实配置对象故障注入，确保单一来源、非线性参数和独立思想失败边界。</summary>
    private static void SensitivityConfigurationTests()
    {
        Check("sensitivity native XML parameters alter consumers without CSharp fallback", () =>
        {
            var ext = sensitivity.GetModExtension<SSCOrgasmSensitivityExtension>();
            float originalRate = ext.multipliers[0], originalOffset = ext.vulnerabilityOffsets[0]; int gate = ext.minChainStages[3];
            try
            {
                ext.multipliers[0] = 2.5f; ext.vulnerabilityOffsets[0] = .6f; ext.minChainStages[3] = 3; ClearSensitivityConfig();
                var p = SensitivityPawn(1); SetSensitivity(p, 1); Near(2.5f, SSCOrgasmSensitivityUtility.GetMultiplier(p)); Near(.6f, SensitivityStat(p));
                SetChainStage(p, 3); SetSensitivity(p, 4); Equal(4, SSCOrgasmSensitivityUtility.GetState(p).EffectiveLevel);
                // 下调门槛后缺少 3/4 心情映射只停心情，不阻止四档身体参数。
                Require(!SensitivityMood(p).Active); Near(3, SensitivityStat(p));
            }
            finally { ext.multipliers[0] = originalRate; ext.vulnerabilityOffsets[0] = originalOffset; ext.minChainStages[3] = gate; ClearSensitivityConfig(); }
        });
        foreach (string fault in new[] { "multiplier", "length", "gate order" })
            Check("sensitivity native bad body config disables effects but permits off: " + fault, () =>
            {
                var p = SensitivityPawn(); SetSensitivity(p, 4); var ext = sensitivity.GetModExtension<SSCOrgasmSensitivityExtension>();
                var rates = ext.multipliers.ToList(); var gates = ext.minChainStages.ToList();
                try
                {
                    switch (fault)
                    {
                        case "multiplier": ext.multipliers[0] = float.NaN; break;
                        case "length": ext.multipliers.RemoveAt(0); break;
                        case "gate order": ext.minChainStages[1] = 3; ext.minChainStages[2] = 2; break;
                    }
                    ClearSensitivityConfig(); Near(1, SSCOrgasmSensitivityUtility.GetMultiplier(p)); Near(0, SensitivityStat(p)); Require(!SensitivityMood(p).Active);
                    Require(!SSCOrgasmSensitivityUtility.TrySet(SSCOrgasmSensitivityUtility.Capture(p), 1, out _)); SetSensitivity(p, 0);
                }
                finally { ext.multipliers = rates; ext.minChainStages = gates; ClearSensitivityConfig(); }
            });
        foreach (string fault in new[] { "missing", "index" })
            Check("sensitivity native bad thought config leaves body effects active: " + fault, () =>
            {
                var p = SensitivityPawn(); SetSensitivity(p, 4);
                var ext = sensitivityThought.GetModExtension<SSCOrgasmSensitivityMoodExtension>(); var original = ext.stageMappings.ToList();
                int originalIndex = original[0].thoughtStageIndex;
                try
                {
                    switch (fault)
                    {
                        case "missing": ext.stageMappings.RemoveAt(0); break;
                        case "index": original[0].thoughtStageIndex = 100; break;
                    }
                    ClearSensitivityConfig(); Require(!SensitivityMood(p).Active); Near(16, SSCOrgasmSensitivityUtility.GetMultiplier(p)); Near(3, SensitivityStat(p));
                }
                finally { ext.stageMappings = original; original[0].thoughtStageIndex = originalIndex; ClearSensitivityConfig(); }
            });
    }

    /// <summary>取真实私有视图字段，仅供断言，不复制布局或提交算法。</summary>
    private static T SensitivityField<T>(object instance, string name)
        => (T)AccessTools.Field(instance.GetType(), name).GetValue(instance);

    /// <summary>图形边界只提供确定的字体测量，保留真实换行行高、菜单构造及闭包。</summary>
    private static bool SensitivityTextHeight(string __0, float __1, ref float __result)
    {
        __result = __0.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length * 9f / Math.Max(1f, __1))) * 20f);
        return false;
    }

    /// <summary>原生 UI 的无图形 CLR 字号高度边界。</summary>
    private static bool SensitivityLineHeight(ref float __result) { __result = 20f; return false; }

    /// <summary>只替代选择器输入；菜单创建与点击提交仍执行生产 ITab 的原闭包。</summary>
    private sealed class SensitivityTab : ITab_SexSlaveTraining
    {
        internal Pawn Selected;
        /// <summary>提供当前选中身体，验证菜单打开后更换角色不会提交旧请求。</summary>
        protected override Pawn SelPawn => Selected;
    }

    /// <summary>验证独立枚举行不受 bool 空列表遮挡，宽度与快照正确进入真实菜单。</summary>
    private static void SensitivityUiTests(Harmony harmony)
    {
        var tabType = typeof(ITab_SexSlaveTraining);
        var calcHeight = AccessTools.Method(typeof(Text), nameof(Text.CalcHeight), new[] { typeof(string), typeof(float) });
        var lineHeight = AccessTools.PropertyGetter(typeof(Text), nameof(Text.LineHeight));
        harmony.Patch(calcHeight, prefix: new HarmonyMethod(typeof(Program), nameof(SensitivityTextHeight)));
        harmony.Patch(lineHeight, prefix: new HarmonyMethod(typeof(Program), nameof(SensitivityLineHeight)));
        object Build(Pawn p, float width) => AccessTools.Method(tabType, "BuildBodyManagementView").Invoke(null,
            new object[] { p, p.GetComp<CompSexSlaveTraining>(), width });
        object[] Rows(object view) => ((IEnumerable)AccessTools.Field(view.GetType(), "Rows").GetValue(view)).Cast<object>().ToArray();
        try
        {
            Check("sensitivity native enum row exists when body boolean rows are empty", () =>
            {
                var p = SensitivityPawn(1); int before = p.health.hediffSet.hediffs.Count; var view = Build(p, 322f);
                var rows = Rows(view); Equal(1, rows.Length); Require(SensitivityField<object>(rows[0], "Sensitivity") != null);
                Equal(before, p.health.hediffSet.hediffs.Count); Require(SSCOrgasmSensitivityUtility.Find(p) == null);
            });
            Check("sensitivity native enum row wraps and preserves GUI text settings", () =>
            {
                var p = SensitivityPawn(); SetSensitivity(p, 4); SetChainStage(p, 1);
                Text.Font = GameFont.Medium; Text.WordWrap = false;
                var narrow = Build(p, 200f); var wide = Build(p, 322f);
                Require(SensitivityField<float>(narrow, "Height") >= SensitivityField<float>(wide, "Height"));
                Equal(GameFont.Medium, Text.Font); Require(!Text.WordWrap); Text.Font = GameFont.Small; Text.WordWrap = true;
                var row = Rows(narrow).Single(r => SensitivityField<object>(r, "Sensitivity") != null);
                var detail = SensitivityField<object>(row, "Sensitivity");
                Near(SensitivityField<float>(detail, "LabelHeight") + 36f, SensitivityField<float>(row, "Height"));
            });
            Check("sensitivity native retained body row stays visible after identity loss", () =>
            {
                var p = SensitivityPawn(); SetSensitivity(p, 4); p.GetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
                Require(Rows(Build(p, 322f)).Any(r => SensitivityField<object>(r, "Sensitivity") != null));
            });
            Check("sensitivity native menu offers off and unlocked level with disabled high levels", () =>
            {
                var p = SensitivityPawn(1); var row = SensitivityField<object>(Rows(Build(p, 322f)).Single(), "Sensitivity");
                var options = (List<FloatMenuOption>)AccessTools.Method(tabType, "BuildOrgasmSensitivityOptions").Invoke(new ITab_SexSlaveTraining(), new[] { row });
                Equal(5, options.Count); Require(options[0].action != null && options[1].action != null);
                Require(options.Skip(2).All(o => o.action == null));
                SetSensitivity(p, 1);
                options = (List<FloatMenuOption>)AccessTools.Method(tabType, "BuildOrgasmSensitivityOptions").Invoke(new ITab_SexSlaveTraining(), new[] { row });
                Require(options.All(o => o.action == null), "missing-record snapshot acquired new owner");
            });
            Check("sensitivity native menu closure submits original missing body context once", () =>
            {
                var p = SensitivityPawn(); var tab = new SensitivityTab { Selected = p };
                var row = SensitivityField<object>(Rows(Build(p, 322f)).Single(), "Sensitivity");
                var options = (List<FloatMenuOption>)AccessTools.Method(tabType, "BuildOrgasmSensitivityOptions").Invoke(tab, new[] { row });
                options[3].action(); var owner = SSCOrgasmSensitivityUtility.Find(p); Near(3, owner.Severity);
                options[1].action(); Equal(owner, SSCOrgasmSensitivityUtility.Find(p)); Near(3, owner.Severity);
            });
            Check("sensitivity native menu closure rejects changed selected pawn", () =>
            {
                var p = SensitivityPawn(); var tab = new SensitivityTab { Selected = p };
                var row = SensitivityField<object>(Rows(Build(p, 322f)).Single(), "Sensitivity");
                var options = (List<FloatMenuOption>)AccessTools.Method(tabType, "BuildOrgasmSensitivityOptions").Invoke(tab, new[] { row });
                tab.Selected = SensitivityPawn(); options[1].action();
                Require(SSCOrgasmSensitivityUtility.Find(p) == null && SSCOrgasmSensitivityUtility.Find(tab.Selected) == null);
            });
        }
        finally { harmony.Unpatch(calcHeight, HarmonyPatchType.All, harmony.Id); harmony.Unpatch(lineHeight, HarmonyPatchType.All, harmony.Id); }
    }

    private static Action sensitivityRemovalCallback;
    private static Action sensitivityWriteCallback;
    /// <summary>只注入外部健康回调的变动，生产规范化／提交仍走真实代码。</summary>
    private static void AfterSensitivityRemoval()
    { var action = sensitivityRemovalCallback; sensitivityRemovalCallback = null; action?.Invoke(); }

    /// <summary>模拟第三方在真实健康写入通知中调整同一记录；一次性取走回调避免递归改写。</summary>
    private static void AfterSensitivityWrite(Hediff hediff)
    {
        if (hediff?.def != sensitivity) return;
        var action = sensitivityWriteCallback; sensitivityWriteCallback = null; action?.Invoke();
    }

    /// <summary>自有清理触发第三方回调后，必须重查阶段及原 owner，不能顺手接管回调中新建的实例。</summary>
    private static void SensitivityCallbackTests(Harmony harmony)
    {
        var remove = AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff));
        var changed = AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged));
        harmony.Patch(remove, postfix: new HarmonyMethod(typeof(Program), nameof(AfterSensitivityRemoval)));
        harmony.Patch(changed, postfix: new HarmonyMethod(typeof(Program), nameof(AfterSensitivityWrite)));
        try
        {
            foreach (string change in new[] { "stage", "owner", "personality" })
                Check("sensitivity native cleanup callbacks cannot authorize stale writes: " + change, () =>
                {
                    var p = SensitivityPawn(); var original = SetSensitivity(p, 1);
                    var invalid = HediffMaker.MakeHediff(sensitivity, p, p.RaceProps.body.corePart); invalid.Severity = 4;
                    p.health.hediffSet.hediffs.Add(invalid); var context = SSCOrgasmSensitivityUtility.Capture(p);
                    Hediff replacement = null;
                    sensitivityRemovalCallback = () =>
                    {
                        if (change == "stage") SetChainStage(p, 1);
                        else if (change == "owner")
                        {
                            p.health.hediffSet.hediffs.Remove(original); replacement = HediffMaker.MakeHediff(sensitivity, p);
                            replacement.Severity = 2; p.health.hediffSet.hediffs.Add(replacement);
                        }
                        else { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); scope.Dispose(); }
                    };
                    Require(!SSCOrgasmSensitivityUtility.TrySet(context, 4, out _));
                    Near(change == "owner" ? 2 : 1, SSCOrgasmSensitivityUtility.Find(p).Severity);
                    if (replacement != null) Equal(replacement, SSCOrgasmSensitivityUtility.Find(p));
                });
            Check("sensitivity native setter rejects callback-rewritten final state", () =>
            {
                // 同一最终状态契约分别覆盖数值、部位和 Pawn 归属；列表中仍有原引用不足以视为成功。
                foreach (string changedField in new[] { "level", "part", "pawn" })
                {
                    var p = SensitivityPawn(); var record = SetSensitivity(p, 1);
                    var context = SSCOrgasmSensitivityUtility.Capture(p);
                    sensitivityWriteCallback = () =>
                    {
                        if (changedField == "level") record.Severity = 2;
                        else if (changedField == "part") record.Part = p.RaceProps.body.corePart;
                        else record.pawn = NewPawn();
                    };
                    Require(!SSCOrgasmSensitivityUtility.TrySet(context, 4, out var reason), "setter reported success after callback changed " + changedField);
                    Require(!string.IsNullOrWhiteSpace(reason)); Require(p.health.hediffSet.hediffs.Contains(record));
                    if (changedField == "level") { Equal(record, SSCOrgasmSensitivityUtility.Find(p)); Near(2, record.Severity); }
                    else Require(SSCOrgasmSensitivityUtility.Find(p) == null);
                }
            });
        }
        finally
        {
            sensitivityRemovalCallback = sensitivityWriteCallback = null;
            harmony.Unpatch(remove, HarmonyPatchType.Postfix, harmony.Id);
            // 保留本套已经安装的生产生命周期 Postfix，只移除本例注入的写入回调。
            harmony.Unpatch(changed, AccessTools.Method(typeof(Program), nameof(AfterSensitivityWrite)));
        }
    }
}
