// AI 辅助生成：六档性别规范的固定期望、真实原版反馈/生成/缓存与只读工作量测试。
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly string[] genderOptions = { "FemaleOnly", "MaleOnly", "Unrestricted", "FemaleRequired", "MaleRequired", "NonMasterRequired" };

    private static Pawn GenderPawn(Gender gender, string option = null, PawnIdentity identity = PawnIdentity.Unset, params string[] extra)
    {
        // 成人年龄由公共 Believer 真实 tracker 提供；目标可不持有规范戒律，用于跨文化观察方向验证。
        string[] precepts = option == null ? extra : extra.Prepend("SlaveGender_" + option).ToArray();
        Pawn pawn = Believer(precepts, identity);
        pawn.gender = gender;
        return pawn;
    }

    private static Pawn[] GenderOthers(int count, Gender gender, bool chained)
    {
        // 只布置链的实际健康列表；不替换生产 HasChain，也不要求目标有性奴途径或有效链主人。
        Pawn[] pawns = Enumerable.Range(0, count).Select(_ => GenderPawn(gender)).ToArray();
        if (chained) foreach (Pawn pawn in pawns) Chain(pawn, MakePawn(PawnIdentity.Master));
        return pawns;
    }

    private static void GenderFeedbackTests(string repo, string game)
    {
        GenderResourceTests(repo);
        GenderStateTests();
        GenderEnvironmentTests();
        GenderInteractionTests();
        GenderSnapshotTests();
        GenderLifecycleTests(game);
        EnviousDutyTests();
        foreach(string language in new[]{"ChineseSimplified","ChineseTraditional","English","Russian"})
            Check("gender actual four-language field injection: "+language,()=>
            {
                // 使用与命名专项相同的原版注入入口；设置语言基础设施，不替换字段解析或注入结果。
                var lang=Bare<LoadedLanguage>(); lang.folderName=language; Set(lang,"legacyFolderName",language);
                lang.info=new LanguageInfo {languageWorkerClass=typeof(LanguageWorker_English)};
                lang.stringFiles=new(); lang.keyedReplacements=new(); lang.loadErrors=new();
                Set(lang,"dataIsLoaded",true); Set(lang,"wordInfo",new LanguageWordInfo());
                LanguageDatabase.activeLanguage=LanguageDatabase.defaultLanguage=lang;
                InjectNaming(repo,language,typeof(IssueDef),6);
                InjectNaming(repo,language,typeof(PreceptDef),99);
                InjectNaming(repo,language,typeof(ThoughtDef),134);
                var entries=XDocument.Load(Path.Combine(repo,"1.7/Languages",language,"DefInjected/PreceptDef/SSC_Culture.xml")).Root;
                Equal(entries.Element("SSC_Precept_SlaveGender_FemaleOnly.label").Value,D("SlaveGender_FemaleOnly").label);
                // 比较交付文件与实际注入字段，既允许正式文案修订，也能发现注入未生效。
                var thoughts=XDocument.Load(Path.Combine(repo,"1.7/Languages",language,"DefInjected/ThoughtDef/SSC_Culture.xml")).Root;
                Equal(thoughts.Element("SSC_Culture_SlaveGender_RequiredDuty_Self.stages.0.description").Value,T("SlaveGender_RequiredDuty_Self").stages[0].description);
            });
    }

    private static void GenderResourceTests(string repo)
    {
        Check("gender resource fixed twelve shared thoughts, 21 attachments, flags and config errors", () =>
        {
            // 数值为独立固定表，不从 XML 或生产判断公式反推期望。
            var families = new[] {
                (name:"LimitedViolation", prefix:"GenderViolation", values:new[] {-6,-10,-1}),
                (name:"RequiredViolation", prefix:"GenderViolation", values:new[] {-12,-20,-2}),
                (name:"RequiredDuty", prefix:"UnfulfilledDuty", values:new[] {-8,-10,-2}),
                (name:"NonMasterDuty", prefix:"UnfulfilledDuty", values:new[] {-12,-15,-3}) };
            foreach (var f in families)
                for (int k=0;k<3;k++)
                {
                    string kind = new[] {"Self","Social","Environment"}[k];
                    var thought=T("SlaveGender_"+f.name+"_"+kind);
                    bool dutySelf=k==0 && f.prefix=="UnfulfilledDuty";
                    Equal(dutySelf?2:1,thought.stages.Count);
                    if(dutySelf) Equal(f.name=="RequiredDuty"?-12f:-16f,thought.stages[1].baseMoodEffect);
                    Equal((float)f.values[k], k==1 ? thought.stages[0].baseOpinionOffset : thought.stages[0].baseMoodEffect);
                    var config=thought.GetModExtension<SSCCultureThoughtExtension>();
                    Equal(f.prefix+(k==1?"Other":kind),config.condition.ToString());
                    Equal(DevelopmentalStage.Adult,thought.developmentalStageFilter);
                    if(k==1) Equal(DevelopmentalStage.Adult,thought.socialTargetDevelopmentalStageFilter);
                    if(k==2)
                    {
                        float[] curve={0,1,3,6}; Equal(4,config.environmentMoodCurve.PointsCount);
                        for(int n=0;n<4;n++) { Equal((float)n,config.environmentMoodCurve[n].x); Equal(curve[n],config.environmentMoodCurve[n].y); }
                    }
                }
            string[][] expected={new[]{"LimitedViolation"},new[]{"LimitedViolation"},Array.Empty<string>(),
                new[]{"RequiredViolation","RequiredDuty"},new[]{"RequiredViolation","RequiredDuty"},new[]{"NonMasterDuty"}};
            for(int i=0;i<genderOptions.Length;i++)
            {
                var d=D("SlaveGender_"+genderOptions[i]);
                Equal(i<2?PreceptImpact.Medium:i==2?PreceptImpact.Low:PreceptImpact.High,d.impact);
                Equal(string.Join(',',expected[i].SelectMany(f=>new[]{"Self","Social","Environment"}.Select(k=>"SSC_Culture_SlaveGender_"+f+"_"+k))),
                    string.Join(',',d.comps.Cast<PreceptComp_SituationalThought>().Select(c=>c.thought.defName)));
            }
            Equal(21,genderOptions.Sum(o=>D("SlaveGender_"+o).comps.Count));
            foreach(var d in DefDatabase<ThoughtDef>.AllDefs)
            {
                var c=d.GetModExtension<SSCCultureThoughtExtension>();
                Equal(d.defName is "SSC_Culture_SexSlave_Sacred_Self" or "SSC_Culture_SexSlave_Envy_Self"
                    or "SSC_Culture_SexSlave_Sacred_Social" or "SSC_Culture_SexSlave_Envy_Social",c.requireAcceptedSlaveGender);
                Equal(d.defName=="SSC_Culture_SexSlave_Sacred_Environment",c.filterAcceptedManagedGender);
            }
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.MasterSelf,requireAcceptedSlaveGender=true}.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.ChainedSelf,filterAcceptedManagedGender=true}.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.GenderViolationEnvironment}.ConfigErrors().Any());
        });

        Check("gender native default, feasibility negative, zero-weight preservation and required meme removal", () =>
        {
            // 真正调用原版完整生成/改革；禁止默认项不能被擅自回退成任一极端档。
            var ordinary=Culture(); ordinary.foundation.RandomizePrecepts(false,Parms());
            Require(ordinary.HasPrecept(D("SlaveGender_Unrestricted")));
            var parms=Parms(); parms.disallowedPrecepts=new(){D("SlaveGender_Unrestricted")};
            var ideo=Culture(mainMeme); Require(!Feasible(ideo,parms));
            Add(ideo,"SlaveGender_FemaleRequired"); Require(Feasible(ideo,parms));
            ideo.foundation.EnsurePreceptsCompatibleWithMemes(new(){mainMeme},new(){mainMeme},Parms());
            Require(ideo.HasPrecept(D("SlaveGender_FemaleRequired")));
            ideo.memes.Clear(); ideo.foundation.EnsurePreceptsCompatibleWithMemes(new(){mainMeme},new(),Parms());
            Require(ideo.HasPrecept(D("SlaveGender_Unrestricted")) && !ideo.HasPrecept(D("SlaveGender_FemaleRequired")));
            // 普通限定在移除模因后仍合法，关联不是前置；仅观察普通替换资格，不用开发模式绕过。
            var limited=Culture(mainMeme); Add(limited,"SlaveGender_FemaleOnly");
            limited.memes.Clear(); limited.foundation.EnsurePreceptsCompatibleWithMemes(new(){mainMeme},new(),Parms());
            Require(limited.HasPrecept(D("SlaveGender_FemaleOnly")));
        });

        foreach(string language in new[]{"ChineseSimplified","ChineseTraditional","English","Russian"})
            Check("gender all localized fields contain final copy: "+language,()=>
            {
                // 六档名称、说明与独立艳羡阶段都应已定稿；数值仍由上方固定合同逐项断言。
                foreach(string type in new[]{"PreceptDef","ThoughtDef"})
                {
                    var entries=XDocument.Load(Path.Combine(repo,"1.7/Languages",language,"DefInjected",type,"SSC_Culture.xml")).Root.Elements();
                    foreach(var e in entries.Where(e=>e.Name.LocalName.Contains("SlaveGender")))
                        RequireFinalCopy(e.Value);
                }
            });
    }

    private static void GenderStateTests()
    {
        // 下表顺序为 Female/Male/None；个人和社交固定表独立列出，避免复制生产分支自证。
        int[][] violation={new[]{0,-6,-6},new[]{-6,0,-6},new[]{0,0,0},new[]{0,-12,-12},new[]{-12,0,-12},new[]{0,0,0}};
        int[][] duty={new[]{0,0,0},new[]{0,0,0},new[]{0,0,0},new[]{-8,0,0},new[]{0,-8,0},new[]{-12,-12,-12}};
        int[][] badOpinion={new[]{0,-10,-10},new[]{-10,0,-10},new[]{0,0,0},new[]{0,-20,-20},new[]{-20,0,-20},new[]{0,0,0}};
        int[][] dutyOpinion={new[]{0,0,0},new[]{0,0,0},new[]{0,0,0},new[]{-10,0,0},new[]{0,-10,0},new[]{-15,-15,-15}};
        Gender[] genders={Gender.Female,Gender.Male,Gender.None};
        for(int option=0;option<6;option++)
            for(int g=0;g<3;g++)
                for(int state=0;state<3;state++)
                {
                    int o=option,j=g,s=state;
                    Check($"gender literal own state {genderOptions[o]}/{genders[j]}/{s}",()=>
                    {
                        FeedbackContext(); var p=GenderPawn(genders[j],genderOptions[o],s==1?PawnIdentity.Master:PawnIdentity.Unset);
                        if(s==2) Chain(p,MakePawn(PawnIdentity.Master)); MakeMap(p);
                        Equal((float)(s==2?violation[o][j]:s==1?0:duty[o][j]),MoodSum(p));
                        Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
                    });
                    Check($"gender observer-owned social {genderOptions[o]}/{genders[j]}/{s}",()=>
                    {
                        FeedbackContext(); var viewer=GenderPawn(Gender.Female,genderOptions[o],PawnIdentity.Master);
                        var target=GenderPawn(genders[j],identity:s==1?PawnIdentity.Master:PawnIdentity.Unset);
                        if(s==2) Chain(target,MakePawn(PawnIdentity.Master)); MakeMap(viewer,target);
                        Equal(s==2?badOpinion[o][j]:s==1?0:dutyOpinion[o][j],Opinion(viewer,target));
                        Equal(0,Opinion(target,viewer)); Equal(0,Opinion(viewer,viewer));
                        foreach(var thought in Social(viewer,target).Cast<Thought>()) Equal(viewer.Ideo,thought.sourcePrecept.ideo);
                    });
                }

        Check("gender actual chain overrides preselection, qualification is no exemption, hidden duty excluded",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired",PawnIdentity.Slave); MakeMap(p);
            Equal(-8f,MoodSum(p)); // 仅预选性奴途径，尚无实际链，义务仍未履行。
            // 永久资格字段只用于布置状态，不调用身份转换；当前身份仍不是主人。
            var training=p.TryGetComp<CompSexSlaveTraining>(); training.masterQualificationUnlocked=true;
            training.pawnIdentity=PawnIdentity.Unset;
            AdvanceNeeds(p,250);
            Equal(-8f,MoodSum(p));
            var hidden=GenderPawn(Gender.Female,"FemaleRequired"); Equal(0f,MoodSum(hidden)); Equal(0,Opinion(p,hidden));
            Chain(hidden,MakePawn(PawnIdentity.Master)); hidden.gender=Gender.Male;
            tick+=100; Equal(-20,Opinion(p,hidden));
        });

        Check("gender adult boundary rejects teenage stage, null tracker and nonhuman without changing old roster",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired"); MakeMap(p);
            p.ageTracker.AgeBiologicalTicks=17L*GenDate.TicksPerYear;
            Equal(DevelopmentalStage.Adult,p.DevelopmentalStage); Require(!SSCCultureGenderUtility.IsAdult(p)); Equal(0f,MoodSum(p));
            p.ageTracker.AgeBiologicalTicks=18L*GenDate.TicksPerYear;
            Require(SSCCultureGenderUtility.IsAdult(p)); AdvanceNeeds(p,250); Equal(-8f,MoodSum(p));
            var tracker=p.ageTracker; p.ageTracker=null; Require(!SSCCultureGenderUtility.IsAdult(p)); p.ageTracker=tracker;
            p.def.race.intelligence=Intelligence.Animal; Require(!SSCCultureGenderUtility.IsAdult(p));
            p.def.race.intelligence=Intelligence.Humanlike;
            Require(Collect().Contains(p));
        });
    }

    private static void GenderEnvironmentTests()
    {
        foreach(var scenario in new[]{(option:"FemaleOnly",chained:true,gender:Gender.Male,values:new[]{0,-1,-3,-6,-6,-6}),
            (option:"FemaleRequired",chained:true,gender:Gender.Male,values:new[]{0,-2,-6,-12,-12,-12}),
            (option:"FemaleRequired",chained:false,gender:Gender.Female,values:new[]{0,-2,-6,-12,-12,-12}),
            (option:"NonMasterRequired",chained:false,gender:Gender.None,values:new[]{0,-3,-9,-18,-18,-18})})
            Check("gender literal environment curve and full count: "+scenario.option+"/"+scenario.chained,()=>
            {
                // 接收者用当前主人，排除本人义务；分别核对零人、各累进点及超过封顶。
                for(int n=0;n<scenario.values.Length;n++)
                {
                    var cache=FeedbackContext(); var viewer=GenderPawn(Gender.Female,scenario.option,PawnIdentity.Master);
                    var others=GenderOthers(n,scenario.gender,scenario.chained); MakeMap(others.Prepend(viewer).ToArray());
                    Equal((float)scenario.values[n],MoodSum(viewer));
                    if(scenario.chained) { Require(cache.TryGetManagedChainedCount(out int full)); Equal(n,full); }
                    else { Require(cache.TryGetUnfulfilledDutyCount(SSCCultureGenderUtility.Policy(viewer),viewer,out int full)); Equal(n,full); }
                }
            });

        foreach(var sample in new[]{(option:"FemaleOnly",sex:Gender.Male,chained:true,expected:-12),
            (option:"FemaleRequired",sex:Gender.Male,chained:true,expected:-36),
            (option:"FemaleRequired",sex:Gender.Female,chained:false,expected:-32),
            (option:"NonMasterRequired",sex:Gender.Female,chained:false,expected:-30)})
            Check("gender independent negative bound and self exclusion: "+sample.expected,()=>
            {
                FeedbackContext(); var p=GenderPawn(sample.sex,sample.option);
                if(sample.chained) Chain(p,MakePawn(PawnIdentity.Master));
                var bad=GenderOthers(3,Gender.Male,true);
                var unfulfilled=sample.option=="FemaleOnly" ? Array.Empty<Pawn>() : GenderOthers(3,Gender.Female,false);
                MakeMap(bad.Concat(unfulfilled).Prepend(p).ToArray()); Equal((float)sample.expected,MoodSum(p));
            });

        Check("gender managed prisoners differ from duties; minors and hidden players do not inflate new counts",()=>
        {
            var cache=FeedbackContext(); var viewer=GenderPawn(Gender.Female,"FemaleRequired",PawnIdentity.Master);
            var prisoner=GenderPawn(Gender.Male); Set(prisoner,"factionInt",new Faction());
            Set(prisoner.guest,"hostFactionInt",player); Set(prisoner.guest,"guestStatusInt",GuestStatus.Prisoner);
            Chain(prisoner,MakePawn(PawnIdentity.Master));
            var minor=GenderPawn(Gender.Male); minor.ageTracker.AgeBiologicalTicks=17L*GenDate.TicksPerYear; Chain(minor,MakePawn(PawnIdentity.Master));
            var hidden=GenderPawn(Gender.Female); var guest=GenderPawn(Gender.Female); Set(guest,"factionInt",new Faction()); Set(guest.guest,"hostFactionInt",player);
            MakeMap(viewer,prisoner,minor,guest); MakeMap(prisoner);
            Equal(-2f,MoodSum(viewer)); Equal(-20,Opinion(viewer,prisoner)); Equal(0,Opinion(viewer,hidden));
            Require(cache.TryGetManagedChainedCount(out int full)); Equal(2,full);
            Require(cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out int duty)); Equal(0,duty);
            Require(!Collect().Contains(prisoner) && !Collect().Contains(hidden));
        });
    }

    private static void GenderInteractionTests()
    {
        foreach(var sample in new[]{(attitude:"Hostile",option:"FemaleOnly",own:-24,opinion:-25),
            (attitude:"Revulsion",option:"FemaleOnly",own:-12,opinion:-15),
            (attitude:"Indifference",option:"FemaleOnly",own:-6,opinion:-10),
            (attitude:"Respect",option:"FemaleOnly",own:-6,opinion:-5),
            (attitude:"Respect",option:"FemaleRequired",own:-12,opinion:-15),
            (attitude:"Sacred",option:"FemaleOnly",own:-6,opinion:-10),
            (attitude:"Sacred",option:"FemaleRequired",own:-12,opinion:-20),
            (attitude:"Envy",option:"FemaleRequired",own:-12,opinion:-20)})
            Check("gender preserve or cancel existing attitude: "+sample.attitude+"/"+sample.option,()=>
            {
                FeedbackContext(); var p=GenderPawn(Gender.Male,sample.option,extra:new[]{"SexSlave_"+sample.attitude});
                Chain(p,MakePawn(PawnIdentity.Master)); MakeMap(p); Equal((float)sample.own,MoodSum(p));
                // 目标不入管理来源，避免环境反馈影响本人值；一般态度社交仍可以评价该目标。
                var target=GenderPawn(Gender.Male); Chain(target,MakePawn(PawnIdentity.Master)); Equal(sample.opinion,Opinion(p,target));
            });

        Check("gender sacred accepted curve, chained self mutual exclusion and fixed -14 combination",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired",extra:new[]{"SexSlave_Sacred"});
            MakeMap(GenderOthers(3,Gender.Female,true).Concat(GenderOthers(2,Gender.Male,true)).Concat(GenderOthers(2,Gender.Female,false)).Prepend(p).ToArray());
            Equal(-14f,MoodSum(p)); Equal(4,Mood(p).Count);
            Chain(p,MakePawn(PawnIdentity.Master)); AdvanceNeeds(p,250);
            Equal(-2f,MoodSum(p)); // 本人 +10，两个环境各 -6；不再取得旧正向环境 +10。
        });

        foreach(var sample in new[]{(option:"FemaleOnly",gender:Gender.Female,expected:-6),
            (option:"FemaleRequired",gender:Gender.Female,expected:-12),
            (option:"FemaleRequired",gender:Gender.Male,expected:0),
            (option:"NonMasterRequired",gender:Gender.None,expected:-16)})
            Check("gender envy replaces rather than adds wish: "+sample.option+"/"+sample.gender,()=>
            {
                FeedbackContext(); var p=GenderPawn(sample.gender,sample.option,extra:new[]{"SexSlave_Envy"}); MakeMap(p);
                Equal((float)sample.expected,MoodSum(p));
            });

        Check("gender envy observer and target filters while new negative social ignores chained observer restriction",()=>
        {
            FeedbackContext(); var viewer=GenderPawn(Gender.Female,"FemaleOnly",extra:new[]{"SexSlave_Envy"});
            var allowed=GenderPawn(Gender.Female); var rejected=GenderPawn(Gender.Male);
            Chain(allowed,MakePawn(PawnIdentity.Master)); Chain(rejected,MakePawn(PawnIdentity.Master));
            MakeMap(viewer); Equal(15,Opinion(viewer,allowed)); Equal(-10,Opinion(viewer,rejected));
            viewer.gender=Gender.Male; tick+=100; Equal(0,Opinion(viewer,allowed)); Equal(-10,Opinion(viewer,rejected));
            Chain(viewer,MakePawn(PawnIdentity.Master)); tick+=100; Equal(-10,Opinion(viewer,rejected));
        });

        Check("gender quantity still counts rejected bindings and slave leader penalty remains independent",()=>
        {
            FeedbackContext(); var owner=GenderPawn(Gender.Female,"FemaleRequired",PawnIdentity.Master,"SlaveCount_One");
            var slaves=GenderOthers(3,Gender.Male,true);
            foreach(var slave in slaves) SSCBondUtility.GetChain(slave).target=owner;
            BindCandidates(owner,slaves); MakeMap(slaves.Prepend(owner).ToArray());
            Equal(-21f,MoodSum(owner)); // 名下超额两人 -9，违规环境三人 -12。
            var leader=GenderPawn(Gender.Male); Chain(leader,owner); MakeMap(leader);
            var ideo=Culture(); player.ideos.SetPrimary(ideo); Role(ideo,leader);
            var viewer=GenderPawn(Gender.Female,"FemaleOnly",PawnIdentity.Master,"Master_HolyMaster"); MakeMap(viewer);
            tick+=100; Equal(-30,Opinion(viewer,leader)); // 违规 -10，性奴领袖 -20。
        });
    }

    private static void GenderSnapshotTests()
    {
        Check("gender cached self contribution survives same-generation gender and chain changes before cap",()=>
        {
            var cache=FeedbackContext(); var p=GenderPawn(Gender.Male,"FemaleOnly"); Chain(p,MakePawn(PawnIdentity.Master));
            MakeMap(GenderOthers(1,Gender.Male,true).Prepend(p).ToArray());
            Require(cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleOnly,p,out int count)); Equal(1,count);
            p.gender=Gender.Female; p.health.hediffSet.hediffs.Clear();
            Require(cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleOnly,p,out count)); Equal(1,count);
            Equal(1,CallCount("CollectManagedChained")); Equal(1,CallCount("BuildManagedGender"));
            tick+=100; cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleOnly,p,out count); Equal(1,count);
            Equal(2,CallCount("CollectManagedChained"));
        });

        Check("gender cached duty contribution uses captured gender and membership rather than current identity",()=>
        {
            var cache=FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired"); MakeMap(GenderOthers(2,Gender.Female,false).Prepend(p).ToArray());
            cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,p,out int count); Equal(2,count);
            p.gender=Gender.Male; p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master;
            cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,p,out count); Equal(2,count);
            // 源外同性别观察者不得被盲减；快照保留完整三人，不按当前身份改写本批贡献。
            var hidden=GenderPawn(Gender.Female); cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,hidden,out count); Equal(3,count);
        });

        Check("gender lazy work is shared by many observers and expires with original generation",()=>
        {
            var cache=FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired");
            MakeMap(GenderOthers(4,Gender.Male,true).Concat(GenderOthers(4,Gender.Female,false)).Prepend(p).ToArray());
            cache.TryIsMember(p,out _); tick+=90;
            for(int i=0;i<50;i++)
            {
                cache.TryGetAcceptedManagedChainedCount(SSCCultureGenderPolicy.MaleOnly,out _);
                cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleOnly,p,out _);
                cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,p,out _);
            }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CollectManagedChained"));
            Equal(1,CallCount("BuildManagedGender")); Equal(1,CallCount("BuildDutyGender"));
            int recursive=CallCount("get_AllPawnsUnspawned");
            tick+=9; cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.NonMasterRequired,p,out _); Equal(recursive,CallCount("get_AllPawnsUnspawned"));
            tick++; cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.NonMasterRequired,p,out _);
            Equal(2,CallCount("CollectMembers")); Equal(2,CallCount("BuildDutyGender"));
            Require(CallCount("get_AllPawnsUnspawned")>recursive);
        });

        Check("gender absent policy fallback and personal violation avoid unrelated population preparation",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Male); Chain(p,MakePawn(PawnIdentity.Master));
            Equal(SSCCultureGenderPolicy.Unrestricted,SSCCultureGenderUtility.Policy(p));
            Add(p.Ideo,"SlaveGender_FemaleOnly");
            Require(T("SlaveGender_LimitedViolation_Self").Worker.CurrentState(p).Active);
            Equal(0,CallCount("CollectMembers")); Equal(0,CallCount("CollectManagedChained"));
            p.Ideo.RemovePrecept(p.Ideo.GetPrecept(D("SlaveGender_FemaleOnly")));
            Equal(SSCCultureGenderPolicy.Unrestricted,SSCCultureGenderUtility.Policy(p));
            Add(p.Ideo,"SexSlave_Sacred"); MakeMap(p); MoodSum(p);
            Equal(0,CallCount("BuildManagedGender")); Equal(0,CallCount("BuildDutyGender"));
        });

        Check("gender detailed snapshots reset on loading, rollback and Game switch; zero is cached",()=>
        {
            var cache=FeedbackContext(); var viewer=GenderPawn(Gender.Female,"FemaleRequired",PawnIdentity.Master);
            var other=GenderPawn(Gender.Male); Chain(other,MakePawn(PawnIdentity.Master)); MakeMap(viewer,other);
            cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out int count); Equal(1,count);
            cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out count); Equal(0,count);
            cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out count); Equal(1,CallCount("BuildDutyGender"));
            tick+=50; cache.TryIsMember(viewer,out _); other.gender=Gender.Female; tick-=20;
            cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out count); Equal(0,count);
            // 不可查询不能当作有效零人数；恢复时必须重新准备所有已请求桶。
            Scribe.mode=LoadSaveMode.LoadingVars;
            try { Require(!cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out _)); }
            finally { Scribe.mode=LoadSaveMode.Inactive; }
            other.gender=Gender.Male;
            Require(cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out count)); Equal(1,count);
            SetStatic(typeof(ModsConfig),"ideologyActive",false);
            try
            {
                Equal(SSCCultureGenderPolicy.Unrestricted,SSCCultureGenderUtility.Policy(viewer));
                Require(!cache.TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out _));
            }
            finally { SetStatic(typeof(ModsConfig),"ideologyActive",true); }
            FeedbackContext(); Require(!cache.TryGetGenderViolationCount(SSCCultureGenderPolicy.FemaleRequired,viewer,out _));
        });
    }

    private static void GenderLifecycleTests(string game)
    {
        Check("gender native needs refresh replaces duty by allowed self reward and back without memories",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired",extra:new[]{"SexSlave_Envy"}); MakeMap(p);
            Equal(-12f,MoodSum(p)); Chain(p,MakePawn(PawnIdentity.Master)); AdvanceNeeds(p,250); Equal(10f,MoodSum(p));
            p.gender=Gender.Male; AdvanceNeeds(p,250); Equal(-12f,MoodSum(p));
            p.health.hediffSet.hediffs.Clear(); AdvanceNeeds(p,250); Equal(0f,MoodSum(p));
            p.gender=Gender.Female; AdvanceNeeds(p,250); Equal(-12f,MoodSum(p));
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity=PawnIdentity.Master; AdvanceNeeds(p,250); Equal(0f,MoodSum(p));
            Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });

        Check("gender native social refresh converges at every roster generation phase",()=>
        {
            // 穷举两个100 tick缓存的相位；不调用 dirty，不用直接worker结果代替实际双人社交。
            for(int phase=0;phase<100;phase++)
            {
                var cache=FeedbackContext(); var viewer=GenderPawn(Gender.Female,"FemaleRequired",PawnIdentity.Master);
                var target=GenderPawn(Gender.Female); MakeMap(viewer,target); cache.TryIsMember(viewer,out _); tick+=phase;
                Equal(-10,Opinion(viewer,target)); Chain(target,MakePawn(PawnIdentity.Master));
                for(int i=0;i<200;i++) { tick++; _=Opinion(viewer,target); } Equal(0,Opinion(viewer,target));
                target.gender=Gender.Male;
                for(int i=0;i<200;i++) { tick++; _=Opinion(viewer,target); } Equal(-20,Opinion(viewer,target));
            }
        });

        // 只隔离无界面宿主的窗口根和历史存储，SetIdeo、文化重缓存、worker 与社交计算仍执行原版。
        var boundary=new Harmony("ssc.tests.culture.gender-conversion-boundary");
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.WindowStack)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackWindows)));
        boundary.Patch(AccessTools.PropertyGetter(typeof(Find),nameof(Find.HistoryEventsManager)),prefix:new HarmonyMethod(typeof(Program),nameof(FeedbackHistory)));
        boundary.Patch(AccessTools.Method(typeof(HistoryEventsManager),nameof(HistoryEventsManager.RecordEvent)),prefix:new HarmonyMethod(typeof(Program),nameof(SkipBootstrap)));
        try
        {
            Check("gender actual SetIdeo and reform cache ID remove old duty and gender opinions",()=>
            {
                FeedbackContext(); Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
                var p=GenderPawn(Gender.Female,"FemaleRequired",extra:new[]{"SexSlave_Envy"});
                var target=GenderPawn(Gender.Male); Chain(target,MakePawn(PawnIdentity.Master)); MakeMap(p,target);
                Equal(-14f,MoodSum(p)); Equal(-20,Opinion(p,target)); // 艳羡义务本人 -12，加一名违规环境 -2。
                var next=Culture(mainMeme); Add(next,"SlaveGender_Unrestricted"); Add(next,"SexSlave_Envy"); player.ideos.SetPrimary(next);
                p.ideo.SetIdeo(next); Equal(-6f,MoodSum(p)); Equal(15,Opinion(p,target));
                int id=next.currentCacheId;
                next.RemovePrecept(next.GetPrecept(D("SlaveGender_Unrestricted"))); Add(next,"SlaveGender_FemaleOnly");
                Require(next.currentCacheId!=id); p.ideo.IdeoTrackerTickInterval(1);
                Equal(-7f,MoodSum(p)); Equal(-10,Opinion(p,target)); // 旧愿望 -6，加违规环境 -1。
                Require(Social(p,target).Cast<Thought>().All(t=>t.sourcePrecept.ideo==next));
                // 男女限定共用同一 ThoughtDef；原版改革通知应清缓存并更新 sourcePrecept，不能残留已移除档位。
                next.RemovePrecept(next.GetPrecept(D("SlaveGender_FemaleOnly"))); Add(next,"SlaveGender_MaleOnly");
                p.ideo.IdeoTrackerTickInterval(1); target.gender=Gender.Female;
                Equal(-10,Opinion(p,target));
                Require(Social(p,target).Cast<Thought>().All(t=>t.sourcePrecept.def==D("SlaveGender_MaleOnly")));
            });
        }
        finally { boundary.UnpatchAll(boundary.Id); }
    }
}
