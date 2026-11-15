// AI 辅助生成：链接真实太空处理器、剂量、快照、暴露和人格凭据，物理接口仅提供可观察边界。
using System;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunSpacerWeaponTests()
    {
        Run("太空有效链直接吸收并按剂量修复", () =>
        {
            var pawn = SpacerSlave();
            var injury = SpacerInjury(pawn, 10);
            ArmorUtility.Boundary = (Pawn _, float amount, ref DamageDef damage) => 0;
            var worker = new DamageWorker_SSCPnaSpacer();
            var result = worker.Apply(SpacerShot(.10f), pawn);
            Near(.10, SSCPnaExposureUtility.GetLoad(pawn));
            Near(.5, injury.Healed);
            Equal(0, ArmorUtility.Calls);
            Equal(0, worker.InjuryCalls);
            Assert(!result.wounded, "不伪造创伤");
        });
        // 身份分流与护甲门槛：主人优先；转钝的物理结果由边界提供，不能当作原版概率实测。
        Run("太空主人免疫优先于链",()=>
        { var p=SpacerSlave();var injury=SpacerInjury(p,10);p.IsMaster=true;p.health.capacities.Consciousness=3;
            var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);Near(0,injury.Healed);Equal(0,w.InjuryCalls);Equal(0,ArmorUtility.Calls);AssertNoMasterDrugState(p); });
        Run("太空普通完全阻挡无药无新快照",()=>
        {var p=CoatedPawn();p.health.capacities.Consciousness=3;ArmorUtility.Boundary=(Pawn _,float a,ref DamageDef d)=>0;
            var w=new DamageWorker_SSCPnaSpacer();Assert(w.Apply(SpacerShot(),p).deflected,"弹开");AssertNoCoatingDrug(p);Equal(0,w.InjuryCalls);});
        Run("太空普通转钝保留实际物理结果而不投药",()=>
        {const float damage=1f;var p=CoatedPawn();p.health.capacities.Consciousness=3;ArmorUtility.Boundary=(Pawn _,float a,ref DamageDef d)=>{d=new DamageDef{defName="Bruise"};return damage;};
            var w=new DamageWorker_SSCPnaSpacer();var r=w.Apply(SpacerShot(),p);Near(damage,r.totalDamageDealt);Equal(damage>0?1:0,w.InjuryCalls);AssertNoCoatingDrug(p);Equal(1,ArmorUtility.Calls);});
        foreach(bool suppressed in new[]{false,true}) Run("太空穿透不依赖可见伤口 "+suppressed,()=>
        {var p=CoatedPawn();p.SuppressInjury=suppressed;p.IncomingFactor=suppressed?1:0;
            var r=new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);Assert(!r.wounded,"无伤口");Near(.2,SSCPnaExposureUtility.GetLoad(p));});
        // 投送按命中时与回调后的真实部位判断；人工祖先、缺失和外来部位不能借无伤分支受药。
        foreach (var row in new[] { (false, "solid"), (true, "parent"), (true, "missing"), (false, "foreign") })
            Run("太空投送部位准入 " + row, () =>
            {
                var pawn = row.Item1 ? SpacerSlave() : CoatedPawn();
                var part = pawn.RaceProps.body.AllParts[0];
                var shot = SpacerShot();
                switch (row.Item2)
                {
                    case "solid": part.def.Solid = true; break;
                    case "parent":
                        part.parent = new BodyPartRecord();
                        pawn.RaceProps.body.AllParts.Add(part.parent);
                        pawn.health.hediffSet.hediffs.Add(new Hediff_AddedPart { pawn = pawn, Part = part.parent, def = new HediffDef() });
                        break;
                    case "missing": pawn.health.hediffSet.hediffs.Add(new Hediff_MissingPart { pawn = pawn, Part = part }); break;
                    case "foreign": shot.SetHitPart(new BodyPartRecord()); break;
                }
                new DamageWorker_SSCPnaSpacer().Apply(shot, pawn);
                AssertNoCoatingDrug(pawn);
            });
        Run("太空有机移植物可吸收与修复",()=>
        {var p=SpacerSlave();var part=p.RaceProps.body.AllParts[0];p.health.hediffSet.hediffs.Add(new Hediff_AddedPart{pawn=p,Part=part,def=new HediffDef{organicAddedBodypart=true}});
            var injury=SpacerInjury(p,4);new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);Near(1,injury.Healed);Near(.2,SSCPnaExposureUtility.GetLoad(p));});
        Run("太空无效残链和原版奴隶仍走普通护甲",()=>
        {var p=CoatedPawn();p.IsSlave=true;p.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave{pawn=p,def=ChainDef});new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);
            Equal(1,ArmorUtility.Calls);Assert(Record(p)!=null&&RootedRecord(p)==null,"普通身体");});
        // 修复强度与选择：只处理一处、溢出舍弃、满负荷仍修复，意识变化仅影响药量。
        Run("太空修复只选最严重单伤且不跨伤口",()=>
        {var p=SpacerSlave();var a=SpacerInjury(p,.4f);var b=SpacerInjury(p,.7f);new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);Near(0,a.Healed);Near(.7,b.Healed);Near(0,b.Severity);});
        Run("太空同严重度保留首伤且无伤不储存",()=>
        {var p=SpacerSlave();var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);var a=SpacerInjury(p,3);var b=SpacerInjury(p,3);w.Apply(SpacerShot(),p);Near(1,a.Healed);Near(0,b.Healed);});
        foreach(string kind in new[]{"permanent","parent","foreign"}) Run("太空修复排除 "+kind,()=>
        {var p=SpacerSlave();var validPart=p.RaceProps.body.AllParts[0];var part=new BodyPartRecord();p.RaceProps.body.AllParts.Add(part);var injury=SpacerInjury(p,10,part);
            if(kind=="permanent")injury.Permanent=true;
            if(kind=="parent")
            {
                var parent = new BodyPartRecord();
                part.parent = parent;
                p.RaceProps.body.AllParts.Add(parent);
                p.health.hediffSet.hediffs.Add(new Hediff_AddedPart { pawn=p, Part=parent, def=new HediffDef() });
            }
            if(kind=="foreign")injury.pawn=CoatedPawn();
            var shot=SpacerShot();shot.SetHitPart(validPart);new DamageWorker_SSCPnaSpacer().Apply(shot,p);Near(0,injury.Healed);});
        Run("太空满负荷仍修复且不放大",()=>
        {var p=SpacerSlave();Input(p,1);p.health.capacities.Consciousness=.1f;var injury=SpacerInjury(p,8);new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(.1f),p);Near(.5,injury.Healed);Near(1,SSCPnaExposureUtility.GetLoad(p));});
        Run("太空高意识快照取受伤前数值",()=>
        {var p=CoatedPawn();p.health.capacities.Consciousness=3;p.health.OnAdded=h=>{if(h is Hediff_Injury)p.health.capacities.Consciousness=.2f;};
            var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);Near(.2/3,SSCPnaExposureUtility.GetLoad(p));Near(3,p.health.hediffSet.GetFirstHediffOfDef(CoatingResistance).Severity);w.Apply(SpacerShot(),p);Near(.4/3,SSCPnaExposureUtility.GetLoad(p));});
        // 连发、多射手、同 tick 与重入都执行独立 Apply，验证不存在跨事件吞并或限速。
        Run("太空逐针意识变化与三个独立修复",()=>
        {var p=SpacerSlave();var injury=SpacerInjury(p,10);var w=new DamageWorker_SSCPnaSpacer();for(int i=0;i<3;i++){p.health.capacities.Consciousness=i==0?1:.5f;w.Apply(SpacerShot(.1f),p);}
            Near(.5,SSCPnaExposureUtility.GetLoad(p));Near(1.5,injury.Healed);Equal(3,SSCPnaWeaponEffectsUtility.Absorptions.Count);});
        Run("太空传播标记不拆分投送且多射手同tick独立",()=>
        {var p=SpacerSlave();var injury=SpacerInjury(p,10);var shot=SpacerShot(.1f);shot.Parts=4;shot.AllowDamagePropagation=true;
            new DamageWorker_SSCPnaSpacer().Apply(shot,p);new DamageWorker_SSCPnaSpacer().Apply(shot,p);Near(.2,SSCPnaExposureUtility.GetLoad(p));Near(1,injury.Healed);});
        Run("太空同处理器嵌套命中互不认领",()=>
        {var p=SpacerSlave();var other=SpacerSlave();var a=SpacerInjury(p,8);var b=SpacerInjury(other,8);var w=new DamageWorker_SSCPnaSpacer();
            p.health.OnAdded=h=>{if(!SSCPnaExposureUtility.IsBodyLoadDef(h.def))return;p.health.OnAdded=null;w.Apply(SpacerShot(.1f),other);};w.Apply(SpacerShot(),p);Near(1,a.Healed);Near(.5,b.Healed);Near(.2,SSCPnaExposureUtility.GetLoad(p));Near(.1,SSCPnaExposureUtility.GetLoad(other));});
        // 同步健康回调是核心边界：停止余下修复／反馈，并保留已经获得的创伤保护。
        foreach(string mutation in new[]{"unbind","body","personality","saving"}) Run("太空输入回调失效不补创伤 "+mutation,()=>
        {var p=SpacerSlave();var injury=SpacerInjury(p,8);p.health.OnAdded=h=>{if(!SSCPnaExposureUtility.IsBodyLoadDef(h.def))return;p.health.OnAdded=null;
                if(mutation=="unbind")p.health.RemoveHediff(p.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().Single());
                if(mutation=="body")p.RaceProps.body=new BodyDef();if(mutation=="saving")Scribe.mode=LoadSaveMode.Saving;
                if(mutation=="personality"){SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope);scope.Dispose();}};
            var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);Near(0,injury.Healed);Equal(0,w.InjuryCalls);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);});
        Run("太空衣甲回调获得保护不追加微创",()=>
        {var p=CoatedPawn();ArmorUtility.Boundary=(Pawn at,float a,ref DamageDef d)=>{Hediff_ChainOfSexSlave.AddToPawn(at,NewPawn());return a;};
            var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);Equal(0,w.InjuryCalls);Near(.2,SSCPnaExposureUtility.GetLoad(p));});
        Run("太空受保护命中回调解绑无后续治疗",()=>
        {var p=SpacerSlave();var injury=SpacerInjury(p,10);p.health.capacities.Consciousness=3;p.health.OnAdded=h=>{if(h.def==CoatingResistance)p.health.RemoveHediff(p.health.hediffSet.hediffs.OfType<Hediff_ChainOfSexSlave>().Single());};
            var w=new DamageWorker_SSCPnaSpacer();w.Apply(SpacerShot(),p);Equal(0,w.InjuryCalls);Near(0,injury.Healed);AssertNoCoatingDrug(p);});
        Run("太空拒绝输入清理本次新快照",()=>
        {var p=SpacerSlave();p.health.capacities.Consciousness=3;p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsBodyLoadDef(h.def))p.health.RemoveHediff(h);};new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);AssertNoCoatingDrug(p);});
        // 快照只清理本次持有的新实例；回调移除、原有记录及输入拒绝分别验证。
        Run("太空新快照被回调移除时拒绝本次投送",()=>
        {var p=SpacerSlave();p.health.capacities.Consciousness=3;p.health.OnAdded=h=>{if(h.def==CoatingResistance)p.health.RemoveHediff(h);};new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);AssertNoCoatingDrug(p);});
        Run("太空缺失父部位的子伤不修复",()=>
        {var p=SpacerSlave();var valid=p.RaceProps.body.AllParts[0];var parent=new BodyPartRecord();var child=new BodyPartRecord{parent=parent};p.RaceProps.body.AllParts.AddRange(new[]{parent,child});
            var injury=SpacerInjury(p,10,child);p.health.hediffSet.hediffs.Add(new Hediff_MissingPart{pawn=p,Part=parent});var shot=SpacerShot();shot.SetHitPart(valid);new DamageWorker_SSCPnaSpacer().Apply(shot,p);Near(0,injury.Healed);});
        Run("太空普通来源沿用成瘾和需求增量一次",()=>
        {var p=CoatedPawn();AddOldAddiction(p,.4f);p.needs.PnaNeed=.2f;new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(.1f),p);Near(.41,SSCPnaExposureUtility.GetAddiction(p).Severity);Near(.4,p.needs.PnaNeed.Value);});
        Run("太空拒绝输入不删除原有快照",()=>
        {var p=SpacerSlave();var snapshot=new Hediff{pawn=p,def=CoatingResistance,Severity=3};p.health.hediffSet.hediffs.Add(snapshot);p.health.OnAdded=h=>{if(SSCPnaExposureUtility.IsBodyLoadDef(h.def))p.health.RemoveHediff(h);};
            new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);Assert(p.health.hediffSet.hediffs.Contains(snapshot),"已有快照保留");});
        Run("太空治疗回调转主人不冒心且不二次治疗",()=>
        {var p=SpacerSlave();var injury=SpacerInjury(p,10);injury.OnHeal=()=>{p.IsMaster=true;SSCPnaExposureUtility.RefreshBodyState(p);};new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);Near(1,injury.Healed);Equal(0,SSCPnaWeaponEffectsUtility.Absorptions.Count);});
        Run("太空死亡伤势回调不再投药",()=>
        {var p=CoatedPawn();p.health.OnAdded=h=>{if(h is Hediff_Injury)p.Dead=true;};new DamageWorker_SSCPnaSpacer().Apply(SpacerShot(),p);AssertNoCoatingDrug(p);});
        // 无效身体、保存阶段和损坏配置均安全退出；信息卡也不能显示非法有效剂量。
        Run("太空暂停目标无伤无药", () =>
        {
            var pawn = CoatedPawn();
            pawn.Suspended = true;
            var worker = new DamageWorker_SSCPnaSpacer();
            worker.Apply(SpacerShot(), pawn);
            Equal(0, worker.InjuryCalls);
            AssertNoCoatingDrug(pawn);
        });
        Run("太空读档无伤无药", () =>
        {
            var pawn = CoatedPawn();
            Scribe.mode = LoadSaveMode.LoadingVars;
            var worker = new DamageWorker_SSCPnaSpacer();
            worker.Apply(SpacerShot(), pawn);
            Equal(0, worker.InjuryCalls);
            AssertNoCoatingDrug(pawn);
        });
        foreach(float invalid in new[]{0f,float.NaN,float.MaxValue}) Run("太空损坏剂量安全退出 "+invalid,()=>
        {var p=SpacerSlave();var shot=SpacerShot(invalid);var w=new DamageWorker_SSCPnaSpacer();w.Apply(shot,p);Equal(0,w.InjuryCalls);AssertNoCoatingDrug(p);Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(PotencyRanged(shot.Def),out _),"不展示非法值");});
        Run("太空建筑不破坏且缺扩展不回退",()=>
        {var w=new DamageWorker_SSCPnaSpacer();Near(0,w.Apply(SpacerShot(),new Thing()).totalDamageDealt);var p=CoatedPawn();w.Apply(new DamageInfo{Amount=1,Def=new DamageDef()},p);Equal(0,w.InjuryCalls);});
        Run("太空与涂覆效力冲突不误报统一基线",()=>
        {var gun=PotencyRanged(SpacerShot().Def);gun.verbs.Add(PotencyRanged(PotencyDamage(.1f)).verbs[0]);Assert(!SSCPnaWeaponPotencyUtility.TryGetBaseline(gun,out _),"冲突隐藏");});
    }
    // 以下对象只存在于测试内存；不创建正式武器 Def、配方或可加载的 XML。
    private static Pawn SpacerSlave() {var p=CoatedPawn();Hediff_ChainOfSexSlave.AddToPawn(p,NewPawn());return p;}
    private static DamageInfo SpacerShot(float load=.2f)=>new DamageInfo{Amount=1,ArmorPenetrationInt=.6f,Def=new DamageDef{defName="Stab",workerClass=typeof(DamageWorker_SSCPnaSpacer),Extension=new SSCPnaSpacerExtension{baseLoad=load,recordHediff=CoatingResistance}}};
    private static Hediff_Injury SpacerInjury(Pawn pawn,float severity,BodyPartRecord part=null)
    {var injury=new Hediff_Injury{pawn=pawn,def=new HediffDef{defName="Cut",maxSeverity=100},Part=part??pawn.RaceProps.body.AllParts[0],Severity=severity};pawn.health.hediffSet.hediffs.Add(injury);return injury;}
}
