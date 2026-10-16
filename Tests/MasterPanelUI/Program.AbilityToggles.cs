// AI 辅助生成：执行真实注册表、开关、人格凭据、标记和面板，不重写规则。
using SexSlaveCraft;
using Verse;
using RimWorld;

internal static partial class Program
{
    private const string Infestation = SSCAbilityToggleUtility.MasterDesireInfestation;
    private static int MarkerCount(Pawn pawn) => pawn.health.hediffSet.hediffs.Count(h => SSCAbilityToggleUtility.IsMarkerDef(h.def));
    private static void Enable(Pawn pawn, bool enabled = true)
        => Assert(SSCAbilityToggleUtility.TrySetEnabled(pawn, Infestation, enabled, pawn.Training, SSCPersonalityRuntimeUtility.Capture(pawn)));

    private static void RunAbilityToggleTests()
    {
        var marker = new HediffDef { defName = SSCAbilityToggleUtility.InfestationMarker,
            hediffClass = typeof(Hediff_SSCToggleAbilityMarker), maxSeverity = 1 };
        DefDatabase<HediffDef>.AllDefs.Add(marker);
        Run("能力开关默认关闭，读取不创建保存条目/成长/标记", () =>
        {
            var p = new Pawn();
            for (int i=0;i<5;i++) { Assert(!SSCAbilityToggleUtility.GetEnabled(p,Infestation)); Tab().Draw(p); }
            Assert(!p.Training.abilityToggles.HasEntries && p.health.Adds==0 && SSCMasterProgressionUtility.GetRecord(p)==null);
        });
        Run("无缰绳无成长仍可开启和关闭侵染", () =>
        {
            var p=new Pawn(); Widgets.Click=true; Tab().Draw(p);
            Assert(SSCAbilityToggleUtility.IsActive(p,Infestation) && MarkerCount(p)==1 && SSCBondUtility.GetBridle(p)==null);
            Widgets.Click=false; Enable(p,false); Assert(MarkerCount(p)==0 && !SSCAbilityToggleUtility.IsActive(p,Infestation));
        });
        Run("能力标记幂等、重复清理且不显示严重度", () =>
        {
            var p=new Pawn(); Enable(p); int adds=p.health.Adds;
            for(int i=0;i<5;i++) SSCAbilityToggleUtility.SyncMarkers(p);
            Assert(p.health.Adds==adds);
            p.health.AddHediff(HediffMaker.MakeHediff(marker,p)); SSCAbilityToggleUtility.SyncMarkers(p);
            Assert(MarkerCount(p)==1 && p.health.hediffSet.hediffs.Single().SeverityLabel==null);
        });
        Run("退出身份保留偏好，返回恢复；空壳停用", () =>
        {
            var p=new Pawn(); Enable(p); p.Training.pawnIdentity=PawnIdentity.Unset;
            Assert(!p.health.hediffSet.hediffs.Single().Visible); SSCAbilityToggleUtility.SyncMarkers(p);
            Assert(MarkerCount(p)==0 && SSCAbilityToggleUtility.GetEnabled(p,Infestation));
            p.Training.pawnIdentity=PawnIdentity.Master; SSCAbilityToggleUtility.SyncMarkers(p); Assert(MarkerCount(p)==1);
            p.health.AddHediff(new Hediff{def=SSCDefOf.SSC_PersonalityExcreted_Done,pawn=p});
            SSCAbilityToggleUtility.SyncMarkers(p); Assert(MarkerCount(p)==0 && !SSCAbilityToggleUtility.CanEdit(p,Infestation));
        });
        Run("非玩家主人保留实际能力但无法编辑", () =>
        {
            var p=new Pawn(); Enable(p); p.Faction=new Faction();
            Assert(SSCAbilityToggleUtility.IsActive(p,Infestation) && !SSCAbilityToggleUtility.CanEdit(p,Infestation));
        });
        Run("死亡销毁丢弃分别停用，存活恢复不丢偏好", () =>
        {
            var p=new Pawn(); Enable(p);
            foreach(int kind in new[]{0,1,2})
            {
                p.Dead=kind==0; p.Destroyed=kind==1; p.Discarded=kind==2;
                SSCAbilityToggleUtility.SyncMarkers(p); Assert(MarkerCount(p)==0 && !SSCAbilityToggleUtility.IsActive(p,Infestation));
                p.Dead=p.Destroyed=p.Discarded=false; SSCAbilityToggleUtility.SyncMarkers(p); Assert(MarkerCount(p)==1);
            }
        });
        Run("同组件同身份换人格使旧点击失效，流转期间暂停且拒绝重入", () =>
        {
            var p=new Pawn(); Enable(p); object old=SSCPersonalityRuntimeUtility.Capture(p);
            Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope));
            using(scope)
            {
                Assert(!SSCAbilityToggleUtility.IsActive(p,Infestation) && !SSCAbilityToggleUtility.CanEdit(p,Infestation));
                Assert(!SSCPersonalityRuntimeUtility.TryBeginMutation(p,out _));
                Assert(!SSCAbilityToggleUtility.TrySetEnabled(p,Infestation,false,p.Training,old));
            }
            Assert(!SSCAbilityToggleUtility.TrySetEnabled(p,Infestation,false,p.Training,old));
            Assert(SSCAbilityToggleUtility.IsActive(p,Infestation));
        });
        Run("标记添加回调关闭能力及重入同步，最终无残留", () =>
        {
            var p=new Pawn();
            p.health.OnAdded=h=>{p.Training.abilityToggles.Set(Infestation,false);SSCAbilityToggleUtility.SyncMarkers(p);};
            Enable(p); Assert(MarkerCount(p)==0 && !SSCAbilityToggleUtility.IsActive(p,Infestation));
        });
        Run("标记删除回调再次同步不会递归或恢复已关闭状态", () =>
        {
            var p=new Pawn();Enable(p);p.health.OnRemoved=h=>SSCAbilityToggleUtility.SyncMarkers(p);
            Enable(p,false);Assert(MarkerCount(p)==0);
        });
        Run("独立字典保留明确关闭和未知键，完整恢复覆盖宿主", () =>
        {
            var p=new Pawn(); Enable(p); var snapshot=new SSCAbilityToggleState(); snapshot.Set(Infestation,false);snapshot.Set("SSC_FutureAbility",true);
            SSCAbilityToggleUtility.RestorePreference(p,snapshot);snapshot.Set(Infestation,true);
            Assert(!SSCAbilityToggleUtility.GetEnabled(p,Infestation) && p.Training.abilityToggles.Get("SSC_FutureAbility") && MarkerCount(p)==0);
        });
        Run("第二个性奴能力共用框架，角色规则/默认值独立，无需主人专用入口", () =>
        {
            const string id="SSC_TestSlaveToggle";
            SSCAbilityToggleUtility.Register(new SSCAbilityToggleDefinition(id,SSCAbilityToggleGroup.Slave,PawnIdentity.Slave,
                "test","test",defaultEnabled:true,activationRule:p=>!p.Downed));
            var p=new Pawn();Assert(!SSCAbilityToggleUtility.IsActive(p,id));p.Training.pawnIdentity=PawnIdentity.Slave;
            Assert(!SSCAbilityToggleUtility.GetEnabled(null,id));
            Assert(SSCAbilityToggleUtility.IsActive(p,id) && SSCAbilityToggleUtility.CanEdit(p,id));
            var tab=Tab();Assert(tab.DrawToggleGroup(p,SSCAbilityToggleGroup.Slave,120)>0 && Widgets.Items.Any(i=>i.Kind=="checkbox"));
            Assert(tab.DrawToggleGroup(p,SSCAbilityToggleGroup.Master,120)>0);
            p.Downed=true;Assert(!SSCAbilityToggleUtility.IsActive(p,id) && SSCAbilityToggleUtility.CanEdit(p,id));
            Assert(SSCAbilityToggleUtility.TrySetEnabled(p,id,false,p.Training,SSCPersonalityRuntimeUtility.Capture(p)));
            p.Downed=false;Assert(!SSCAbilityToggleUtility.IsActive(p,id));
        });
        Run("加载阶段拒绝点击与健康同步", () =>
        {
            var p=new Pawn(); Scribe.mode=LoadSaveMode.LoadingVars;
            Assert(!SSCAbilityToggleUtility.TrySetEnabled(p,Infestation,true,p.Training,SSCPersonalityRuntimeUtility.Capture(p)));
            p.Training.abilityToggles.Set(Infestation,true);SSCAbilityToggleUtility.SyncMarkers(p);Assert(MarkerCount(p)==0);
            Scribe.mode=LoadSaveMode.Inactive;SSCAbilityToggleUtility.SyncMarkers(p);Assert(MarkerCount(p)==1);
        });
        Run("同人格标记存在也不反推开启，未知标记清理",()=>
        {
            var p=new Pawn();p.health.AddHediff(HediffMaker.MakeHediff(marker,p));
            p.health.AddHediff(new Hediff_SSCToggleAbilityMarker{pawn=p,def=new HediffDef{defName="SSC_UnregisteredMarker",hediffClass=typeof(Hediff_SSCToggleAbilityMarker)}});
            SSCAbilityToggleUtility.SyncMarkers(p);Assert(MarkerCount(p)==0 && !p.Training.abilityToggles.HasEntries);
        });
        Run("真实复选框迟到点击不能改同组件换入的人格",()=>
        {
            var p=new Pawn();Enable(p);Widgets.Click=true;
            Widgets.BeforeCheckbox=()=>
            {
                if(Widgets.Items.Last().Text!="SSC_AbilityToggle_MasterDesireInfestation_Label".Translate())return;
                Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(p,out var scope));
                using(scope) SSCAbilityToggleUtility.RestorePreference(p,p.Training.abilityToggles);
            };
            Tab().Draw(p);Assert(SSCAbilityToggleUtility.GetEnabled(p,Infestation),"旧点击覆盖了新人格");
        });
    }
}
