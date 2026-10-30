// AI 辅助生成：实际Ability、原生建孕入口及人格载荷；只隔离地图、遗传抽样和完整AI。
using System.Reflection;
using System.Xml;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static readonly HashSet<Thing> skillPawns = new();
    private static Map skillMap;
    private static bool skillBlockPregnancy, skillReenter, skillQueueBoundary;
    private static Action skillPregnancyCallback, skillPairCallback;
    private static int skillPregnancyCalls;
    private static bool skillContactAllowed;
    private static LocalTargetInfo skillPathTarget;
    private static PathEndMode skillPathMode;
    private static int skillPathStarts, skillPathStops;
    private static Ability_FertilityConception skillAbility;
    private static Pawn skillTarget;

    private static void InitializeFertilityAbilityDefs(string repo)
    {
        var xml = new XmlDocument();
        xml.Load(Path.Combine(repo, "1.7/Defs/JobDefs/SSC_FertilityConception.xml"));
        foreach (XmlNode n in xml.DocumentElement.ChildNodes.OfType<XmlElement>()) RegisterFertility(DirectXmlToObject.ObjectFromXml<JobDef>(n, false));
        xml.Load(Path.Combine(repo, "1.7/Defs/AbilityDefs/SSC_FertilityConception.xml"));
        foreach (XmlNode n in xml.DocumentElement.ChildNodes.OfType<XmlElement>()) RegisterFertility(DirectXmlToObject.ObjectFromXml<AbilityDef>(n, false));
    }
    private static bool SkillMap(Thing __instance, ref Map __result)
    { if (!skillPawns.Contains(__instance)) return true; __result = skillMap; return false; }
    private static bool SkillMapHeld(Thing __instance, ref Map __result)
    { if (!skillPawns.Contains(__instance)) return true; __result = null; return false; }
    private static bool SkillSpawned(Thing __instance, ref bool __result)
    { if (!skillPawns.Contains(__instance)) return true; __result = true; return false; }
    // Random gene selection is an engine boundary; the actual RJW helper still constructs/parents/adds the Hediff.
    private static bool SkillAwake(ref bool __result) { __result = true; return false; }
    // 地图接触／寻路是宿主边界；选取、GotoThing、预热和暂停执行实际生产及原版入口。
    private static bool SkillTouchBoundary(LocalTargetInfo target, ref bool __result)
    { if (!skillPawns.Contains(target.Thing)) return true; __result=skillContactAllowed; return false; }
    private static bool SkillStartPath(Pawn ___pawn, LocalTargetInfo dest, PathEndMode peMode)
    { if (!skillPawns.Contains(___pawn)) return true; skillPathStarts++; skillPathTarget=dest; skillPathMode=peMode; return false; }
    private static bool SkillStopPath(Pawn ___pawn)
    { if (!skillPawns.Contains(___pawn)) return true; skillPathStops++; return false; }
    private static bool SkillPairBoundary(ref bool __result) { if (!skillQueueBoundary) return true; skillPairCallback?.Invoke(); __result = true; return false; }
    private static bool SkillEventBoundary(ref bool __result) { if (!skillQueueBoundary) return true; __result = true; return false; }
    private static bool SkillGenes(ref GeneSet __result) { __result = new GeneSet(); return false; }
    private static bool SkillPregnancy()
    {
        skillPregnancyCalls++;
        skillPregnancyCallback?.Invoke();
        if (skillReenter) { skillReenter = false; Require(!skillAbility.Activate(skillTarget, LocalTargetInfo.Invalid)); }
        return !skillBlockPregnancy;
    }
    private static Pawn SkillPawn(bool final = true)
    {
        var p = FertilityPawn();
        skillPawns.Add(p);
        p.SetFactionDirect(preconceptionPlayer);
        p.abilities = new Pawn_AbilityTracker(p);
        p.jobs = new Pawn_JobTracker(p);
        p.stances = new Pawn_StanceTracker(p);
        p.pather = new Pawn_PathFollower(p);
        p.health.AddHediff(vagina, p.RaceProps.body.corePart);
        Toggle(p, true);
        if (final) p.health.AddHediff(FertilitySpecializationUtility.FinalDef);
        FertilityAbilityUtility.Maintain(p);
        return p;
    }
    private static (Pawn, Pawn, Ability_FertilityConception, Job) SkillRequest()
    {
        var a = SkillPawn(); var b = SkillPawn(false); b.gender = Gender.Male;
        var ability = (Ability_FertilityConception)a.abilities.GetAbility(FertilityAbilityUtility.Definition);
        var job = ability.GetJob(b, LocalTargetInfo.Invalid);
        AccessTools.Field(typeof(Pawn_JobTracker), "curJob").SetValue(a.jobs, job);
        skillAbility = ability; skillTarget = b; skillPregnancyCalls = 0; skillBlockPregnancy = skillReenter = false; skillPregnancyCallback = null;
        skillContactAllowed=true;skillPathStarts=skillPathStops=0;
        return (a, b, ability, job);
    }
    /// <summary>枚举实际驱动Toil，只将地图路径启动交给记录边界，不复制接近／施法顺序。</summary>
    private static List<Toil> SkillToils(Pawn pawn, Job job)
    {
        var driver=new JobDriver_FertilityConception {pawn=pawn,job=job};
        pawn.jobs.curDriver=driver;
        var toils=((IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_FertilityConception),"MakeNewToils").Invoke(driver,null)).ToList();
        foreach (var toil in toils) toil.actor=pawn;
        return toils;
    }
    /// <summary>只执行本批行为；完整地图和自愿AI联动仍待实机。</summary>
    private static void FertilityAbilityTests(string repo, Harmony harmony)
    {
        InitializeBirth(harmony);
        skillMap = Bare<Map>();
        skillMap.info = new MapInfo();
        AccessTools.Field(typeof(MapInfo), "sizeInt").SetValue(skillMap.info, new IntVec3(700, 1, 700));
        // 只隔离画面、地图接触和寻路引擎；实际Toil、Verb预热、暂停及Stance安装执行原版。
        harmony.Patch(AccessTools.Method(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.Notify_WarmingCastAlongLine)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        harmony.Patch(AccessTools.Method(typeof(ReachabilityImmediate), nameof(ReachabilityImmediate.CanReachImmediate),
            new[] { typeof(IntVec3), typeof(LocalTargetInfo), typeof(Map), typeof(PathEndMode), typeof(Pawn) }),
            prefix: new HarmonyMethod(typeof(Program), nameof(SkillTouchBoundary)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StartPath)),
            prefix:new HarmonyMethod(typeof(Program),nameof(SkillStartPath)));
        harmony.Patch(AccessTools.Method(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StopDead)),
            prefix:new HarmonyMethod(typeof(Program),nameof(SkillStopPath)));
        StatDefOf.AimingDelayFactor = new StatDef { defName = "AimingDelayFactor", defaultBaseValue = 1f };
        harmony.Patch(AccessTools.PropertyGetter(typeof(PawnCapacitiesHandler), nameof(PawnCapacitiesHandler.CanBeAwake)), prefix: new HarmonyMethod(typeof(Program), nameof(SkillAwake)));
        vagina ??= new HediffDef_SexPart { defName = "SkillVagina", hediffClass = typeof(Hediff), genitalFamily = GenitalFamily.Vagina, genitalTags = new() };
        if (DefDatabase<HediffDef>.GetNamedSilentFail(vagina.defName) == null) DefDatabase<HediffDef>.Add(vagina);
        harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)), prefix: new HarmonyMethod(typeof(Program), nameof(SkillMap)));
        // Keep native base Ability cooldown checks; isolate only world-map biome/layer queries.
        harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.MapHeld)), prefix: new HarmonyMethod(typeof(Program), nameof(SkillMapHeld)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned)), prefix: new HarmonyMethod(typeof(Program), nameof(SkillSpawned)));
        harmony.Patch(AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.GetInheritedGeneSet), new[] { typeof(Pawn), typeof(Pawn) }), prefix: new HarmonyMethod(typeof(Program), nameof(SkillGenes)));
        harmony.Patch(AccessTools.Method(typeof(PregnancyHelper), nameof(PregnancyHelper.StartVanillaPregnancy)), prefix: new HarmonyMethod(typeof(Program), nameof(SkillPregnancy)));
        harmony.Patch(AccessTools.Method(typeof(FertilityFollowupUtility), "CanPair"), prefix: new HarmonyMethod(typeof(Program), nameof(SkillPairBoundary)));
        harmony.Patch(AccessTools.Method(typeof(SSCRestrictionJobGuard), "PrepareEvent"), prefix: new HarmonyMethod(typeof(Program), nameof(SkillEventBoundary)));
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(ReservationUtility)).Where(m=>m.Name=="CanReserveAndReach"))
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(SkillPairBoundary)));


        Check("fertility ability global selection approaches selected adult before any warmup or conception", () =>
        {
            var (a,b,ability,job) = SkillRequest(); AccessTools.Field(typeof(Thing), "positionInt").SetValue(a,new IntVec3(0,0,0)); AccessTools.Field(typeof(Thing), "positionInt").SetValue(b,new IntVec3(699,0,699));
            skillContactAllowed=false;
            Require(ability.verb.ValidateTarget(b,false));Require(!ability.verb.CanHitTarget(b));Require(!ability.verb.CanHitTargetFrom(a.Position,b));
            Require(!ability.verb.TryStartCastOn(b,LocalTargetInfo.Invalid));Require(!ability.Activate(b,LocalTargetInfo.Invalid));
            Require(!b.stances.stunner.Stunned);
            var toils=SkillToils(a,job);Require(toils[0].defaultCompleteMode==ToilCompleteMode.PatherArrival);
            toils[0].initAction();Equal(1,skillPathStarts);Require(skillPathTarget.Pawn==b);Equal(PathEndMode.Touch,skillPathMode);
            Require(!ability.verb.WarmingUp && !b.stances.stunner.Stunned);
            Require(!SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(a));Equal(0,ability.CooldownTicksRemaining);
            b.gender=Gender.Female; b.health.RemoveHediff(b.health.hediffSet.GetFirstHediffOfDef(vagina));
            Require(FertilityAbilityUtility.CanTarget(a,b,out _)); Require(!FertilityAbilityUtility.CanTarget(a,a,out _));
            b.ageTracker.AgeBiologicalTicks=17L*GenDate.TicksPerYear; Require(!FertilityAbilityUtility.CanTarget(a,b,out _));
        });
        Check("fertility ability contact warmup stops both pawns and interrupted contact cannot conceive",()=>
        {
            var (a,b,ability,job)=SkillRequest();var before=b.CurJob;
            var aiming=StatDefOf.AimingDelayFactor;float original=aiming.defaultBaseValue;aiming.defaultBaseValue=2f;
            try
            {
                AccessTools.Field(typeof(Pawn), "drawer").SetValue(a,Bare<Pawn_DrawTracker>());
                var toils=SkillToils(a,job);toils[1].initAction();Equal(1,skillPathStops);
                toils[2].initAction();Require(ability.verb.WarmingUp && a.stances.curStance is Stance_Warmup);
                Require(a.stances.FullBodyBusy && b.stances.FullBodyBusy);Require(b.CurJob==before);
                Require((int)AccessTools.Field(typeof(StunHandler),"stunTicksLeft").GetValue(b.stances.stunner)>=ability.verb.WarmupTicksLeft);
                Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
                // 外部搬走／失去接触时，实际驱动的预热终止条件必须拒绝，不能远程补交效果。
                skillContactAllowed=false;Require(toils[2].endConditions.Any(f=>f()==JobCondition.Incompletable));
                Require(!ability.Activate(b,LocalTargetInfo.Invalid));Equal(0,skillPregnancyCalls);
                ((Stance_Warmup)a.stances.curStance).Interrupt();ability.verb.Reset();
                Equal(0,ability.CooldownTicksRemaining);Require(!SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(a));
            }
            finally {aiming.defaultBaseValue=original;}
        });
        Check("fertility ability native conception records both parents once and starts seven day cooldown", () =>
        {
            var (a,b,ability,job)=SkillRequest(); skillReenter=true;
            Require(ability.Activate(b,LocalTargetInfo.Invalid)); Equal(1,skillPregnancyCalls);
            var pregnancy=a.health.hediffSet.hediffs.OfType<HediffWithParents>().Single(); Require(pregnancy.Mother==a && pregnancy.Father==b);
            Equal(420000,ability.CooldownTicksRemaining); Require(!ability.Activate(b,LocalTargetInfo.Invalid)); Equal(1,skillPregnancyCalls);
        });
        Check("fertility ability rejected backend rolls back only unsuccessful cooldown", () =>
        {
            var (a,b,ability,job)=SkillRequest(); skillBlockPregnancy=true;
            Require(!ability.Activate(b,LocalTargetInfo.Invalid)); Equal(0,ability.CooldownTicksRemaining); Require(!SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(a));
        });
        Check("fertility ability stale personality request cannot acquire fresh submission rights", () =>
        {
            var (a,b,ability,job)=SkillRequest(); var context=FertilityAbilityUtility.Request(job);
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a,out var mutation)); mutation.Dispose();
            Require(!FertilityAbilityUtility.IsCurrent(context)); Require(!ability.Activate(b,LocalTargetInfo.Invalid)); Equal(0,skillPregnancyCalls);
        });
        Check("fertility ability pregnancy retains same ability without starting a new cooldown", () =>
        {
            var (a,b,ability,job)=SkillRequest(); a.health.AddHediff(HediffDefOf.PregnantHuman);
            FertilityAbilityUtility.Maintain(a); Require(a.abilities.GetAbility(FertilityAbilityUtility.Definition)==ability);
            Require(!FertilityAbilityUtility.CanUse(a,out string reason)); Require(reason=="SSC_FertilitySkill_Pregnant"); Equal(0,ability.CooldownTicksRemaining);
            a.health.RemoveHediff(a.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman));
            a.health.AddHediff(HediffDefOf.PregnancyLabor); Require(!FertilityAbilityUtility.CanUse(a,out reason)); Require(reason=="SSC_FertilitySkill_Pregnant");
        });
        Check("fertility ability cooldown follows gel copy scribe and actual personality restore", () =>
        {
            var (a,b,ability,job)=SkillRequest(); ability.StartCooldown(420000);
            var data=new CompPersonalityStore(); data.StorePawnData(a); var copy=new CompPersonalityStore(); copy.CopyFrom(data);
            Equal(Find.TickManager.TicksGame+420000,copy.fertilityConceptionCooldownEndTick);
            var fixture=new ImprintGelFixture { Data=copy }; string path=Path.Combine(saveDirectory,"fertility-ability-gel.xml");
            Scribe.saver.InitSaving(path,"Gel"); Scribe_Deep.Look(ref fixture,"data"); Scribe.saver.FinalizeSaving();
            fixture=null; Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref fixture,"data"); Scribe.loader.FinalizeLoading();
            AccessTools.Field(typeof(TickManager),"ticksGameInt").SetValue(Current.Game.tickManager,Find.TickManager.TicksGame+60000);
            var host=SkillPawn(false); Require(ExcretionUtility.InheritEverything(host,fixture.Data));
            Equal(360000,host.abilities.GetAbility(FertilityAbilityUtility.Definition).CooldownTicksRemaining);
            FertilitySpecializationGelUtility.DetachAfterExtraction(a); Require(a.abilities.GetAbility(FertilityAbilityUtility.Definition)==null);
        });
        Check("fertility ability followup rejection cannot undo pregnancy or cooldown", () =>
        {
            var (a,b,ability,job)=SkillRequest(); Require(ability.Activate(b,LocalTargetInfo.Invalid));
            b.ageTracker.AgeBiologicalTicks=17L*GenDate.TicksPerYear;
            Require(!FertilityFollowupUtility.TryQueue(a,b)); Equal(420000,ability.CooldownTicksRemaining); Require(SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(a));
        });
        Check("fertility ability followup queues one job without replacing target work or changing pregnancy", () =>
        {
            var (a,b,ability,job)=SkillRequest(); Require(ability.Activate(b,LocalTargetInfo.Invalid));
            AccessTools.Field(typeof(Thing), "positionInt").SetValue(a,new IntVec3(1,0,1));
            AccessTools.Field(typeof(Thing), "positionInt").SetValue(b,new IntVec3(2,0,2));
            var before=b.CurJob; skillQueueBoundary=true;
            try
            {
                Require(FertilityFollowupUtility.TryQueue(a,b));
                var queued=a.jobs.jobQueue.Single().job; Equal("SSC_Job_FertilityFollowup",queued.def.defName);
                Require(queued.GetTarget(TargetIndex.A).Pawn==b && !queued.playerForced);
                Require(FertilityFollowupUtility.Get(queued).Current && b.CurJob==before);
                Equal(420000,ability.CooldownTicksRemaining); Equal(1,a.health.hediffSet.hediffs.OfType<Hediff_Pregnant>().Count());
            }
            finally { skillQueueBoundary=false; }
        });
        Check("fertility ability pooled job reuse invalidates old context and replaces weak registration", () =>
        {
            var (a,b,ability,job)=SkillRequest(); var original=FertilityAbilityUtility.Request(job);
            job.loadID++; FertilityAbilityUtility.Register(job,ability,b);
            Require(!FertilityAbilityUtility.IsCurrent(original)); Require(FertilityAbilityUtility.IsCurrent(FertilityAbilityUtility.Request(job)));
        });
        Check("fertility ability exception after native pregnancy landing keeps successful cooldown", () =>
        {
            var (a,b,ability,job)=SkillRequest();
            skillPregnancyCallback=()=> { var h=(Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman,a); h.SetParents(a,b,new GeneSet()); a.health.AddHediff(h); throw new InvalidOperationException("boundary"); };
            Require(ability.Activate(b,LocalTargetInfo.Invalid)); Equal(420000,ability.CooldownTicksRemaining);
            skillPregnancyCallback=null;
        });
        FertilityAbilityReviewTests(harmony);
    }
}
