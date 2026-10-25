// AI 辅助生成：执行原生复制通知与真实 SSC 新生/成熟入口，不用手动重置代替接线测试。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>验证新 Comp 默认、已安装克隆准备钩子，以及成熟/读档保留克隆自己的后续选择。</summary>
    private static void Batch4CloneTests()
    {
        var boundary = new Harmony("ssc.tests.romance.batch4.clone");
        boundary.CreateClassProcessor(production.GetType("SexSlaveCraft.SSCRestrictionCloneHook")).Patch();
        boundary.Patch(AccessTools.Method(typeof(Pawn_NeedsTracker), nameof(Pawn_NeedsTracker.AddOrRemoveNeedsAsAppropriate)), prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        try
        {
            Check("batch4 native duplicate notification keeps fresh Comp defaults without copying source command", () =>
            {
                CommandReset(); var (source, target) = CommandPair(SSCRomanceAction.Romance); Comp(source).romanceConfig = Denied();
                Require(SSCRomanceCommandUtility.TrySubmit(SSCRomanceCommandUtility.Capture(source, target), out var request, out _));
                var fresh = SocialPawn(); fresh.Notify_DuplicatedFrom(source);
                Require(Comp(fresh).romanceConfig == null); Require(!Comp(source).romanceConfig.IsDefault);
                Equal(SSCRomanceCommandStatus.Pending, request.Status); Require(fresh.jobs == null || fresh.CurJob == null);
                Require(!SSCRomanceLifecycle.TryAuthorize(fresh, target, SSCRomanceAction.Romance, out _));
            });
            Check("batch4 real newborn preparation hook resets romance and preserves original binding contract", () =>
            {
                CommandReset(); var source = SocialSlave(); Comp(source).romanceConfig = Denied();
                var clone = SocialSlave(SSCBondUtility.GetBoundMaster(source)); Comp(clone).romanceConfig = Denied();
                var sourceConfig = Comp(source).romanceConfig; var chain = SSCBondUtility.GetChain(clone);
                object token = SSCPersonalityRuntimeUtility.Capture(clone);
                AccessTools.Method(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody").Invoke(null, new object[] { source, clone });
                Require(Comp(clone).romanceConfig == null); Require(ReferenceEquals(sourceConfig, Comp(source).romanceConfig));
                Equal(chain, SSCBondUtility.GetChain(clone)); Equal(chain.target, SSCBondUtility.GetBoundMaster(source));
                Require(!SSCPersonalityRuntimeUtility.IsCurrent(clone, token)); Require(!SSCPersonalityRuntimeUtility.IsMutating(clone));
                Require(clone.ageTracker.AgeBiologicalYearsFloat < 16);
            });
            Check("batch4 newborn exception resets copied unknown format without modifying source payload", () =>
            {
                CommandReset(); var source = SocialSlave(); Comp(source).romanceConfig = Denied(); Comp(source).romanceConfig.version = 99;
                var clone = SocialSlave(); Comp(clone).romanceConfig = Comp(source).romanceConfig.Copy();
                AccessTools.Method(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody").Invoke(null, new object[] { source, clone });
                Require(Comp(clone).romanceConfig == null); Equal(99, Comp(source).romanceConfig.version);
                foreach (var setting in Enum.GetValues<SSCRomanceSetting>())
                    Equal(setting <= SSCRomanceSetting.AcceptProposal || setting == SSCRomanceSetting.InitiateToSexSlave, SSCRomancePreferences.GetView(clone, setting).Saved);
            });
            Check("batch4 actual maturation and actual Scribe keep clone later preference independently", () =>
            {
                CommandReset(); var source = SocialSlave(); Comp(source).romanceConfig = Denied();
                var clone = SocialSlave();
                AccessTools.Method(typeof(RabbitCloneUtility), "PrepareFreshRabbitCloneBody").Invoke(null, new object[] { source, clone });
                foreach (var setting in new[] { SSCRomanceSetting.AcceptProposal, SSCRomanceSetting.InitiateOnlyMaster })
                {
                    var view = SSCRomancePreferences.GetView(clone, setting);
                    Require(SSCRomancePreferences.TrySet(clone, setting, setting == SSCRomanceSetting.InitiateOnlyMaster, view.Context, out _));
                }
                var own = Comp(clone).romanceConfig;
                var link = new HediffComp_RabbitCloneLink();
                RabbitCloneUtility.MatureRabbitClone(clone, source, link); RabbitCloneUtility.MatureRabbitClone(clone, source, link);
                Require(link.mature); Require(ReferenceEquals(own, Comp(clone).romanceConfig));
                string path = Save(new RomanceFixture(Comp(clone))); var restored = SocialSlave(); Load(path, Comp(restored));
                SSCRomanceLifecycle.InitializeAfterLoad(restored);
                Require(!Comp(restored).romanceConfig.allowAcceptProposal && Comp(restored).romanceConfig.initiateOnlyMaster);
                Require(!Comp(restored).romanceConfig.alwaysAcceptMaster); Require(Comp(source).romanceConfig.alwaysAcceptMaster);
            });
        }
        finally { boundary.UnpatchAll(boundary.Id); }
    }
}
