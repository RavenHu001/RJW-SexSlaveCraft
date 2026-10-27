// AI 辅助生成：实际 1.7 近战 IL、命中／闪避分支及两代鞭束的配合受药规则回归。
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using SexSlaveCraft;

internal static partial class Program
{
    private sealed class CooperativeFixture
    {
        internal Pawn Attacker, Target, OldThreat;
        internal Verb_MeleeAttack Verb;
        internal ThingWithComps Weapon;
        internal int SpawnReads, DamageCalls, InitialThreatTick;
        internal bool TargetHostile, AttackerHostile;
        internal Pawn ThreatDuringDamage;
        internal int TickDuringDamage;
    }
    private static CooperativeFixture cooperating;
    private static readonly MethodInfo NativeCast = AccessTools.Method(typeof(Verb_MeleeAttack), "TryCastShot");
    private static readonly MethodInfo NativeDodge = AccessTools.Method(typeof(Verb_MeleeAttack), "GetDodgeChance");
    private static readonly MethodInfo NativeHit = AccessTools.Method(typeof(Verb_MeleeAttack), "GetNonMissChance");
    private static readonly TickManager CooperationClock = (TickManager)RuntimeHelpers.GetUninitializedObject(typeof(TickManager));

    private static void RunWhipCooperationChecks()
    {
        var hooks = new Harmony("ssc.tests.pna.whip-cooperation.native");
        StatDef previousHit=StatDefOf.MeleeHitChance, previousDodge=StatDefOf.MeleeDodgeChance;
        MentalStateDef previousSocialFight=MentalStateDefOf.SocialFighting;
        void Boundary(MethodBase method, string prefix) => hooks.Patch(method, prefix: new HarmonyMethod(typeof(Program), prefix));
        try
        {
            // SSC 关系与地图写入沿用宿主显式边界。保留实际 TryCastShot、GetNonMissChance、
            // GetDodgeChance、Rand.Chance；图形、声效、日志和假地图位置不属于本专项。
            StatDefOf.MeleeHitChance=new StatDef{defName="MeleeHitChance"};
            StatDefOf.MeleeDodgeChance=new StatDef{defName="MeleeDodgeChance"};
            // 禁用本专项无关的 Ideology 光照修正，避免独立宿主初始化玩家配置目录。
            // 仍执行原生命中／闪避属性读取与 Rand 分支，不修改两种概率的生产算法。
            hooks.Patch(NativeHit, transpiler:new HarmonyMethod(typeof(Program),nameof(CooperationNoIdeology)));
            hooks.Patch(NativeDodge, transpiler:new HarmonyMethod(typeof(Program),nameof(CooperationNoIdeology)));
            Boundary(AccessTools.Method(typeof(GenHostility), nameof(GenHostility.HostileTo), new[]{typeof(Thing),typeof(Thing)}), nameof(CooperationHostility));
            Boundary(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned)), nameof(CooperationSpawned));
            Boundary(AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.DrawPos)), nameof(CooperationDrawPos));
            Boundary(AccessTools.PropertyGetter(typeof(Find), nameof(Find.TickManager)), nameof(CooperationTickManager));
            Boundary(AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TicksGame)), nameof(CooperationTick));
            Boundary(AccessTools.PropertyGetter(typeof(Pawn_StanceTracker), nameof(Pawn_StanceTracker.FullBodyBusy)), nameof(CooperationImmobile));
            Boundary(AccessTools.Method(typeof(Pawn_RotationTracker), nameof(Pawn_RotationTracker.Face), new[]{typeof(Vector3)}), nameof(CooperationSideEffect));
            Boundary(AccessTools.Method(typeof(Verb), nameof(Verb.CanHitTarget)), nameof(CooperationCanHit));
            Boundary(AccessTools.Method(typeof(Verb_MeleeAttack), "IsTargetImmobile"), nameof(CooperationImmobile));
            foreach(string name in new[]{"SoundHitPawn","SoundMiss","SoundDodge"})
                Boundary(AccessTools.Method(typeof(Verb_MeleeAttack), name), nameof(CooperationSound));
            Boundary(AccessTools.Method(typeof(Verb_MeleeAttack), "CreateCombatLog"), nameof(CooperationLog));
            Boundary(AccessTools.Method(typeof(DamageWorker.DamageResult), nameof(DamageWorker.DamageResult.AssociateWithLog)), nameof(CooperationSideEffect));
            Boundary(AccessTools.Method(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget"), nameof(CooperationDamage));
            Boundary(AccessTools.Method(typeof(Verb_MeleeAttackSSCPnaSpacer), "ApplyMeleeDamageToTarget"), nameof(CooperationDamage));
            hooks.CreateClassProcessor(typeof(Harmony_SSCPnaWhipCooperativeThreat)).Patch();
            hooks.CreateClassProcessor(typeof(Harmony_SSCPnaWhipCooperativeDodge)).Patch();

            foreach(bool silk in new[]{false,true})
            {
                Check("真实近战：有效链鞭束免闪避且命中回调前不登记威胁 "+silk,()=>
                {
                    var f=Cooperation(silk); cooperating=f;
                    Near(0,Dodge(f));Near(1,HitChance(f));
                    Assert(Cast(f));Equal(1,f.DamageCalls);
                    Assert(f.ThreatDuringDamage==null&&f.Target.mindState.meleeThreat==null);
                    Equal(f.InitialThreatTick,f.TickDuringDamage);Equal(f.InitialThreatTick,f.Target.mindState.lastMeleeThreatHarmTick);
                    // 原版回击任务在无 meleeThreat 时直接返回；实际执行这一原版入口。
                    var giver=new JobGiver_ReactToCloseMeleeThreat();
                    Assert(AccessTools.Method(typeof(JobGiver_ReactToCloseMeleeThreat),"TryGiveJob").Invoke(giver,new object[]{f.Target})==null);
                });
                Check("真实近战：有效链仍保留攻击者失手 "+silk,()=>
                {
                    var f=Cooperation(silk);cooperating=f;Ratings[f.Attacker]=0;
                    Near(0,HitChance(f));Assert(!Cast(f));Equal(0,f.DamageCalls);
                    Assert(f.Target.mindState.meleeThreat==null);Equal(f.InitialThreatTick,f.Target.mindState.lastMeleeThreatHarmTick);
                });
                Check("真实近战：本次不覆盖其他攻击留下的威胁及时间 "+silk,()=>
                {
                    var f=Cooperation(silk);cooperating=f;f.Target.mindState.meleeThreat=f.OldThreat;
                    f.Target.mindState.lastMeleeThreatHarmTick=81;Assert(Cast(f));
                    Assert(f.ThreatDuringDamage==f.OldThreat&&f.Target.mindState.meleeThreat==f.OldThreat);
                    Equal(81,f.TickDuringDamage);Equal(81,f.Target.mindState.lastMeleeThreatHarmTick);
                });
                foreach(string gate in silk
                    ? new[]{"handle","capacity","damage","worker","dose","record"}
                    : new[]{"unlinked","invalid-target","target-hostile","attacker-hostile","unequipped","spear","handle","capacity","damage","worker","dose","record","saving"})
                    Check("鞭束合作准入即时失效且原版闪避保留 "+silk+"/"+gate,()=>
                    {
                        var f=Cooperation(silk);cooperating=f;
                        switch(gate)
                        {
                            case "unlinked":Rooted.Remove(f.Target);break;
                            case "invalid-target":Valid.Remove(f.Target);break;
                            case "target-hostile":f.TargetHostile=true;break;
                            case "attacker-hostile":f.AttackerHostile=true;break;
                            case "unequipped":Set(f.Attacker.equipment.GetDirectlyHeldThings(),"innerList",new List<ThingWithComps>());break;
                            case "spear":f.Weapon.def.defName="SSC_Weapon_PnaSpikeStaff";break;
                            case "handle":OrdinaryHandleAttack(f);break;
                            case "capacity":f.Verb.tool.capacities[0].defName="Blunt";break;
                            case "damage":f.Verb.verbProps.meleeDamageDef.defName="Blunt";break;
                            case "worker":f.Verb.verbProps.meleeDamageDef.workerClass=typeof(DamageWorker_AddInjury);break;
                            case "dose":if(silk)f.Verb.verbProps.meleeDamageDef.GetModExtension<SSCPnaSpacerExtension>().baseLoad=0;
                                else f.Verb.verbProps.meleeDamageDef.GetModExtension<SSCPnaCoatingExtension>().baseLoad=0;break;
                            case "record":if(silk)f.Verb.verbProps.meleeDamageDef.GetModExtension<SSCPnaSpacerExtension>().recordHediff=null;
                                else f.Verb.verbProps.meleeDamageDef.GetModExtension<SSCPnaCoatingExtension>().recordHediff=null;break;
                            case "saving":Scribe.mode=LoadSaveMode.Saving;break;
                        }
                        try {Assert(!SSCPnaWhipCooperationUtility.Allows(f.Verb,f.Target));Near(1,Dodge(f));}
                        finally {Scribe.mode=LoadSaveMode.Inactive;}
                    });
                foreach(string gate in new[]{"unlinked","hostile","handle","spear"})
                    Check("真实近战：排除对象即使失手仍按原版登记威胁 "+silk+"/"+gate,()=>
                    {
                        var f=Cooperation(silk);cooperating=f;
                        if(gate=="unlinked")Rooted.Remove(f.Target);
                        if(gate=="hostile")f.TargetHostile=true;
                        if(gate=="handle")OrdinaryHandleAttack(f);
                        if(gate=="spear")f.Weapon.def.defName="SSC_Weapon_PnaSpikeStaff";
                        Ratings[f.Attacker]=0;Assert(!Cast(f));Equal(0,f.DamageCalls);
                        Assert(f.Target.mindState.meleeThreat==f.Attacker);Equal(1234,f.Target.mindState.lastMeleeThreatHarmTick);
                    });
                Check("合作状态不缓存：同一动作失去锁链后立即恢复防御 "+silk,()=>
                {
                    var f=Cooperation(silk);cooperating=f;Near(0,Dodge(f));Rooted.Remove(f.Target);Near(1,Dodge(f));
                    Rooted.Add(f.Target);Near(0,Dodge(f));Ratings[f.Attacker]=.37f;Near(.37f,HitChance(f));
                });
                Check("真实近战随机命中仍有命中及失手，且不登记威胁 "+silk,()=>
                {
                    var f=Cooperation(silk);cooperating=f;Ratings[f.Attacker]=.37f;
                    int hits=0;Rand.PushState(84723);
                    try {for(int i=0;i<100;i++)if(Cast(f))hits++;}finally{Rand.PopState();}
                    Assert(hits>0&&hits<100);Equal(hits,f.DamageCalls);
                    Assert(f.Target.mindState.meleeThreat==null);Equal(f.InitialThreatTick,f.Target.mindState.lastMeleeThreatHarmTick);
                });
            }
            Check("原生IL只替换两次威胁写入，命中闪避与伤害调用保持",()=>
            {
                var original=PatchProcessor.GetOriginalInstructions(NativeCast).ToList();
                var before=original.Select(c=>(c.opcode,c.operand)).ToList();
                var after=Harmony_SSCPnaWhipCooperativeThreat.Transpiler(original).ToList();
                Equal(before.Count+2,after.Count);
                foreach(string method in new[]{"GetNonMissChance","GetDodgeChance","ApplyMeleeDamageToTarget"})
                {var info=AccessTools.Method(typeof(Verb_MeleeAttack),method);Equal(1,after.Count(c=>c.Calls(info)));}
                Equal(2,after.Count(c=>c.operand is MethodInfo m&&m.DeclaringType==typeof(Harmony_SSCPnaWhipCooperativeThreat)));
                // 除目标 stfld→ldarg.0/call 外，其余指令按序原样保留。
                int j=0;
                for(int i=0;i<before.Count;i++)
                {
                    var b=before[i];
                    if(b.Item1==OpCodes.Stfld&&b.Item2 is FieldInfo field&&field.DeclaringType==typeof(Pawn_MindState)&&
                        (field.Name=="meleeThreat"||field.Name=="lastMeleeThreatHarmTick"))
                    {Assert(after[j].opcode==OpCodes.Ldarg_0);j+=2;}
                    else {Assert(after[j].opcode==b.Item1&&Equals(after[j].operand,b.Item2));j++;}
                }
                Equal(after.Count,j);
            });
            Check("未知IL布局明确拒绝，避免部分威胁适配",()=>
            {
                bool rejected=false;
                try{Harmony_SSCPnaWhipCooperativeThreat.Transpiler(new[]{new CodeInstruction(OpCodes.Ret)}).ToList();}
                catch(InvalidOperationException){rejected=true;}Assert(rejected);
            });
        }
        finally
        {
            cooperating=null;Scribe.mode=LoadSaveMode.Inactive;hooks.UnpatchAll(hooks.Id);
            StatDefOf.MeleeHitChance=previousHit;StatDefOf.MeleeDodgeChance=previousDodge;
            MentalStateDefOf.SocialFighting=previousSocialFight;
        }
    }

    private static CooperativeFixture Cooperation(bool silk)
    {
        cooperating=null;
        Scribe.mode=LoadSaveMode.Inactive;
        var f=new CooperativeFixture{Attacker=Pawn(),Target=Pawn(),OldThreat=Pawn()};
        cooperating=f;
        f.Attacker.stances=new Pawn_StanceTracker(f.Attacker);f.Target.stances=new Pawn_StanceTracker(f.Target);
        f.Attacker.mindState=new Pawn_MindState(f.Attacker);f.Target.mindState=new Pawn_MindState(f.Target);
        f.InitialThreatTick=f.Target.mindState.lastMeleeThreatHarmTick;
        f.Attacker.rotationTracker=new Pawn_RotationTracker(f.Attacker);
        MentalStateDefOf.SocialFighting=new MentalStateDef{defName="SocialFighting"};
        var workingTool=new Tool{power=silk?1:3,capacities=new List<ToolCapacityDef>{new(){defName=silk?"SSC_ToolCapacity_PnaSilkWhip":"SSC_ToolCapacity_PnaWhip"}}};
        f.Weapon=new ThingWithComps{def=new ThingDef{defName=silk?"SSC_Weapon_PnaSilkWhip":"SSC_Weapon_PnaShortWhip",category=ThingCategory.Item,equipmentType=EquipmentType.Primary,tools=new List<Tool>{workingTool}}};
        var comp=new CompEquippable{parent=f.Weapon};Set(f.Weapon,"comps",new List<ThingComp>{comp});
        var tracker=new VerbTracker(comp);Set(comp,"verbTracker",tracker);
        f.Verb=silk?new Verb_MeleeAttackSSCPnaSpacer():new Verb_MeleeAttackSSCPnaWhip();
        f.Verb.caster=f.Attacker;f.Verb.verbTracker=tracker;f.Verb.tool=workingTool;
        var record=new HediffDef{defName="Record"};
        var damage=new DamageDef{defName=silk?"SSC_Damage_PnaSilkWhip":"SSC_Damage_PnaWhipCut",workerClass=silk?typeof(DamageWorker_SSCPnaSpacer):typeof(DamageWorker_SSCPnaCoated),
            modExtensions=new List<DefModExtension>{silk?new SSCPnaSpacerExtension{baseLoad=.2f,recordHediff=record}:new SSCPnaCoatingExtension{baseLoad=.2f,recordHediff=record}}};
        f.Verb.verbProps=new VerbProperties{meleeDamageDef=damage};Set(f.Verb,"currentTarget",new LocalTargetInfo(f.Target));
        Set(tracker,"verbs",new List<Verb>{f.Verb});f.Attacker.equipment=new Pawn_EquipmentTracker(f.Attacker);
        Set(f.Attacker.equipment.GetDirectlyHeldThings(),"innerList",new List<ThingWithComps>{f.Weapon});
        Rooted.Add(f.Target);Ratings[f.Attacker]=1;Ratings[f.Target]=1;
        return f;
    }
    private static float Dodge(CooperativeFixture f)=>(float)NativeDodge.Invoke(f.Verb,new object[]{new LocalTargetInfo(f.Target)});
    private static float HitChance(CooperativeFixture f)=>(float)NativeHit.Invoke(f.Verb,new object[]{new LocalTargetInfo(f.Target)});
    private static bool Cast(CooperativeFixture f){f.SpawnReads=0;return (bool)NativeCast.Invoke(f.Verb,null);}
    private static void OrdinaryHandleAttack(CooperativeFixture f)
    {
        f.Verb=new Verb_MeleeAttackDamage{caster=f.Attacker,verbTracker=f.Verb.verbTracker,
            tool=new Tool{power=2,capacities=new List<ToolCapacityDef>{new(){defName="Blunt"}}},
            verbProps=new VerbProperties{meleeDamageDef=new DamageDef{defName="Blunt",workerClass=typeof(DamageWorker_AddInjury)}}};
        Set(f.Verb,"currentTarget",new LocalTargetInfo(f.Target));
    }
    private static bool CooperationHostility(Thing a,Thing b,ref bool __result)
    {
        var f=cooperating;if(f==null)return true;
        if(a==f.Target&&b==f.Attacker){__result=f.TargetHostile;return false;}
        if(a==f.Attacker&&b==f.Target){__result=f.AttackerHostile;return false;}
        return true;
    }
    private static IEnumerable<CodeInstruction> CooperationNoIdeology(IEnumerable<CodeInstruction> codes)
    {
        MethodInfo getter=AccessTools.PropertyGetter(typeof(ModsConfig),nameof(ModsConfig.IdeologyActive));
        foreach(var code in codes)
            yield return code.Calls(getter)?new CodeInstruction(OpCodes.Ldc_I4_0).MoveLabelsFrom(code).MoveBlocksFrom(code):code;
    }
    private static bool CooperationSpawned(Thing __instance,ref bool __result)
    // 首次查询允许真实出手；后续查询隐藏假地图，隔离原版绘制／踉跄等地图副作用。
    {var f=cooperating;if(f==null)return true;__result=__instance==f.Attacker&&f.SpawnReads++==0;return false;}
    private static bool CooperationDrawPos(ref Vector3 __result){if(cooperating==null)return true;__result=default;return false;}
    private static bool CooperationTickManager(ref TickManager __result){if(cooperating==null)return true;__result=CooperationClock;return false;}
    private static bool CooperationTick(ref int __result){if(cooperating==null)return true;__result=1234;return false;}
    private static bool CooperationSideEffect()=>cooperating==null;
    private static bool CooperationCanHit(ref bool __result){if(cooperating==null)return true;__result=true;return false;}
    private static bool CooperationImmobile(ref bool __result){if(cooperating==null)return true;__result=false;return false;}
    private static bool CooperationSound(ref SoundDef __result){if(cooperating==null)return true;__result=null;return false;}
    private static bool CooperationLog(ref BattleLogEntry_MeleeCombat __result){if(cooperating==null)return true;__result=null;return false;}
    private static bool CooperationDamage(ref DamageWorker.DamageResult __result)
    {
        var f=cooperating;if(f==null)return true;f.DamageCalls++;f.ThreatDuringDamage=f.Target.mindState.meleeThreat;
        f.TickDuringDamage=f.Target.mindState.lastMeleeThreatHarmTick;__result=new();return false;
    }
}
