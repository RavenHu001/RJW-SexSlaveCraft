// AI 辅助生成：实机反馈后的艳羡义务专属阶段；复用真实需求、文化、来源和缓存，不另造结算算法。
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void EnviousDutyTests()
    {
        foreach(var sample in new[]{(option:"FemaleRequired",gender:Gender.Female,normal:-8f,envious:-12f,family:"RequiredDuty"),
            (option:"MaleRequired",gender:Gender.Male,normal:-8f,envious:-12f,family:"RequiredDuty"),
            (option:"NonMasterRequired",gender:Gender.None,normal:-12f,envious:-16f,family:"NonMasterDuty")})
            Check("envious duty literal exclusive stage and native source: "+sample.option,()=>
            {
                // 每组均验证普通/艳羡固定数值及 sourcePrecept；不能把普通义务和额外愿望相加冒充专属阶段。
                foreach(bool envy in new[]{false,true})
                {
                    FeedbackContext(); var p=GenderPawn(sample.gender,sample.option,extra:new[]{envy?"SexSlave_Envy":"SexSlave_Sacred"}); MakeMap(p);
                    Equal(envy?sample.envious:sample.normal,MoodSum(p)); Equal(1,Mood(p).Count);
                    var thought=Mood(p).Single(); Equal(T("SlaveGender_"+sample.family+"_Self"),thought.def);
                    Equal(envy?1:0,thought.CurStageIndex); Equal(p.Ideo.GetPrecept(D("SlaveGender_"+sample.option)),thought.sourcePrecept);
                    Require(thought.def.stages[0].label!=thought.def.stages[1].label);
                    Require(thought.def.stages[0].description!=thought.def.stages[1].description);
                }
            });

        Check("envious duty fixed config only permits optional stage one on duty self",()=>
        {
            // 错误作用域/索引在资源检查时报错；其他原有反馈的默认 -1 不变。
            foreach(var d in DefDatabase<ThoughtDef>.AllDefs)
            {
                var config=d.GetModExtension<SSCCultureThoughtExtension>();
                bool special=d.defName is "SSC_Culture_SlaveGender_RequiredDuty_Self" or "SSC_Culture_SlaveGender_NonMasterDuty_Self";
                Equal(special?1:-1,config.enviousDutyStage);
            }
            Require(!new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.UnfulfilledDutySelf,enviousDutyStage=1}.ConfigErrors().Any());
            foreach(int stage in new[]{-2,0,2,99})
                Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.UnfulfilledDutySelf,enviousDutyStage=stage}.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.EnvySelf,enviousDutyStage=1}.ConfigErrors().Any());
        });

        Check("envious duty reform switches stage and text then binding removes it via native needs",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired",extra:new[]{"SexSlave_Envy"}); MakeMap(p);
            // 原版文化 tracker 需要世界文化管理器；提供真实根对象，不手工清 thought 缓存。
            Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
            Equal(-12f,MoodSum(p)); string enviousLabel=Mood(p).Single().CurStage.label;
            p.Ideo.RemovePrecept(p.Ideo.GetPrecept(D("SexSlave_Envy"))); p.ideo.IdeoTrackerTickInterval(1);
            Equal(-8f,MoodSum(p)); Equal(0,Mood(p).Single().CurStageIndex); Require(enviousLabel!=Mood(p).Single().CurStage.label);
            Add(p.Ideo,"SexSlave_Envy"); p.ideo.IdeoTrackerTickInterval(1);
            Equal(-12f,MoodSum(p)); Equal(enviousLabel,Mood(p).Single().CurStage.label);
            // 有链即履行义务，随后只保留合规本人 +10；移链后恢复专属 -12，不产生记忆。
            Chain(p,MakePawn(PawnIdentity.Master)); AdvanceNeeds(p,250); Equal(10f,MoodSum(p)); Equal(1,Mood(p).Count);
            p.health.hediffSet.hediffs.Clear(); AdvanceNeeds(p,250); Equal(-12f,MoodSum(p));
            Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });

        Check("envious duty only changes own stage while environment social and population work stay identical",()=>
        {
            FeedbackContext(); var p=GenderPawn(Gender.Female,"FemaleRequired",extra:new[]{"SexSlave_Envy"});
            Current.Game.World.ideoManager=new IdeoManager {classicMode=true};
            var bad=GenderPawn(Gender.Male); Chain(bad,MakePawn(PawnIdentity.Master)); var owing=GenderPawn(Gender.Female); MakeMap(p,bad,owing);
            Equal(-16f,MoodSum(p)); Equal(-20,Opinion(p,bad)); Equal(-10,Opinion(p,owing));
            int members=CallCount("CollectMembers"),managed=CallCount("CollectManagedChained");
            int duties=CallCount("BuildDutyGender"),genders=CallCount("BuildManagedGender");
            p.Ideo.RemovePrecept(p.Ideo.GetPrecept(D("SexSlave_Envy"))); p.ideo.IdeoTrackerTickInterval(1);
            Equal(-12f,MoodSum(p)); Equal(-20,Opinion(p,bad)); Equal(-10,Opinion(p,owing));
            Equal(members,CallCount("CollectMembers")); Equal(managed,CallCount("CollectManagedChained"));
            Equal(duties,CallCount("BuildDutyGender")); Equal(genders,CallCount("BuildManagedGender"));
        });

        Check("envious duty preserves master exemption, wrong gender exclusion and ordinary envy wish",()=>
        {
            foreach(var sample in new[]{(option:"FemaleRequired",gender:Gender.Female,identity:PawnIdentity.Master,expected:0f),
                (option:"FemaleRequired",gender:Gender.Male,identity:PawnIdentity.Unset,expected:0f),
                (option:"FemaleOnly",gender:Gender.Female,identity:PawnIdentity.Unset,expected:-6f)})
            {
                FeedbackContext(); var p=GenderPawn(sample.gender,sample.option,sample.identity,"SexSlave_Envy"); MakeMap(p);
                Equal(sample.expected,MoodSum(p));
            }
        });

        foreach(var sample in new[]{(option:"FemaleRequired",expected:-36f),(option:"NonMasterRequired",expected:-34f)})
            Check("envious duty own plus environment fixed negative bound: "+sample.option,()=>
            {
                // 三人封顶继续只作用于环境；新的本人专属值独立相加，不新增全文化总下限。
                FeedbackContext(); var p=GenderPawn(Gender.Female,sample.option,extra:new[]{"SexSlave_Envy"});
                var owing=GenderOthers(3,Gender.Female,false);
                var rejected=sample.option=="FemaleRequired"?GenderOthers(3,Gender.Male,true):Array.Empty<Pawn>();
                MakeMap(owing.Concat(rejected).Prepend(p).ToArray()); Equal(sample.expected,MoodSum(p));
            });
    }
}
