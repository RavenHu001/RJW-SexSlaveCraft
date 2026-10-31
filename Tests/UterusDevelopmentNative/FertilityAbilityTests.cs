// AI 辅助生成：真实同场驱动、完成提交、原生妊娠／冷却及局部Harmony受孕拦截。
// 地图路径、RJW动作随机选择／Start／计时与完整性交结果为显式宿主边界，不复制技能结算算法。
using System.Xml;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;
internal static partial class Program
{
    private static readonly HashSet<Thing> skillPawns=new();
    private static Map skillMap;
    private static bool skillBlockPregnancy,skillContactAllowed,skillRejectStart,skillPermission=true;
    private static int skillPregnancyCalls,skillProcessCalls,skillNaturalCalls,skillPathStarts,skillEndCalls;
    private static Action skillPregnancyCallback,skillProcessCallback,skillSelectCallback,skillPolicyCallback;
    private static Action<Pawn> skillAppearanceCallback;
    private static Ability_FertilityConception skillAbility;
    private static Pawn skillTarget;
    private static bool SkillMap(Thing __instance,ref Map __result) {if(!skillPawns.Contains(__instance))return true;__result=skillMap;return false;}
    private static bool SkillMapHeld(Thing __instance,ref Map __result) {if(!skillPawns.Contains(__instance))return true;__result=null;return false;}
    private static bool SkillSpawned(Thing __instance,ref bool __result) {if(!skillPawns.Contains(__instance))return true;__result=true;return false;}
    private static bool SkillAwake(ref bool __result) {__result=true;return false;}
    private static bool SkillTouch(LocalTargetInfo target,ref bool __result) {if(!skillPawns.Contains(target.Thing))return true;__result=skillContactAllowed;return false;}
    private static bool SkillGenes(ref GeneSet __result) {__result=new GeneSet();return false;}
    private static bool SkillPregnancy() {skillPregnancyCalls++;skillPregnancyCallback?.Invoke();return !skillBlockPregnancy;}
    private static bool SkillSelect(Pawn pawn,Pawn partner,ref SexProps __result) {skillSelectCallback?.Invoke();__result=new SexProps(pawn,partner);return false;}
    private static bool SkillSetup(JobDriver_Sex __instance) {if(__instance is not JobDriver_FertilityConception && __instance is not JobDriver_FertilityReceiver)return true;__instance.duration=__instance.ticks_left=__instance.sex_ticks=3;return false;}
    private static bool SkillPermission(JobDriver_Sex driver,ref bool allowed,ref bool __result) {if(driver is not JobDriver_FertilityConception)return true;skillPolicyCallback?.Invoke();allowed=skillPermission;__result=true;return false;}
    private static bool SkillStarted(JobDriver_Sex driver,ref bool __result) {if(driver is not JobDriver_FertilityConception)return true;__result=!skillRejectStart;return false;}
    private static bool SkillStart(JobDriver_SexBaseInitiator __instance) {if(__instance is not JobDriver_FertilityConception)return true;var r=(JobDriver_Sex)__instance.Partner.jobs.curDriver;r.Sexprops=new SexProps(__instance.Partner,__instance.pawn){isReceiver=true};return false;}
    private static bool SkillTick(JobDriver_Sex __instance) {if(__instance is not JobDriver_FertilityConception)return true;__instance.ticks_left--;PregnancyHelper.impregnate(__instance.Sexprops);PregnancyHelper.impregnate((__instance.Partner.jobs.curDriver as JobDriver_Sex).Sexprops);return false;}
    private static bool SkillProcess(SexProps props) {skillProcessCalls++;PregnancyHelper.impregnate(props);skillProcessCallback?.Invoke();return false;}
    private static bool SkillNatural() {skillNaturalCalls++;return false;}
    private static bool SkillReady(JobDriver __instance)=>__instance is not JobDriver_FertilityConception;
    private static bool SkillStartPath(Pawn ___pawn) {if(!skillPawns.Contains(___pawn))return true;skillPathStarts++;return false;}
    private static bool SkillStopPath(Pawn ___pawn)=>!skillPawns.Contains(___pawn);
    private static bool SkillInstall(Pawn_JobTracker __instance,Pawn ___pawn,Job newJob)
    {
        if(!skillPawns.Contains(___pawn))return true;
        var r=new JobDriver_FertilityReceiver{pawn=___pawn,job=newJob};r.parteners=new List<Pawn>{newJob.targetA.Pawn};
        AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(__instance,newJob);__instance.curDriver=r;return false;
    }
    private static bool SkillEnd(Pawn_JobTracker __instance,Pawn ___pawn)
    {if(!skillPawns.Contains(___pawn))return true;if(__instance.curDriver is JobDriver_FertilityConception d)d.CleanupScene();AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(__instance,null);__instance.curDriver=null;return false;}
    /// <summary>图形为边界；可注入异常，核对外观清理失败也不遗留本场受方。</summary>
    private static bool SkillAppearance(Pawn pawn) {if(!skillPawns.Contains(pawn))return true;skillAppearanceCallback?.Invoke(pawn);return false;}
    /// <summary>隔离原生End的图形／结锁任务，记录生产驱动是否把中断误送入正常收尾。</summary>
    private static bool SkillEndScene(JobDriver_SexBaseInitiator __instance) {if(__instance is not JobDriver_FertilityConception)return true;skillEndCalls++;return false;}
    private static Pawn SkillPawn(bool final=true)
    {
        var p=FertilityPawn();skillPawns.Add(p);p.SetFactionDirect(preconceptionPlayer);
        p.abilities=new Pawn_AbilityTracker(p);p.jobs=new Pawn_JobTracker(p);p.stances=new Pawn_StanceTracker(p);p.pather=new Pawn_PathFollower(p);
        p.health.AddHediff(vagina,p.RaceProps.body.corePart);Toggle(p,true);if(final)p.health.AddHediff(FertilitySpecializationUtility.FinalDef);
        FertilityAbilityUtility.Maintain(p);return p;
    }
    private static (Pawn,Pawn,Ability_FertilityConception,Job) SkillRequest()
    {
        skillPregnancyCalls=skillProcessCalls=skillNaturalCalls=skillPathStarts=skillEndCalls=0;skillContactAllowed=skillPermission=true;skillBlockPregnancy=skillRejectStart=false;skillPregnancyCallback=skillProcessCallback=skillSelectCallback=skillPolicyCallback=null;skillAppearanceCallback=null;
        var a=SkillPawn();var b=SkillPawn(false);b.gender=Gender.Male;
        var ability=(Ability_FertilityConception)a.abilities.GetAbility(FertilityAbilityUtility.Definition);var job=ability.GetJob(b,LocalTargetInfo.Invalid);
        AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(a.jobs,job);a.jobs.curDriver=new JobDriver_FertilityConception{pawn=a,job=job};skillAbility=ability;skillTarget=b;return(a,b,ability,job);
    }
    private static JobDriver_FertilityConception Scene(Pawn a,bool finish=false)
    {var d=(JobDriver_FertilityConception)a.jobs.curDriver;d.BeginScene();if(finish)for(int i=0;i<3;i++)d.TickScene();return d;}
    private static void InitializeFertilityAbilityDefs(string repo)
    {
        var xml=new XmlDocument();foreach(string path in new[]{"1.7/Defs/JobDefs/SSC_FertilityConception.xml","1.7/Defs/AbilityDefs/SSC_FertilityConception.xml"})
        {xml.Load(Path.Combine(repo,path));foreach(XmlNode n in xml.DocumentElement.ChildNodes.OfType<XmlElement>())if(path.Contains("JobDefs"))RegisterFertility(DirectXmlToObject.ObjectFromXml<JobDef>(n,false));else RegisterFertility(DirectXmlToObject.ObjectFromXml<AbilityDef>(n,false));}
    }
    private static void FertilityAbilityTests(string repo,Harmony harmony)
    {
        InitializeBirth(harmony);skillMap=Bare<Map>();
        vagina??=new HediffDef_SexPart{defName="SkillVagina",hediffClass=typeof(Hediff),genitalFamily=GenitalFamily.Vagina,genitalTags=new()};
        if(DefDatabase<HediffDef>.GetNamedSilentFail(vagina.defName)==null)DefDatabase<HediffDef>.Add(vagina);
        void Patch(System.Reflection.MethodBase m,string name)=>harmony.Patch(m,prefix:new HarmonyMethod(typeof(Program),name));
        Patch(AccessTools.PropertyGetter(typeof(Thing),nameof(Thing.Map)),nameof(SkillMap));Patch(AccessTools.PropertyGetter(typeof(Thing),nameof(Thing.MapHeld)),nameof(SkillMapHeld));Patch(AccessTools.PropertyGetter(typeof(Thing),nameof(Thing.Spawned)),nameof(SkillSpawned));
        Patch(AccessTools.PropertyGetter(typeof(PawnCapacitiesHandler),nameof(PawnCapacitiesHandler.CanBeAwake)),nameof(SkillAwake));
        Patch(AccessTools.Method(typeof(ReachabilityImmediate),nameof(ReachabilityImmediate.CanReachImmediate),new[]{typeof(IntVec3),typeof(LocalTargetInfo),typeof(Map),typeof(PathEndMode),typeof(Pawn)}),nameof(SkillTouch));
        Patch(AccessTools.Method(typeof(PregnancyUtility),nameof(PregnancyUtility.GetInheritedGeneSet),new[]{typeof(Pawn),typeof(Pawn)}),nameof(SkillGenes));
        Patch(AccessTools.Method(typeof(PregnancyHelper),nameof(PregnancyHelper.StartVanillaPregnancy)),nameof(SkillPregnancy));
        Patch(AccessTools.Method(typeof(PregnancyHelper),nameof(PregnancyHelper.impregnate)),nameof(SkillNatural));
        harmony.Patch(AccessTools.Method(typeof(PregnancyHelper),nameof(PregnancyHelper.impregnate)),prefix:new HarmonyMethod(typeof(FertilityScenePregnancyHook),nameof(FertilityScenePregnancyHook.Prefix)));
        Patch(AccessTools.Method(typeof(SexUtility),nameof(SexUtility.SelectSextype)),nameof(SkillSelect));Patch(AccessTools.Method(typeof(JobDriver_Sex),nameof(JobDriver_Sex.setup_ticks)),nameof(SkillSetup));
        Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator),nameof(JobDriver_SexBaseInitiator.Start)),nameof(SkillStart));Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator),nameof(JobDriver_SexBaseInitiator.End)),nameof(SkillEndScene));
        Patch(AccessTools.Method(typeof(JobDriver_Sex),nameof(JobDriver_Sex.SexTick)),nameof(SkillTick));Patch(AccessTools.Method(typeof(SexUtility),nameof(SexUtility.ProcessSex)),nameof(SkillProcess));
        Patch(AccessTools.Method(typeof(SSCRestrictionJobGuard),nameof(SSCRestrictionJobGuard.TryCheck)),nameof(SkillPermission));Patch(AccessTools.Method(typeof(SSCRestrictionJobGuard),"HasStartedScene"),nameof(SkillStarted));
        Patch(AccessTools.Method(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.StartJob)),nameof(SkillInstall));foreach(var m in AccessTools.GetDeclaredMethods(typeof(Pawn_JobTracker)).Where(m=>m.Name=="EndCurrentJob"))Patch(m,nameof(SkillEnd));
        Patch(AccessTools.Method(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StartPath)),nameof(SkillStartPath));Patch(AccessTools.Method(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StopDead)),nameof(SkillStopPath));Patch(AccessTools.Method(typeof(JobDriver),nameof(JobDriver.ReadyForNextToil)),nameof(SkillReady));Patch(AccessTools.Method(typeof(FertilitySceneUtility),"ClearAppearance"),nameof(SkillAppearance));

        Check("fertility scene global selection approaches without a separate warmup or early effect",()=>{
            var(a,b,ability,job)=SkillRequest();skillContactAllowed=false;Require(ability.verb.ValidateTarget(b,false));Require(!ability.verb.TryStartCastOn(b,LocalTargetInfo.Invalid));
            var toils=((IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_FertilityConception),"MakeNewToils").Invoke(a.jobs.curDriver,null)).ToList();toils[0].actor=a;toils[0].initAction();Equal(1,skillPathStarts);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene completes once after actual scene ticks and preserves both native parents",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);d.TickScene();Require(!d.CompleteScene());Equal(0,skillPregnancyCalls);d.TickScene();d.TickScene();
            skillPregnancyCallback=()=>{Equal(0,ability.CooldownTicksRemaining);Equal(0,FertilityAbilityUtility.CaptureCooldown(a));};
            Require(d.CompleteScene());Require(!d.CompleteScene());Equal(1,skillPregnancyCalls);Equal(1,skillProcessCalls);Equal(0,skillNaturalCalls);Equal(420000,ability.CooldownTicksRemaining);
            var h=a.health.hediffSet.hediffs.OfType<HediffWithParents>().Single();Require(h.Mother==a&&h.Father==b);d.CleanupScene();d.CleanupScene();Equal(1,skillEndCalls);Require(b.CurJob==null);Equal(0,a.jobs.jobQueue.Count);
        });
        Check("fertility scene cancellation cannot submit pregnancy or cooldown",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);d.TickScene();d.CleanupScene();Require(!d.CompleteScene());Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);Equal(0,skillEndCalls);Require(b.CurJob==null);
        });
        Check("fertility scene receiver ended before tracker replacement cancels completion",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);b.jobs.curDriver.ended=true;d.TickScene();Require(!d.CompleteScene());Require(a.CurJob==null&&b.CurJob==null);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);Equal(0,skillEndCalls);
        });
        Check("fertility scene appearance cleanup exception still releases the owned receiver",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);skillAppearanceCallback=p=>{if(p==a)throw new InvalidOperationException("appearance boundary");};
            bool threw=false;try{d.CleanupScene();}catch(InvalidOperationException){threw=true;}Require(threw);Require(b.CurJob==null);d.CleanupScene();Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);Equal(0,skillEndCalls);
        });
        Check("fertility scene direct activation is rejected even after start before normal completion",()=>{
            var(a,b,ability,job)=SkillRequest();Scene(a);Require(!ability.Activate(b,LocalTargetInfo.Invalid));Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene backend rejection and duplicate finish do not start cooldown",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a,true);skillBlockPregnancy=true;skillPregnancyCallback=()=>Equal(0,ability.CooldownTicksRemaining);Require(!d.CompleteScene());Require(!d.CompleteScene());Equal(1,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene reentrant completion cannot replay pregnancy",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a,true);skillProcessCallback=()=>Require(!d.CompleteScene());skillPregnancyCallback=()=>Require(!ability.Activate(b,LocalTargetInfo.Invalid));Require(d.CompleteScene());Equal(1,skillPregnancyCalls);
        });
        Check("fertility scene exception after native pregnancy landing retains success cooldown",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a,true);skillPregnancyCallback=()=>{var h=(Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman,a);h.SetParents(a,b,new GeneSet());a.health.AddHediff(h);throw new InvalidOperationException("boundary");};Require(d.CompleteScene());Equal(420000,ability.CooldownTicksRemaining);
        });
        Check("fertility scene receiver personality replacement invalidates original request",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a,true);Require(SSCPersonalityRuntimeUtility.TryBeginMutation(b,out var mutation));mutation.Dispose();Require(!d.CompleteScene());Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene permission rejection does not install receiver or conceive",()=>{
            var(a,b,ability,job)=SkillRequest();skillPermission=false;Scene(a);Require(b.CurJob==null);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene permission callback replacing actor personality cannot install receiver",()=>{
            var(a,b,ability,job)=SkillRequest();skillPolicyCallback=()=>{Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a,out var mutation));mutation.Dispose();};Scene(a);Require(b.CurJob==null);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene rejected Start cleans its own receiver without committing",()=>{
            var(a,b,ability,job)=SkillRequest();skillRejectStart=true;Scene(a);Require(b.CurJob==null);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene replaced target work is preserved by late cleanup",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);var later=JobMaker.MakeJob(new JobDef{defName="LaterTargetCommand"});AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(b.jobs,later);b.jobs.curDriver=null;d.CleanupScene();Require(b.CurJob==later);Require(!d.CompleteScene());
        });
        Check("fertility scene natural pregnancy is blocked only for exact scene data including late callbacks",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a);var saved=new SexProps(a,b);d.Sexprops=saved;PregnancyHelper.impregnate(saved);Equal(0,skillNaturalCalls);d.CleanupScene();
            // 清理后让原驱动真正离开任务跟踪器，迟到回调不能继续借当前驱动匹配来掩盖记录缺失。
            AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(a.jobs,null);a.jobs.curDriver=null;
            PregnancyHelper.impregnate(saved);Equal(0,skillNaturalCalls);PregnancyHelper.impregnate(new SexProps(a,b));Equal(1,skillNaturalCalls);
        });
        Check("fertility scene callback replacing caster work cancels completion and preserves new command",()=>{
            var(a,b,ability,job)=SkillRequest();var d=Scene(a,true);var later=JobMaker.MakeJob(new JobDef{defName="LaterActorCommand"});skillProcessCallback=()=>{AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(a.jobs,later);a.jobs.curDriver=null;};Require(!d.CompleteScene());d.CleanupScene();Require(a.CurJob==later);Equal(0,skillPregnancyCalls);
        });
        Check("fertility scene ability survives pregnancy and keeps existing cooldown",()=>{
            var(a,b,ability,job)=SkillRequest();a.health.AddHediff(HediffDefOf.PregnantHuman);FertilityAbilityUtility.Maintain(a);Require(a.abilities.GetAbility(FertilityAbilityUtility.Definition)==ability);Require(!FertilityAbilityUtility.CanUse(a,out _));Equal(0,ability.CooldownTicksRemaining);
        });
        Check("fertility scene cooldown gel copy scribe and personality restore keep absolute deadline",()=>{
            var(a,b,ability,job)=SkillRequest();ability.StartCooldown(420000);var data=new CompPersonalityStore();data.StorePawnData(a);var copy=new CompPersonalityStore();copy.CopyFrom(data);var fixture=new ImprintGelFixture{Data=copy};string path=Path.Combine(saveDirectory,"fertility-scene-gel.xml");Scribe.saver.InitSaving(path,"Gel");Scribe_Deep.Look(ref fixture,"data");Scribe.saver.FinalizeSaving();fixture=null;Scribe.loader.InitLoading(path);Scribe_Deep.Look(ref fixture,"data");Scribe.loader.FinalizeLoading();AccessTools.Field(typeof(TickManager),"ticksGameInt").SetValue(Current.Game.tickManager,Find.TickManager.TicksGame+60000);var host=SkillPawn(false);Require(ExcretionUtility.InheritEverything(host,fixture.Data));Equal(360000,host.abilities.GetAbility(FertilityAbilityUtility.Definition).CooldownTicksRemaining);
        });
        FertilityAbilityReviewTests(harmony);
    }
}
