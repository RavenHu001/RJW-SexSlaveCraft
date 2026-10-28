// AI 辅助生成：真实 1.7 护甲随机、伤害类型与工作端选择；不启动地图或完整 Def 加载。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using RimWorld;
using Verse;
using SexSlaveCraft;

internal static class Entry
{
    private static int Main(string[] args)
    {
        if(args.Length!=2)throw new ArgumentException("repositoryRoot gameRoot");
        string game=Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving+=(context,name)=>{string path=Path.Combine(game,name.Name+".dll");return File.Exists(path)?context.LoadFromAssemblyPath(path):null;};
        // Program 的静态夹具含游戏类型；用反射延迟解析，确保先安装本机 DLL 解析器。
        return (int)typeof(Entry).Assembly.GetType("Program").GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{game});
    }
}

internal static partial class Program
{
    internal static readonly HashSet<Pawn> Valid=new(),Rooted=new(),Masters=new();
    internal static readonly List<float> Doses=new();
    private static readonly Dictionary<Thing,float> Ratings=new();
    private static readonly Dictionary<Pawn,List<Apparel>> Clothes=new();
    private static int passed,failed,armorCalls,wearCalls,injuryCalls,takeCalls;
    private static float lastInjury;
    private static DamageDef lastType;
    private static bool interceptPawnHit,shield;
    private static DamageInfo lastShot;
    private static DamageDef nano,blunt;
    private static Verb fallback;
    private static int fallbackCalls;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int Run(string game)
    {
        Console.WriteLine($"Game: {File.ReadAllText(Path.Combine(game,"Version.txt")).Trim()}, MVID: {typeof(Pawn).Module.ModuleVersionId:N}");
        // 只安装本宿主需要的生产补丁及游戏边界；finally 全部卸载，避免把其他专项当成覆盖。
        var harmony=new Harmony("ssc.tests.pna.spacer.native");
        void Patch(MethodBase method,string prefix=null,string postfix=null)=>harmony.Patch(method,
            prefix==null?null:new HarmonyMethod(typeof(Program),prefix),postfix==null?null:new HarmonyMethod(typeof(Program),postfix));
        // 用内存 Def 提供护甲类别和剂量元数据，不启动完整加载器，也不写正式武器 XML。
        StatDefOf.IncomingDamageFactor=new StatDef{defName="IncomingDamageFactor"};
        var rating=new StatDef{defName="ArmorRating_Sharp"};
        DamageArmorCategoryDefOf.Sharp=new DamageArmorCategoryDef{defName="Sharp",armorRatingStat=rating};
        blunt=new DamageDef{defName="Blunt",armorCategory=new DamageArmorCategoryDef{defName="Blunt",armorRatingStat=rating}};
        DamageDefOf.Blunt=blunt;
        nano=new DamageDef{defName="SSC_Test_Nano",workerClass=typeof(DamageWorker_SSCPnaSpacer),armorCategory=DamageArmorCategoryDefOf.Sharp,
            modExtensions=new List<DefModExtension>{new SSCPnaSpacerExtension{baseLoad=.2f,recordHediff=new HediffDef{defName="Record"}}}};
        // 护甲函数只计调用次数，函数本体照常执行；属性缓存、地图健康写入及衣甲
        // TakeDamage 单独隔离。因此可以验证概率／取整／磨损调用次数，不能声称已跑整张地图。
        Patch(AccessTools.Method(typeof(ArmorUtility),nameof(ArmorUtility.GetPostArmorDamage)),nameof(CountArmor));
        Patch(AccessTools.Method(typeof(StatExtension),nameof(StatExtension.GetStatValue),new[]{typeof(Thing),typeof(StatDef),typeof(bool),typeof(int)}),nameof(Stat));
        // 扩展方法可能已内联到护甲方法中，同时隔离实际 StatWorker 缓存对地图 TickManager 的依赖。
        Patch(AccessTools.Method(typeof(StatWorker),nameof(StatWorker.GetValue),new[]{typeof(Thing),typeof(bool),typeof(int)}),nameof(NativeStat));
        Patch(AccessTools.Method(typeof(PawnCapacitiesHandler),nameof(PawnCapacitiesHandler.GetLevel)),nameof(Capacity));
        Patch(AccessTools.Method(typeof(DamageDef),nameof(DamageDef.ExternalViolenceFor)),nameof(External));
        Patch(AccessTools.Method(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new[]{typeof(Pawn),typeof(float),typeof(DamageInfo),typeof(DamageWorker.DamageResult)}),nameof(Injury));
        Patch(AccessTools.Method(typeof(Thing),nameof(Thing.TakeDamage)),nameof(Take));
        Patch(AccessTools.Method(typeof(Pawn),nameof(Verse.Pawn.PreApplyDamage)),nameof(PreDamage));
        Patch(AccessTools.PropertyGetter(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.WornApparel)),nameof(Worn));
        Patch(AccessTools.Method(typeof(Verb),nameof(Verb.IsStillUsableBy)),nameof(Usable));
        harmony.Patch(AccessTools.Method(typeof(Pawn_MeleeVerbs),nameof(Pawn_MeleeVerbs.TryGetMeleeVerb)),
            prefix:new HarmonyMethod(typeof(Program),nameof(Fallback)){priority=Priority.Last});
        harmony.CreateClassProcessor(typeof(Harmony_SSCPnaSpacerMeleeSelection)).Patch();
        harmony.CreateClassProcessor(typeof(Harmony_SSCPnaSpacerExplicitMelee)).Patch();
        try
        {
            RunDamageShareChecks(harmony);
            RunWhipCooperationChecks();
            RunEmpowermentChecks();
            // 实际游戏护甲算法与生产处理器组合验证；固定种子使随机分支可重复。
            Check("无甲目标判甲一次并按基线投送",()=>{var p=Pawn();Hit(p,.7f);Near(1,lastInjury);Equal(1,injuryCalls);Equal(1,armorCalls);Near(.2f,Doses.Single());});
            Check("有效链满甲不进入护甲或衣甲磨损",()=>{var p=Pawn(3);Rooted.Add(p);Wear(p,3);Hit(p,.6f);Equal(0,armorCalls);Equal(0,wearCalls);Equal(0,injuryCalls);Near(.2f,Doses.Single());});
            Check("主人优先免疫且不进入护甲",()=>{var p=Pawn(3);Rooted.Add(p);Masters.Add(p);Wear(p,3);Hit(p,.6f);Equal(0,armorCalls);Equal(0,wearCalls);Equal(0,Doses.Count);});
            // 同一护甲算法只保留必挡／必穿两个数学边界，不按普通穿甲数值重复。
            Check("真实护甲高于穿甲2必挡 "+.7f,()=>{var p=Pawn(2.7f);var r=Hit(p,.7f);Assert(r.deflected);Equal(0,Doses.Count);Equal(0,injuryCalls);});
            Check("真实护甲不高于穿甲必穿 "+.7f,()=>{var p=Pawn(.7f);Hit(p,.7f);Near(.2f,Doses.Single());Near(1,lastInjury);});
            Check("真实减伤随机能产生0或1钝伤且均不投药",()=>
            {var p=Pawn(1.6f);var seen=new HashSet<int>();int pass=0,block=0;
                Rand.PushState(8123);try{for(int i=0;i<2000;i++){Doses.Clear();injuryCalls=0;lastType=null;var r=Hit(p,.6f);
                    if(lastType==blunt){seen.Add((int)lastInjury);Equal(0,Doses.Count);}else if(Doses.Count>0)pass++;else block++;
                    // 转钝取整为0不调用最终写入，但仍必须拒绝药效。
                    if(r.deflected&&Doses.Count==0)seen.Add(0);}}
                finally{Rand.PopState();}Assert(seen.Contains(0)&&seen.Contains(1)&&pass==0&&block>0);});
            Check("衣甲实际只磨损一遍，后续天然甲各自判定",()=>{var p=Pawn();Wear(p,.2f);Hit(p,.9f);Equal(1,armorCalls);Equal(1,wearCalls);Near(.2f,Doses.Single());});
            Check("人工父部位的子部位不得吸收",()=>{var p=Pawn();Rooted.Add(p);var part=p.RaceProps.body.AllParts[0];var parent=new BodyPartRecord{def=new BodyPartDef()};part.parent=parent;p.RaceProps.body.AllParts.Add(parent);
                p.health.hediffSet.hediffs.Add(new Hediff_AddedPart{pawn=p,Part=parent,def=new HediffDef{addedPartProps=new AddedBodyPartProps{solid=true}}});Hit(p,.7f);Equal(0,Doses.Count);});
            Check("原版自然愈合准入不能排除人工伤势",()=>{var p=Pawn();var part=p.RaceProps.body.AllParts[0];p.health.hediffSet.hediffs.Add(new Hediff_AddedPart{pawn=p,Part=part,def=new HediffDef{addedPartProps=new AddedBodyPartProps{solid=true}}});
                var injury=new Hediff_Injury{pawn=p,Part=part,def=new HediffDef()};Assert(injury.CanHealNaturally());Assert(!SSCPnaSpacerBodyUtility.IsOrganic(p,part));});
            Check("单次纳米命中忽略传播请求而不重复判甲",()=>{var p=Pawn();var info=Info(p,.6f);info.SetApplyAllDamage(true);info.SetAllowDamagePropagation(true,new IntRange(3,3));nano.Worker.Apply(info,p);Equal(1,armorCalls);Equal(1,injuryCalls);Equal(1,Doses.Count);});
            Check("上游吸收回调在真实TakeDamage进入Worker前结束",()=>{var p=Pawn();shield=true;var r=p.TakeDamage(Info(p,.6f));Equal(0,armorCalls);Equal(0,Doses.Count);Assert(!r.wounded);});

            // 正常选择调用真实 Harmony 入口；显式近战另验证参数替换和非近战放行。
            Check("配置正确的丝鞭优先纳米端而非原版缓存拳击",()=>{var p=Pawn();var v=Equip(p);fallback=new Verb_Shoot();var chosen=new Pawn_MeleeVerbs(p).TryGetMeleeVerb(Pawn());Assert(chosen==v);});
            Check("普通武器原版动作选择放行",()=>{var p=Pawn();fallback=new Verb_Shoot();Assert(new Pawn_MeleeVerbs(p).TryGetMeleeVerb(Pawn())==fallback);Equal(1,fallbackCalls);});
            Check("建筑目标不强制替换近战动作",()=>{var p=Pawn();Equip(p);fallback=new Verb_Shoot();Assert(new Pawn_MeleeVerbs(p).TryGetMeleeVerb(new Thing())==fallback);});
            Check("不可用工作端回退原版选择",()=>{var p=Pawn();Equip(p).Enabled=false;fallback=new Verb_Shoot();Assert(new Pawn_MeleeVerbs(p).TryGetMeleeVerb(Pawn())==fallback);});
            Check("任务传入普通近战动作也优先纳米工作端",()=>{var p=Pawn();var v=Equip(p);Verb explicitVerb=new Verb_MeleeAttackDamage{verbProps=new VerbProperties{verbClass=typeof(Verb_MeleeAttackDamage)}};Harmony_SSCPnaSpacerExplicitMelee.Prefix(new Pawn_MeleeVerbs(p),Pawn(),ref explicitVerb);Assert(explicitVerb==v);});
            Check("任务传入非近战动作不强制转换",()=>{var p=Pawn();Equip(p);Verb explicitVerb=new Verb_Shoot{verbProps=new VerbProperties{verbClass=typeof(Verb_Shoot)}};var original=explicitVerb;Harmony_SSCPnaSpacerExplicitMelee.Prefix(new Pawn_MeleeVerbs(p),Pawn(),ref explicitVerb);Assert(explicitVerb==original);});
            Check("错误普通伤害配置不认作工作端",()=>{var p=Pawn();var v=Equip(p);v.verbProps.meleeDamageDef=blunt;Assert(!SSCPnaSpacerMeleeUtility.IsWorkingEnd(v));});
            Check("真实近战入口低于1仍保持纳米类型及一次TakeDamage",()=>{var p=Pawn();var v=Equip(p);interceptPawnHit=true;var target=Pawn();var values=new List<float>();Rand.PushState(719);
                try{for(int i=0;i<150;i++){v.Hit(target);Assert(lastShot.Def==nano&&!lastShot.IgnoreArmor);values.Add(lastShot.Amount);}}finally{Rand.PopState();}Assert(values.Min()<1&&values.Max()>1);Equal(150,takeCalls);});
            Check("效力信息卡识别真实投射物并拒绝不同剂量混用",()=>{var weapon=Gun(nano);Assert(SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon,out var value));Near(.2f,value);
                var other=new DamageDef{workerClass=typeof(DamageWorker_SSCPnaSpacer),modExtensions=new List<DefModExtension>{new SSCPnaSpacerExtension{baseLoad=.1f,recordHediff=new HediffDef()}}};
                Set(weapon,"verbs",new List<VerbProperties>{new(){defaultProjectile=new ThingDef{projectile=new ProjectileProperties{damageDef=nano}}},new(){defaultProjectile=new ThingDef{projectile=new ProjectileProperties{damageDef=other}}}});
                Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(weapon,out _));});
            Check("丝鞭无需品质标记即可识别，远程装备排除",()=>{var p=Pawn();var v=Equip(p);Assert(v.EquipmentSource.def.modExtensions == null);
                Assert(SSCPnaSpacerMeleeUtility.Preferred(p,Pawn())==v);Set(v.EquipmentSource.def,"verbs",new List<VerbProperties>{new(){verbClass=typeof(Verb_Shoot)}});
                Assert(!SSCPnaSpacerMeleeUtility.IsWorkingEnd(v));Assert(SSCPnaSpacerMeleeUtility.Preferred(p,Pawn())==null);});
        }
        finally{harmony.UnpatchAll(harmony.Id);}
        Console.WriteLine($"结果：{passed}/{passed+failed} 项通过。");return failed==0?0:1;
    }
    // 构造最小真实健康／身体实例。未初始化的地图调度依赖由上面的边界隔离，
    // 所以该宿主是原生 API 检查，不是完整 Def 加载、存档或游戏 AI 验收。
    private static Pawn Pawn(float armor=0)
    {
        var p=new Pawn();
        var part=new BodyPartRecord{def=new BodyPartDef{tags=new List<BodyPartTagDef>()},groups=new List<BodyPartGroupDef>()};var body=new BodyDef();Set(body,"cachedAllParts",new List<BodyPartRecord>{part});body.corePart=part;
        p.def=new ThingDef{race=new RaceProperties{body=body}};Set(p.def.race,"fleshType",new FleshTypeDef());
        p.health=(Pawn_HealthTracker)RuntimeHelpers.GetUninitializedObject(typeof(Pawn_HealthTracker));
        Set(p.health,"healthState",PawnHealthState.Mobile);Set(p.health,"pawn",p);
        p.health.hediffSet=new HediffSet(p);p.health.capacities=(PawnCapacitiesHandler)RuntimeHelpers.GetUninitializedObject(typeof(PawnCapacitiesHandler));
        Valid.Add(p);Ratings[p]=armor;return p;
    }
    private static void Wear(Pawn p,float armor)
    {
        var group=new BodyPartGroupDef();p.RaceProps.body.AllParts[0].groups.Add(group);
        var item=new Apparel{def=new ThingDef{apparel=new ApparelProperties{bodyPartGroups=new List<BodyPartGroupDef>{group}}}};
        p.apparel=(Pawn_ApparelTracker)RuntimeHelpers.GetUninitializedObject(typeof(Pawn_ApparelTracker));Set(p.apparel,"pawn",p);Clothes[p]=new List<Apparel>{item};Ratings[item]=armor;
        var container=new ThingOwner<Apparel>(p.apparel);Set(container,"innerList",Clothes[p]);Set(p.apparel,"wornApparel",container);
    }
    private static DamageInfo Info(Pawn p,float ap)=>new(nano,1,ap,hitPart:p.RaceProps.body.AllParts[0]);
    private static DamageWorker.DamageResult Hit(Pawn p,float ap)=>nano.Worker.Apply(Info(p,ap),p);
    private static ThingDef Gun(DamageDef damage){var def=new ThingDef{category=ThingCategory.Item,equipmentType=EquipmentType.Primary};Set(def,"verbs",new List<VerbProperties>{new(){verbClass=typeof(Verb_Shoot),defaultProjectile=new ThingDef{projectile=new ProjectileProperties{damageDef=damage}}}});return def;}
    // 内存中建立真实装备、CompEquippable 和 VerbTracker 关联，保留生产代码的归属查询。
    private static TestVerb Equip(Pawn pawn)
    {
        var weapon=new ThingWithComps{def=new ThingDef{category=ThingCategory.Item,equipmentType=EquipmentType.Primary,tools=new List<Tool>{new(){power=1}}}};
        var comp=new CompEquippable{parent=weapon};Set(weapon,"comps",new List<ThingComp>{comp});
        var tracker=new VerbTracker(comp);Set(comp,"verbTracker",tracker);
        var verb=new TestVerb{caster=pawn,verbTracker=tracker,verbProps=new VerbProperties{meleeDamageDef=nano,meleeDamageBaseAmount=1},tool=new Tool{power=1,armorPenetration=.6f,cooldownTime=1.2f}};
        Set(tracker,"verbs",new List<Verb>{verb});pawn.equipment=new Pawn_EquipmentTracker(pawn);Set(pawn.equipment.GetDirectlyHeldThings(),"innerList",new List<ThingWithComps>{weapon});
        return verb;
    }
    private sealed class TestVerb:Verb_MeleeAttackSSCPnaSpacer { public bool Enabled=true;public DamageWorker.DamageResult Hit(Thing target)=>ApplyMeleeDamageToTarget(target);public override bool IsUsableOn(Thing target)=>Enabled; }
    private static void Set(object obj,string name,object value)=>AccessTools.Field(obj.GetType(),name).SetValue(obj,value);
    private static void Assert(bool condition){if(!condition)throw new Exception("Assertion failed");}
    private static void Equal(int a,int b){if(a!=b)throw new Exception($"Expected {a}, actual {b}");}
    private static void Near(float a,float b){if(Math.Abs(a-b)>.00001f)throw new Exception($"Expected {a}, actual {b}");}
    private static void Check(string name,Action action)
    {
        if (!SscTestProfile.ShouldRun(name)) return;Valid.Clear();Rooted.Clear();Masters.Clear();Ratings.Clear();Clothes.Clear();Doses.Clear();armorCalls=wearCalls=injuryCalls=takeCalls=fallbackCalls=0;lastInjury=0;lastType=null;interceptPawnHit=shield=false;try{action();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
    // 以下钩子只服务测试：捕获实际伤害元数据或提供外部上下文，不替代被测护甲算法。
    private static void CountArmor()=>armorCalls++;
    private static bool Stat(Thing thing,StatDef stat,ref float __result){__result=stat==StatDefOf.IncomingDamageFactor||stat==StatDefOf.MeleeWeapon_DamageMultiplier?1:Ratings.GetValueOrDefault(thing,1);return false;}
    private static bool NativeStat(Thing thing,StatDef ___stat,ref float __result)=>Stat(thing,___stat,ref __result);
    private static bool Capacity(ref float __result){__result=1;return false;}
    private static bool External(ref bool __result){__result=!nativeShare;return false;}
    private static bool Injury(Pawn pawn,float totalDamage,DamageInfo dinfo,DamageWorker.DamageResult result,ref float __result)
    {injuryCalls++;lastInjury=totalDamage;lastType=dinfo.Def;result.wounded=true;result.totalDamageDealt=totalDamage;__result=totalDamage;return false;}
    private static bool Take(Thing __instance,DamageInfo dinfo,ref DamageWorker.DamageResult __result)
    {if(__instance is Apparel){wearCalls++;__result=new();return false;}if(interceptPawnHit&&__instance is Pawn){takeCalls++;lastShot=dinfo;__result=new();return false;}return true;}
    private static bool Worn(Pawn_ApparelTracker __instance,ref List<Apparel> __result){var p=(Pawn)AccessTools.Field(typeof(Pawn_ApparelTracker),"pawn").GetValue(__instance);__result=Clothes[p];return false;}
    private static bool PreDamage(out bool absorbed){absorbed=shield;return !shield&&!nativeShare;}
    private static bool Usable(Verb __instance,ref bool __result){__result=__instance is TestVerb v&&v.Enabled;return false;}
    private static bool Fallback(ref Verb __result){fallbackCalls++;__result=fallback;return false;}
}
