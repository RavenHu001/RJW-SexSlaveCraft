// AI 辅助生成：实机调参后的原版人数曲线、五人峰值、回落和共享快照契约。
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void SacredEnvironmentCurveTests()
    {
        Check("only sacred environment has six literal native curve points; invalid curve configs are rejected", () =>
        {
            // Def 的逐点期望独立写死，不能从被测曲线反推；其余环境与数量反馈不得意外改用该曲线。
            var sacred=T("SexSlave_Sacred_Environment"); var config=sacred.GetModExtension<SSCCultureThoughtExtension>();
            float[] values={0,1,3,6,10,15}; Equal(6,config.environmentMoodCurve.PointsCount);
            for(int i=0;i<values.Length;i++) { Equal((float)i,config.environmentMoodCurve[i].x); Equal(values[i],config.environmentMoodCurve[i].y); }
            Require(DefDatabase<ThoughtDef>.AllDefs.Where(d=>d!=sacred)
                .All(d=>d.GetModExtension<SSCCultureThoughtExtension>().environmentMoodCurve==null));
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.ManagedChainedEnvironment,environmentMoodCurve=new SimpleCurve()}.ConfigErrors().Any());
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.MasterSelf,environmentMoodCurve=config.environmentMoodCurve}.ConfigErrors().Any());
            var reversed=new SimpleCurve(); reversed.Points.Add(new CurvePoint(2,3)); reversed.Points.Add(new CurvePoint(1,1));
            Require(new SSCCultureThoughtExtension {condition=SSCCultureThoughtCondition.ManagedChainedEnvironment,environmentMoodCurve=reversed}.ConfigErrors().Any());
        });
        Check("native sacred mood for zero through seven and far above cap keeps full population count", () =>
        {
            // 原版 MoodOffset/TotalMoodOffset 与实际 GameComponent 共同验证；100 人仍缓存完整 100，不改统计口径。
            float[] values={0,1,3,6,10,15,15,15};
            for(int count=0;count<values.Length;count++)
            {
                var cache=FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); var owner=MakePawn(PawnIdentity.Master);
                var slaves=BoundGroup(owner,count); MakeMap(slaves.Prepend(p).ToArray());
                Equal(values[count],MoodSum(p)); Require(cache.TryGetManagedChainedCount(out int actual)); Equal(count,actual);
                if(count>0) Equal(values[count],Mood(p).Single().MoodOffset()); else Equal(0,Mood(p).Count);
            }
            var farCache=FeedbackContext(); var viewer=Believer(new[] {"SexSlave_Sacred"});
            MakeMap(BoundGroup(MakePawn(PawnIdentity.Master),100).Prepend(viewer).ToArray());
            Equal(15f,MoodSum(viewer)); Require(farCache.TryGetManagedChainedCount(out int full)); Equal(100,full);
        });
        Check("sacred environment rises then falls via actual NeedsTracker; chained self remains exclusive", () =>
        {
            FeedbackContext(); var p=Believer(new[] {"SexSlave_Sacred"}); var owner=MakePawn(PawnIdentity.Master); MakeMap(p);
            Equal(0f,MoodSum(p)); var group=BoundGroup(owner,8); MakeMap(group); AdvanceNeeds(p,250); Equal(15f,MoodSum(p));
            foreach(var sample in new (int count,float value)[] {(5,15),(4,10),(3,6),(2,3),(1,1),(0,0)})
            {
                // 保留地图成员，只移除链，使人数真实回落；不手动清 thought 或共享快照。
                for(int i=sample.count;i<group.Length;i++) group[i].health.hediffSet.hediffs.Clear();
                AdvanceNeeds(p,250); Equal(sample.value,MoodSum(p));
            }
            foreach(var slave in group) Chain(slave,owner); AdvanceNeeds(p,250); Equal(15f,MoodSum(p));
            var old=Mood(p).Single(); Chain(p,owner); Equal(0f,old.MoodOffset()); AdvanceNeeds(p,150);
            Equal(10f,MoodSum(p)); Equal(1,Mood(p).Count); Equal(0,p.needs.mood.thoughts.memories.Memories.Count);
        });
        Check("nonlinear sacred and linear hostile/revulsion observers share one unchanged population snapshot", () =>
        {
            FeedbackContext(); var sacred=Believer(new[] {"SexSlave_Sacred"}); var hostile=Believer(new[] {"SexSlave_Hostile"});
            var revulsion=Believer(new[] {"SexSlave_Revulsion"}); var group=BoundGroup(MakePawn(PawnIdentity.Master),8);
            MakeMap(group.Concat(new[] {sacred,hostile,revulsion}).ToArray()); calls.Clear();
            for(int i=0;i<30;i++)
            {
                // 同一个真实人数因戒律走不同倍率；共享 worker 不能保留上一个观察者的曲线或人数。
                sacred.needs.mood.thoughts.ThoughtInterval(); hostile.needs.mood.thoughts.ThoughtInterval(); revulsion.needs.mood.thoughts.ThoughtInterval();
                Equal(15f,MoodSum(sacred)); Equal(-24f,MoodSum(hostile)); Equal(-16f,MoodSum(revulsion));
            }
            Equal(1,CallCount("CollectMembers")); Equal(1,CallCount("CountManagedChained"));
            Equal(0,CallCount("CountMasters")); Equal(0,CallCount("CountBoundSlaves")); Equal(0,CallCount("Query"));
        });
    }
}
