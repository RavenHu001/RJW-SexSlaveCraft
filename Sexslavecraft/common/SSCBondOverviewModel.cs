// AI 辅助生成
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    public enum SSCBondOverviewQueryState { Ready, Unavailable, InvalidSource }
    public enum SSCBondOverviewMasterState { Unavailable, NoChain, InvalidTarget, Bound }
    public enum SSCBondOverviewDirection { Master, Slave }
    public enum SSCBondOverviewTrainerState { Unavailable, None, Active, Inactive, InvalidTarget }
    public enum SSCBondOverviewLocationKind { Unknown, Map, MapContainer, Caravan, Transport, WorldObject, WorldTile }

    /// <summary>只保存位置事实；是否可以跳转以及实际跳转目标由导航入口实时判断。</summary>
    public sealed class SSCBondOverviewLocation
    {
        public SSCBondOverviewLocationKind Kind { get; }
        public Map Map { get; }
        public Thing Subject { get; }
        public Thing Container { get; }
        public WorldObject WorldObject { get; }

        internal SSCBondOverviewLocation(SSCBondOverviewLocationKind kind, Thing subject,
            Map map = null, Thing container = null, WorldObject worldObject = null)
        {
            Kind = kind;
            Subject = subject;
            Map = map;
            Container = container;
            WorldObject = worldObject;
        }
    }

    /// <summary>本次查询的一条直接绑定；保留两端引用，供后续界面在操作前复查。</summary>
    public sealed class SSCBondOverviewEntry
    {
        public Pawn Pawn { get; }
        public Pawn Slave { get; }
        public Pawn Master { get; }
        public SSCBondOverviewDirection Direction { get; }
        public string Name { get; }
        public int StableId { get; }
        public int ChainStage { get; }
        public bool IsDead { get; }
        public SexSlaveSpecializationType? Specialization { get; }
        public SSCBondOverviewLocation Location { get; }

        internal SSCBondOverviewEntry(Pawn source, Pawn related, SSCBondOverviewDirection direction,
            Hediff_ChainOfSexSlave chain, SSCBondOverviewLocation location)
        {
            Pawn = related;
            Direction = direction;
            Slave = direction == SSCBondOverviewDirection.Master ? source : related;
            Master = direction == SSCBondOverviewDirection.Master ? related : source;
            // 显示字段在构建时捕获，排序和绘制无需重复读取角色状态；两端引用仅用于后续复查。
            // 这不是实时对象代理，角色改名、死亡或移动后的显示更新需要重新构建快照。
            Name = related.LabelShort ?? string.Empty;
            StableId = related.thingIDNumber;
            ChainStage = SSCIdentityUtility.GetSexSlaveStageFromChain(chain);
            IsDead = related.Dead;
            Specialization = related.TryGetComp<CompSexSlaveTraining>()?.specializationType;
            Location = location;
        }
    }

    /// <summary>属于一次游戏与查询的运行时视图，不参与 Scribe 或人格保存。</summary>
    public sealed class SSCBondOverviewSnapshot
    {
        public Game Game { get; }
        public Pawn Source { get; }
        public Map ViewedMap { get; }
        public SSCBondOverviewQueryState State { get; }
        public PawnIdentity Identity { get; }
        public SSCBondOverviewMasterState MasterState { get; }
        public SSCBondOverviewEntry Master { get; }
        public Pawn AssignedTrainer { get; }
        public SSCBondOverviewLocation AssignedTrainerLocation { get; }
        public SSCBondOverviewTrainerState AssignedTrainerState { get; }
        public IReadOnlyList<SSCBondOverviewEntry> Slaves { get; }
        public int LivingSlaveCount { get; }
        public int TotalSlaveCount => Slaves.Count;
        private readonly HashSet<SSCBondOverviewEntry> slaveEntries;

        internal SSCBondOverviewSnapshot(Game game, Pawn source, Map viewedMap,
            SSCBondOverviewQueryState state, SSCBondOverviewMasterState masterState,
            SSCBondOverviewEntry master, List<SSCBondOverviewEntry> slaves, SSCBondOverviewLocation trainerLocation = null)
        {
            Game = game;
            Source = source;
            ViewedMap = viewedMap;
            State = state;
            Identity = source?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset;
            MasterState = masterState;
            Master = master;
            // 指派状态是独立的次级信息：保留原始指定者，再用既有只读规则判定是否生效。
            // 指定者失效时不清空字段，也不将其当成锁链主人，更不在查询中整理身份或指派。
            AssignedTrainer = source?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            AssignedTrainerLocation = trainerLocation;
            AssignedTrainerState = state != SSCBondOverviewQueryState.Ready ? SSCBondOverviewTrainerState.Unavailable
                : AssignedTrainer == null ? SSCBondOverviewTrainerState.None
                : !SSCBondOverviewUtility.IsValidPawn(AssignedTrainer) || ReferenceEquals(source, AssignedTrainer)
                    ? SSCBondOverviewTrainerState.InvalidTarget
                    : ReferenceEquals(TrainerAssignmentUtility.GetActiveAssignedTrainer(source), AssignedTrainer)
                        ? SSCBondOverviewTrainerState.Active : SSCBondOverviewTrainerState.Inactive;
            // 接收查询器新建列表的所有权；外部只取得包装，不再复制同一份行引用。
            // 调用方交出列表后不得继续改写；列表不属于可复用上下文，下一次查询也不会清空它。
            // 空结果无需行索引；非空结果的引用集合只用于成员校验，不跨快照复用。
            if (slaves == null || slaves.Count == 0)
            {
                Slaves = Array.Empty<SSCBondOverviewEntry>();
                return;
            }
            Slaves = new ReadOnlyCollection<SSCBondOverviewEntry>(slaves);
            slaveEntries = new HashSet<SSCBondOverviewEntry>(slaves, SSCBondOverviewReferenceComparer<SSCBondOverviewEntry>.Instance);
            foreach (SSCBondOverviewEntry entry in Slaves)
                if (!entry.IsDead) LivingSlaveCount++;
        }

        // 行复查不扫描整份列表，避免逐行绘制时形成二次复杂度。
        internal bool Contains(SSCBondOverviewEntry entry) => ReferenceEquals(Master, entry) || slaveEntries?.Contains(entry) == true;
    }
}
