// AI 辅助生成：生成器、图形/地图为边界；请求处理、印记、原生准入和分娩汇总执行候选DLL。
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static PawnGenerationRequest birthRequest;
    private static bool birthSpawnSucceeded;
    private static Action birthGenerationCallback;
    private static readonly List<string> birthMessages = new();

    /// <summary>只准备独立分区所需玩家及临产定义，不依赖先执行其他用例。</summary>
    private static void InitializeBirth(Harmony harmony)
    {
        AccessTools.Field(typeof(Game), "maps").SetValue(Current.Game, new List<Map>());
        Current.Game.World.factionManager ??= new FactionManager();
        if (Faction.OfPlayer == null)
            Current.Game.World.factionManager.Add(new Faction { def = new FactionDef { defName = "BirthPlayer", isPlayer = true, techLevel = TechLevel.Industrial } });
        preconceptionPlayer = Faction.OfPlayer;
        HediffDefOf.PregnancyLabor = DefDatabase<HediffDef>.GetNamedSilentFail("PregnancyLabor") ?? PregnancyDef("PregnancyLabor");
        HediffDefOf.PregnancyLaborPushing = DefDatabase<HediffDef>.GetNamedSilentFail("PregnancyLaborPushing") ?? PregnancyDef("PregnancyLaborPushing");
        harmony.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthGeneratorBoundary)));
        harmony.Patch(AccessTools.Method(typeof(PawnUtility), nameof(PawnUtility.TrySpawnHatchedOrBornPawn)),
            prefix: new HarmonyMethod(typeof(Program), nameof(BirthSpawnBoundary)) { priority = Priority.First });
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(Messages)).Where(m => m.Name == "Message" &&
            m.GetParameters().Length == 4 && m.GetParameters()[1].ParameterType == typeof(LookTargets)))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(BirthMessageBoundary)) { priority = Priority.First });
    }

    /// <summary>只替代完整Pawn生成引擎，提供带真实SSC组件/健康的返回个体，并捕获生产送入的请求。</summary>
    private static bool BirthGeneratorBoundary(PawnGenerationRequest request, ref Pawn __result)
    {
        birthRequest = request;
        var baby = ImprintPawn(age: 0); baby.def = request.KindDef.race; baby.kindDef = request.KindDef;
        baby.gender = request.FixedGender ?? Gender.Female;
        birthGenerationCallback?.Invoke();
        __result = baby;
        return false;
    }
    // 消息边界优先于公共初始化的Skip，先采集再隔离屏幕队列，不依赖其他前缀的执行顺序。
    private static bool BirthMessageBoundary(object __0) { birthMessages.Add(__0.ToString()); return false; }
    /// <summary>地图／世界放置引擎为边界；生产RJW包装器继续读取原返回值并判断是否活产。</summary>
    private static bool BirthSpawnBoundary(ref bool __result) { __result = birthSpawnSucceeded; return false; }
    /// <summary>仅提供原请求，不在测试夹具中复制胎儿调控的修改算法。</summary>
    private static PawnGenerationRequest NewbornRequest(PawnKindDef kind)
        => new PawnGenerationRequest(kind, developmentalStages: DevelopmentalStage.Newborn);
    /// <summary>通过生产提交设定预设，再设置本例阶段，保持身体保存值与当前资格相互独立。</summary>
    private static Pawn BirthMother(float progress = .999f)
    {
        var mother = PreconceptionPawn();
        SetPreconceptionChoices(mother, SSCOffspringGender.Male, SSCOffspringSpeciesSource.Self, true);
        Toggle(mother, true);
        mother.TryGetComp<CompSexSlaveTraining>().specializationProgress = progress;
        return mother;
    }

    /// <summary>真实候选执行生成、印记及结算；只隔离完整生成与地图、屏幕末端。</summary>
    private static void BirthTests(Harmony harmony)
    {
        InitializeBirth(harmony);
        Check("birth generation applies current sex species and initial imprint while preserving inherited parameters", () =>
        {
            var mother = BirthMother(); var father = ImprintPawn();
            Mark(mother, SSCImprintUtility.SlaveDefName);
            var genes = new List<GeneDef> { new GeneDef { defName = "InheritedBoundaryGene" } };
            var xenotype = new XenotypeDef { defName = "InheritedBoundaryXenotype" };
            var request = new PawnGenerationRequest(father.kindDef, forcedEndogenes: genes, forcedXenotype: xenotype,
                developmentalStages: DevelopmentalStage.Newborn);
            request.PawnKindDefGetter = _ => father.kindDef;
            var baby = SSCFertilityGenerationPatches.GenerateNative(request, mother, father);
            Equal(mother.def, baby.def); Equal(Gender.Male, baby.gender);
            Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(baby));
            Equal(PawnIdentity.Unset, SSCIdentityUtility.GetIdentity(baby));
            Equal(PawnIdentity.Unset, SSCImprintUtility.GetImprintLockedIdentity(baby));
            Require(birthRequest.PawnKindDefGetter == null);
            Require(ReferenceEquals(request.ForcedEndogenes, birthRequest.ForcedEndogenes));
            Equal(xenotype, birthRequest.ForcedXenotype);
            Equal(request.Faction, birthRequest.Faction); Equal(request.AllowedDevelopmentalStages, birthRequest.AllowedDevelopmentalStages);
        });
        Check("birth late generation reads current qualification and natural choices keep original request", () =>
        {
            var mother = BirthMother(); var father = ImprintPawn(); var request = NewbornRequest(father.kindDef);
            var comp = mother.TryGetComp<CompSexSlaveTraining>(); comp.SetSpecialization(SexSlaveSpecializationType.None);
            var baby = SSCFertilityOffspringUtility.Generate(request, mother, father);
            Equal(father.def, baby.def); Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(baby));
            Equal(SSCOffspringGender.Male, SSCUterusDevelopmentUtility.Find(mother).OffspringGender);
            mother.health.AddHediff(FertilitySpecializationUtility.FinalDef);
            baby = SSCFertilityOffspringUtility.Generate(request, mother, father);
            Equal(mother.def, baby.def); Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(baby));
            var natural = PreconceptionPawn(); Toggle(natural, true); request.FixedGender = Gender.Female;
            baby = SSCFertilityOffspringUtility.Generate(request, natural, father);
            Equal(father.def, baby.def); Equal(Gender.Female, baby.gender); Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(baby));
        });
        Check("birth unresolved partner species warns and preserves other independently applicable controls", () =>
        {
            var mother = PreconceptionPawn(); SetPreconceptionChoices(mother, SSCOffspringGender.Male, SSCOffspringSpeciesSource.Partner, true); Toggle(mother, true);
            Require(SSCFertilityPreconceptionUtility.TryGetApplicableSettings(mother, out var settings)); Equal(SSCOffspringSpeciesSource.Partner, settings.SpeciesSource);
            var kind = ImprintPawn().kindDef; birthMessages.Clear();
            var baby = SSCFertilityOffspringUtility.Generate(NewbornRequest(kind), mother, null);
            Equal(kind.race, baby.def); Equal(Gender.Male, baby.gender); Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(baby));
            Equal(1, birthMessages.Count); Require(!birthMessages[0].Contains("SSC_Birth_"));
        });
        Check("birth nested and sequential babies award once per delivery and later deliveries still advance", () =>
        {
            var mother = BirthMother(.4f); var comp = mother.TryGetComp<CompSexSlaveTraining>();
            var outer = SSCFertilityBirthUtility.Begin(mother);
            for (int i = 0; i < 2; i++)
            {
                var inner = SSCFertilityBirthUtility.Begin(mother, ImprintPawn(), true);
                inner.Observe(ImprintPawn(age: 0)); inner.Finish(null);
                Near(.4f, comp.specializationProgress);
            }
            outer.Finish(null); outer.Finish(null); Near(.48f, comp.specializationProgress);
            var next = SSCFertilityBirthUtility.Begin(mother); next.Observe(ImprintPawn(age: 0)); next.Finish(null);
            Near(.56f, comp.specializationProgress);
        });
        Check("birth no live result skipped original and exceptional exits do not award or leak scope", () =>
        {
            var mother = BirthMother(.3f); var comp = mother.TryGetComp<CompSexSlaveTraining>();
            var scope = SSCFertilityBirthUtility.Begin(mother);
            Harmony_FertilityNativeBirth.Postfix(ImprintPawn(age: 0), false, scope); scope.Finish(null); Near(.3f, comp.specializationProgress);
            scope = SSCFertilityBirthUtility.Begin(mother); Harmony_FertilityNativeBirth.Postfix(new Corpse(), true, scope); scope.Finish(null); Near(.3f, comp.specializationProgress);
            scope = SSCFertilityBirthUtility.Begin(mother); scope.Observe(ImprintPawn(age: 0)); scope.Finish(new InvalidOperationException("boundary"));
            Near(.3f, comp.specializationProgress); Require(SSCFertilityBirthUtility.CurrentFather(mother) == null);
            scope = SSCFertilityBirthUtility.Begin(mother); birthSpawnSucceeded = false;
            Require(!Harmony_FertilityRjwBirth.Spawn(ImprintPawn(age: 0), mother, null)); scope.Finish(null); Near(.3f, comp.specializationProgress);
            scope = SSCFertilityBirthUtility.Begin(mother); birthSpawnSucceeded = true;
            Require(Harmony_FertilityRjwBirth.Spawn(ImprintPawn(age: 0), mother, null)); scope.Finish(null); Near(.38f, comp.specializationProgress);
        });
        Check("birth delivery token rejects personality replacement before awarding progress", () =>
        {
            var mother = BirthMother(.3f); var comp = mother.TryGetComp<CompSexSlaveTraining>();
            var scope = SSCFertilityBirthUtility.Begin(mother); scope.Observe(ImprintPawn(age: 0));
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(mother, out var mutation)); using (mutation) { }
            scope.Finish(null); Near(.3f, comp.specializationProgress);
        });
        Check("birth generation callback cannot grant imprint using the previous personality context", () =>
        {
            var mother = BirthMother(); var father = ImprintPawn();
            birthGenerationCallback = () =>
            {
                Require(SSCPersonalityRuntimeUtility.TryBeginMutation(mother, out var mutation));
                using (mutation) { }
            };
            try
            {
                var baby = SSCFertilityOffspringUtility.Generate(NewbornRequest(father.kindDef), mother, father);
                Equal(mother.def, baby.def); Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(baby));
                Require(SSCUterusDevelopmentUtility.Find(mother).OffspringImprintEnabled);
            }
            finally { birthGenerationCallback = null; }
        });
        Check("birth pregnancy and labor preserve excretion preparation and reject both extraction routes", () =>
        {
            var mother = BirthMother(.3f);
            var preparation = new Hediff_SSC_PersonalityExcreting { def = SSCDefOf.SSC_PersonalityExcreting, pawn = mother };
            mother.health.AddHediff(preparation); preparation.Severity = 1;
            var pregnancy = NewPregnancy(mother, "PregnantHuman");
            Require(!SSCPersonalityExcretionAdmission.CanExtract(mother)); Require(!preparation.ShouldRemove);
            ExcretionUtility.DoExcretion(mother);
            var driver = Bare<JobDriver_PE>(); driver.pawn = ImprintPawn(); driver.job = new Verse.AI.Job { targetA = mother };
            AccessTools.Method(typeof(JobDriver_PE), "CompletePersonalityExtraction").Invoke(driver, null);
            Require(mother.health.hediffSet.hediffs.Contains(preparation)); Near(1, preparation.Severity);
            SSCDefOf.SSC_InducePersonalityExcretion ??= new RecipeDef { defName = "SSC_InducePersonalityExcretion" };
            var surgery = new Recipe_AddHediff { recipe = SSCDefOf.SSC_InducePersonalityExcretion };
            Require(!Harmony_MasterBlocksExcretionOperation.Prefix(surgery, mother));
            mother.health.RemoveHediff(pregnancy);
            var labor = mother.health.AddHediff(HediffDefOf.PregnancyLaborPushing);
            Require(!SSCPersonalityExcretionAdmission.CanExtract(mother)); Require(!preparation.ShouldRemove);
            Require(!SSCUterusDevelopmentUtility.TrySetFertilityEnabled(mother, false, out _));
            SSCPersonalityExcretionAdmission.RemoveForbiddenPreparation(mother); Require(mother.health.hediffSet.hediffs.Contains(preparation));
            mother.health.RemoveHediff(labor); Require(SSCPersonalityExcretionAdmission.CanExtract(mother));
        });
        Check("birth actual native and RJW IL install local adapters and preserve branch entry", () =>
        {
            var method = AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome));
            var codes = PatchProcessor.GetOriginalInstructions(method).ToList();
            var generate = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) });
            int at = codes.FindIndex(c => c.Calls(generate)); Require(at >= 0);
            var label = new DynamicMethod("birth_labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel(); codes[at].labels.Add(label);
            var rewritten = Harmony_FertilityNativeGeneration.Transpiler(codes, method).ToList();
            Require(rewritten[at].labels.Contains(label)); Require(rewritten[at + 2].Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateNative")));
            foreach (Type type in new[] { typeof(Harmony_FertilityNativeGeneration), typeof(Harmony_FertilityRjwGeneration), typeof(Harmony_FertilityNativeBirth),
                typeof(Harmony_FertilityLaborBirthScope), typeof(Harmony_FertilityRitualBirthScope), typeof(Harmony_FertilityRjwBirth) })
                harmony.CreateClassProcessor(type).Patch();
            var current = PatchProcessor.GetCurrentInstructions(method).ToList();
            Equal(1, current.Count(c => c.Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateNative"))));
            var rjw = AccessTools.Method(typeof(rjw.Hediff_BasePregnancy), "GenerateBabies");
            Equal(1, PatchProcessor.GetCurrentInstructions(rjw).Count(c => c.Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateRjw"))));
        });
        BirthExtractionReviewTests(harmony);
    }
}
