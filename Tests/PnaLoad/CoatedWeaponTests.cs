// AI 辅助生成：直接执行生产处理器，物理结果由边界夹具提供，不冒充真实护甲模拟。
using System;
using System.Collections.Generic;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly HediffDef CoatingResistance = new HediffDef
        { defName = "Hediff_PNA_Consciousness_Record", maxSeverity = 100f };

    private static void RunCoatedWeaponTests()
    {
        // 三种载体分别验证伤势与剂量；父类边界收到原参数，返回结果实例也必须原样保留。
        foreach (var row in new (float dose, float damage, string type)[] { (.15f,5f,"Stab"), (.06f,3f,"Stab"), (.10f,7f,"Cut") })
            Run("涂覆三载体基础伤势与剂量 " + row, () =>
            {
                var p=CoatedPawn(); var w=CoatedWorker(row.type); var shot=CoatedShot(row.dose,row.damage);
                shot.ArmorPenetrationInt=.12f; shot.AllowDamagePropagation=false;
                var physical=w.PhysicalApply; DamageWorker.DamageResult expected=null;
                w.PhysicalApply=(info,target)=>
                {
                    Near(row.damage,info.Amount); Near(.12,info.ArmorPenetrationInt);
                    Assert(!info.IgnoreArmor && !info.AllowDamagePropagation,"保留原物理参数");
                    return expected=physical(info,target);
                };
                var result=w.Apply(shot,p);
                Assert(ReferenceEquals(expected,result),"返回原结果实例"); Equal(1,w.ApplyCalls);
                Near(row.damage,result.totalDamageDealt); Near(row.dose,SSCPnaExposureUtility.GetLoad(p));
                Assert(result.hediffs.Single().def.defName==row.type,"保留伤势类型");
            });

        // 伤口门槛：受伤、扣血和结果条目都不足以独立证明本次形成了真实开放伤口。
        foreach(string kind in new[]{"Bruise","Crush","Crack","Burn"})
            Run("涂覆非开放创伤不投药 " + kind,()=>
            { var p=CoatedPawn(); CoatedWorker(kind).Apply(CoatedShot(),p); Near(0,SSCPnaExposureUtility.GetLoad(p)); });

        Run("涂覆完全阻挡不因旧伤投药或建立快照",()=>
        {
            var p=CoatedPawn(); BoundaryWound(p,2); p.health.capacities.Consciousness=3;
            var w=CoatedWorker(); w.PhysicalApply=(i,t)=>new DamageWorker.DamageResult{deflected=true};
            w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p); Equal(1,w.ApplyCalls);
        });
        Run("涂覆零严重度和零实际伤害不投药",()=>
        {
            var p=CoatedPawn(); var w=CoatedWorker(); w.PhysicalApply=(i,t)=>BoundaryWound(p,0);
            w.Apply(CoatedShot(.15f,0),p); AssertNoCoatingDrug(p);
        });
        Run("涂覆只有受伤标记而无结果伤口不投药",()=>
        {
            var p=CoatedPawn(); var w=CoatedWorker();
            w.PhysicalApply=(i,t)=>new DamageWorker.DamageResult{wounded=true,totalDamageDealt=5};
            w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
        });
        Run("涂覆结果中的旧伤和未实际写入伤口均不投药",()=>
        {
            foreach(bool old in new[]{false,true})
            {
                var p=CoatedPawn(); var r=BoundaryWound(p,5);
                if(!old)p.health.RemoveHediff(r.hediffs[0]);
                var w=CoatedWorker(); w.PhysicalApply=(i,t)=>r;
                w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
            }
        });
        Run("涂覆同步移除新伤口不投药",()=>
        {
            var p=CoatedPawn(); p.health.OnAdded=h=>{if(h is Hediff_Injury)p.health.RemoveHediff(h);};
            CoatedWorker().Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
        });
        Run("涂覆结果伤口必须属于当前身体及有效部位",()=>
        {
            foreach(bool wrongPawn in new[]{false,true})
            {
                var p=CoatedPawn(); var w=CoatedWorker();
                w.PhysicalApply=(i,t)=>
                {
                    var r=BoundaryWound(p,5);
                    if(wrongPawn)r.hediffs[0].pawn=CoatedPawn();
                    else r.hediffs[0].Part=new BodyPartRecord();
                    return r;
                };
                w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
            }
        });
        // 部位资格：人工父部位也会覆盖其子部位；自然断肢与人工部位摧毁分别验证。
        foreach(bool parent in new[]{false,true})
            Run("涂覆排除非有机人工部位及其子部位 parent="+parent,()=>
            {
                var p=CoatedPawn(); var part=p.RaceProps.body.AllParts[0];
                if(parent){part.parent=new BodyPartRecord();p.RaceProps.body.AllParts.Add(part.parent);}
                p.health.hediffSet.hediffs.Add(new Hediff_AddedPart{pawn=p,Part=parent?part.parent:part,
                    def=new HediffDef{defName="Artificial",organicAddedBodypart=false}});
                CoatedWorker().Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
            });
        Run("涂覆有机移植部位仍允许真实刺伤",()=>
        {
            var p=CoatedPawn(); p.health.hediffSet.hediffs.Add(new Hediff_AddedPart{pawn=p,Part=p.RaceProps.body.AllParts[0],
                def=new HediffDef{defName="Organic",organicAddedBodypart=true}});
            CoatedWorker().Apply(CoatedShot(),p); Near(.15,SSCPnaExposureUtility.GetLoad(p));
        });
        foreach(bool fresh in new[]{false,true})
            Run("涂覆自然断肢只认新鲜开放伤口 fresh="+fresh,()=>
            {
                var p=CoatedPawn(); var w=CoatedWorker(); w.PhysicalApply=(i,t)=>BoundaryMissing(p,fresh);
                w.Apply(CoatedShot(),p); Near(fresh?.15:0,SSCPnaExposureUtility.GetLoad(p));
            });
        Run("涂覆人工部位被摧毁移除后仍不误算自然断肢",()=>
        {
            var p=CoatedPawn(); var added=new Hediff_AddedPart{pawn=p,Part=p.RaceProps.body.AllParts[0],
                def=new HediffDef{defName="Artificial"}}; p.health.hediffSet.hediffs.Add(added);
            var w=CoatedWorker(); w.PhysicalApply=(i,t)=>{p.health.RemoveHediff(added);return BoundaryMissing(p,true);};
            w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
        });
        Run("涂覆实体部位与非开放断肢不投药",()=>
        {
            foreach(bool solid in new[]{false,true})
            {
                var p=CoatedPawn(); p.RaceProps.body.AllParts[0].def.Solid=solid;
                var w=CoatedWorker(); w.PhysicalApply=(i,t)=>BoundaryMissing(p,true,solid?"Stab":"Crack");
                w.Apply(CoatedShot(),p); AssertNoCoatingDrug(p);
            }
        });

        // 命中归属：一次结果可有多个伤口；同 tick、同身体和异身体的独立调用不得合并。
        Run("涂覆多部位及部分阻挡仍只投药一次",()=>
        {
            var p=CoatedPawn(); var w=CoatedWorker();
            w.PhysicalApply=(i,t)=>
            {
                var r=BoundaryWound(p,3); var next=BoundaryWound(p,2);
                r.hediffs.AddRange(next.hediffs);r.totalDamageDealt+=2;r.deflected=true;r.diminished=true;return r;
            };
            w.Apply(CoatedShot(),p); Near(.15,SSCPnaExposureUtility.GetLoad(p)); Equal(2,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        Run("涂覆同tick独立命中分别投药",()=>
        { var p=CoatedPawn();var w=CoatedWorker();w.Apply(CoatedShot(),p);w.Apply(CoatedShot(),p);Near(.30,SSCPnaExposureUtility.GetLoad(p)); });
        foreach(bool same in new[]{false,true})
            Run("涂覆嵌套命中归属 same="+same,()=>
            {
                var outer=CoatedPawn();var inner=same?outer:CoatedPawn();var w=CoatedWorker();bool nested=false;
                w.PhysicalApply=(i,t)=>{if(!nested){nested=true;w.Apply(CoatedShot(),inner);}return BoundaryWound((Pawn)t,5);};
                w.Apply(CoatedShot(),outer);Near(same?.30:.15,SSCPnaExposureUtility.GetLoad(outer));
                if(!same)Near(.15,SSCPnaExposureUtility.GetLoad(inner));
            });
        Run("涂覆内层有效而外层阻挡不借用内层伤口",()=>
        {
            var p=CoatedPawn();var w=CoatedWorker();bool nested=false;
            w.PhysicalApply=(i,t)=>{if(nested)return BoundaryWound(p,5);nested=true;w.Apply(CoatedShot(),p);return new DamageWorker.DamageResult{deflected=true};};
            w.Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));
        });
        Run("涂覆异常退出后独立命中不受旧状态影响",()=>
        {
            var p=CoatedPawn();var w=CoatedWorker();var normal=w.PhysicalApply;
            w.PhysicalApply=(i,t)=>throw new InvalidOperationException("physical boundary");
            bool threw=false;try{w.Apply(CoatedShot(),p);}catch(InvalidOperationException){threw=true;}
            Assert(threw,"原物理异常正常传播");w.PhysicalApply=normal;w.Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));
        });

        // 意识取样在物理回调之前，快照写入在有效伤口之后；嵌套命中分别保留自己的候选值。
        foreach(var row in new (float dose, float expected)[]{(.15f,.5f),(.06f,.3f),(.10f,.5f)})
            Run("涂覆低意识保底及50%上限 "+row,()=>
            {var p=CoatedPawn();p.health.capacities.Consciousness=.1f;CoatedWorker().Apply(CoatedShot(row.dose),p);Near(row.expected,SSCPnaExposureUtility.GetLoad(p));});
        Run("涂覆先取意识且成功后快照以完整严重度加入",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            p.health.OnAdded=h=>{if(h is Hediff_Injury)p.health.capacities.Consciousness=.2f;
                if(h.def==CoatingResistance)Near(3,h.Severity);};
            var w=CoatedWorker();w.Apply(CoatedShot(),p);Near(.05,SSCPnaExposureUtility.GetLoad(p));
            p.health.capacities.Consciousness=5;w.Apply(CoatedShot(),p);Near(.10,SSCPnaExposureUtility.GetLoad(p));
            Near(3,p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance).Severity);
        });
        Run("涂覆普通意识取样不被本击创伤降低",()=>
        {
            var p=CoatedPawn();p.health.OnAdded=h=>{if(h is Hediff_Injury)p.health.capacities.Consciousness=.2f;};
            CoatedWorker().Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));
        });
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            Run("涂覆非法旧快照回退1 "+bad,()=>
            {
                var p=CoatedPawn();p.health.hediffSet.hediffs.Add(new Hediff{pawn=p,def=CoatingResistance,RawSeverity=bad});
                CoatedWorker().Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));
            });
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Run("涂覆异常意识不建立非法快照 "+bad,()=>
            {var p=CoatedPawn();p.health.capacities.Consciousness=bad;CoatedWorker().Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));Assert(!p.health.hediffSet.HasHediff(CoatingResistance),"无快照");});
        Run("涂覆嵌套先建快照不被外层覆盖或反算剂量",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;var w=CoatedWorker();bool nested=false;
            w.PhysicalApply=(i,t)=>{if(!nested){nested=true;p.health.capacities.Consciousness=5;w.Apply(CoatedShot(),p);}return BoundaryWound(p,5);};
            w.Apply(CoatedShot(),p);Near(.08,SSCPnaExposureUtility.GetLoad(p));Near(5,p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance).Severity);
        });
        foreach(bool removeWound in new[]{false,true})
            Run("涂覆快照回调失效不得继续投药 removeWound="+removeWound,()=>
            {
                var p=CoatedPawn();p.health.capacities.Consciousness=3;
                p.health.OnAdded=h=>{if(h.def!=CoatingResistance)return;
                    if(removeWound)foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(injury);
                    else p.health.RemoveHediff(h);};
                CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);
            });
        // 配置错误只能禁用新药效，不能使原物理伤害消失。
        foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
            Run("涂覆非法药量只禁用药效 "+bad,()=>
            {var p=CoatedPawn();var w=CoatedWorker();var r=w.Apply(CoatedShot(bad),p);Near(5,r.totalDamageDealt);AssertNoCoatingDrug(p);});
        foreach(bool missingExtension in new[]{false,true})
            Run("涂覆缺配置仍执行物理伤害 ext="+missingExtension,()=>
            {
                var p=CoatedPawn();var shot=CoatedShot();
                if(missingExtension)shot.Def.Extension=null;else ((SSCPnaCoatingExtension)shot.Def.Extension).recordHediff=null;
                Near(5,CoatedWorker().Apply(shot,p).totalDamageDealt);AssertNoCoatingDrug(p);
            });

        // 身份与回调：新处理器只继承共享药效规则，不继承旧枪的无伤／治疗分支。
        Run("涂覆有效链保留创伤且不治疗",()=>
        {
            var p=CoatedPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());var old=WeaponInjury(p);
            CoatedWorker().Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));
            Assert(RootedRecord(p)!=null && Record(p)==null,"活化载体");Near(0,old.Healed);
            Equal(2,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());Assert(SSCPnaExposureUtility.GetAddiction(p)==null,"不成瘾");
        });
        foreach(bool chain in new[]{false,true})
            Run("涂覆主人仅药效免疫而保留伤口 chain="+chain,()=>
            {
                var p=CoatedPawn();p.IsMaster=true;p.health.capacities.Consciousness=3;
                if(chain)Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
                var old=WeaponInjury(p);CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);Near(0,old.Healed);
                Equal(2,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
            });
        foreach(string mutation in new[]{"bind","unbind","master"})
            Run("涂覆物理回调后的当前身份分流 "+mutation,()=>
            {
                var p=CoatedPawn();Hediff chain=mutation=="unbind"?Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn()):null;
                p.health.OnAdded=h=>{if(h is not Hediff_Injury)return;
                    if(mutation=="bind")Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
                    else if(mutation=="unbind")p.health.RemoveHediff(chain);else p.IsMaster=true;};
                CoatedWorker().Apply(CoatedShot(),p);
                if(mutation=="master")AssertNoCoatingDrug(p);
                else {Near(.15,SSCPnaExposureUtility.GetLoad(p));Assert((RootedRecord(p)!=null)==(mutation=="bind"),"当前链分流");}
                Equal(1,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
            });
        Run("涂覆药效回调绑定保留伤口并使用活化载体",()=>
        {
            var p=CoatedPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsBodyLoadDef(h.def))Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());};
            CoatedWorker().Apply(CoatedShot(),p);Near(.15,SSCPnaExposureUtility.GetLoad(p));Assert(RootedRecord(p)!=null,"活化");
            Equal(1,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        Run("涂覆协调移除伤口后不再投药",()=>
        {
            var p=CoatedPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());
            var addiction=HediffMaker.MakeHediff(AddictionDef,p);p.health.hediffSet.hediffs.Add(addiction);
            p.health.OnRemoved=h=>
            {
                if(h!=addiction)return;
                foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(injury);
            };
            CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);
        });
        Run("涂覆快照回调切主人后清药而保留创伤",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;
            p.health.OnAdded=h=>{if(h.def==CoatingResistance)p.IsMaster=true;};
            CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);
            Equal(1,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        Run("涂覆药效回调切主人仍保留物理结果",()=>
        {
            var p=CoatedPawn();p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsBodyLoadDef(h.def))p.IsMaster=true;};
            var r=CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);Near(5,r.totalDamageDealt);
            Equal(1,p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count());
        });
        Run("涂覆缺身体或健康上下文只交原物理入口",()=>
        {
            foreach(bool noHealth in new[]{false,true})
            {
                var p=CoatedPawn();if(noHealth)p.health=null;else p.RaceProps.body=null;
                var w=CoatedWorker();w.PhysicalApply=(i,t)=>new DamageWorker.DamageResult{totalDamageDealt=5};
                Near(5,w.Apply(CoatedShot(),p).totalDamageDealt);Equal(1,w.ApplyCalls);
            }
        });
        Run("涂覆物理回调进入存读档后不制造暴露",()=>
        {
            var p=CoatedPawn();var w=CoatedWorker();
            w.PhysicalApply=(i,t)=>{var r=BoundaryWound(p,5);Scribe.mode=LoadSaveMode.Saving;return r;};
            w.Apply(CoatedShot(),p);AssertNoCoatingDrug(p);
        });
        foreach(string invalid in new[]{"dead","destroyed","discarded","suspended","nonflesh"})
            Run("涂覆无效目标仍交原物理入口 "+invalid,()=>
            {
                var p=CoatedPawn();SetCoatedInvalid(p,invalid);var w=CoatedWorker();w.Apply(CoatedShot(),p);
                Equal(1,w.ApplyCalls);AssertNoCoatingDrug(p);
            });
        Run("涂覆物理致死后不再输入药效",()=>
        {
            var p=CoatedPawn();p.health.capacities.Consciousness=3;p.health.OnAdded=h=>{if(h is Hediff_Injury)p.Dead=true;};
            CoatedWorker().Apply(CoatedShot(),p);AssertNoCoatingDrug(p);
        });
        foreach(var mode in new[]{LoadSaveMode.Saving,LoadSaveMode.LoadingVars,LoadSaveMode.PostLoadInit})
            Run("涂覆存读档不制造暴露 "+mode,()=>
            {var p=CoatedPawn();Scribe.mode=mode;var w=CoatedWorker();w.Apply(CoatedShot(),p);Equal(1,w.ApplyCalls);AssertNoCoatingDrug(p);});
        Run("涂覆非Pawn仅执行物理入口",()=>
        {
            var w=CoatedWorker();w.PhysicalApply=(i,t)=>new DamageWorker.DamageResult{totalDamageDealt=i.Amount};
            Near(5,w.Apply(CoatedShot(),new Thing()).totalDamageDealt);Equal(1,w.ApplyCalls);
        });
        // 通过真实共享入口验证 Weapon 来源，避免测试只检查负荷而漏掉需求或成瘾的重复输入。
        Run("涂覆普通来源补需求并仅一次加深已有成瘾",()=>
        {
            var p=CoatedPawn();AddOldAddiction(p,.4f);p.needs.PnaNeed=.2f;
            var w=CoatedWorker();w.PhysicalApply=(i,t)=>{var r=BoundaryWound(p,3);r.hediffs.AddRange(BoundaryWound(p,2).hediffs);r.totalDamageDealt+=2;return r;};
            w.Apply(CoatedShot(.06f),p);Near(.41,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.32,p.needs.PnaNeed.Value);
        });
        Run("涂覆97%阶段未满不首次成瘾且满负荷才建立",()=>
        {
            var p=CoatedPawn();Raw(p,1.65f);var w=CoatedWorker();w.Apply(CoatedShot(),p);
            Near(.975,SSCPnaExposureUtility.GetLoad(p));Assert(SSCPnaExposureUtility.GetAddiction(p)==null,"末期未满");
            w.Apply(CoatedShot(),p);Near(1,SSCPnaExposureUtility.GetLoad(p));Near(1,SSCPnaExposureUtility.GetAddiction(p).Severity);
            Equal(1,p.health.hediffSet.hediffs.Count(h=>SSCPnaExposureUtility.IsAddictionDef(h.def)));
        });
        Run("涂覆末期对已有成瘾沿用合计0.21",()=>
        {
            var p=CoatedPawn();Raw(p,1.65f);AddOldAddiction(p,.4f);
            CoatedWorker().Apply(CoatedShot(),p);Near(.61,SSCPnaExposureUtility.GetAddiction(p).Severity);
        });
    }

    private static Pawn CoatedPawn()
    {var p=NewPawn();p.RaceProps.body.AllParts.Add(new BodyPartRecord());return p;}
    private static DamageInfo CoatedShot(float dose=.15f,float damage=5f)=>new DamageInfo
    {Amount=damage,Def=new DamageDef{Extension=new SSCPnaCoatingExtension{baseLoad=dose,recordHediff=CoatingResistance}}};
    private static DamageWorker_SSCPnaCoated CoatedWorker(string type="Stab")=>new DamageWorker_SSCPnaCoated
    {PhysicalApply=(info,target)=>BoundaryWound((Pawn)target,info.Amount,type)};
    // 提供已经完成的物理伤势及健康通知边界；这里不模拟护甲概率、部位随机或原版传播算法。
    private static DamageWorker.DamageResult BoundaryWound(Pawn p,float amount,string type="Stab")
    {
        var h=new Hediff_Injury{pawn=p,def=new HediffDef{defName=type,maxSeverity=100},Part=p.RaceProps.body.AllParts[0],Severity=amount};
        p.health.AddHediff(h);
        return new DamageWorker.DamageResult{wounded=true,totalDamageDealt=amount,hediffs=new List<Hediff>{h}};
    }
    // 模拟原伤口已因断肢清理、只留下真实缺失记录的结果形态。
    private static DamageWorker.DamageResult BoundaryMissing(Pawn p,bool fresh,string type="Stab")
    {
        var h=new Hediff_MissingPart{pawn=p,def=new HediffDef{defName="MissingBodyPart"},Part=p.RaceProps.body.AllParts[0],
            IsFresh=fresh,lastInjury=new HediffDef{defName=type}};p.health.AddHediff(h);
        return new DamageWorker.DamageResult{wounded=true,totalDamageDealt=5,hediffs=new List<Hediff>{h}};
    }
    private static void AssertNoCoatingDrug(Pawn p)
    {Near(0,SSCPnaExposureUtility.GetLoad(p));Assert(!p.health.hediffSet.HasHediff(CoatingResistance),"无新抗性快照");}
    private static void SetCoatedInvalid(Pawn p,string invalid)
    {
        switch(invalid){case "dead":p.Dead=true;break;case "destroyed":p.Destroyed=true;break;
            case "discarded":p.Discarded=true;break;case "suspended":p.Suspended=true;break;case "nonflesh":p.RaceProps.IsFlesh=false;break;}
    }
}
