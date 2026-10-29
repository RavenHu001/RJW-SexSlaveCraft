// AI 辅助生成：真实候选、XML、属性消费者、教学/陪玩补丁与原生Scribe。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static RecipeDef_PSTag fertilityRecipe;
    private static Thing fertilityProduct;

    /// <summary>扩展现有原生宿主；图形、地图放置与完整健康状态机仍是边界。</summary>
    private static void FertilityTests(string repo, Harmony harmony)
    {
        // 复用原翻译器，将本批Keyed内容并入当前已载入语言。
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
        bootstrap = true;
        try { RuntimeHelpers.RunClassConstructor(typeof(rjw.xxx).TypeHandle); }
        finally { bootstrap = false; }
        errors.Clear();
        human.race.gestationPeriodDays = 20;
        if (DefDatabase<HediffDef>.GetNamedSilentFail("FertilityEnhancer") == null) PregnancyDef("FertilityEnhancer");
        var xml = new XmlDocument();
        foreach (string rel in new[] { "HediffDefs/SSC_UterusDevelopment.xml", "HediffDefs/SSC_FertilitySpecialization.xml" })
        {
            xml.Load(Path.Combine(repo, "1.7/Defs", rel));
            foreach (XmlNode n in xml.DocumentElement.SelectNodes("HediffDef")) RegisterFertility(DirectXmlToObject.ObjectFromXml<HediffDef>(n, false));
        }
        development = DefDatabase<HediffDef>.GetNamed(SSCUterusDevelopmentUtility.DefName);
        foreach (string name in new[] { "SSC_RES_SlaveSpecialization", "SSC_RES_UterusDevelopment" })
            RegisterFertility(new ResearchProjectDef { defName = name });
        RegisterFertility(new ResearchTabDef { defName = "SSC_ResearchTabDef" });
        xml.Load(Path.Combine(repo, "1.7/Defs/ResearchDefs/SSC_FertilitySpecialization.xml"));
        var research = DirectXmlToObject.ObjectFromXml<ResearchProjectDef>(xml.DocumentElement.FirstChild, false); DefDatabase<ResearchProjectDef>.Add(research);
        // 配方仍经原生XML/引用解析；外部台、音效和凝胶定义只提供必要环境。
        var table = Bare<ThingDef>(); table.defName = "TableSculpting"; RegisterFertility(table);
        if (DefDatabase<StatDef>.GetNamedSilentFail("GeneralLaborSpeed") == null) DefDatabase<StatDef>.Add(new StatDef { defName = "GeneralLaborSpeed" });
        RegisterFertility(new EffecterDef { defName = "Sculpt" });
        RegisterFertility(new SoundDef { defName = "Recipe_Sculpt" });
        foreach (string name in new[] { "SSC_PersonalitySlime", "SSC_PersonalitySlime_PLUS", "SSC_PersonalitySlime_ULTRA", "SSC_PersonalitySlime_PLUS_ULTRA", "SSC_PersonalitySlime_Edited", "SSC_PersonalitySlime_PLUS_Edited", "SSC_PersonalitySlime_ULTRA_Edited", "SSC_PersonalitySlime_PLUS_ULTRA_Edited" })
        {
            var def = Bare<ThingDef>(); def.defName=name; def.category=ThingCategory.Item; def.thingClass=typeof(ThingWithComps);
            def.stackLimit=1; def.destroyable=true; def.comps=new() { new CompProperties { compClass=typeof(CompPersonalityStore) } };
            RegisterFertility(def);
        }
        SSCDefOf.SSC_PersonalitySlime=DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime");
        SSCDefOf.SSC_PersonalitySlime_Edited=DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime_Edited");
        xml.Load(Path.Combine(repo, "1.7/Defs/RecipeDefs/SSC_FertilitySpecialization.xml"));
        fertilityRecipe = (RecipeDef_PSTag)DirectXmlToObject.ObjectFromXml<RecipeDef>(xml.DocumentElement.FirstChild, false);
        DefDatabase<RecipeDef>.Add(fertilityRecipe);
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        Current.Game.World = Bare<World>(); Current.Game.World.worldPawns = new WorldPawns();
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly)), prefix: new HarmonyMethod(typeof(Program), nameof(PregnancyTypeBoundary)));
        LoadPregnancyResources(repo, Path.GetDirectoryName(typeof(Pawn).Assembly.Location));
        Require(errors.Count == 0, string.Join('\n', errors));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(GenPlace), nameof(GenPlace.TryPlaceThing), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing,int>), typeof(Predicate<IntVec3>), typeof(Rot4?), typeof(int) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(FertilityPlace)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(LearningUtility), nameof(LearningUtility.LearningRateFactor)), postfix: new HarmonyMethod(typeof(Harmony_FertilityLessonRate), nameof(Harmony_FertilityLessonRate.Postfix)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(JobDriver_Lessongiving), "MakeNewToils"), postfix: new HarmonyMethod(typeof(Harmony_FertilityLessonTime), nameof(Harmony_FertilityLessonTime.Postfix)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.FaceTarget)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(BabyPlayUtility), nameof(BabyPlayUtility.PlayTickCheckEnd)), prefix: new HarmonyMethod(typeof(Harmony_FertilityBabyPlay), nameof(Harmony_FertilityBabyPlay.Prefix)), postfix: new HarmonyMethod(typeof(Harmony_FertilityBabyPlay), nameof(Harmony_FertilityBabyPlay.Postfix)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.TickInterval)), transpiler: new HarmonyMethod(typeof(Program), nameof(NativePregnancyTranspiler)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(rjw.Hediff_BasePregnancy), "GetGestationPeriod"), postfix: new HarmonyMethod(typeof(Program), nameof(RjwPregnancyPostfix)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Hediff_Pregnant), "NotifyPlayerOfTrimesterPassing"), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        InstallFertilityPatch(harmony,AccessTools.PropertyGetter(typeof(Need_Food), nameof(Need_Food.CurCategory)), prefix: new HarmonyMethod(typeof(Program), nameof(PregnancyHungerBoundary)));
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.StartLabor)), prefix: new HarmonyMethod(typeof(Program), nameof(LaborBoundary)));
        Current.Game.storyteller = Bare<Storyteller>(); Current.Game.storyteller.difficulty = new Difficulty { babiesAreHealthy = true };
        pregnancyHunger = HungerCategory.Fed;

        Check("fertility real research and recipe use approved gates", () =>
        {
            Near(1200, research.baseCost); Near(3500, fertilityRecipe.workAmount); Near(1, fertilityRecipe.ingredients.Single().GetBaseCount());
            var p = FertilityPawn(); Require(!FertilitySpecializationUtility.CanSelect(p, out _));
            ((Dictionary<ResearchProjectDef,float>)AccessTools.Field(typeof(ResearchManager),"progress").GetValue(Current.Game.researchManager))[research]=1200; Require(FertilitySpecializationUtility.CanSelect(p, out _));
        });
        Check("fertility admission pauses without a valid complete uterus or adult slave identity", () =>
        {
            var p = ImprintPawn(PawnIdentity.Slave); Require(!FertilitySpecializationUtility.MeetsConditions(p, out _));
            var uterus = Start(p, .99f); Require(!FertilitySpecializationUtility.MeetsConditions(p, out _));
            uterus.Severity = 1; Require(FertilitySpecializationUtility.MeetsConditions(p, out _));
            p.ageTracker.AgeBiologicalTicks = 17L * GenDate.TicksPerYear; Require(!FertilitySpecializationUtility.MeetsConditions(p, out _));
            p.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear; p.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
            Require(!FertilitySpecializationUtility.MeetsConditions(p, out _));
        });
        Check("fertility ordinary factor combines with uterus once and completion uses .999", () =>
        {
            var p = FertilityPawn(.5f); Toggle(p,true); Near(3.5f, Factor(p));
            var c=p.TryGetComp<CompSexSlaveTraining>(); c.specializationProgress=.999f; FertilitySpecializationUtility.Sync(p); Near(4.5f,Factor(p));
            p.health.RemoveHediff(SSCUterusDevelopmentUtility.Find(p)); Near(1,Factor(p)); Start(p,1); Toggle(p,true); Near(4.5f,Factor(p));
            c.SetSpecialization(SexSlaveSpecializationType.None); Near(2.5f,Factor(p)); c.SetSpecialization(SexSlaveSpecializationType.Fertility); Near(4.5f,Factor(p));
        });
        Check("fertility final persists across directions while invalid bodies return neutral", () =>
        {
            var p=FertilityPawn(); p.health.AddHediff(FertilitySpecializationUtility.FinalDef); FertilitySpecializationUtility.Sync(p);
            p.TryGetComp<CompSexSlaveTraining>().SetSpecialization(SexSlaveSpecializationType.None); Toggle(p,true); Near(5,Factor(p)); Near(1.5f,FertilitySpecializationUtility.CareFactor(p));
            Require(!p.health.hediffSet.HasHediff(FertilitySpecializationUtility.OrdinaryDef));
            var c=p.TryGetComp<CompSexSlaveTraining>(); c.pawnIdentity=PawnIdentity.Unset; FertilitySpecializationUtility.Sync(p); Near(2.5f,Factor(p)); Require(FertilitySpecializationUtility.HasFinalState(p));
            c.pawnIdentity=PawnIdentity.Slave; Near(5,Factor(p));
        });
        Check("fertility unified training uses original score and bound-master bonus", () =>
        {
            var p=FertilityPawn(); var t=ImprintPawn(PawnIdentity.Master); var c=p.TryGetComp<CompSexSlaveTraining>();
            Near(.04f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(t,p,100));
            Require(SSCBondUtility.Bind(t,p)); Near(.06f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(t,p,100));
            c.specializationProgress=.999f; Near(0,SpecializationTrainingProgressUtility.NotifyTrainingCompleted(t,p,100));
        });
        Check("fertility care keeps remainder, has no daily cap and rejects duplicate or stale callbacks", () =>
        {
            var p=FertilityPawn(); var c=p.TryGetComp<CompSexSlaveTraining>(); object token=SSCPersonalityRuntimeUtility.Capture(p);
            AdvanceCare(c,token,1250); Equal(1250,c.fertilityCareTicks); c.GainFertilityCare(1250,token); Equal(1250,c.fertilityCareTicks);
            AdvanceCare(c,token,6250); Near(.03f,c.specializationProgress); Equal(0,c.fertilityCareTicks);
            var old=token; using (SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope) ? scope : throw new Exception()) { }
            AdvanceCare(c,old,2500); Near(.03f,c.specializationProgress);
            var newToken=SSCPersonalityRuntimeUtility.Capture(p); c.SetSpecialization(SexSlaveSpecializationType.None); AdvanceCare(c,newToken,2500); c.SetSpecialization(SexSlaveSpecializationType.Fertility); Near(.03f,c.specializationProgress);
        });
        Check("fertility gel copy Scribe processing and full insertion preserve source and body ownership", () =>
        {
            var source=FertilityPawn(.999f); var c=source.TryGetComp<CompSexSlaveTraining>(); c.fertilityCareTicks=1234;
            SSCImprintUtility.TryGrantInitial(source); var data=new CompPersonalityStore(); data.StorePawnData(source);
            Require(SpecializationFinalizationRecipeUtility.IsEligibleGel(fertilityRecipe,data));
            var gel=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SSC_PersonalitySlime")); gel.GetComp<CompPersonalityStore>().CopyFrom(data);
            fertilityProduct=null; new Recipe_PSTagWorker { recipe=fertilityRecipe }.Notify_IterationCompleted(source,new List<Thing>{gel}); Require(fertilityProduct != null);
            var fixture=new ImprintGelFixture { Data=fertilityProduct.TryGetComp<CompPersonalityStore>() }; string path=Path.Combine(saveDirectory,"fertility-gel.xml");
            Scribe.saver.InitSaving(path,"Gel"); Scribe_Deep.Look(ref fixture,"data"); Scribe.saver.FinalizeSaving(); fixture=null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture,"data"); Scribe.loader.FinalizeLoading(); data=fixture.Data;
            Equal(1234,data.fertilityCareTicks); Require(!data.HasTag(FertilitySpecializationUtility.OrdinaryDef)); Require(!SpecializationFinalizationRecipeUtility.IsEligibleGel(fertilityRecipe,data));
            var host=ImprintPawn(PawnIdentity.Slave); Require(ExcretionUtility.InheritEverything(host,data)); Require(FertilitySpecializationUtility.HasFinalState(host)); Equal(-1,FertilitySpecializationUtility.Stage(host));
            Equal(SSCImprintKind.Initial,SSCImprintUtility.GetKind(host)); Equal(1234,host.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks);
            var uterus=Start(host,1); Toggle(host,true); Near(5,Factor(host));
            var empty=new CompPersonalityStore(); empty.StorePawnData(ImprintPawn()); Require(ExcretionUtility.InheritEverything(host,empty));
            Require(!FertilitySpecializationUtility.HasFinalState(host)); Equal(0,host.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks); Require(ReferenceEquals(uterus,SSCUterusDevelopmentUtility.Find(host))); Require(uterus.FertilityEnabled);
            FertilitySpecializationGelUtility.DetachAfterExtraction(source); Equal(0,c.fertilityCareTicks); Equal(SexSlaveSpecializationType.None,c.specializationType); Require(SSCUterusDevelopmentUtility.Find(source)!=null);
        });
        Check("fertility training Comp Scribe preserves remainder and old saves default to zero", () =>
        {
            var source=FertilityPawn(.5f); var c=source.TryGetComp<CompSexSlaveTraining>(); c.fertilityCareTicks=1250;
            var fixture=new FertilityCompFixture(c); string path=Path.Combine(saveDirectory,"fertility-comp.xml");
            Scribe.saver.InitSaving(path,"Comp"); Scribe_Deep.Look(ref fixture,"data"); Scribe.saver.FinalizeSaving();
            var target=FertilityPawn().TryGetComp<CompSexSlaveTraining>(); fixture=null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture,"data",target); Scribe.loader.FinalizeLoading(); Equal(1250,target.fertilityCareTicks);
            AdvanceCare(target,SSCPersonalityRuntimeUtility.Capture(target.parent as Pawn),1250); Near(.51f,target.specializationProgress);
            Edit(path,doc=>doc.Descendants("fertilityCareTicks").Remove()); fixture=null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture,"data",target); Scribe.loader.FinalizeLoading(); Equal(0,target.fertilityCareTicks);
        });
        FertilityEffectDisplayTests(harmony);
        FertilityRecipeReviewTests(harmony);
        FertilityUiReviewTests(harmony);
        FertilityPregnancyTests();
        InstallFertilityPatch(harmony,AccessTools.PropertyGetter(typeof(SkillRecord), nameof(SkillRecord.TotallyDisabled)), prefix: new HarmonyMethod(typeof(Program), nameof(FertilitySkillEnabled)));
        FertilityActivityTests(harmony);
    }

    /// <summary>实际健康属性列表与悬停均应包含自定义倍率，且不丢失子宫原版属性行。</summary>
    private static void FertilityEffectDisplayTests(Harmony harmony)
    {
        FleshTypeDefOf.Mechanoid ??= new FleshTypeDef { defName="Mechanoid" };
        foreach (string name in stageStats) DefDatabase<StatDef>.GetNamed(name).label ??= name;
        StatCategoryDefOf.CapacityEffects ??= new StatCategoryDef { defName="CapacityEffects",label="Effects" };
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(HediffStatsUtility),nameof(HediffStatsUtility.SpecialDisplayStats)),postfix:new HarmonyMethod(typeof(Program),nameof(FertilityDisplayPostfix)));
        Check("fertility health display preserves native effects and exposes individual duration and care factors", () =>
        {
            var pawn=FertilityPawn(.5f); var uterus=SSCUterusDevelopmentUtility.Find(pawn); Toggle(pawn,true);
            var ordinary=pawn.health.hediffSet.GetFirstHediffOfDef(FertilitySpecializationUtility.OrdinaryDef);
            string Value(Hediff h,string key)=>HediffStatsUtility.SpecialDisplayStats(h.CurStage,h).Single(e=>e.LabelCap==key.Translate().ToString()).ValueString;
            Equal("×0.8",Value(uterus,"SSC_HediffFertility_Duration")); Equal("×0.75",Value(ordinary,"SSC_HediffFertility_Duration"));
            Equal("×1.2",Value(ordinary,"SSC_HediffFertility_Education")); Equal("×1.2",Value(ordinary,"SSC_HediffFertility_BabyPlay"));
            var entries=HediffStatsUtility.SpecialDisplayStats(uterus.CurStage,uterus).ToList();
            foreach (var factor in uterus.CurStage.statFactors) Require(entries.Any(e=>e.LabelCap==factor.stat.LabelCap.ToString()),factor.stat.defName);
            Require(ordinary.TipStringExtra.Contains("SSC_HediffFertility_Duration".Translate().ToString()));
            Require(ordinary.TipStringExtra.Contains("SSC_HediffFertility_Education".Translate().ToString()));
            Toggle(pawn,false); Equal("×1",Value(uterus,"SSC_HediffFertility_Duration")); Equal("×1",Value(ordinary,"SSC_HediffFertility_Duration"));
            pawn.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
            Equal("×1",Value(ordinary,"SSC_HediffFertility_Education"));
        });
    }

    private static int fertilityGuardRejects;

    /// <summary>消耗前终止任务是引擎边界；原料资格与守卫闭包仍执行生产实现。</summary>
    private static bool FertilityRejectJob() { fertilityGuardRejects++; return false; }

    /// <summary>筛料遵循原账单拒绝，坏产物与最终结算时失格均保料。</summary>
    private static void FertilityRecipeReviewTests(Harmony harmony)
    {
        ThingWithComps Gel()
        {
            var gel=(ThingWithComps)ThingMaker.MakeThing(SSCDefOf.SSC_PersonalitySlime);
            gel.GetComp<CompPersonalityStore>().StorePawnData(FertilityPawn(.999f));
            return gel;
        }
        Check("fertility screening preserves bill rejection and refuses a product without personality storage", () =>
        {
            var gel=Gel(); var bill=Bare<Bill_Production>(); bill.recipe=fertilityRecipe;
            bool allowed=false; Patch_IsFixedOrAllowedIngredient.Postfix(bill,gel,ref allowed); Require(!allowed);
            allowed=true; Patch_IsFixedOrAllowedIngredient.Postfix(bill,gel,ref allowed); Require(allowed);
            var product=PersonalityGelUtility.GetEditedThingDef(gel.def); var saved=product.comps;
            try
            {
                product.comps=new List<CompProperties>(); allowed=true;
                Patch_IsFixedOrAllowedIngredient.Postfix(bill,gel,ref allowed); Require(!allowed);
                fertilityProduct=null; new Recipe_PSTagWorker { recipe=fertilityRecipe }.Notify_IterationCompleted(ImprintPawn(),new List<Thing>{gel});
                Require(fertilityProduct==null && !gel.Destroyed);
            }
            finally { product.comps=saved; }
        });
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob)),prefix:new HarmonyMethod(typeof(Program),nameof(FertilityRejectJob)));
        Check("fertility final completion rechecks source and malformed output before consuming", () =>
        {
            var gel=Gel(); var actor=ImprintPawn(); actor.jobs=new Pawn_JobTracker(actor);
            var bill=Bare<Bill_Production>(); bill.recipe=fertilityRecipe;
            actor.jobs.curJob=new Job(new JobDef { defName="FertilityReviewWork" }); actor.CurJob.bill=bill;
            actor.CurJob.placedThings=new List<ThingCountClass>{new ThingCountClass(gel,1)};
            int completed=0; var toil=new Toil { actor=actor,initAction=()=>completed++ };
            Harmony_TrainerRecipeCompletion.Postfix(toil); toil.initAction(); Equal(1,completed);
            fertilityGuardRejects=0; gel.GetComp<CompPersonalityStore>().specializationProgress=.9f; toil.initAction();
            Equal(1,completed); Equal(1,fertilityGuardRejects); Require(!gel.Destroyed);
            gel.GetComp<CompPersonalityStore>().specializationProgress=.999f;
            var saved=fertilityRecipe.hediffToAdd;
            try { fertilityRecipe.hediffToAdd=null; toil.initAction(); Equal(1,completed); Equal(2,fertilityGuardRejects); }
            finally { fertilityRecipe.hediffToAdd=saved; }
        });
    }

    /// <summary>复用字体测量边界，调用真实状态文字与特化区域测高，覆盖长行和跨方向终极。</summary>
    private static void FertilityUiReviewTests(Harmony harmony)
    {
        InstallFertilityPatch(harmony,AccessTools.Method(typeof(Text),nameof(Text.CalcHeight),new[]{typeof(string),typeof(float)}),prefix:new HarmonyMethod(typeof(Program),nameof(SensitivityTextHeight)));
        InstallFertilityPatch(harmony,AccessTools.PropertyGetter(typeof(Text),nameof(Text.LineHeight)),prefix:new HarmonyMethod(typeof(Program),nameof(SensitivityLineHeight)));
        Check("fertility status reserves wrapped height for retained final effects", () =>
        {
            var pawn=FertilityPawn(.5f); var comp=pawn.TryGetComp<CompSexSlaveTraining>(); var tab=typeof(ITab_SexSlaveTraining);
            float Height(float width)=>(float)AccessTools.Method(tab,"GetSpecializationSectionHeight").Invoke(null,new object[]{pawn,comp,width});
            float normal=Height(322f); Require(Height(140f)>normal);
            pawn.health.AddHediff(FertilitySpecializationUtility.FinalDef); comp.SetSpecialization(SexSlaveSpecializationType.None);
            string text=(string)AccessTools.Method(tab,"GetFertilityStatusText").Invoke(null,new object[]{pawn,comp}); Require(!string.IsNullOrEmpty(text));
            Require(Height(140f)>124f); Require(Height(140f)>=Height(322f));
        });
    }

    /// <summary>完整宿主已登记的同一入口不重装第二份孕期补丁。</summary>
    private static void InstallFertilityPatch(Harmony harmony, MethodBase method, HarmonyMethod prefix=null, HarmonyMethod postfix=null, HarmonyMethod transpiler=null)
    {
        if (Harmony.GetPatchInfo(method)?.Owners.Contains(harmony.Id) == true) return;
        harmony.Patch(method,prefix,postfix,transpiler);
    }
    /// <summary>完整宿主与专项共用注册，不重复添加同名外部定义。</summary>
    private static void RegisterFertility<T>(T def) where T : Def, new()
    { if (DefDatabase<T>.GetNamedSilentFail(def.defName)==null) DefDatabase<T>.Add(def); }

    // 独立宿主没有任务禁工系统；实际经验、学习倍率与技能记录继续执行原生实现。
    private static bool FertilitySkillEnabled(ref bool __result) { __result=false; return false; }
    /// <summary>截获实际生成的产物，只隔离地图放置，不替换配方工作者。</summary>
    private static bool FertilityPlace(Thing __0, ref Thing __4, ref bool __result) { fertilityProduct=__0; __4=__0; __result=true; return false; }
    /// <summary>经生产子宫创建及方向选择建立独立测试人格。</summary>
    private static Pawn FertilityPawn(float progress=0)
    {
        var p=ImprintPawn(PawnIdentity.Slave); p.gender=Gender.Female; p.RaceProps.gestationPeriodDays=20;
        Start(p,1); var c=p.TryGetComp<CompSexSlaveTraining>(); c.SetSpecialization(SexSlaveSpecializationType.Fertility); c.specializationProgress=progress; FertilitySpecializationUtility.Sync(p); return p;
    }
    /// <summary>推进游戏时钟后调用生产计时入口，重复回调另由用例直接调用。</summary>
    private static void AdvanceCare(CompSexSlaveTraining comp,object token,int delta)
    { AccessTools.Field(typeof(TickManager),"ticksGameInt").SetValue(Current.Game.tickManager,Current.Game.tickManager.TicksGame+delta); comp.GainFertilityCare(delta,token); }

    /// <summary>覆盖实际新孕、冻结待遇、初始化及旧字段兼容，不处理后续婴儿生成。</summary>
    private static void FertilityPregnancyTests()
    {
        Check("fertility native real Tick keeps acquired duration after stage change and reaches labor", () =>
        {
            var p=FertilityPawn(.5f); Toggle(p,true); var food=Bare<Need_Food>(); AccessTools.Field(typeof(Need),"pawn").SetValue(food,p); food.def=new NeedDef { defName="Food" }; p.needs.food=food; var h=(Hediff_Pregnant)NewPregnancy(p,"PregnantHuman"); h.Severity=.1f;
            Near(.6f,h.TryGetComp<HediffComp_SSCUterusPregnancy>().DurationFactor); h.TickInterval(1200); Near(.1f+1200f/(20*60000*.6f),h.GestationProgress);
            p.TryGetComp<CompSexSlaveTraining>().specializationProgress=.999f; h.PostAdd(null); Near(.6f,h.TryGetComp<HediffComp_SSCUterusPregnancy>().DurationFactor);
            var restored=Load(Save(p)); Near(.6f,restored.health.hediffSet.hediffs.Single(x=>x.def.defName=="PregnantHuman").TryGetComp<HediffComp_SSCUterusPregnancy>().DurationFactor);
            h.Severity=.9999f; laborCalls=0; h.TickInterval(1200); Equal(1,laborCalls);
        });
        Check("fertility RJW Initialize applies final duration only once", () =>
        {
            var p=FertilityPawn(); p.health.AddHediff(FertilitySpecializationUtility.FinalDef); Toggle(p,true); var h=(rjw.Hediff_BasePregnancy)NewPregnancy(p,"RJW_pregnancy");
            h.babies=new(){NewPawn()}; rjw.RJWPregnancySettings.normal_pregnancy_duration=1; h.Initialize(p,null,default); Near(384000,h.p_end_tick-h.p_start_tick);
            h.Initialize(p,null,default); Near(384000,h.p_end_tick-h.p_start_tick);
        });
        Check("fertility old shortened and missing decision do not acquire new stage after load", () =>
        {
            var p=FertilityPawn(); Toggle(p,true); NewPregnancy(p,"PregnantHuman"); var path=Save(p);
            p.TryGetComp<CompSexSlaveTraining>().specializationProgress=.999f;
            var loaded=Load(path); Near(.8f,loaded.health.hediffSet.hediffs.Single(x=>x.def.defName=="PregnantHuman").TryGetComp<HediffComp_SSCUterusPregnancy>().DurationFactor);
            Edit(path,doc=>doc.Descendants("sscUterusPregnancyDecision").Remove()); loaded=Load(path);
            Near(1,loaded.health.hediffSet.hediffs.Single(x=>x.def.defName=="PregnantHuman").TryGetComp<HediffComp_SSCUterusPregnancy>().DurationFactor);
        });
    }

    /// <summary>执行实际课堂和陪玩消费者，课堂培养不依赖学生技能经验增长。</summary>
    private static void FertilityActivityTests(Harmony harmony)
    {
        Check("fertility real baby play gains care bonus and only counts effective play time", () =>
        {
            var adult=FertilityPawn(.5f); var baby=ImprintPawn(); var faction=new Faction(); adult.SetFactionDirect(faction); baby.SetFactionDirect(faction);
            baby.needs.play=new Need_Play(baby); baby.needs.play.CurLevel=.1f;
            BabyPlayUtility.PlayTickCheckEnd(baby,adult,1,100); Near(.124f,baby.needs.play.CurLevel); Equal(100,adult.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks);
            baby.needs.play.CurLevel=1; BabyPlayUtility.PlayTickCheckEnd(baby,adult,1,100); Equal(100,adult.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks);
        });
        Check("fertility Biotech activity adapters skip safely without the DLC", () =>
        {
            var adult=FertilityPawn(.5f); var baby=ImprintPawn(); var faction=new Faction(); adult.SetFactionDirect(faction); baby.SetFactionDirect(faction);
            baby.needs.play=new Need_Play(baby); baby.needs.play.CurLevel=.1f;
            SetStatic(typeof(ModsConfig),"biotechActive",false);
            try
            {
                BabyPlayUtility.PlayTickCheckEnd(baby,adult,1,100); Near(.12f,baby.needs.play.CurLevel);
                Equal(0,adult.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks); Require(FertilityCareUtility.LessonTeacher(baby)==null);
            }
            finally { SetStatic(typeof(ModsConfig),"biotechActive",true); }
        });
        Check("fertility classroom student multiplier stacks with initial imprint and teacher time is single", () =>
        {
            var teacher=FertilityPawn(); teacher.health.AddHediff(FertilitySpecializationUtility.FinalDef); var student=ImprintPawn();
            var faction=new Faction(); teacher.SetFactionDirect(faction); student.SetFactionDirect(faction);
            JobDefOf.Lessongiving=new JobDef { defName="Lessongiving" }; JobDefOf.Lessontaking=new JobDef { defName="Lessontaking" };
            var deskDef=Bare<ThingDef>(); deskDef.defName="LessonDesk"; var desk=new Thing { def=deskDef }; var skill=new SkillDef { defName="LessonSkill" };
            teacher.jobs=new Pawn_JobTracker(teacher); student.jobs=new Pawn_JobTracker(student);
            teacher.jobs.curJob=new Job(JobDefOf.Lessongiving,desk,student); student.jobs.curJob=new Job(JobDefOf.Lessontaking,desk,teacher);
            var giving=new JobDriver_Lessongiving { pawn=teacher, job=teacher.CurJob, isReadyToTeach=true, taughtSkill=skill };
            teacher.jobs.curDriver=giving; student.jobs.curDriver=new JobDriver_Lessontaking { pawn=student,job=student.CurJob,isReadyToLearn=true };
            StatDefOf.LearningRateFactor=DefDatabase<StatDef>.GetNamed("LearningRateFactor");
            SSCImprintUtility.TryGrantInitial(student); Near(2.25f,LearningUtility.LearningRateFactor(student));
            giving.isReadyToTeach=false; Near(1.5f,LearningUtility.LearningRateFactor(student)); giving.isReadyToTeach=true;
            // 使用原MakeNewToils及实际技能经验分支；只隔离面对目标的图形动作。
            teacher.rotationTracker=new Pawn_RotationTracker(teacher);
            teacher.interactions=new Pawn_InteractionsTracker(teacher);
            SkillDefOf.Social ??= new SkillDef { defName="Social" };
            teacher.skills.skills.Add(new SkillRecord(teacher,SkillDefOf.Social));
            var record=new SkillRecord(student,skill); student.skills.skills.Add(record);
            StatDefOf.GlobalLearningFactor=DefDatabase<StatDef>.GetNamed("GlobalLearningFactor");
            LearningDesireDefOf.Lessontaking=new LearningDesireDef { defName="Lessontaking",xpPerTick=.1f };
            var toils=(IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_Lessongiving),"MakeNewToils").Invoke(giving,null);
            var actual=toils.Single(t=>t.activeSkill?.Invoke()==SkillDefOf.Social);
            var comp=teacher.TryGetComp<CompSexSlaveTraining>(); comp.pawnIdentity=PawnIdentity.Unset;
            actual.tickIntervalAction(100); float baseline=record.xpSinceLastLevel;
            comp.pawnIdentity=PawnIdentity.Slave; actual.tickIntervalAction(100); Near(baseline*1.5f,record.xpSinceLastLevel-baseline);
            student.needs.learning=new Need_Learning(student); student.needs.learning.CurLevel=.1f;
            LearningUtility.LearningTickCheckEnd(student,100,true); Near(.1027f,student.needs.learning.CurLevel);
            teacher.health.RemoveHediff(teacher.health.hediffSet.GetFirstHediffOfDef(FertilitySpecializationUtility.FinalDef)); FertilitySpecializationUtility.Sync(teacher);
            actual.tickIntervalAction(100); actual.tickIntervalAction(100);
            Equal(100,teacher.TryGetComp<CompSexSlaveTraining>().fertilityCareTicks);
            float xpBefore = record.xpSinceLastLevel;
            AccessTools.Field(typeof(TickManager),"ticksGameInt").SetValue(Current.Game.tickManager,Current.Game.tickManager.TicksGame+100);
            LearningDesireDefOf.Lessontaking.xpPerTick = 0f;
            actual.tickIntervalAction(100);
            Near(xpBefore, record.xpSinceLastLevel);
            Equal(200, comp.fertilityCareTicks);
        });
    }
}

/// <summary>委托给实际训练Comp的Scribe；身体和地图由宿主保持，避免复制序列化算法。</summary>
public sealed class FertilityCompFixture : IExposable
{
    private readonly CompSexSlaveTraining comp;
    public FertilityCompFixture(CompSexSlaveTraining comp) { this.comp=comp; }
    public void ExposeData() => comp.PostExposeData();
}
