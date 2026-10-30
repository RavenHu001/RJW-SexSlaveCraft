// AI 辅助生成：实际Start/End补丁、原生Scribe及原上下文守卫；完整地图任务安装仅为边界。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static readonly HashSet<Pawn> reviewEndPawns = new();
    private static int reviewEndCount;
    private static bool ReviewEndJob(Pawn_JobTracker __instance, Pawn ___pawn)
    {
        if (!reviewEndPawns.Contains(___pawn)) return true;
        reviewEndCount++;
        AccessTools.Field(typeof(Pawn_JobTracker), "curJob").SetValue(__instance,null);
        __instance.curDriver=null;
        return false;
    }
    // A later third-party prefix rejects Start after our own ownership check has passed.
    private static bool ReviewSkipNativeStart() => false;
    private static JobDriver_FertilityFollowup ReviewFollowup(Pawn a, Pawn b)
    {
        AccessTools.Field(typeof(Thing), "positionInt").SetValue(a,new IntVec3(1,0,1));
        AccessTools.Field(typeof(Thing), "positionInt").SetValue(b,new IntVec3(2,0,2));
        Require(FertilityFollowupUtility.TryQueue(a,b));
        Job job=a.jobs.jobQueue.Dequeue().job;
        var driver=Bare<JobDriver_FertilityFollowup>(); driver.pawn=a;driver.job=job;
        AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(a.jobs,job);a.jobs.curDriver=driver;
        return driver;
    }
    private static Job ReviewInstallReceiver(Pawn a,Pawn b)
    {
        var job=JobMaker.MakeJob(xxx.getting_quickie,a,b);
        var receiver=Bare<JobDriver_SexBaseReciever>();receiver.pawn=b;receiver.job=job;receiver.parteners=new List<Pawn>{a};
        AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(b.jobs,job);b.jobs.curDriver=receiver;
        return job;
    }
    private static void FertilityAbilityReviewTests(Harmony harmony)
    {
        Require(xxx.getting_quickie?.defName=="GettingQuickie");
        harmony.Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator),nameof(JobDriver_SexBaseInitiator.Start)),
            prefix:new HarmonyMethod(typeof(FertilityFollowupStartHook),nameof(FertilityFollowupStartHook.Prefix)),
            postfix:new HarmonyMethod(typeof(FertilityFollowupStartHook),nameof(FertilityFollowupStartHook.Postfix)));
        harmony.Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator),nameof(JobDriver_SexBaseInitiator.Start)),
            prefix:new HarmonyMethod(typeof(Program),nameof(ReviewSkipNativeStart)) { priority=Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(JobDriver_SexBaseInitiator),nameof(JobDriver_SexBaseInitiator.End)),
            prefix:new HarmonyMethod(typeof(FertilityFollowupEndHook),nameof(FertilityFollowupEndHook.Prefix)));
        foreach(var method in AccessTools.GetDeclaredMethods(typeof(Pawn_JobTracker)).Where(m=>m.Name=="EndCurrentJob"))
            harmony.Patch(method,prefix:new HarmonyMethod(typeof(Program),nameof(ReviewEndJob)));

        Check("fertility review callback personality replacement rejects followup before job registration",()=>
        {
            var (a,b,ability,job)=SkillRequest(); skillQueueBoundary=true;
            skillPairCallback=()=> { Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a,out var mutation));mutation.Dispose(); };
            try { Require(!FertilityFollowupUtility.TryQueue(a,b));Equal(0,a.jobs.jobQueue.Count); }
            finally {skillQueueBoundary=false;skillPairCallback=null;}
        });
        Check("fertility review rejected native Start aborts init and releases only its unstarted receiver",()=>
        {
            var (a,b,ability,job)=SkillRequest(); Require(ability.Activate(b,LocalTargetInfo.Invalid));skillQueueBoundary=true;
            reviewEndPawns.Add(a);reviewEndPawns.Add(b);reviewEndCount=0;
            try
            {
                var driver=ReviewFollowup(a,b);
                driver.InvokeNativeInitialization(()=>ReviewInstallReceiver(a,b));
                Require(driver.CheckBeforeStart());
                // End before Start must not access the native null Sexprops.
                driver.End();
                bool continued=false;
                driver.InvokeNativeInitialization(()=> { driver.Start();continued=true; });
                Require(!continued);Equal(2,reviewEndCount);Require(b.CurJob==null);
                Equal(420000,ability.CooldownTicksRemaining);Require(SSCFertilityPreconceptionUtility.HasPregnancyOrLabor(a));
            }
            finally {skillQueueBoundary=false;reviewEndPawns.Clear();}
        });
        Check("fertility review receiver replaced by a new job is preserved during late cleanup",()=>
        {
            var (a,b,ability,job)=SkillRequest();skillQueueBoundary=true;
            reviewEndPawns.Add(a);reviewEndPawns.Add(b);reviewEndCount=0;
            try
            {
                var driver=ReviewFollowup(a,b);driver.InvokeNativeInitialization(()=>ReviewInstallReceiver(a,b));
                var later=JobMaker.MakeJob(new JobDef {defName="LaterUserWork"});
                AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(b.jobs,later);b.jobs.curDriver=null;
                driver.ReleaseOwnUnstartedReceiver();Equal(0,reviewEndCount);Require(b.CurJob==later);
                Require(!driver.CheckBeforeStart());Equal(1,reviewEndCount);Require(b.CurJob==later);
            }
            finally {skillQueueBoundary=false;reviewEndPawns.Clear();}
        });
        Check("fertility review live Ability scribe and body loss preserve cooldown and owner binding",()=>
        {
            var (a,b,ability,job)=SkillRequest();ability.StartCooldown(420000);
            var holder=new FertilityLiveAbilityFixture(a){ Value=ability };string path=Path.Combine(saveDirectory,"fertility-live-ability.xml");
            Scribe.saver.InitSaving(path,"Ability");Scribe_Deep.Look(ref holder,"data",a);Scribe.saver.FinalizeSaving();holder=null;
            Scribe.loader.InitLoading(path);Scribe_Deep.Look(ref holder,"data",a);Scribe.loader.FinalizeLoading();
            Equal(420000,holder.Value.CooldownTicksRemaining);Require(holder.Value.pawn==a);
            Require(((Verb_CastAbility)holder.Value.verb).ability==holder.Value);
            var uterus=SSCUterusDevelopmentUtility.Find(a);uterus.Severity=.5f;
            FertilityAbilityUtility.Maintain(a);Require(a.abilities.GetAbility(FertilityAbilityUtility.Definition)==ability);
            Require(!FertilityAbilityUtility.CanUse(a,out _));Equal(420000,ability.CooldownTicksRemaining);
        });
        Check("fertility review old gel missing cooldown defaults to ready and unregistered cast cannot resume",()=>
        {
            var holder=new ImprintGelFixture {Data=new CompPersonalityStore {fertilityConceptionCooldownEndTick=420000}};
            string path=Path.Combine(saveDirectory,"fertility-old-cooldown.xml");
            Scribe.saver.InitSaving(path,"Gel");Scribe_Deep.Look(ref holder,"data");Scribe.saver.FinalizeSaving();
            Edit(path,doc=>doc.Descendants("fertilityConceptionCooldownEndTick").Remove());holder=null;
            Scribe.loader.InitLoading(path);Scribe_Deep.Look(ref holder,"data");Scribe.loader.FinalizeLoading();Equal(0,holder.Data.fertilityConceptionCooldownEndTick);
            var (a,b,ability,job)=SkillRequest();var loaded=JobMaker.MakeJob(job.def,b);loaded.ability=ability;loaded.verbToUse=ability.verb;
            var driver=Bare<JobDriver_FertilityConception>();driver.pawn=a;driver.job=loaded;
            Require(!driver.TryMakePreToilReservations(false));Equal(0,skillPregnancyCalls);
        });
    }
}
public sealed class FertilityLiveAbilityFixture : IExposable
{
    public Ability Value;
    private readonly Pawn owner;
    public FertilityLiveAbilityFixture(Pawn owner) { this.owner=owner; }
    public void ExposeData() => Scribe_Deep.Look(ref Value,"ability",owner,FertilityAbilityUtility.Definition);
}
