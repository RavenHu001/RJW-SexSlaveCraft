// AI 辅助生成
using System;
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private const string ToggleId = SSCAbilityToggleUtility.MasterDesireInfestation;
    private static void RunAbilityToggleTransferCases()
    {
        var marker=new HediffDef{defName=SSCAbilityToggleUtility.InfestationMarker,hediffClass=typeof(Hediff_SSCToggleAbilityMarker),maxSeverity=1};
        DefDatabase<HediffDef>.AllDefs.Add(marker);
        foreach(bool sourceOn in new[]{false,true}) foreach(bool hostOn in new[]{false,true})
            Run($"个人能力来源/宿主开关精确替换 source={sourceOn} host={hostOn}",()=>
            {
                var source=Body("Source");var sourceComp=MasterTraining(source,PawnIdentity.Master,false);
                sourceComp.abilityToggles.Set(ToggleId,sourceOn);
                var oldToken=SSCPersonalityRuntimeUtility.Capture(source);
                var gel=Excrete(source);
                Assert(gel.abilityToggles.Get(ToggleId)==sourceOn && !sourceComp.abilityToggles.HasEntries,"保存后清源");
                Assert(!SSCPersonalityRuntimeUtility.IsCurrent(source,oldToken),"旧归属失效");
                var host=Body("Host");var hostComp=MasterTraining(host,PawnIdentity.Master,false);hostComp.abilityToggles.Set(ToggleId,hostOn);
                Assert(ExcretionUtility.InheritEverything(host,gel),"植入成功");
                Assert(SSCAbilityToggleUtility.GetEnabled(host,ToggleId)==sourceOn,"精确恢复，包括false");
                Assert(host.health.hediffSet.hediffs.Count(h=>SSCAbilityToggleUtility.IsMarkerDef(h.def))==(sourceOn?1:0),"恢复后标记");
            });
        Run("个人能力凝胶保存/复制不共享字典，缺字段旧凝胶关闭",()=>
        {
            var gel=Gel();gel.abilityToggles.Set(ToggleId,true);gel.abilityToggles.Set("SSC_Future",false);
            Scribe.mode=LoadSaveMode.Saving;gel.PostExposeData();var loaded=Gel();
            Scribe.mode=LoadSaveMode.LoadingVars;loaded.PostExposeData();Scribe.mode=LoadSaveMode.Inactive;
            var copy=Gel();copy.CopyFrom(loaded);loaded.abilityToggles.Set(ToggleId,false);
            Assert(copy.abilityToggles.Get(ToggleId) && !copy.abilityToggles.Get("SSC_Future",true),"独立快照与明确关闭");
            Scribe.Values.Remove("sscAbilityToggles");Scribe.mode=LoadSaveMode.LoadingVars;var legacy=Gel();legacy.PostExposeData();
            Scribe.mode=LoadSaveMode.Inactive;Assert(legacy.abilityToggles==null,"缺字段保持空配置");
            var host=Body("Host");MasterTraining(host,PawnIdentity.Master,false).abilityToggles.Set(ToggleId,true);
            Assert(ExcretionUtility.InheritEverything(host,legacy) && !SSCAbilityToggleUtility.GetEnabled(host,ToggleId),"旧来源覆盖宿主开启");
        });
        Run("通用开关原生字段契约保存false与未知键，PostLoadInit修复空字典",()=>
        {
            var state=new SSCAbilityToggleState();state.Set(ToggleId,false);state.Set("SSC_Future",true);
            Scribe.mode=LoadSaveMode.Saving;state.ExposeData();var loaded=new SSCAbilityToggleState();
            Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
            Assert(!loaded.Get(ToggleId,true) && loaded.Get("SSC_Future"),"字典存档契约");
            Scribe.Values.Clear();Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;loaded.ExposeData();
            Scribe.mode=LoadSaveMode.Inactive;Assert(!loaded.HasEntries,"旧档空字典");
        });
        Run("非主人来源保存开启偏好，但植入不显示侵染标记",()=>
        {
            var source=Body("Source");MasterTraining(source,PawnIdentity.Unset,false).abilityToggles.Set(ToggleId,true);
            var gel=Excrete(source);var host=Body("Host");MasterTraining(host,PawnIdentity.Master,false);
            Assert(ExcretionUtility.InheritEverything(host,gel),"植入");
            Assert(SSCAbilityToggleUtility.GetEnabled(host,ToggleId) && !SSCAbilityToggleUtility.IsActive(host,ToggleId),"偏好与生效分离");
        });
        Run("侵染标记通用标签不得绕过关闭快照，复制与恢复均过滤",()=>
        {
            var gel=Gel();gel.sscIdentity=PawnIdentity.Master;gel.hediffTags[marker]=1;
            var copy=Gel();copy.CopyFrom(gel);Assert(!copy.hediffTags.ContainsKey(marker),"复制过滤");
            var host=Body("Host");MasterTraining(host,PawnIdentity.Master,false);
            Assert(ExcretionUtility.InheritEverything(host,gel) && !host.health.hediffSet.HasHediff(marker),"直接旧标签恢复也过滤");
        });
        Run("个人能力预检失败不清空壳、不消耗凝胶、不更换人格token",()=>
        {
            var host=Body("Unsupported");host.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
            object token=SSCPersonalityRuntimeUtility.Capture(host);var gel=Gel();gel.abilityToggles.Set(ToggleId,true);
            Assert(!ExcretionUtility.InheritEverything(host,gel),"缺组件预检拒绝");
            Assert(!gel.parent.Destroyed && host.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done) &&
                SSCPersonalityRuntimeUtility.IsCurrent(host,token),"未进入修改阶段");
            Assert(Log.Errors.Count==1,"记录失败");Log.Errors.Clear();
        });
        Run("植入作用域内不能重入替换，清理后重新取得稳定token",()=>
        {
            var host=Body("Host");MasterTraining(host,PawnIdentity.Master,false);
            Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(host,out var scope),"进入流转");
            using(scope) Assert(!ExcretionUtility.InheritEverything(host,Gel()),"拒绝外部重入");
            Assert(!SSCPersonalityRuntimeUtility.IsMutating(host),"作用域释放");
        });
        Run("凝胶生成异常不提前清除偏好，人格作用域仍释放",()=>
        {
            var source=Body("Source");var comp=MasterTraining(source,PawnIdentity.Master,false);comp.abilityToggles.Set(ToggleId,true);
            GenSpawn.FailSpawn=true;
            try
            {
                bool failed=false;
                try { ExcretionUtility.DoExcretion(source); } catch(InvalidOperationException){failed=true;}
                Assert(failed && comp.abilityToggles.Get(ToggleId) && !SSCPersonalityRuntimeUtility.IsMutating(source),"清源边界与异常释放");
            }
            finally { GenSpawn.FailSpawn=false; }
        });
    }
}
