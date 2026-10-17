// AI 辅助生成
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
        public SSCBondOverviewTrainerState AssignedTrainerState { get; }
        public IReadOnlyList<SSCBondOverviewEntry> Slaves { get; }
        public int LivingSlaveCount { get; }
        public int TotalSlaveCount => Slaves.Count;
        private readonly HashSet<SSCBondOverviewEntry> slaveEntries;

        internal SSCBondOverviewSnapshot(Game game, Pawn source, Map viewedMap,
            SSCBondOverviewQueryState state, SSCBondOverviewMasterState masterState,
            SSCBondOverviewEntry master, List<SSCBondOverviewEntry> slaves)
        {
            Game = game;
            Source = source;
            ViewedMap = viewedMap;
            State = state;
            Identity = source?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset;
            MasterState = masterState;
            Master = master;
            AssignedTrainer = source?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
            AssignedTrainerState = state != SSCBondOverviewQueryState.Ready ? SSCBondOverviewTrainerState.Unavailable
                : AssignedTrainer == null ? SSCBondOverviewTrainerState.None
                : !SSCBondOverviewUtility.IsValidPawn(AssignedTrainer) || ReferenceEquals(source, AssignedTrainer)
                    ? SSCBondOverviewTrainerState.InvalidTarget
                    : ReferenceEquals(TrainerAssignmentUtility.GetActiveAssignedTrainer(source), AssignedTrainer)
                        ? SSCBondOverviewTrainerState.Active : SSCBondOverviewTrainerState.Inactive;
            // 对私有副本加只读包装，调用者不能通过集合接口改写本次人数或顺序。
            Slaves = new ReadOnlyCollection<SSCBondOverviewEntry>(new List<SSCBondOverviewEntry>(slaves));
            slaveEntries = new HashSet<SSCBondOverviewEntry>(slaves, SSCBondOverviewReferenceComparer<SSCBondOverviewEntry>.Instance);
            foreach (SSCBondOverviewEntry entry in Slaves)
                if (!entry.IsDead) LivingSlaveCount++;
        }

        // 行复查不扫描整份列表，避免逐行绘制时形成二次复杂度。
        internal bool Contains(SSCBondOverviewEntry entry) => ReferenceEquals(Master, entry) || slaveEntries.Contains(entry);
    }
}
