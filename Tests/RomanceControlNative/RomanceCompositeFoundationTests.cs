// AI 辅助生成：第一批执行真实目录、原生记忆和 Scribe；实际事件切换留后续批次。
using System.Globalization;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>从已提交 Markdown 独立读取期望，不复制生产目录数组或用旧验收代替新验证。</summary>
    private static void RomanceCompositeFoundationTests(string repo)
    {
        SetupCompositeCarriers(repo);
        string document = File.ReadAllText(Path.Combine(repo, "Docs/Design/性奴浪漫综合心情完整分支表.md"));
        string eventTable = document.Split("## 2. 完整事件分支")[1].Split("## 3.")[0];
        var rows = eventTable.Split('\n').Where(line => line.StartsWith("| `E"))
            .Select(line => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray()).ToList();
        Check("composite catalog has complete immutable stable identities", () =>
        {
            Equal(108, rows.Count); Equal(108, SSCRomanceMoodCatalog.EventBranches.Count); Equal(14, SSCRomanceMoodCatalog.ControlBranches.Count);
            var all = SSCRomanceMoodCatalog.EventBranches.Concat(SSCRomanceMoodCatalog.ControlBranches).ToList();
            Equal(122, all.Select(b => b.Id).Distinct().Count()); Equal(122, all.Select(b => b.NameKey).Distinct().Count());
            Equal(122, all.Select(b => b.DescriptionKey).Distinct().Count()); Require(((IList<SSCRomanceMoodBranch>)SSCRomanceMoodCatalog.EventBranches).IsReadOnly);
            Equal(5, SSCRomanceMoodCatalog.EventBranches.Count(b => b.Value == 0));
        });
        int longCount = 0, splitCount = 0;
        foreach (var row in rows)
        {
            string bid = row[0].Trim('`'); var tokens = bid.Split('-');
            int eventNumber = int.Parse(tokens[0].Substring(1)), stage = int.Parse(tokens[1].Substring(1));
            bool maso = tokens[2] == "M"; int value = int.Parse(row[4].Replace('−','-'), CultureInfo.InvariantCulture);
            int longDays = eventNumber == 13 ? 30 : new[] { 5,9,10,11,12 }.Contains(eventNumber) ? 25 : 0;
            int[] opinions = tokens[3] == "D" ? new[] { -100,-10 } : tokens[3] == "G" ? new[] { -9,0,1,59 } : new[] { 60,100 };
            if (longDays > 0 && value < 0 && tokens[3] != "D") { longCount++; if (tokens[3] == "G") splitCount++; }
            Check("composite documented branch " + bid, () =>
            {
                foreach (int opinion in opinions)
                {
                    Require(SSCRomanceMoodCatalog.TryResolve((SSCRomanceCompositeEvent)eventNumber,stage,maso,opinion,out var selection));
                    Equal(bid, selection.Branch.Id); Equal(value, selection.Branch.Value);
                    int days = value == 0 ? 0 : longDays > 0 && value < 0 && opinion > 0 ? longDays : 3;
                    Equal(days * 60000, selection.DurationTicks);
                    Equal(days == 0 ? SSCRomanceMoodGroup.None : days == 3 ? SSCRomanceMoodGroup.Short : SSCRomanceMoodGroup.Long, selection.Group);
                    var data = selection.CreateSnapshot(); Require(SSCRomanceMoodCatalog.TryReadSnapshot(data,out var branch));
                    Equal(bid, branch.Id); Equal(opinion, data.RawOpinion); Require(data.HasRawOpinion && data.IsComposite);
                    Equal(days * 60000,data.NominalDurationTicks); Equal(value != 0,Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory));
                    if (value == 0) { Require(memory == null); continue; }
                    memory.pawn = SocialPawn(); Equal(value + 15,memory.CurStageIndex); Equal((float)value,memory.MoodOffset());
                    Equal(value,data.Copy().Value); Equal(days * 60000,memory.DurationTicks); Require(!ReferenceEquals(data,data.Copy()));
                }
            });
        }
        Check("composite 26 long variants include 12 general raw opinion splits",()=>{Equal(26,longCount);Equal(12,splitCount);});

        string controlTable = document.Split("## 4. 持续思想分支补全")[1].Split("## 5.")[0];
        foreach (string line in controlTable.Split('\n').Where(l=>l.StartsWith("| `C-")))
        {
            var cells=line.Trim().Trim('|').Split('|').Select(c=>c.Trim()).ToArray(); string bid=cells[0].Trim('`'); var tokens=bid.Split('-');
            var state=tokens[1]=="A"?SSCRomanceControlState.Active:tokens[1]=="B"?SSCRomanceControlState.Passive:SSCRomanceControlState.Both;
            Check("composite continuous directory " + bid,()=>
            {
                Require(SSCRomanceMoodCatalog.TryResolveControl(state,int.Parse(tokens[2].Substring(1)),tokens[3]=="M",out var branch));
                Equal(bid,branch.Id);Equal(int.Parse(cells[4].Replace('−','-')),branch.Value);Require(branch.IsContinuous);Require(!branch.Opinion.HasValue);
            });
        }
        foreach (int eventNumber in Enumerable.Range(0,15))
        foreach (int stage in new[]{0,1,2,3,4,5})
        {
            int ev=eventNumber,s=stage;
            if (ev>=1 && ev<=13 && (s==4 || s==3 && ev<=5)) continue;
            Check($"composite unsupported event stage {ev}/{s}",()=>Require(!SSCRomanceMoodCatalog.TryResolve((SSCRomanceCompositeEvent)ev,s,false,60,out _)));
        }
        Check("composite missing and malformed snapshot never becomes a valid zero",()=>
        {
            Require(!SSCRomanceMoodCatalog.TryGet(null,out _));Require(!SSCRomanceMoodCatalog.TryGet("unknown",out _));
            Require(!SSCRomanceMoodCatalog.TryReadSnapshot(new SSCRomanceMoodData(),out _));
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            Action<Action<SSCRomanceMoodData>> invalid=change=>{var data=selection.CreateSnapshot();change(data);Require(!SSCRomanceMoodCatalog.TryReadSnapshot(data,out _));};
            invalid(d=>d.DataVersion=99); invalid(d=>d.BranchId="missing"); invalid(d=>d.HasRawOpinion=false);
            invalid(d=>d.Masochist=true); invalid(d=>d.RawOpinion=0); invalid(d=>d.Value=16);
            invalid(d=>d.NominalDurationTicks=25*60000); invalid(d=>{d.Group=SSCRomanceMoodGroup.Short;d.NominalDurationTicks=3*60000;});
            Require(!SSCRomanceMoodCatalog.TryResolveControl(SSCRomanceControlState.Passive,2,false,out _));
        });
        Check("composite missing carrier and malformed stage fail without acquiring memory",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,false,60,out var selection);
            var carrier=DefDatabase<ThoughtDef>.GetNamed(Thought_MemorySSCCompositeRomance.ShortDefName);int index=selection.Branch.Value+15;
            var stage=carrier.stages[index];var savedType=carrier.thoughtClass;
            try
            {
                carrier.stages[index]=null;Require(!Thought_MemorySSCCompositeRomance.TryCreate(selection,out _));carrier.stages[index]=stage;
                carrier.thoughtClass=typeof(Thought_Memory);Require(!Thought_MemorySSCCompositeRomance.TryCreate(selection,out _));
            }
            finally{carrier.stages[index]=stage;carrier.thoughtClass=savedType;}
        });
        Check("composite snapshot preserves actual context without copying old branch or corruption",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,true,60,out var selection);
            var context=new SSCRomanceMoodData{Relation=SSCRomanceOldRelation.Spouse,Master=SocialPawn(),HasRule=true,
                Rule=SSCRomanceSetting.AlwaysAcceptMaster,BranchId="unrelated"};
            Require(Thought_MemorySSCCompositeRomance.TryCreate(selection,context,out var memory));
            Equal(context.Relation,memory.Data.Relation);Equal(context.Master,memory.Data.Master);Require(memory.Data.HasRule);
            Equal(context.Rule,memory.Data.Rule);Equal(selection.Branch.Id,memory.Data.BranchId);
            Equal("unrelated",context.BranchId);
        });
        CompositeTextTests(repo);
        CompositeSaveTests();
    }

    /// <summary>从新增实际 XML 构建原生 Def；地图与 DLC Def 合并不属于宿主，条件引用另静态核对。</summary>
    private static void SetupCompositeCarriers(string repo)
    {
        if(DefDatabase<ThoughtDef>.GetNamedSilentFail(Thought_MemorySSCCompositeRomance.ShortDefName)!=null)return;
        var xml=XDocument.Load(Path.Combine(repo,"1.7/Defs/ThoughtDefs/SSC_RomanceCompositeMood.xml"));
        foreach (var node in xml.Root.Elements())
        {
            var def=new ThoughtDef{defName=node.Element("defName").Value,thoughtClass=typeof(Thought_MemorySSCCompositeRomance),
                durationDays=float.Parse(node.Element("durationDays").Value,CultureInfo.InvariantCulture),
                stackLimit=int.Parse(node.Element("stackLimit").Value),showBubble=false,developmentalStageFilter=DevelopmentalStage.Adult,stages=new()};
            Equal(typeof(Thought_MemorySSCCompositeRomance).FullName,node.Element("thoughtClass").Value);
            foreach(var stage in node.Element("stages").Elements())def.stages.Add(new ThoughtStage{label=stage.Element("label").Value,description=stage.Element("description").Value,
                baseMoodEffect=float.Parse(stage.Element("baseMoodEffect").Value,CultureInfo.InvariantCulture)});
            DefDatabase<ThoughtDef>.Add(def);
            Check("composite real carrier XML config " + def.defName,()=>
            {
                Equal(31,def.stages.Count);Require(!def.ConfigErrors().Any(),string.Join(";",def.ConfigErrors()));
                Equal("Inhumanized",node.Element("nullifyingHediffs").Element("li").Value);
                Equal("Ludeon.RimWorld.Anomaly",node.Element("nullifyingHediffs").Element("li").Attribute("MayRequire").Value);
            });
        }
    }

    /// <summary>加载四语真实 Keyed 资源并调用实际显示入口；正式文案只检查独立键，不替维护者定稿。</summary>
    private static void CompositeTextTests(string repo)
    {
        // 宿主未运行游戏启动后的颜色缓存初始化；沿其他原生显示专项建立真实格式化环境。
        FactionDefOf.PlayerColony ??= new FactionDef{defName="CompositePlayerColony",pawnSingular="colonist",pawnsPlural="colonists"};
        ColoredText.ResetStaticData();
        var language=LanguageDatabase.activeLanguage;var original=language.keyedReplacements;
        var branches=SSCRomanceMoodCatalog.EventBranches.Concat(SSCRomanceMoodCatalog.ControlBranches).ToList();
        foreach(string locale in new[]{"ChineseSimplified","ChineseTraditional","English","Russian"})
        {
            Check("composite independent text keys and mirror " + locale,()=>
            {
                string path=Path.Combine(repo,"1.7/Languages",locale,"Keyed/SSC_RomanceMood.xml");
                Equal(File.ReadAllText(path),File.ReadAllText(Path.Combine(repo,"Sexslavecraft/Languages",locale,"Keyed/SSC_RomanceMood.xml")));
                var entries=XDocument.Load(path).Root.Elements().ToList();Equal(246,entries.Count);Equal(246,entries.Select(e=>e.Name.LocalName).Distinct().Count());
                try
                {
                    language.keyedReplacements=entries.ToDictionary(e=>e.Name.LocalName,e=>new LoadedLanguage.KeyedReplacement{key=e.Name.LocalName,value=e.Value});
                    foreach(var branch in branches)
                    {
                        Equal(language.keyedReplacements[branch.NameKey].value,SSCRomanceMoodText.Name(branch));
                        Equal(language.keyedReplacements[branch.DescriptionKey].value,SSCRomanceMoodText.Description(branch));
                    }
                }
                finally{language.keyedReplacements=original;}
            });
        }
        Check("composite equal values keep distinct names descriptions and language changes",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,true,60,out var a);
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandProposalAccepted,4,true,60,out var b);
            Require(Thought_MemorySSCCompositeRomance.TryCreate(a,out var first));Require(Thought_MemorySSCCompositeRomance.TryCreate(b,out var second));
            first.pawn=SocialPawn();second.pawn=first.pawn;Equal(15f,first.MoodOffset());Equal(15f,second.MoodOffset());
            try
            {
                language.keyedReplacements=new(original);
                void Text(string key,string value)=>language.keyedReplacements[key]=new LoadedLanguage.KeyedReplacement{key=key,value=value};
                Text(a.Branch.NameKey,"First name");Text(a.Branch.DescriptionKey,"First description");
                Text(b.Branch.NameKey,"Second name");Text(b.Branch.DescriptionKey,"Second description");
                Equal("First name",first.LabelCap);Equal("First description",first.Description);
                Equal("Second name",second.LabelCap);Equal("Second description",second.Description);
                Text(a.Branch.NameKey,"Changed language");Equal("Changed language",first.LabelCap);Equal(a.Branch.Id,first.Data.BranchId);
                var frozen=first.Data.Copy();first.pawn.story.traits.allTraits.Clear();Equal("First description",first.Description);Equal(frozen.BranchId,first.Data.BranchId);
            }
            finally{language.keyedReplacements=original;}
        });
        Check("composite missing text and unknown historical branch use explicit placeholder",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory);memory.pawn=SocialPawn();memory.Data.BranchId="unknown";
            try
            {
                language.keyedReplacements=new(original);
                language.keyedReplacements["SSC_RomanceMood_Unknown_Name"]=new LoadedLanguage.KeyedReplacement{key="SSC_RomanceMood_Unknown_Name",value="Unknown history"};
                Equal("Unknown history",memory.LabelCap);Equal(0,memory.pawn.needs.mood.thoughts.memories.Memories.Count);
                memory.pawn.needs.mood.thoughts.memories.TryGainMemory(memory);Equal(0,memory.pawn.needs.mood.thoughts.memories.Memories.Count);
            }
            finally{language.keyedReplacements=original;}
        });
        Check("composite description retains native nullification explanation",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandRomanceAccepted,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory);memory.pawn=SocialPawn();
            var boundary=new Harmony("ssc.tests.composite.nullifier");
            try
            {
                boundary.Patch(AccessTools.Method(typeof(ThoughtUtility),nameof(ThoughtUtility.ThoughtNullifiedMessage)),prefix:new HarmonyMethod(typeof(Program),nameof(CompositeNullifierMessage)));
                Require(memory.Description.EndsWith("\n\n([test nullifier])"));
            }
            finally{boundary.UnpatchAll(boundary.Id);}
        });
    }

    /// <summary>真实保存、人格载荷与 CopyFrom 往返；不把复制当成新事件，不构造命令或运行请求。</summary>
    private static void CompositeSaveTests()
    {
        foreach(var tuple in new[]{(SSCRomanceCompositeEvent.CommandRomanceAccepted,60),(SSCRomanceCompositeEvent.CommandProposalRejected,0),(SSCRomanceCompositeEvent.CommandProposalRejected,1),(SSCRomanceCompositeEvent.CommandDivorce,60)})
        {
            var kind=tuple.Item1;int opinion=tuple.Item2;
            Check($"composite real Scribe frozen history {kind}/{opinion}",()=>
            {
                Require(SSCRomanceMoodCatalog.TryResolve(kind,4,false,opinion,out var selection));
                var owner=SocialPawn();var other=SocialPawn();var master=SocialPawn();
                var context=new SSCRomanceMoodData{Relation=kind==SSCRomanceCompositeEvent.CommandDivorce?SSCRomanceOldRelation.Spouse:
                    kind==SSCRomanceCompositeEvent.CommandProposalRejected?SSCRomanceOldRelation.Lover:SSCRomanceOldRelation.None,
                    Master=master,HasRule=true,Rule=SSCRomanceSetting.AlwaysAcceptMaster};
                Require(Thought_MemorySSCCompositeRomance.TryCreate(selection,context,out var memory));
                memory.pawn=owner;memory.otherPawn=other;memory.Data.OccurrenceOrder=SSCRomanceMoodOrder.Next();memory.age=4321;memory.moodPowerFactor=.5f;memory.moodOffset=2;
                // 标准二十五天与实际剩余二十九天必须独立保留。
                if(kind==SSCRomanceCompositeEvent.CommandProposalRejected && opinion==1)memory.durationTicksOverride=29*60000+memory.age;
                var saved=PersonalityMemoryUtility.Capture(memory);var copy=PersonalityMemoryUtility.Copy(saved);
                Require(!ReferenceEquals(saved.romanceMood,copy.romanceMood));
                var scene=new RomanceMoodScene{Memory=memory,Stored=new(){copy}};string path=Path.Combine(saveDirectory,"composite-"+(++id)+".xml");
                Scribe.saver.InitSaving(path,"Composite");Scribe_Deep.Look(ref scene,"scene");Scribe.saver.FinalizeSaving();
                RomanceMoodScene loaded=null;Scribe.loader.InitLoading(path);
                var directory=(LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler),"loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
                directory.RegisterLoaded(owner);directory.RegisterLoaded(other);directory.RegisterLoaded(master);
                Scribe_Deep.Look(ref loaded,"scene");Scribe.loader.FinalizeLoading();
                var actual=(Thought_MemorySSCCompositeRomance)loaded.Memory;
                Equal(memory.Data.BranchId,actual.Data.BranchId);Equal(memory.Data.RawOpinion,actual.Data.RawOpinion);Equal(memory.Data.Value,actual.Data.Value);
                Equal(memory.Data.OccurrenceOrder,actual.Data.OccurrenceOrder);Equal(memory.Data.NominalDurationTicks,actual.Data.NominalDurationTicks);Equal(memory.durationTicksOverride,actual.durationTicksOverride);Equal(4321,actual.age);
                Equal(.5f,actual.moodPowerFactor);Equal(2,actual.moodOffset);Equal(memory.Data.Group,actual.Data.Group);
                // 原版不把运行 owner 写入独立思想；人格恢复由目标处理器重新绑定，不能指回旧身体。
                Require(actual.pawn==null);Equal(other,actual.otherPawn);Equal(master,actual.Data.Master);Equal(context.Relation,actual.Data.Relation);
                Require(actual.Data.HasRule);Equal(context.Rule,actual.Data.Rule);
                var clone=(Thought_MemorySSCCompositeRomance)ThoughtMaker.MakeThought(memory.def,memory.CurStageIndex);clone.CopyFrom(memory);
                Require(!ReferenceEquals(memory.Data,clone.Data));Equal(memory.Data.BranchId,clone.Data.BranchId);Equal(memory.age,clone.age);
                var host=SocialPawn();PersonalityMemoryUtility.Restore(host,loaded.Stored);
                var restored=(Thought_MemorySSCCompositeRomance)host.needs.mood.thoughts.memories.Memories.Single();
                Equal(memory.Data.BranchId,restored.Data.BranchId);Equal(memory.durationTicksOverride,restored.durationTicksOverride);Equal(4321,restored.age);
                Equal(other,restored.otherPawn);Equal(master,restored.Data.Master);Equal(context.Relation,restored.Data.Relation);
                Equal(host,restored.pawn);
            });
        }
        Check("composite frozen base keeps native multiplier and instance offset",()=>
        {
            SSCRomanceMoodCatalog.TryResolve(SSCRomanceCompositeEvent.CommandDivorce,4,false,60,out var selection);
            Thought_MemorySSCCompositeRomance.TryCreate(selection,out var memory);memory.pawn=SocialPawn();
            memory.Data.Value=-14;memory.moodPowerFactor=.5f;memory.moodOffset=2;
            Equal(-5f,memory.MoodOffset());Equal(-14,memory.Data.Value);
        });

    }

    /// <summary>只提供 DLC 说明边界，正文拼接仍执行生产入口；不把此项冒充完整 DLC 模组验收。</summary>
    private static bool CompositeNullifierMessage(ref string __result){__result="[test nullifier]";return false;}
}
