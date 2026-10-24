// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    public partial class CompSexSlaveTraining
    {
        public SSCRomanceConfig romanceConfig;
        // 授权归属当前执行载体，不进入人格／凝胶快照；缺字段旧档没有可续行的旧命令。
        internal string romanceAuthorizationId = Guid.NewGuid().ToString("N");
        // 以下为进程内观察缓存，不参与人格快照或存档，完整读档后从最终绑定重建。
        internal bool romanceObserved;
        internal Hediff_ChainOfSexSlave romanceObservedChain;
        internal Pawn romanceObservedMaster;
        internal PawnIdentity romanceObservedIdentity;
        internal int romanceObservedStage;
        internal bool romanceObservedHollow;

        /// <summary>偏好与授权分节点保存，加载不旋转已有凭据；观察缓存等完整初始化后建立。</summary>
        public void ExposeRomance()
        {
            Scribe_Deep.Look(ref romanceConfig, "sscRomanceConfig");
            Scribe_Values.Look(ref romanceAuthorizationId, "sscRomanceAuthorizationId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 只给没有凭据的旧档补新世代；每次读档重置已有世代会误取消合法在途命令。
                if (string.IsNullOrEmpty(romanceAuthorizationId)) romanceAuthorizationId = Guid.NewGuid().ToString("N");
                romanceObserved = false;
            }
        }
    }

    public partial class CompPersonalityStore
    {
        public SSCRomanceConfig romanceConfig;
        /// <summary>凝胶只保存人格选择，不保存来源身体的授权世代或观察缓存。</summary>
        public void ExposeRomanceSnapshot() => Scribe_Deep.Look(ref romanceConfig, "sscRomanceConfig");
        /// <summary>采集原始偏好副本；来源缺字段也覆盖复用凝胶上的旧快照，不保存命令。</summary>
        public void CaptureRomance(Pawn pawn) => romanceConfig = pawn?.TryGetComp<CompSexSlaveTraining>()?.romanceConfig?.Copy();
        /// <summary>加工复制独立配置；来源缺配置也清掉目标旧快照，不应用新生克隆的重置例外。</summary>
        public void CopyRomanceSnapshot(CompPersonalityStore source) => romanceConfig = source?.romanceConfig?.Copy();
    }
}
