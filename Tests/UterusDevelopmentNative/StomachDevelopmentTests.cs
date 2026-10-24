// AI 辅助生成：真实胃部 Hediff、原版属性／医疗／Scribe 与三个生产成长出口。
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static HediffDef stomachDefinition;
    private static RecipeDef stomachSurgery;
    private static BodyPartDef stomachMouthDef;
    private static BodyDef stomachTestBody;
    private static readonly Type stomachGrowthType = typeof(SSCStomachDevelopmentGrowthUtility);

    /// <summary>使用真实 XML 解析与继承；在原种族注入器初始化之前准备胃部配方引用。</summary>
    private static void LoadStomachResources(string repo, string game)
    {
        SSCDefOf.Stomach = new BodyPartDef { defName = "Stomach", label = "胃", hitPoints = 10 };
        stomachMouthDef = new BodyPartDef { defName = "Jaw", label = "口部", hitPoints = 20 };
        DefDatabase<BodyPartDef>.Add(SSCDefOf.Stomach); DefDatabase<BodyPartDef>.Add(stomachMouthDef);
        // 给原生 RJW 身体分类提供实际部位 Def 数据，不替换 has_mouth 或部位查询算法。
        bootstrap = true;
        try { ((HashSet<BodyPartDef>)AccessTools.Field(typeof(Genital_Helper), "mouthBPs").GetValue(null)).Add(stomachMouthDef); }
        finally { bootstrap = false; }
        HediffDefOf.MissingBodyPart = new HediffDef { defName = "MissingBodyPart", label = "缺失部位",
            hediffClass = typeof(Hediff_MissingPart), initialSeverity = 1, isBad = true };
        DefDatabase<HediffDef>.Add(HediffDefOf.MissingBodyPart);
        var eating = XDocument.Load(Path.Combine(game, "Data/Core/Defs/Stats/Stats_Pawns_General.xml")).Root.Elements()
            .Single(n => (string)n.Element("defName") == "EatingSpeed");
        StatDefOf.EatingSpeed = new StatDef { defName = "EatingSpeed", label = "进食速度", defaultBaseValue = (float)eating.Element("defaultBaseValue") };
        DefDatabase<StatDef>.Add(StatDefOf.EatingSpeed);
        var xml = new XmlDocument(); xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_StomachDevelopment.xml"));
        stomachDefinition = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement.FirstChild, false);
        DefDatabase<HediffDef>.Add(stomachDefinition);
        var nodes = XDocument.Load(Path.Combine(game, "Data/Core/Defs/RecipeDefs/Recipes_Surgery_Misc.xml")).Root.Elements()
            .Where(n => (string)n.Attribute("Name") == "SurgeryFlesh")
            .Concat(XDocument.Load(Path.Combine(repo, "1.7/Defs/RecipeDefs/SSC_StomachSurgery.xml")).Root.Elements())
            .Select(n => { var d = new XmlDocument(); d.LoadXml(n.ToString()); return d.DocumentElement; }).ToArray();
        XmlInheritance.Clear(); foreach (var n in nodes) XmlInheritance.TryRegister(n, null); XmlInheritance.Resolve();
        var recipeNode = (XmlElement)XmlInheritance.GetResolvedNodeFor(nodes.Last()).CloneNode(true);
        recipeNode.RemoveAttribute("ParentName");
        stomachSurgery = DirectXmlToObject.ObjectFromXml<RecipeDef>(recipeNode, false);
        DefDatabase<RecipeDef>.Add(stomachSurgery);
        XmlInheritance.Clear(); DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        stomachSurgery.ResolveReferences(); SSCDefOf.SSC_Surgery_StartStomachDevelopment = stomachSurgery;
    }

    /// <summary>独立身体包含真实胃和口部；不更改旧子宫用例所用的共享身体。</summary>
    private static void SetupStomachBody(Pawn pawn)
    {
        // 游戏中同种身体共享 BodyDef；Scribe_BodyParts 也按该 Def 与索引保存部位引用。
        if (stomachTestBody == null)
        {
            stomachTestBody = new BodyDef { defName = "StomachTestBody", corePart = new BodyPartRecord { def = BodyPartDefOf.Torso } };
            stomachTestBody.corePart.parts.Add(new BodyPartRecord { def = SSCDefOf.Stomach, parent = stomachTestBody.corePart });
            stomachTestBody.corePart.parts.Add(new BodyPartRecord { def = stomachMouthDef, parent = stomachTestBody.corePart });
            stomachTestBody.ResolveReferences(); DefDatabase<BodyDef>.Add(stomachTestBody);
        }
        pawn.def.race.body = stomachTestBody; pawn.health.Notify_HediffChanged(null);
    }

    /// <summary>使用原医疗身体夹具，再给当前 Pawn 分配胃／口部齐备的新身体。</summary>
    private static Pawn StomachPawn(PawnIdentity identity = PawnIdentity.Slave)
    { var pawn = SurgeryPawn(identity); SetupStomachBody(pawn); return pawn; }

    /// <summary>返回该测试身体的原口部引用，不参与生产资格或接触判定。</summary>
    private static BodyPartRecord StomachMouth(Pawn pawn) => pawn.RaceProps.body.AllParts.Single(p => p.def == stomachMouthDef);

    /// <summary>经实际生产创建入口建立记录，额外进度仅用于指定测试状态。</summary>
    private static Hediff_SSCStomachDevelopment StartStomach(Pawn pawn, float progress = .01f)
    { Require(SSCStomachDevelopmentUtility.TryCreate(pawn, out var record, out var reason), reason); record.Severity = progress; return record; }

    /// <summary>读取唯一当前身体记录，排除断言指向已被移除的旧实例。</summary>
    private static float StomachProgress(Pawn pawn) => SSCStomachDevelopmentUtility.Find(pawn)?.Severity ?? 0f;

    /// <summary>建立指向实际胃部的原生医疗账单，接单检查继续走真实生产后缀。</summary>
    private static Bill_Medical StomachBill(Pawn pawn)
    {
        var bill = new Bill_Medical(stomachSurgery, null);
        AccessTools.Field(typeof(Bill_Medical), "part").SetValue(bill, SSCStomachDevelopmentUtility.FindStomach(pawn));
        pawn.BillStack.AddBill(bill); return bill;
    }

    /// <summary>按与保存时相同结构创建加载身体；只复用原版健康记录的 Scribe 外壳。</summary>
    private static Pawn LoadStomach(string path)
    {
        var pawn = StomachPawn(); HealthFixture fixture = null;
        Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture, "body", pawn); Scribe.loader.FinalizeLoading(); return fixture.Pawn;
    }

    /// <summary>在既有外部地图边界内执行新增胃部业务，不重复实现任何被测规则。</summary>
    private static void StomachDevelopmentTests(string repo, string game, Harmony harmony)
    {
        Check("stomach native resource and race registration", () =>
        {
            Equal(typeof(Hediff_SSCStomachDevelopment), stomachDefinition.hediffClass);
            Near(.01f, stomachDefinition.initialSeverity); Near(1, stomachDefinition.maxSeverity);
            Require(!stomachDefinition.keepOnBodyPartRestoration);
            Equal(uterusResearch, stomachSurgery.researchPrerequisite); Require(stomachSurgery.genderPrerequisite == null);
            Near(4000, stomachSurgery.workAmount); Near(0, stomachSurgery.deathOnFailedSurgeryChance);
            Equal(10, stomachSurgery.skillRequirements.Single().minLevel);
            Near(5, stomachSurgery.ingredients[0].GetBaseCount()); Near(6, stomachSurgery.ingredients[1].GetBaseCount());
            Require(stomachSurgery.recipeUsers.Any(p => p.defName.StartsWith("SurgeryRace")));
            Equal("恶堕化（胃）", stomachDefinition.label); Equal("胃部开发手术", stomachSurgery.label);
        });
        // 原版中毒因子需要故事讲述者的难度配置；宿主不运行任何故事事件。
        Current.Game.storyteller = Bare<Storyteller>(); Current.Game.storyteller.difficulty = new Difficulty();
        StomachBodyTests(); StomachSurgeryTests(); StomachGrowthTests(); StomachLifecycleTests();
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            Check("stomach native four-language finalized injection and parameters: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                var texts = new Dictionary<Type, Dictionary<string, string>>();
                // XML 路径和字段写入继续走原版注入器；以当前语言资源核对真实 Def 的全部九个字段。
                foreach (var type in new[] { typeof(HediffDef), typeof(RecipeDef) })
                {
                    var file = new TranslationFile(Path.Combine(repo, "1.7/Languages", language, "DefInjected", type.Name, "SSC_StomachDevelopment.xml"));
                    var package = new DefInjectionPackage(type); package.AddDataFromFile(file, out bool error, file.ReadAllText());
                    Require(!error && package.loadErrors.Count == 0); package.InjectIntoDefs(true);
                    Require(package.loadErrors.Count == 0 && package.injections.Values.All(p => p.injected));
                    texts[type] = XDocument.Load(file.FullPath).Root.Elements().ToDictionary(p => p.Name.LocalName, p => p.Value);
                }
                var hediffText = texts[typeof(HediffDef)]; var surgeryText = texts[typeof(RecipeDef)];
                Equal(hediffText[stomachDefinition.defName + ".label"], stomachDefinition.label);
                Equal(hediffText[stomachDefinition.defName + ".description"], stomachDefinition.description);
                Equal(surgeryText[stomachSurgery.defName + ".label"], stomachSurgery.label);
                Equal(surgeryText[stomachSurgery.defName + ".description"], stomachSurgery.description);
                Equal(surgeryText[stomachSurgery.defName + ".jobString"], stomachSurgery.jobString);
                var keyed = XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_StomachDevelopment.xml"))
                    .Root.Elements().ToDictionary(p => p.Name.LocalName, p => p.Value);
                foreach (var entry in keyed)
                {
                    Require(!entry.Value.Contains("WIP")); Equal(entry.Value, entry.Key.Translate().ToString());
                }
                // 进度由调用者提供；只替换 {0}，不得遗留参数、重复百分号或显示内部翻译键。
                string message = "SSC_StomachDevelopment_Started".Translate("1%");
                Equal(keyed["SSC_StomachDevelopment_Started"].Replace("{0}", "1%"), message);
                Require(message.Contains("1%") && !message.Contains("{0}") && !message.Contains("%%"));
                var pawn = StomachPawn(); var record = StartStomach(pawn, 1);
                // 同一实例遍历四档，确认注入后的阶段与共用正文实际出现在健康悬停中。
                foreach (var row in new[] { (.01f, 0), (.3f, 1), (.7f, 2), (1f, 3) })
                {
                    record.Severity = row.Item1;
                    string stage = hediffText[$"{stomachDefinition.defName}.stages.{row.Item2}.label"];
                    Equal(stage, record.CurStage.label);
                    string tip = record.GetTooltip(pawn, false);
                    Require(tip.Contains(stage) && tip.Contains(record.Description));
                    Require(!tip.Contains("WIP") && !tip.Contains("SSC_StomachDevelopment"));
                }
                Near(.5f, pawn.health.hediffSet.GetHungerRateFactor());
            });
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }

    /// <summary>检查真实原版属性计算、规范化、存读档及阶段边界。</summary>
    private static void StomachBodyTests()
    {
        Check("stomach creation targets stomach only and persists at one percent", () =>
        {
            var p = StomachPawn(); Require(SSCStomachDevelopmentUtility.Find(p) == null);
            var h = StartStomach(p); Equal(SSCStomachDevelopmentUtility.FindStomach(p), h.Part);
            Require(h.Part != p.RaceProps.body.corePart); h.TickInterval(60000); Near(.01f, h.Severity); Require(!h.ShouldRemove);
            Near(1, Factor(p));
        });
        foreach (var row in new[] { (.01f,1f,1f,1f),(.29999f,1f,1f,1f),(.3f,.9f,1.1f,1f),
            (.69999f,.9f,1.1f,1f),(.7f,.75f,1.25f,1f),(.99999f,.75f,1.25f,1f),(1f,.5f,1.5f,0f) })
            Check("stomach native stage hunger eating poison: " + row, () =>
            {
                var p = StomachPawn(); StartStomach(p, row.Item1);
                Near(row.Item2, p.health.hediffSet.GetHungerRateFactor()); Near(row.Item3, StageStat(p, "EatingSpeed"));
                Near(row.Item4, FoodUtility.GetFoodPoisonChanceFactor(p)); Near(1, Factor(p));
            });
        foreach (float hunger in new[] { .75f, .25f }) Check("stomach native artificial organ stacking: " + hunger, () =>
        {
            var p = StomachPawn(); StartStomach(p, 1);
            p.health.AddHediff(new HediffDef { defName = "ArtificialStomach" + ++nextId, hediffClass = typeof(Hediff),
                stages = new() { new HediffStage { hungerRateFactor = hunger } } }, SSCStomachDevelopmentUtility.FindStomach(p));
            Near(hunger == .75f ? .375f : .125f, p.health.hediffSet.GetHungerRateFactor());
        });
        Check("stomach completed record prevents native contaminated-meal poisoning without curing existing poisoning", () =>
        {
            var p = StomachPawn(); StartStomach(p, 1);
            var poisoning = new HediffDef { defName = "StomachExistingPoisoning", hediffClass = typeof(Hediff), initialSeverity = .4f };
            var existing = p.health.AddHediff(poisoning);
            var meal = new CompFoodPoisonable();
            AccessTools.Field(typeof(CompFoodPoisonable), "poisonPct").SetValue(meal, 1f);
            // 执行实际原版 PostIngested；×0 应阻止添加分支，已有中毒记录也不得被移除或修改。
            meal.PostIngested(p);
            Require(p.health.hediffSet.hediffs.Contains(existing)); Near(.4f, existing.Severity);
            Equal(1, p.health.hediffSet.hediffs.Count(h => h.def == poisoning));
        });
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, -1f, 0f, 2f }) Check("stomach severity is finite bounded: " + value, () =>
        { var h = StartStomach(StomachPawn()); h.Severity = value; Near(value == 2 ? 1 : 0, h.Severity); });
        Check("stomach duplicate native add uses maximum not sum", () =>
        {
            var p = StomachPawn(); var h = StartStomach(p,.4f); var next = HediffMaker.MakeHediff(stomachDefinition,p); next.Severity=.7f;
            p.health.AddHediff(next); Equal(h,SSCStomachDevelopmentUtility.Find(p)); Near(.7f,h.Severity);
            Equal(1,p.health.hediffSet.hediffs.Count(x=>x.def==stomachDefinition));
        });
        foreach (float progress in new[] { .01f,.7f,1f }) Check("stomach real Scribe preserves body attachment and progress: " + progress, () =>
        {
            var p=StomachPawn(); StartStomach(p,progress); var restored=LoadStomach(Save(p));
            Near(progress,StomachProgress(restored)); Equal(SSCStomachDevelopmentUtility.FindStomach(restored),SSCStomachDevelopmentUtility.Find(restored).Part);
        });
        Check("stomach missing part rejects creation without torso fallback", () =>
        { var p=SurgeryPawn(); Require(!SSCStomachDevelopmentUtility.TryCreate(p,out _,out string reason)); Equal("SSC_StomachDevelopment_MissingStomach".Translate().ToString(),reason); });
        Check("stomach removed record does not recreate after queries", () =>
        { var p=StomachPawn(); p.health.RemoveHediff(StartStomach(p,1)); Near(0,StomachProgress(p)); Near(1,p.health.hediffSet.GetHungerRateFactor()); });
    }

    /// <summary>实际资格报告、医疗账单及原版成败处理，妊娠始终保留。</summary>
    private static void StomachSurgeryTests()
    {
        foreach (Gender gender in new[] { Gender.Male,Gender.Female,Gender.None })
            foreach (bool pregnant in new[] { false,true }) Check("stomach admission gender and pregnancy: " + gender + pregnant, () =>
            {
                var p=StomachPawn(); p.gender=gender; if(pregnant)p.health.AddHediff(HediffDefOf.PregnantHuman);
                Require(SSCStomachSurgeryUtility.RecipientReport(p).Accepted); Require(stomachSurgery.Worker.AvailableReport(p).Accepted);
                Require(StomachBill(p).PawnAllowedToStartAnew(SurgeryPawn(PawnIdentity.Unset)));
            });
        foreach (string mode in new[] { "identity","young","mouth","research","started","wrong part" }) Check("stomach rejects unavailable patient: " + mode, () =>
        {
            var p=StomachPawn(); BodyPartRecord target=SSCStomachDevelopmentUtility.FindStomach(p);
            if(mode=="identity")p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
            if(mode=="young")p.ageTracker.AgeBiologicalTicks=17L*GenDate.TicksPerYear;
            if(mode=="mouth")p.health.AddHediff(HediffDefOf.MissingBodyPart,StomachMouth(p));
            if(mode=="research")researchProgress[uterusResearch]=0;
            if(mode=="started")StartStomach(p);
            if(mode=="wrong part")target=p.RaceProps.body.corePart;
            try { Require(!SSCStomachSurgeryUtility.RecipientReport(p,target).Accepted); }
            finally { researchProgress[uterusResearch]=float.MaxValue; }
        });
        Check("stomach waiting bill rechecks doctor skill and missing stomach", () =>
        {
            var p=StomachPawn(); var doctor=SurgeryPawn(PawnIdentity.Unset); var bill=StomachBill(p);
            doctor.skills.GetSkill(SkillDefOf.Medicine).Level=9; Require(!bill.PawnAllowedToStartAnew(doctor));
            doctor.skills.GetSkill(SkillDefOf.Medicine).Level=10;
            p.health.AddHediff(HediffDefOf.MissingBodyPart,bill.Part); Require(!bill.PawnAllowedToStartAnew(doctor));
        });
        foreach(bool success in new[]{false,true}) Check("stomach real surgery outcome and pregnancy retention: "+success,()=>
        {
            var p=StomachPawn(); p.gender=Gender.Male; var pregnancy=p.health.AddHediff(HediffDefOf.PregnantHuman); pregnancy.Severity=.42f;
            var bill=StomachBill(p); ResetSurgeryResult(success?1:0);
            try
            {
                stomachSurgery.Worker.ApplyOnPawn(p,bill.Part,SurgeryPawn(PawnIdentity.Unset),new(),bill);
                Near(success ? .01f : 0f,StomachProgress(p)); Equal(1,surgeryRolls); Equal(success?0:1,injuryCalls);
                Require(!p.Dead && p.health.hediffSet.hediffs.Contains(pregnancy)); Near(.42f,pregnancy.Severity); Equal(0,consumedStacks);
                if(success){ stomachSurgery.Worker.ApplyOnPawn(p,bill.Part,SurgeryPawn(),new(),bill); Equal(1,surgeryRolls); }
            }
            finally{ forcedQuality=null; }
        });
        Check("stomach surgery success callback removing stomach prevents landing",()=>
        {
            var p=StomachPawn();var bill=StomachBill(p);ResetSurgeryResult(1);
            afterOutcome=target=>target.health.AddHediff(HediffDefOf.MissingBodyPart,bill.Part);
            try { stomachSurgery.Worker.ApplyOnPawn(p,bill.Part,SurgeryPawn(),new(),bill);Near(0,StomachProgress(p));Equal(0,tales); }
            finally {afterOutcome=null;forcedQuality=null;}
        });
        Check("stomach native direct-death outcome disabled",()=>
        {
            var p=StomachPawn();Require(!stomachSurgery.surgeryOutcomeEffect.outcomes.OfType<SurgeryOutcome_Death>().Single()
                .Apply(0,stomachSurgery,SurgeryPawn(),p,SSCStomachDevelopmentUtility.FindStomach(p)));Require(!p.Dead);
        });
    }

    /// <summary>真实口交快照、三个完成出口及真实 Scribe 去重／旧字段处理。</summary>
    private static void StomachGrowthTests()
    {
        foreach(bool reverse in new[]{false,true}) Check("stomach ordinary strict oral and shared score once: "+reverse,()=>
        {
            var s=OrdinaryGrowth(reverse:reverse,oral:true);FinishOrdinary(s.scene);FinishOrdinary(s.scene);
            Near(.035f,StomachProgress(s.scene.Target));Near(.01f,Progress(s.scene.Target));Equal(1,growthScoreCalls);
        });
        foreach(string mode in new[]{"no contact","tongue","beak","finger","reversed endpoints","no record"})
            Check("stomach strict oral excludes: "+mode,()=>
            {
                var s=new GrowthScene(oral:true);var r=s.Props._resolved;
                if(mode=="no record")s.Target.health.RemoveHediff(SSCStomachDevelopmentUtility.Find(s.Target));
                if(mode=="no contact")r.Penetrations.Clear();
                if(mode is "tongue" or "beak")
                {
                    var part=new VanillaLewdablePart(s.Target,StomachMouth(s.Target),mode=="tongue"?LewdablePartFamily.Tongue:LewdablePartFamily.Beak);
                    r.RecipientParts=new(){part};r.Penetrations=new(){Tuple.Create(s.Penetrator,(ILewdablePart)part)};
                }
                if(mode=="finger")
                {
                    var hand=new VanillaLewdablePart(s.Source,s.Source.RaceProps.body.corePart,LewdablePartFamily.Hand);
                    r.InitiatorParts=new(){hand};r.Penetrations=new(){Tuple.Create((ILewdablePart)hand,s.Vagina)};
                }
                if(mode=="reversed endpoints")r.Penetrations=new(){Tuple.Create(s.Vagina,s.Penetrator)};
                var parts=CaptureParts(s.Props);var plan=Hidden(stomachGrowthType,null,"Capture",s.Target,parts,s.Props,50f);
                Require(plan==null);Near(mode=="no record"?0f:.01f,StomachProgress(s.Target));
            });
        Check("stomach daily completion pays exactly once",()=>
        {var s=DailyGrowth(oral:true);s.payout.initAction();s.payout.initAction();Near(.035f,StomachProgress(s.scene.Target));Equal(1,dailyOutcomes);});
        Check("stomach in-flight record replacement cannot inherit reward",()=>
        {
            var s=OrdinaryGrowth(oral:true);FinishOrdinary(s.scene,original:()=>
            {s.scene.Target.health.RemoveHediff(SSCStomachDevelopmentUtility.Find(s.scene.Target));StartStomach(s.scene.Target);});
            Near(.01f,StomachProgress(s.scene.Target));
        });
        Check("stomach live endpoint reversal cannot reuse original oral evidence", () =>
        {
            var s = OrdinaryGrowth(oral: true);
            FinishOrdinary(s.scene, original: () => s.scene.Props._resolved.Penetrations[0] = Tuple.Create(s.scene.Vagina, s.scene.Penetrator));
            Near(.01f, StomachProgress(s.scene.Target));
        });
        Check("stomach growth cap finishes only remaining progress", () =>
        {
            var s = OrdinaryGrowth(oral: true); SSCStomachDevelopmentUtility.Find(s.scene.Target).Severity = .99f;
            growthScore = 10000; FinishOrdinary(s.scene); Near(1, StomachProgress(s.scene.Target));
        });
        foreach(int contactPhase in new[]{0,5}) Check("stomach ritual earlier/final phase evidence pays once: "+contactPhase,()=>
        {
            ResetGrowthFlow();var f=new GrowthRitual(new GrowthScene(oral:true));
            for(int i=0;i<6;i++)f.Phase(contact:i==contactPhase);
            Near(.02875f,StomachProgress(f.Scene.Target));f.Worker.Apply(1,new(),f.Job);Near(.02875f,StomachProgress(f.Scene.Target));
            BindingRitualStateUtility.EndRitual(f.Job);
            Require(AccessTools.Field(typeof(CompSexSlaveTraining),"stomachRitualEvidence").GetValue(f.Comp)==null);
        });
        foreach(bool old in new[]{false,true}) Check("stomach ordinary Scribe oral flag default and claim: "+old,()=>
        {
            var s=OrdinaryGrowth(oral:true);var restored=RestoreDriver<GrowthOrdinaryDriver>(s.driver);
            RoundTripGrowth(s.driver,restored,s.scene,doc=>{if(old)doc.Descendants("receivedPenileOralPenetration").Remove();});
            using var ctx=(SSCOrdinarySexOutcomeUtility.Context)Hidden(guardType,null,"PrepareOrdinaryOutcome",restored.Sexprops);
            Require(ctx.TryClaim());ctx.Dispatch();Near(old ? .01f : .035f,StomachProgress(s.scene.Target));Require(!ctx.TryClaim());
        });
        foreach(bool old in new[]{false,true}) Check("stomach ritual Scribe completed oral evidence: "+old,()=>
        {
            ResetGrowthFlow();var f=new GrowthRitual(new GrowthScene(oral:true));f.Phase();
            var original=(JobDriver_RitualTraining)f.Scene.Source.jobs.curDriver;var restored=RestoreDriver<JobDriver_RitualTraining>(original);
            var field=AccessTools.Field(typeof(CompSexSlaveTraining),"stomachRitualEvidence");
            RoundTripGrowth(original,restored,f.Scene,doc=>{if(old)doc.Descendants("stomachRitualEvidence").Remove();field.SetValue(f.Comp,null);},f.Comp);
            for(int i=1;i<6;i++)f.Phase(contact:false);Near(old ? .01f : .02875f,StomachProgress(f.Scene.Target));
        });
        foreach (bool old in new[] { false, true }) Check("stomach daily Scribe strict oral flag: " + old, () =>
        {
            var s = DailyGrowth(oral: true); var restored = RestoreDriver<JobDriver_Training>(s.driver);
            RoundTripGrowth(s.driver, restored, s.scene, doc => { if (old) doc.Descendants("receivedPenileOralPenetration").Remove(); });
            var toils = ((IEnumerable<Toil>)Hidden(typeof(JobDriver_Training), restored, "MakeNewToils")).ToArray();
            toils.Last().initAction(); toils.Last().initAction();
            Near(old ? .01f : .035f, StomachProgress(s.scene.Target)); Equal(1, dailyOutcomes);
        });
        Check("stomach ritual interruption clears temporary evidence",()=>
        {
            ResetGrowthFlow();var f=new GrowthRitual(new GrowthScene(oral:true));f.Phase();BindingRitualStateUtility.EndRitual(f.Job);
            Require(AccessTools.Field(typeof(CompSexSlaveTraining),"stomachRitualEvidence").GetValue(f.Comp)==null);Near(.01f,StomachProgress(f.Scene.Target));
        });
    }

    /// <summary>身体状态不随身份取消，克隆照常复制，凝胶化照常保留；人格分类只排除指定两项。</summary>
    private static void StomachLifecycleTests()
    {
        Check("stomach identity changes preserve existing body record",()=>
        {
            var p=StomachPawn();var h=StartStomach(p,.7f);p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
            Equal(h,SSCStomachDevelopmentUtility.Find(p));Near(.7f,h.Severity);
        });
        Check("stomach native removal makes old record neutral and restores no history",()=>
        {
            var p=StomachPawn();var h=StartStomach(p,1);p.health.AddHediff(HediffDefOf.MissingBodyPart,h.Part);
            Require(SSCStomachDevelopmentUtility.Find(p)==null);Near(1,h.CurStage.hungerRateFactor);
            p.health.RestorePart(h.Part);Near(0,StomachProgress(p));
        });
        Check("stomach native clone pipeline copies progress to current stomach",()=>
        {
            var p=StomachPawn();StartStomach(p,.7f);var clone=StomachPawn();
            Hidden(typeof(RabbitCloneUtility),null,"CopySscHediffsFromSource",p,clone);
            Near(.7f,StomachProgress(clone));Equal(SSCStomachDevelopmentUtility.FindStomach(clone),SSCStomachDevelopmentUtility.Find(clone).Part);
        });
        Check("stomach full gelatinization preserves record and old six requirements",()=>
        {
            var p=StomachPawn();var h=StartStomach(p,.7f);var previous=stomachDefinition.modContentPack;
            var pack=Bare<ModContentPack>();AccessTools.Field(typeof(ModContentPack),"packageIdInt").SetValue(pack,"rjw.sexslavecraft.tiejinver");
            stomachDefinition.modContentPack=pack;
            try{FullGelatinizationUtility.CompleteSuccess(p);Equal(h,SSCStomachDevelopmentUtility.Find(p));Near(.7f,h.Severity);}
            finally{stomachDefinition.modContentPack=previous;}
        });
        Check("stomach body identity filter excludes only the two developments",()=>
        {
            Require(SSCBodyDevelopmentUtility.IsBodyStateDef(stomachDefinition));Require(SSCBodyDevelopmentUtility.IsBodyStateDef(development));
            Require(!SSCBodyDevelopmentUtility.IsBodyStateDef(new HediffDef{defName="SSC_Unrelated"}));
        });
    }
}
