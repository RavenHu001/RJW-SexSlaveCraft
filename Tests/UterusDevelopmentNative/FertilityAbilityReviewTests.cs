// AI 辅助生成：保留原生Ability／凝胶Scribe的相关回归。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;
internal static partial class Program
{
    private static void FertilityAbilityReviewTests(Harmony harmony)
    {
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
            // 原版深存curDriver的类型；旧独立联动仅保留可解析、拒绝预约并立即取消的入口。
            var legacyJob=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("SSC_Job_FertilityFollowup"),b);
            var legacy=new JobDriver_FertilityFollowup{pawn=a,job=legacyJob};Require(!legacy.TryMakePreToilReservations(false));
            AccessTools.Field(typeof(Pawn_JobTracker),"curJob").SetValue(a.jobs,legacyJob);a.jobs.curDriver=legacy;
            var toils=((IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_FertilityFollowup),"MakeNewToils").Invoke(legacy,null)).ToList();
            Equal(1,toils.Count);toils[0].initAction();Require(a.CurJob==null);Equal(0,skillPregnancyCalls);Equal(0,ability.CooldownTicksRemaining);
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
