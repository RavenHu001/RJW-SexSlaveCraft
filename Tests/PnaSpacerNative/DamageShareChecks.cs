// AI 辅助生成：真实 TakeDamage + 分摊前置 + 共用上下文 + 两代处理器串联。
// 地图、日志和最终物理伤势为边界；分摊资格、候选／比例／复制及调用顺序执行生产代码。
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using SexSlaveCraft;

internal static partial class Program
{
    internal static readonly Dictionary<Pawn,Hediff_BridleOfSexSlave> ShareBridles=new();
    private static readonly List<(Pawn Target,DamageInfo Info)> ShareHits=new();
    private static bool nativeShare, skipShareHit;
    private static Map shareMap;
    private static Action<Pawn,DamageInfo> onSharePhysical;
    private static DamageDef sharedCoating, sharedOrdinary;

    /// <summary>串联实际 1.7 TakeDamage 与生产分伤／PNA 上下文；地图、最终伤势和日志等保持隔离边界。</summary>
    private static void RunDamageShareChecks(Harmony harmony)
    {
        harmony.CreateClassProcessor(typeof(Harmony_SSCPnaDamageShareContext)).Patch();
        // 让生产分摊前置先于隔离未初始化健康系统的边界执行；不改变生产补丁的实际优先级。
        harmony.Patch(AccessTools.Method(typeof(Pawn),nameof(Verse.Pawn.PreApplyDamage)),
            prefix:new HarmonyMethod(typeof(Harmony_SSC_MasterDamageShare),nameof(Harmony_SSC_MasterDamageShare.Prefix)){priority=Priority.High});
        void Prefix(MethodBase target,string name)=>harmony.Patch(target,prefix:new HarmonyMethod(typeof(Program),name));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),nameof(Thing.MapHeld)),nameof(ShareMap));
        Prefix(AccessTools.PropertyGetter(typeof(Pawn),nameof(Verse.Pawn.LabelShort)),nameof(ShareLabel));
        Prefix(AccessTools.Method(typeof(SSCMasterBondUtility),nameof(SSCMasterBondUtility.CanSlaveShareDamage)),nameof(ShareCandidate));
        Prefix(AccessTools.Method(typeof(DamageWorker_AddInjury),nameof(DamageWorker_AddInjury.Apply)),nameof(SharePhysical));
        Prefix(AccessTools.Method(typeof(Pawn),nameof(Verse.Pawn.PostApplyDamage)),nameof(SharePostDamage));
        Prefix(AccessTools.Method(typeof(Thing),nameof(Thing.TakeDamage)),nameof(ShareTake));
        foreach(var method in typeof(Log).GetMethods().Where(m=>m.Name==nameof(Log.Message))) Prefix(method,nameof(ShareLog));
        foreach(var method in typeof(Translator).GetMethods().Concat(typeof(TranslatorFormattedStringExtensions).GetMethods()).Where(m=>m.Name=="Translate"&&!m.ContainsGenericParameters&&m.ReturnType==typeof(TaggedString)))
            Prefix(method,nameof(ShareTranslation));
        shareMap=(Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
        var wound=new HediffDef{defName="Stab",maxSeverity=100};
        sharedCoating=new DamageDef{defName="SSC_ShareCoated",harmsHealth=true,workerClass=typeof(DamageWorker_SSCPnaCoated),hediff=wound,
            armorCategory=DamageArmorCategoryDefOf.Sharp,modExtensions=new List<DefModExtension>{new SSCPnaCoatingExtension{baseLoad=.15f,recordHediff=new HediffDef{defName="ShareSnapshot"}}}};
        sharedOrdinary=new DamageDef{defName="SSC_ShareOrdinary",harmsHealth=true,workerClass=typeof(DamageWorker_AddInjury),hediff=new HediffDef{defName="Bruise",maxSeverity=100}};
        try
        {
            // 改写原缰绳字段后仍走实际受伤入口，证明公共偏好读取真实控制分伤且没有第二份保存值。
            ShareCheck("串联：原缰绳关闭阻断分伤，重开恢复原比例",()=>
            {
                var (master,slaves)=ShareMaster(1);var bridle=ShareBridles[master];var shot=SharedShot(sharedOrdinary);
                bridle.shareDamageEnabled=false;
                Assert(!SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(master));
                Assert(!SSCMasterBondUtility.CanRedirectDamageFrom(master,shot));
                master.TakeDamage(shot);Equal(1,ShareHits.Count);Assert(ShareHits[0].Target==master);Near(5,ShareHits[0].Info.Amount);
                ShareHits.Clear();bridle.shareDamageEnabled=true;
                // 同一请求重开后恢复旧比例；不通过修改候选、过滤器或伤害处理器来制造结果。
                Assert(SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(master));
                master.TakeDamage(shot);Equal(2,ShareHits.Count);Near(3.5f,ShareHits[0].Info.Amount);Near(1.5f,ShareHits[1].Info.Amount);
            });
            // 偏好开启只放行配置层，不保证无候选或无害事件能生成分担计划。
            ShareCheck("串联：统一偏好仍受无候选与伤害过滤约束",()=>
            {
                var (master,_)=ShareMaster(0);Assert(SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(master));
                Assert(!SSCMasterBondUtility.TryCreateDamageRedirectPlan(master,SharedShot(sharedOrdinary),out _));
                master.TakeDamage(SharedShot(sharedOrdinary));Equal(1,ShareHits.Count);Near(5,ShareHits[0].Info.Amount);
                var harmless=new DamageDef{defName="SSC_ShareHarmless",harmsHealth=false};
                Assert(!SSCMasterBondUtility.CanRedirectDamageFrom(master,SharedShot(harmless)));
            });
            ShareCheck("串联：涂覆命中主人保留70%物理分摊且双方不投药",()=>
            {
                var (master,slaves)=ShareMaster(1);var shot=SharedShot(sharedCoating);
                if(!SSCMasterBondUtility.CanRedirectDamageFrom(master,shot))throw new Exception($"eligibility: health={shot.Def.harmsHealth} map={master.MapHeld!=null} dead={master.Dead} scope={SSCMasterBondUtility.IsRedirectingDamage} blocked={SSCMasterBondUtility.ShouldBlockDamageDef(shot.Def)} bridle={SSCBondUtility.GetBridle(master)!=null}");
                Assert(SSCMasterBondUtility.TryCreateDamageRedirectPlan(master,shot,out var plan));
                Equal(1,plan.Candidates.Count);SSCMasterBondUtility.CreateRedirectedDamageInfo(shot,slaves[0],3.5f,out _);
                master.TakeDamage(shot);
                Equal(2,ShareHits.Count);Assert(ShareHits[0].Target==slaves[0]&&ShareHits[1].Target==master);
                Near(3.5f,ShareHits[0].Info.Amount);Near(1.5f,ShareHits[1].Info.Amount);
                Assert(ShareHits.All(h=>h.Info.Def==shot.Def));Near(.3f,ShareHits[0].Info.ArmorPenetrationInt);
                Equal(0,Doses.Count);Assert(!SSCMasterBondUtility.IsRedirectingDamage);
            });
            ShareCheck("串联：多性奴共享物理伤害而不各自复制PNA",()=>
            {
                var (master,slaves)=ShareMaster(2);master.TakeDamage(SharedShot(sharedCoating));
                Equal(3,ShareHits.Count);Near(1.75f,ShareHits[0].Info.Amount);Near(1.75f,ShareHits[1].Info.Amount);
                Equal(0,Doses.Count);
            });
            ShareCheck("串联：直接命中有效链性奴仍正常投药",()=>
            {var p=Pawn();Rooted.Add(p);p.TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());});
            ShareCheck("串联：同目标同定义嵌套真实命中不继承分摊标记",()=>
            {
                var (master,slaves)=ShareMaster(1);bool nested=false;
                onSharePhysical=(p,i)=>{if(p==slaves[0]&&!nested){nested=true;p.TakeDamage(i);Assert(SSCPnaDamageShareUtility.IsRedirectedHit(p));}};
                master.TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());Equal(3,ShareHits.Count);
            });
            ShareCheck("串联：另一目标的嵌套真实命中仍正常投药",()=>
            {
                var (master,slaves)=ShareMaster(1);var other=Pawn();bool nested=false;
                onSharePhysical=(p,i)=>{if(p==slaves[0]&&!nested){nested=true;other.TakeDamage(SharedShot(sharedCoating));}};
                master.TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());
            });
            ShareCheck("串联：嵌套转移伤害不投药且正确恢复外层帧",()=>
            {
                var p=Pawn();var other=Pawn();bool nested=false;
                onSharePhysical=(t,i)=>{if(!nested){nested=true;SSCPnaDamageShareUtility.ApplyRedirected(other,i);Assert(SSCPnaDamageShareUtility.IsRedirectedHit(t));}};
                SSCPnaDamageShareUtility.ApplyRedirected(p,SharedShot(sharedCoating));Equal(0,Doses.Count);Equal(2,ShareHits.Count);
                Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(p));
            });
            ShareCheck("串联：异常不吞掉且分摊／上下文完整释放",()=>
            {
                var (master,slaves)=ShareMaster(1);onSharePhysical=(p,i)=>throw new InvalidOperationException("share-test");
                try{master.TakeDamage(SharedShot(sharedCoating));throw new Exception("应抛原异常");}catch(InvalidOperationException){}
                Assert(!SSCMasterBondUtility.IsRedirectingDamage&&!SSCPnaDamageShareUtility.IsRedirectedHit(slaves[0]));
                onSharePhysical=null;slaves[0].TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());
            });
            ShareCheck("串联：上游吸收结束后不遗留分摊标记",()=>
            {
                var p=Pawn();shield=true;SSCPnaDamageShareUtility.ApplyRedirected(p,SharedShot(sharedCoating));
                Equal(0,ShareHits.Count);Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(p));
                shield=false;p.TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());
            });
            ShareCheck("串联：其他Prefix跳过TakeDamage也释放帧",()=>
            {
                var p=Pawn();skipShareHit=true;SSCPnaDamageShareUtility.ApplyRedirected(p,SharedShot(sharedCoating));
                Equal(0,ShareHits.Count);Assert(!SSCPnaDamageShareUtility.IsRedirectedHit(p));
                skipShareHit=false;p.TakeDamage(SharedShot(sharedCoating));Near(.15f,Doses.Single());
            });
            ShareCheck("串联：普通枪托类物理攻击继续分摊",()=>
            {var (master,slaves)=ShareMaster(1);master.TakeDamage(SharedShot(sharedOrdinary));Equal(2,ShareHits.Count);Near(3.5f,ShareHits[0].Info.Amount);Equal(0,Doses.Count);});
            foreach(float load in new[]{.20f,.10f,.25f})
                ShareCheck("串联：太空主攻击主人不生成性奴受击 "+load,()=>
                {
                    var (master,slaves)=ShareMaster(1);var damage=new DamageDef{defName="SSC_SpaceShare",harmsHealth=true,workerClass=typeof(DamageWorker_SSCPnaSpacer),armorCategory=DamageArmorCategoryDefOf.Sharp,
                        modExtensions=new List<DefModExtension>{new SSCPnaSpacerExtension{baseLoad=load,recordHediff=new HediffDef{defName="SpaceSnapshot"}}}};
                    Assert(!SSCMasterBondUtility.CanRedirectDamageFrom(master,SharedShot(damage)));
                    master.TakeDamage(SharedShot(damage));Equal(0,ShareHits.Count);Equal(0,Doses.Count);Equal(0,injuryCalls);Equal(0,armorCalls);
                });
            ShareCheck("串联：太空直接有效链投送仍运作",()=>
            {var p=Pawn(3);Rooted.Add(p);p.TakeDamage(Info(p,.6f));Near(.2f,Doses.Single());Equal(0,armorCalls);});
            ShareCheck("普通伤害空闲路径不分配上下文",()=>
            {var p=Pawn();Assert(SSCPnaDamageShareUtility.EnterTakeDamage(p,SharedShot(sharedOrdinary))==null);});
        }
        finally{nativeShare=false;skipShareHit=false;onSharePhysical=null;ShareBridles.Clear();ShareHits.Clear();}
    }
    private static void ShareCheck(string name,Action action)=>Check(name,()=>
    {nativeShare=true;skipShareHit=false;onSharePhysical=null;ShareBridles.Clear();ShareHits.Clear();action();});
    private static (Pawn Master,Pawn[] Slaves) ShareMaster(int count)
    {
        var master=Pawn();Masters.Add(master);var slaves=Enumerable.Range(0,count).Select(_=>Pawn()).ToArray();
        foreach(var p in slaves)Rooted.Add(p);
        ShareBridles[master]=new Hediff_BridleOfSexSlave{shareDamageEnabled=true,slaves=slaves.ToList()};return(master,slaves);
    }
    private static DamageInfo SharedShot(DamageDef damage)=>new(damage,5,.3f);
    private static bool ShareMap(Thing __instance,ref Map __result)
    {if(!nativeShare||__instance is not Pawn p||!Valid.Contains(p))return true;__result=shareMap;return false;}
    private static bool ShareLabel(ref string __result){if(!nativeShare)return true;__result="test pawn";return false;}
    private static bool ShareCandidate(Pawn master,Pawn slave,ref bool __result)
    {if(!nativeShare)return true;__result=Valid.Contains(slave);return false;}
    private static bool SharePhysical(DamageWorker_AddInjury __instance,DamageInfo dinfo,Thing thing,ref DamageWorker.DamageResult __result)
    {
        if(!nativeShare||(__instance is not DamageWorker_SSCPnaCoated&&dinfo.Def!=sharedOrdinary))return true;
        var pawn=(Pawn)thing;ShareHits.Add((pawn,dinfo));var part=dinfo.HitPart??pawn.RaceProps.body.AllParts[0];
        var injury=new Hediff_Injury{pawn=pawn,Part=part,def=dinfo.Def.hediff};Set(injury,"severityInt",dinfo.Amount);pawn.health.hediffSet.hediffs.Add(injury);
        __result=new DamageWorker.DamageResult{wounded=true,totalDamageDealt=dinfo.Amount,hediffs=new List<Hediff>{injury}};
        onSharePhysical?.Invoke(pawn,dinfo);return false;
    }
    private static bool SharePostDamage()=>!nativeShare;
    private static bool ShareLog()=>!nativeShare;
    private static bool ShareTranslation(string __0,ref TaggedString __result)
    {if(!nativeShare)return true;__result=__0;return false;}
    private static bool ShareTake(ref DamageWorker.DamageResult __result)
    {if(!nativeShare||!skipShareHit)return true;__result=new();return false;}
}
namespace SexSlaveCraft
{
    public class Hediff_BridleOfSexSlave:Hediff
    {
        public bool shareDamageEnabled;
        public List<Pawn> slaves;
        // 共振检查仅在这个环境边界提供可计数的枚举和独立的归属结果。
        public IEnumerable<Pawn> targetsOverride;
        public Func<Pawn, bool> boundOverride;
        public IEnumerable<Pawn> ValidTargets=>targetsOverride ?? slaves;
    }
    public class Hediff_GelatinizationTemporary:Hediff { }
    public class Need_Corruption:Need { public Need_Corruption(Pawn pawn):base(pawn){} public override void NeedInterval(){} }
    public static class SSCDefOf { public static HediffDef SSC_MasterBondEmpowerment; }
    public static class SSCBondUtility
    {
        public static Hediff_BridleOfSexSlave GetBridle(Pawn pawn)=>Program.ShareBridles.GetValueOrDefault(pawn);
        public static bool IsBoundTo(Pawn slave,Pawn master)
        {
            var bridle=GetBridle(master);
            return bridle?.boundOverride?.Invoke(slave) ?? (bridle?.slaves.Contains(slave)==true);
        }
    }
}
