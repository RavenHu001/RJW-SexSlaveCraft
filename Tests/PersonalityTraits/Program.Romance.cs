// AI 辅助生成：执行真实提取／加工／植入及浪漫数据处理；授权事件边界由原生专项另验。
#if SSC_RW17
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>在既有模型宿主执行生产人格链路，覆盖采集、清源、加工、植入及失败保护。</summary>
    private static void RunRomanceCases()
    {
        Run("浪漫配置经完整提取清源、凝胶加工及植入精确恢复", () =>
        {
            var source = RomanceBody("RomanceSource"); var training = source.TryGetComp<CompSexSlaveTraining>();
            training.romanceConfig = RomanceDenied(); ExcretionUtility.DoExcretion(source);
            var captured = GenSpawn.LastSpawned.TryGetComp<CompPersonalityStore>();
            Assert(training.romanceConfig == null, "Successful extraction clears the original preference.");
            Assert(captured.romanceConfig != null && !captured.romanceConfig.allowAcceptProposal && captured.romanceConfig.initiateOnlyMaster, "Capture keeps dormant explicit values.");
            var processed = Gel(); processed.CopyFrom(captured);
            var host = RomanceBody("RomanceHost"); host.TryGetComp<CompSexSlaveTraining>().romanceConfig = new SSCRomanceConfig();
            Assert(ExcretionUtility.InheritEverything(host, processed), "Insertion must succeed.");
            var restored = host.TryGetComp<CompSexSlaveTraining>().romanceConfig;
            Assert(!restored.allowAcceptProposal && restored.initiateOnlyMaster && restored.alwaysAcceptMaster, "Source preference replaces host default.");
            restored.allowAcceptProposal = true;
            Assert(!captured.romanceConfig.allowAcceptProposal && !processed.romanceConfig.allowAcceptProposal, "All three mutable snapshots remain independent.");
        });
        Run("旧凝胶缺浪漫字段精确覆盖宿主旧人格选择", () =>
        {
            var source = RomanceBody("OldRomanceSource"); var gel = Gel(); gel.StorePawnData(source);
            var host = RomanceBody("OldRomanceHost"); host.TryGetComp<CompSexSlaveTraining>().romanceConfig = RomanceDenied();
            Assert(ExcretionUtility.InheritEverything(host, gel), "Old gel insertion succeeds.");
            Assert(host.TryGetComp<CompSexSlaveTraining>().romanceConfig == null, "Missing source preference means defaults, never host residue.");
        });
        Run("未知浪漫快照在完整植入入口修改宿主前拒绝", () =>
        {
            var source = RomanceBody("UnknownRomanceSource"); var gel = Gel(); gel.StorePawnData(source); gel.romanceConfig = RomanceDenied(); gel.romanceConfig.version = 99;
            var host = RomanceBody("UnchangedRomanceHost"); var old = host.TryGetComp<CompSexSlaveTraining>().romanceConfig = new SSCRomanceConfig();
            Assert(!ExcretionUtility.InheritEverything(host, gel), "Unknown snapshot is rejected.");
            Assert(host.LabelShort == "UnchangedRomanceHost" && !gel.parent.Destroyed && ReferenceEquals(old, host.TryGetComp<CompSexSlaveTraining>().romanceConfig), "Preflight preserves host and source carrier.");
            Assert(Log.Errors.Count == 1, "Expected a diagnostic."); Log.Errors.Clear();
        });
        Run("无人格载体时拒绝丢失非默认浪漫选择", () =>
        {
            var source = Body("NoCarrierSource"); var gel = Gel(); gel.StorePawnData(source); gel.romanceConfig = RomanceDenied();
            var host = Body("NoCarrierHost"); host.Comps.Remove(typeof(CompSexSlaveTraining));
            Assert(!ExcretionUtility.InheritEverything(host, gel) && !gel.parent.Destroyed && host.LabelShort == "NoCarrierHost", "No carrier rejects before any destructive change.");
            Assert(Log.Errors.Count == 1, "Expected a diagnostic."); Log.Errors.Clear();
        });
        Run("凝胶生成失败不提前清除来源浪漫选择", () =>
        {
            var source = RomanceBody("FailedRomanceExtraction"); var original = source.TryGetComp<CompSexSlaveTraining>().romanceConfig = RomanceDenied();
            try { GenSpawn.FailSpawn = true; ExcretionUtility.DoExcretion(source); Assert(false, "Expected fixture spawn failure."); }
            catch (System.InvalidOperationException) { }
            finally { GenSpawn.FailSpawn = false; }
            Assert(ReferenceEquals(original, source.TryGetComp<CompSexSlaveTraining>().romanceConfig), "Source is cleared only after successful spawn.");
            Assert(!SSCPersonalityRuntimeUtility.IsMutating(source), "Failure releases the existing personality scope.");
        });
        Run("未知来源浪漫版本在提取开始前拒绝且不清源", () =>
        {
            var source = RomanceBody("UnknownRomanceExtraction"); var original = source.TryGetComp<CompSexSlaveTraining>().romanceConfig = RomanceDenied(); original.version = 77;
            var previous = GenSpawn.LastSpawned; ExcretionUtility.DoExcretion(source);
            Assert(ReferenceEquals(original, source.TryGetComp<CompSexSlaveTraining>().romanceConfig) && ReferenceEquals(previous, GenSpawn.LastSpawned), "Unknown source is not stripped or spawned.");
            Assert(Log.Errors.Count == 1, "Expected a diagnostic."); Log.Errors.Clear();
        });
    }
    /// <summary>创建七项非默认偏好，检测迁移时丢弃 false 或错误套用克隆默认。</summary>
    private static SSCRomanceConfig RomanceDenied() => new SSCRomanceConfig { allowInitiateRomance = false, allowInitiateProposal = false,
        allowInitiateBreakup = false, allowAcceptRomance = false, allowAcceptProposal = false, initiateOnlyMaster = true, alwaysAcceptMaster = true };
    /// <summary>沿用既有身体夹具并显式提供训练组件，不将缺组件误当成迁移失败。</summary>
    private static Pawn RomanceBody(string name)
    { var pawn = Body(name); MasterTraining(pawn, PawnIdentity.Slave, false); return pawn; }
}
namespace SexSlaveCraft
{
    // 此宿主不测试命令：仅提供外部授权通知的编译边界，规则、存档和流转使用上述生产源码。
    public static class SSCRomanceLifecycle
    {
        /// <summary>仅为模型提供外部撤权通知边界，持久世代的业务由原生专项测试。</summary>
        public static void Invalidate(Pawn pawn) { }
        /// <summary>第三批同属外部命令撤权通知边界，目标人格取消/Job 去重由原生专项执行真实生产实现。</summary>
        public static void InvalidatePersonality(Pawn pawn) { }
        /// <summary>仅为模型提供外部最终协调边界，不在此复制原生生命周期判定。</summary>
        public static void Notify(Pawn pawn) { }
    }
}
#endif
