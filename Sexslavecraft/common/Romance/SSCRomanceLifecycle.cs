// AI 辅助生成
using System;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>局部事件维护授权世代；不修改偏好、关系或调教员，不扫描全世界。</summary>
    public static class SSCRomanceLifecycle
    {
        /// <summary>撤销当前执行载体的旧授权；即使后来恢复原主人或阶段也不能取得旧世代。</summary>
        public static void Invalidate(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || Scribe.mode != LoadSaveMode.Inactive) return;
            comp.romanceAuthorizationId = Guid.NewGuid().ToString("N");
            SSCRomanceCommandGameComponent.Current?.Invalidate(pawn);
        }
        /// <summary>人格替换开始即撤销本人授权，并取消指向旧目标人格的请求；正常读档不调用。</summary>
        public static void InvalidatePersonality(Pawn pawn)
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
            Invalidate(pawn);
            SSCRomanceCommandGameComponent.Current?.Invalidate(pawn, targetPersonality: true);
        }
        /// <summary>外部空壳事件同时撤销本人及指向旧人格的命令；普通人格流程已在替换开始时撤权。</summary>
        public static void NotifyHollowChanged(Pawn pawn)
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
            if (SSCPersonalityRuntimeUtility.ObserveHollow(pawn, SSCMasterProgressionUtility.IsHollow(pawn)))
                InvalidatePersonality(pawn);
            Notify(pawn);
        }
        /// <summary>完整读档后建立已有空壳基线，不把保存中的目标人格当成一次新提取。</summary>
        public static void InitializeAfterLoad(Pawn pawn)
        {
            if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
            SSCPersonalityRuntimeUtility.ObserveHollow(pawn, SSCMasterProgressionUtility.IsHollow(pawn), baseline: true);
            Notify(pawn);
        }
        /// <summary>运行事件比较真实链／主人和阶段；加载期不观察半恢复引用。</summary>
        public static void Notify(Pawn pawn)
        {
            var comp = pawn?.TryGetComp<CompSexSlaveTraining>();
            if (comp == null || Scribe.mode != LoadSaveMode.Inactive) return;
            var chain = SSCBondUtility.GetChain(pawn);
            var master = chain?.LinkedPawn;
            int stage = SSCIdentityUtility.GetSexSlaveStageFromChain(chain);
            bool hollow = SSCMasterProgressionUtility.IsHollow(pawn);
            // 健康人格首次观察仅建立基线；空壳首次出现也必须撤权，不能在移除标记后恢复旧请求。
            // 后续失去链、换主、退出身份或降出第四阶段同样旋转世代。
            // 不延后撤销事件，避免同一帧解绑重绑后被误判为绑定从未变化。
            if ((hollow && (!comp.romanceObserved || !comp.romanceObservedHollow)) ||
                (comp.romanceObserved && (chain != comp.romanceObservedChain || master != comp.romanceObservedMaster ||
                (comp.romanceObservedIdentity == PawnIdentity.Slave && comp.pawnIdentity != PawnIdentity.Slave) ||
                (comp.romanceObservedStage >= 4 && stage < 4)))) Invalidate(pawn);
            comp.romanceObserved = true;
            comp.romanceObservedChain = chain; comp.romanceObservedMaster = master;
            comp.romanceObservedStage = stage; comp.romanceObservedIdentity = comp.pawnIdentity;
            comp.romanceObservedHollow = hollow;
        }
        /// <summary>只用于新个体初始化；恢复模块默认，不搬运旧模板或重置其他后端。</summary>
        public static void ResetNewClone(Pawn pawn)
        {
            // 清配置代表回模块默认，不是将各项统一写 false；成熟与正常读档不得调用此入口。
            SSCRomancePreferences.Clear(pawn);
            Invalidate(pawn);
            Notify(pawn);
        }
        /// <summary>取得第四阶段授权快照；具体关系、距离及任务条件由后续命令层另行验证。</summary>
        public static bool TryAuthorize(Pawn actor, Pawn target, SSCRomanceAction action, out SSCRomanceAuthorization authorization)
        {
            authorization = null;
            var state = SSCRomanceResolver.Resolve(actor);
            if (!state.Applicable || state.Stage < 4 || state.Chain.loadID < 0 || actor.Dead || target == null || target == actor || target.Dead || target.Destroyed || target.Discarded ||
                action < SSCRomanceAction.Romance || action > SSCRomanceAction.Breakup) return false;
            Notify(actor);
            // 授权创建属于写入事件，可先对账世代；普通策略与 UI 读取不能调用它来修复状态。
            authorization = new SSCRomanceAuthorization(actor, target, state.Master, action, state.Comp);
            return true;
        }
        /// <summary>校验保存凭据并重建运行时授权；原 Job 归属及去重仍由任务层负责，不能据此迁移人格授权。</summary>
        public static bool TryResume(SSCRomanceCommandStamp stamp, out SSCRomanceAuthorization authorization)
        {
            authorization = null;
            // 以当前合法状态重建运行时凭据，再比较保存的主人、世代及原锁链存档身份。
            // 运行时引用会在读档后重建，因此必须额外保存链身份，防止同主换链绕过观察后复活授权。
            if (stamp == null || string.IsNullOrEmpty(stamp.Epoch) || string.IsNullOrEmpty(stamp.ChainId) ||
                !TryAuthorize(stamp.Actor, stamp.Target, stamp.Action, out var current) ||
                stamp.Master != current.Stamp.Master || stamp.Epoch != current.Stamp.Epoch || stamp.ChainId != current.Stamp.ChainId) return false;
            authorization = current;
            return true;
        }
    }

    /// <summary>可保存的授权身份，不含已执行状态；Job 的领取／结果／去重由第三批任务层负责。</summary>
    public sealed class SSCRomanceCommandStamp : IExposable
    {
        private Pawn actor, target, master;
        private string epoch;
        private string chainId;
        private SSCRomanceAction action;
        public Pawn Actor => actor;
        public Pawn Target => target;
        public Pawn Master => master;
        public string Epoch => epoch;
        public string ChainId => chainId;
        public SSCRomanceAction Action => action;
        /// <summary>供 Scribe 创建空凭据；必须经过完整引用恢复和 TryResume 后才能取得授权。</summary>
        public SSCRomanceCommandStamp() { }
        /// <summary>固定本次角色、目标、行为及持久世代，不携带永久接受许可。</summary>
        internal SSCRomanceCommandStamp(Pawn actor, Pawn target, Pawn master, SSCRomanceAction action, string epoch, string chainId)
        { this.actor = actor; this.target = target; this.master = master; this.action = action; this.epoch = epoch; this.chainId = chainId; }
        /// <summary>保存授权身份引用；缺失行为读取为非法枚举，旧残缺凭据不能默认变成求爱命令。</summary>
        public void ExposeData()
        {
            Scribe_References.Look(ref actor, "actor", saveDestroyedThings: true);
            Scribe_References.Look(ref target, "target", saveDestroyedThings: true);
            Scribe_References.Look(ref master, "master", saveDestroyedThings: true);
            Scribe_Values.Look(ref epoch, "epoch");
            // 使用原版持久 ID，不持有可能已经离体且无法跨引用解析的健康实例。
            Scribe_Values.Look(ref chainId, "chainId");
            Scribe_Values.Look(ref action, "action", (SSCRomanceAction)(-1));
        }
    }
    /// <summary>当前进程的授权证明；持久 Stamp 与运行时载体、链和人格 token 必须同时匹配。</summary>
    public sealed class SSCRomanceAuthorization
    {
        public SSCRomanceCommandStamp Stamp { get; }
        private readonly CompSexSlaveTraining comp;
        private readonly Hediff_ChainOfSexSlave chain;
        private readonly object token;
        /// <summary>仅由生命周期入口创建，绑定当前载体实例和人格，不接受外部 bool 冒充命令授权。</summary>
        internal SSCRomanceAuthorization(Pawn actor, Pawn target, Pawn master, SSCRomanceAction action, CompSexSlaveTraining comp)
        {
            this.comp = comp; chain = SSCBondUtility.GetChain(actor);
            token = SSCPersonalityRuntimeUtility.Capture(actor);
            Stamp = new SSCRomanceCommandStamp(actor, target, master, action, comp.romanceAuthorizationId, chain.GetUniqueLoadID());
        }
        /// <summary>只读复查当前人格、同一组件、链和持久世代；此结果不保证互动成功。</summary>
        public bool IsCurrent(Pawn actor, Pawn target, SSCRomanceAction action)
        {
            var state = SSCRomanceResolver.Resolve(actor);
            return actor != null && actor == Stamp.Actor && target != null && target == Stamp.Target && action == Stamp.Action && !actor.Dead &&
                !target.Dead && !target.Destroyed && !target.Discarded && state.Applicable && state.Stage >= 4 &&
                state.Comp == comp && state.Chain == chain && state.Master == Stamp.Master &&
                chain.GetUniqueLoadID() == Stamp.ChainId &&
                comp.romanceAuthorizationId == Stamp.Epoch && SSCPersonalityRuntimeUtility.IsCurrent(actor, token);
        }
    }
}
