// AI 辅助生成：真实 1.7 持续心情/社交、数值、原版缓存和查询工作量契约。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static int feedbackPawnId = 10000;
    private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamed("SSC_Culture_" + name);

    private static SSCCultureGameComponent FeedbackContext()
    {
        // worker 必须从当前 Game.GetComponent 取得真实生产组件，不能绕过组件生命周期。
        var game = Context(); game.components = new(); game.World.worldPawns = new WorldPawns();
        var cache = Cache(); game.components.Add(cache);
        return cache;
    }

    private static Pawn Believer(string[] precepts, PawnIdentity identity = PawnIdentity.Unset, Faction faction = null)
    {
        // 只布置初始文化；后续改宗/改革用例走原版公开路径，不用直接字段赋值代替刷新。
        var p = MakePawn(identity, faction); p.thingIDNumber = ++feedbackPawnId; p.Name = new NameSingle("Fixture");
        p.kindDef = new PawnKindDef { defName = "FixtureColonist", race = p.def };
        // RJW 成年查询会先识别机械体；提供真实肉体/尸体类别，不能把其查询替换为固定成年。
        var corpseCategory = Bare<ThingCategoryDef>(); corpseCategory.defName = "CorpsesHumanlike";
        Set(p.def.race, "fleshType", new FleshTypeDef { defName = "FixtureFlesh", isOrganic = true,
            corpseCategory = corpseCategory });
        p.def.race.lifeStageAges.Add(new LifeStageAge { minAge = 0, def = new LifeStageDef { defName = "FixtureAdult", developmentalStage = DevelopmentalStage.Adult } });
        // 夹具是已经进入成人阶段的人物；不在第一次读年龄时触发生长转场/全套身体能力初始化。
        Set(p.ageTracker, "cachedLifeStageIndex", 0); Set(p.ageTracker, "growth", 1f);
        p.ageTracker.AgeBiologicalTicks = 30L * GenDate.TicksPerYear;
        p.story = new Pawn_StoryTracker(p); p.relations = new Pawn_RelationsTracker(p);
        p.ownership = new Pawn_Ownership(p);
        p.needs = new Pawn_NeedsTracker(p);
        p.needs.mood = new Need_Mood(p) { def = new NeedDef { defName = "Mood", baseLevel = 0.5f } };
        ((List<Need>)AccessTools.Field(typeof(Pawn_NeedsTracker), "needs").GetValue(p.needs)).Add(p.needs.mood);
        var ideo = Culture(mainMeme);
        foreach (string name in precepts) Add(ideo, name);
        Set(p.ideo, "ideo", ideo);
        return p;
    }

    private static Pawn Officer(string[] precepts)
    {
        // 使用生产持续资格：自由殖民者、真实链第 3 阶段、普通训导官 20% 和个人开关。
        var p = Believer(precepts, PawnIdentity.Slave);
        Chain(p, MakePawn(PawnIdentity.Master)).Severity = 0.5f;
        var comp = p.TryGetComp<CompSexSlaveTraining>();
        comp.specializationType = SexSlaveSpecializationType.TrainerOfficer;
        comp.specializationProgress = 0.2f; comp.slaveTrainerEnabled = true;
        Require(SSCCultureStateUtility.IsActiveOfficer(p)); return p;
    }

    private static List<Thought> Mood(Pawn p)
    {
        var result = new List<Thought>(); p.needs.mood.thoughts.GetAllMoodThoughts(result); return result;
    }
    private static List<ISocialThought> Social(Pawn p, Pawn other)
    {
        var result = new List<ISocialThought>(); p.needs.mood.thoughts.situational.AppendSocialThoughts(other, result); return result;
    }
    private static float MoodSum(Pawn p) => p.needs.mood.thoughts.TotalMoodOffset();
    private static int Opinion(Pawn p, Pawn other) => p.needs.mood.thoughts.TotalOpinionOffset(other);

    private static void FeedbackTests(string repo, string game)
    {
        FeedbackResourceTests(repo);
        AttitudeTests();
        EnvironmentFeedbackTests();
        SacredEnvironmentCurveTests();
        LeaderFeedbackTests();
        RefreshFeedbackTests(game);
        LifecycleFeedbackTests();
        FeedbackWorkTests();
        CountFeedbackTests(game);
        SlaveLeaderFeedbackTests();
    }

    private static void FeedbackResourceTests(string repo)
    {
        Check("56 native situational defs, condition configs and no memory/nullifier overrides", () =>
        {
            // 检查真实解析后的 Def/扩展，不只读取 XML 标签；保留原版年龄/亚人/最终系数默认。
            var defs = DefDatabase<ThoughtDef>.AllDefs.ToArray(); Equal(56, defs.Length);
            Equal(35, defs.Count(d => !d.IsSocial)); Equal(21, defs.Count(d => d.IsSocial));
            Equal(67, defs.Sum(d => d.stages.Count));
            foreach (var d in defs)
            {
                Require(!d.IsMemory && d.validWhileDespawned && d.producesMemoryThought == null);
                Equal(d.IsSocial ? typeof(Thought_SituationalSocial) : typeof(Thought_Situational), d.thoughtClass);
                Equal(d.IsSocial ? typeof(ThoughtWorker_SSCCultureSocial) : typeof(ThoughtWorker_SSCCultureMood), d.workerClass);
                Require(d.effectMultiplyingStat == null && d.nullifyingHediffs.NullOrEmpty());
                Require(d.ConfigErrors().Count() == 0, d.defName + ": " + string.Join(";", d.ConfigErrors()));
                Require(d.GetModExtension<SSCCultureThoughtExtension>().ConfigErrors().Count() == 0);
                Equal(d.defName.StartsWith("SSC_Culture_SlaveGender_") && !d.defName.Contains("NonMasterDuty") ? 2 : 1, DefDatabase<PreceptDef>.AllDefs.SelectMany(p => p.comps.OfType<PreceptComp_SituationalThought>()).Count(c => c.thought == d));
            }
            // 无效配置必须被识别；不能把无条件默认枚举当作有效反馈。
            Require(new SSCCultureThoughtExtension().ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension { condition = SSCCultureThoughtCondition.MasterSelf, requireEnviousObserver = true }.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension { condition = SSCCultureThoughtCondition.Leader, nonMasterLeaderStage = 2 }.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension { condition = SSCCultureThoughtCondition.Leader, sexSlaveLeaderStage = 1 }.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension { condition = SSCCultureThoughtCondition.MasterSelf, sexSlaveLeaderStage = 2 }.ConfigErrors().Any());
        });
    }

    private static void AttitudeTests()
    {
        string[] sex = {"Hostile","Revulsion","Indifference","Respect","Sacred","Envy"};
        int[] self = {-18,-6,0,0,10,10}, social = {-15,-5,0,5,15,15};
        for (int i = 0; i < sex.Length; i++)
        {
            string tier = sex[i]; int own = self[i], other = social[i];
            Check("native chained self and observer-directed social: sex slave " + tier, () =>
            {
                FeedbackContext(); var p = Believer(new[] {"SexSlave_" + tier}); var target = Believer(Array.Empty<string>());
                Chain(p, MakePawn(PawnIdentity.Master)); Chain(target, MakePawn(PawnIdentity.Master)); MakeMap(p,target);
                Equal((float)own, MoodSum(p));
                // 艳羡观察者持链时不评价别人；另外一个无链观察者仍可评价不同文化的目标。
                Equal(tier == "Envy" ? 0 : other, Opinion(p,target));
                var observer = Believer(new[] {"SexSlave_" + tier}); MakeMap(observer);
                Equal(other, Opinion(observer,target)); Equal(0, Opinion(target,observer)); Equal(0, Opinion(observer,observer));
                Equal(0, p.needs.mood.thoughts.memories.Memories.Count);
            });
        }
        Check("envy stages ignore preselection/qualification, prioritize a chain and require no other slave", () =>
        {
            FeedbackContext(); var p = Believer(new[] {"SexSlave_Envy"}, PawnIdentity.Slave); MakeMap(p);
            Equal(-6f,MoodSum(p)); Equal(1,Mood(p).Count);
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Master;
            p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
            Chain(p,MakePawn(PawnIdentity.Master)); p.needs.mood.thoughts.ThoughtInterval();
            Equal(10f,MoodSum(p)); Equal(1,Mood(p).Count); Equal(0,Mood(p).Single().CurStageIndex);
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            p.health.hediffSet.hediffs.Clear(); p.needs.mood.thoughts.ThoughtInterval();
            Equal(-6f,MoodSum(p)); Equal(1,Mood(p).Single().CurStageIndex);
        });
        string[] trainer = {"Hostile","Revulsion","Indifference","Reverence","SacredOffice"};
        int[] tm = {-8,-4,0,3,5}, ts = {-20,-10,0,5,10};
        for (int i = 0; i < trainer.Length; i++)
        {
            string tier = trainer[i]; int own=tm[i], other=ts[i];
            Check("native actual officer self/social and lost eligibility: " + tier, () =>
            {
                FeedbackContext(); var officer = Officer(new[] {"Trainer_" + tier}); var observer=Believer(new[] {"Trainer_" + tier}); MakeMap(officer,observer);
                Equal((float)own,MoodSum(officer)); Equal(other,Opinion(observer,officer));
                officer.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled=false;
                officer.needs.mood.thoughts.ThoughtInterval(); tick+=100;
                Equal(0f,MoodSum(officer)); Equal(0,Opinion(observer,officer));
                officer.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled=true;
                officer.TryGetComp<CompSexSlaveTraining>().specializationProgress=0.199f; tick+=100;
                Equal(0,Opinion(observer,officer));
                officer.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master; tick+=100;
                Equal(0,Opinion(observer,officer));
            });
        }
        string[] master = {"Hostile","Revulsion","Indifference","Deference","HolyMaster"};
        int[] mm={-12,-6,0,4,10}, am={-8,-4,0,3,5}, ms={-20,-10,0,5,15};
        for (int i=0;i<master.Length;i++)
        {
            string tier=master[i]; int own=mm[i], actual=am[i], other=ms[i];
            Check("native master self, actual owner and general master opinion: " + tier, () =>
            {
                FeedbackContext(); var p=Believer(new[] {"Master_"+tier},PawnIdentity.Master);
                var slave=Believer(new[] {"Master_"+tier}); var target=Believer(Array.Empty<string>(),PawnIdentity.Master);
                // 个人和双人反馈即使没有地图/成员名册也可按原版离图资格查询。
                Chain(slave,target); Equal((float)own,MoodSum(p)); Equal((float)actual,MoodSum(slave));
                Equal(other,Opinion(slave,target)); Equal(other,Opinion(p,target));
                target.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset;
                slave.needs.mood.thoughts.ThoughtInterval(); tick+=100;
                Equal(0f,MoodSum(slave)); Equal(0,Opinion(slave,target));
            });
        }
        foreach (var sample in new[] {("Sacred","SacredOffice",25),("Sacred","Hostile",-5),("Revulsion","SacredOffice",5),("Hostile","SacredOffice",-5)})
            Check("native independent social groups fully add " + sample, () =>
            {
                FeedbackContext(); var target=Officer(Array.Empty<string>());
                var p=Believer(new[] {"SexSlave_"+sample.Item1,"Trainer_"+sample.Item2});
                Equal(sample.Item3,Opinion(p,target)); Equal(2,Social(p,target).Count);
                // 直接走最终关系入口，且相反符号相加时两份条目不能互相覆盖。
                Equal(sample.Item3,p.relations.OpinionOf(target));
                for(int i=0;i<10;i++) Equal(sample.Item3,Opinion(p,target));
                Equal(0,p.relations.DirectRelations.Count); Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
            });
        Check("native self groups fully add and retain source precepts", () =>
        {
            FeedbackContext(); var p=Officer(new[] {"SexSlave_Sacred","Trainer_SacredOffice"});
            Equal(15f,MoodSum(p)); Equal(2,Mood(p).Count);
            Require(Mood(p).All(t=>t.sourcePrecept!=null && t.sourcePrecept.ideo==p.Ideo));
            var observer=Believer(new[] {"SexSlave_Sacred","Trainer_SacredOffice"});
            Require(Social(observer,p).Cast<Thought>().All(t=>t.sourcePrecept.ideo==observer.Ideo));
        });
    }

    private static void EnvironmentFeedbackTests()
    {
        foreach(var item in new[] {("Hostile",-3),("Revulsion",-2),("Sacred",1)})
            Check("native environment multiplier and chain exclusion: " + item, () =>
            {
                FeedbackContext(); var p=Believer(new[] {"SexSlave_"+item.Item1}); var master=MakePawn(PawnIdentity.Master);
                var a=MakePawn(); var b=MakePawn(); Chain(a,master); Chain(b,master); MakeMap(p,a,b);
                Equal(item.Item1=="Sacred" ? 3f : 2*item.Item2,MoodSum(p)); Equal(1,Mood(p).Count);
                var thought=Mood(p).Single(); Equal((float)item.Item2,thought.CurStage.baseMoodEffect);
                // 尚未原版重算时倍率也应停止给持链本人环境效果；重算后只剩对应本人项。
                Chain(p,master); Equal(0f,thought.MoodOffset()); p.needs.mood.thoughts.ThoughtInterval();
                Equal(item.Item1=="Hostile" ? -18f : item.Item1=="Revulsion" ? -6f : 10f,MoodSum(p));
                Equal(1,Mood(p).Count);
            });
        Check("environment counts managed prisoners but recipients must be members; zero is inactive", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); MakeMap(p); Equal(0f,MoodSum(p));
            var prisoner=MakePawn(faction:new Faction()); Set(prisoner.guest,"hostFactionInt",player); Set(prisoner.guest,"guestStatusInt",GuestStatus.Prisoner);
            var visitor=MakePawn(faction:new Faction()); Set(visitor.guest,"hostFactionInt",player);
            var master=MakePawn(PawnIdentity.Master); Chain(prisoner,master); Chain(visitor,master); MakeMap(prisoner,visitor);
            tick+=100; p.needs.mood.thoughts.ThoughtInterval(); Equal(1f,MoodSum(p));
            var hidden=Believer(new[] {"SexSlave_Sacred"}); Equal(0f,MoodSum(hidden));
            var captured=Believer(new[] {"SexSlave_Sacred"}); Set(captured.guest,"guestStatusInt",GuestStatus.Prisoner); MakeMap(captured);
            tick+=100; Equal(0f,MoodSum(captured));
        });
    }

    private static void LeaderFeedbackTests()
    {
        string[] tiers={"Hostile","Revulsion","Indifference","Deference","HolyMaster"};
        int[] yes={-6,-3,0,3,6}, no={0,0,0,-3,-6};
        for(int i=0;i<tiers.Length;i++)
        {
            string tier=tiers[i]; int masterValue=yes[i], nonValue=no[i];
            Check("native leader mood three states and conflict: " + tier, () =>
            {
                FeedbackContext(); var p=Believer(new[] {"Master_"+tier}); var leader=MakePawn(PawnIdentity.Master); MakeMap(p,leader);
                var ideo=Culture(); player.ideos.SetPrimary(ideo); var role=Role(ideo,leader);
                Equal((float)masterValue,MoodSum(p));
                leader.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Unset; tick+=100;
                p.needs.mood.thoughts.ThoughtInterval(); Equal((float)nonValue,MoodSum(p));
                Set(role,"chosenPawn",null); tick+=100; p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
                Role(ideo,leader); Role(ideo,p); tick+=100; p.needs.mood.thoughts.ThoughtInterval(); Equal(0f,MoodSum(p));
            });
        }
        Check("leader receiver scope and no master reward duplication; actual-owner and leader remain independent", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"Master_HolyMaster"},PawnIdentity.Master); var owner=MakePawn(PawnIdentity.Master);
            var slave=Believer(new[] {"Master_HolyMaster"},PawnIdentity.Slave); Chain(slave,owner);
            MakeMap(p,owner,slave); var ideo=Culture(); player.ideos.SetPrimary(ideo);
            player.leader=owner; Equal(10f,MoodSum(p)); Equal(5f,MoodSum(slave));
            Role(ideo,owner); tick+=100; p.needs.mood.thoughts.ThoughtInterval(); slave.needs.mood.thoughts.ThoughtInterval();
            Equal(10f,MoodSum(p)); Equal(11f,MoodSum(slave));
            var hidden=Believer(new[] {"Master_HolyMaster"}); Equal(0f,MoodSum(hidden));
        });
    }

    private static void RefreshFeedbackTests(string game)
    {
        Check("hostile slave opinion follows actual chains and native 99/100 cache without population work", () =>
        {
            FeedbackContext(); var observer=Believer(new[] {"SexSlave_Hostile"});
            var target=Believer(Array.Empty<string>(),PawnIdentity.Slave);
            // 途径预选无链不产生敌视；持链后即使目标被外部改成 Master 身份也仍是文化性奴。
            calls.Clear(); Equal(0,Opinion(observer,target)); Chain(target,MakePawn(PawnIdentity.Master));
            tick=1099; Equal(0,Opinion(observer,target)); tick=1100; Equal(-15,Opinion(observer,target));
            target.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master;
            tick+=100; Equal(-15,Opinion(observer,target)); Equal(-15,observer.relations.OpinionOf(target));
            Equal(0,Opinion(target,observer)); Equal(0,Opinion(observer,observer));
            Require(Social(observer,target).Cast<Thought>().All(t=>t.sourcePrecept==observer.Ideo.GetPrecept(D("SexSlave_Hostile"))));
            target.health.hediffSet.hediffs.Clear(); tick+=99; Equal(-15,Opinion(observer,target));
            tick++; Equal(0,Opinion(observer,target)); Equal(0,calls.Count);
            Equal(0,observer.relations.DirectRelations.Count); Equal(0,observer.needs.mood.thoughts.memories.Memories.Count);
        });
        // 刷新与最终原版资格另列，避免把直接调用 worker 的成功当作真实缓存已经收敛。
        Check("social cache keeps old state at 99 and recomputes at 100 without broadcast", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Respect"}); var other=Believer(Array.Empty<string>());
            Equal(0,Opinion(p,other)); Chain(other,MakePawn(PawnIdentity.Master));
            tick=1099; Equal(0,Opinion(p,other)); tick=1100; Equal(5,Opinion(p,other));
            other.health.hediffSet.hediffs.Clear(); tick=1199; Equal(5,Opinion(p,other)); tick=1200; Equal(0,Opinion(p,other));
        });
        Check("native thought cache changes envy stage without creating memories or duplicate entries", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Envy"}); var target=Believer(Array.Empty<string>()); Chain(target,MakePawn(PawnIdentity.Master));
            Equal(-6f,MoodSum(p)); Equal(15,Opinion(p,target)); Chain(p,MakePawn(PawnIdentity.Master));
            tick+=150; p.needs.mood.thoughts.ThoughtInterval(); Equal(10f,MoodSum(p)); Equal(0,Opinion(p,target));
            Equal(1,Mood(p).Count); Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("native final opinion obeys actual Anomaly Inhumanized factor and restores after removal", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); var target=Believer(Array.Empty<string>()); Chain(target,MakePawn(PawnIdentity.Master));
            Equal(15,Opinion(p,target)); Equal(15,p.relations.OpinionOf(target));
            // 读取本机原版 Hediff 的真实阶段配置，不用测试替身将最终 OpinionOf 改成零。
            string path=Path.Combine(game,"Data/Anomaly/Defs/HediffDefs/Hediffs_Global_Misc.xml");
            var original=XDocument.Load(path).Root.Elements("HediffDef").Single(n=>(string)n.Element("defName")=="Inhumanized");
            var projected=new XElement("HediffDef",original.Elements().Where(n=>new[] {"defName","stages"}.Contains(n.Name.LocalName)));
            // 仅投影阶段内与最终好感直接相关的字段，其他图形/能力依赖不属于此结算用例。
            foreach(var stage in projected.Element("stages").Elements())
                foreach(var field in stage.Elements().Where(n=>n.Name.LocalName!="opinionOfOthersFactor" && n.Name.LocalName!="minSeverity").ToArray()) field.Remove();
            ReadDef<HediffDef>(projected); var def=DefDatabase<HediffDef>.GetNamed("Inhumanized");
            var hediff=new Hediff {pawn=p,def=def}; p.health.hediffSet.hediffs.Add(hediff);
            Equal(15,Opinion(p,target)); Equal(0,p.relations.OpinionOf(target));
            p.health.hediffSet.hediffs.Remove(hediff); Equal(15,p.relations.OpinionOf(target));
        });
    }

    private static void FeedbackWorkTests()
    {
        Check("personal and social feedback prepare no population or bound-count snapshots", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Envy","Trainer_SacredOffice"}); var target=Officer(Array.Empty<string>());
            calls.Clear(); Equal(-6f,MoodSum(p)); Equal(25,Opinion(p,target)); Equal(0,calls.Count);
        });
        Check("environment and leader feedback share one roster and environment count per generation", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred","Master_HolyMaster"}); var slave=MakePawn(); Chain(slave,MakePawn(PawnIdentity.Master)); MakeMap(p,slave);
            calls.Clear(); Equal(1f,MoodSum(p));
            for(int i=0;i<50;i++) { p.needs.mood.thoughts.ThoughtInterval(); Equal(1f,MoodSum(p)); }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CollectManagedChained")); Equal(1,CallCount("Query"));
            Equal(0,CallCount("CountMasters")); Equal(0,CallCount("CountBoundSlaves"));
            int recursion=CallCount("get_AllPawnsUnspawned"); tick+=100; p.needs.mood.thoughts.ThoughtInterval();
            Equal(2,CallCount("CollectMembers")); Equal(2,CallCount("CollectManagedChained")); Require(CallCount("get_AllPawnsUnspawned")>recursion);
        });
        Check("indifference and chained environment short-circuit; shared workers do not leak pawn state", () =>
        {
            FeedbackContext(); var zero=Believer(new[] {"SexSlave_Indifference","Trainer_Indifference","Master_Indifference"});
            calls.Clear(); Equal(0f,MoodSum(zero)); Equal(0,calls.Count);
            var chained=Believer(new[] {"SexSlave_Sacred"}); Chain(chained,MakePawn(PawnIdentity.Master));
            Equal(10f,MoodSum(chained)); Equal(0,calls.Count);
            var a=Believer(new[] {"SexSlave_Envy"}); var b=Believer(new[] {"SexSlave_Envy"}); Chain(b,MakePawn(PawnIdentity.Master));
            for(int i=0;i<10;i++) { Equal(-6f,MoodSum(a)); Equal(10f,MoodSum(b)); }
            Equal(0,calls.Count);
        });
    }
}
