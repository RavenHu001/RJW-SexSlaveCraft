// AI 辅助生成。只保存当前游戏的短期显示状态；不参与 Scribe 或绑定维护。
using System;
using Verse;

namespace SexSlaveCraft
{
    internal sealed partial class SSCBondOverviewView
    {
        internal readonly Pawn Source;
        internal Map Map;
        internal SSCBondOverviewSnapshot Snapshot;
        internal bool Pending = true, Failed;
        internal int Revision;
        internal double NextLocalRefresh;
        internal bool LocalRetryDue(double now) => !Failed || now >= NextLocalRefresh;
        private object observedChain, observedMaster, observedTrainer, observedBridle, observedTargets;
        private int observedCount;
        private PawnIdentity observedIdentity;
        internal SSCBondOverviewView(Pawn source, Map map) { Source = source; Map = map; }

        internal bool ShowCard => Snapshot?.State == SSCBondOverviewQueryState.Ready
            ? Snapshot.Identity != PawnIdentity.Unset || Snapshot.Master != null || Snapshot.TotalSlaveCount > 0 || Snapshot.AssignedTrainer != null ||
                Snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget
            : (Source?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset) != PawnIdentity.Unset ||
                Source?.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer != null || SSCBondUtility.GetChain(Source) != null;

        internal void SetMap(Map map)
        {
            if (ReferenceEquals(Map, map)) return;
            Map = map; Pending = true; Changed();
        }
        // 常规 GUI 登记只比较直接字段和缰绳容器，不在每次重绘遍历从属或分配快照。
        // 等长原地替换、阶段/位置等变化由独立的两秒本地刷新兜底。
        internal bool DirectChanged(bool capture = false)
        {
            var comp = Source?.TryGetComp<CompSexSlaveTraining>();
            var chain = SSCBondUtility.GetChain(Source);
            var bridle = SSCBondUtility.GetBridle(Source);
            bool changed = Snapshot == null || !ReferenceEquals(Snapshot.ViewedMap, Map) ||
                !ReferenceEquals(observedChain, chain) || !ReferenceEquals(observedMaster, chain?.LinkedPawn) ||
                !ReferenceEquals(observedTrainer, comp?.selectedTrainer) ||
                observedIdentity != (comp?.pawnIdentity ?? PawnIdentity.Unset) || !ReferenceEquals(observedBridle, bridle) ||
                !ReferenceEquals(observedTargets, bridle?.targets) || observedCount != (bridle?.targets?.Count ?? 0);
            if (capture)
            {
                observedChain = chain; observedMaster = chain?.LinkedPawn; observedTrainer = comp?.selectedTrainer;
                observedIdentity = comp?.pawnIdentity ?? PawnIdentity.Unset; observedBridle = bridle;
                observedTargets = bridle?.targets; observedCount = bridle?.targets?.Count ?? 0;
            }
            return changed;
        }
        partial void ResetPresentation();
        internal void Changed() { Revision++; ResetPresentation(); }
        internal void Clear()
        {
            Snapshot = null; Pending = false;
            observedChain = observedMaster = observedTrainer = observedBridle = observedTargets = null;
            ResetPresentation();
        }
    }

    /// <summary>最多两个活跃视图；只读取直接关系，不调度地图、世界或货物候选扫描。</summary>
    internal sealed class SSCBondOverviewSession
    {
        internal readonly Game Owner;
        private readonly Func<double> clock;
        private readonly Func<Pawn, Map, SSCBondOverviewSnapshot> buildLocal;
        private readonly Action<Exception> report;
        internal SSCBondOverviewView Card { get; private set; }
        internal SSCBondOverviewView Details { get; private set; }
        private int cardFrame = int.MinValue;
        private double previousTime = double.NegativeInfinity, nextError;

        internal SSCBondOverviewSession(Game owner, Func<double> clock,
            Func<Pawn, Map, SSCBondOverviewSnapshot> build = null, Action<Exception> report = null)
        {
            // 注入点也只代表本地构建，供回归控制计时和异常；生产始终走 BuildLocal。
            Owner = owner; this.clock = clock; buildLocal = build ?? SSCBondOverviewUtility.BuildLocal; this.report = report;
        }

        internal SSCBondOverviewView ObserveCard(Pawn source, Map map, int frame)
        {
            cardFrame = frame;
            if (Card == null || !ReferenceEquals(Card.Source, source))
            {
                if (!ReferenceEquals(Card, Details)) Card?.Clear();
                Card = Details != null && ReferenceEquals(Details.Source, source) ? Details : new SSCBondOverviewView(source, map);
            }
            Card.SetMap(map);
            RefreshLocal(Card);
            return Card;
        }

        internal SSCBondOverviewView OpenDetails(Pawn source, Map map)
        {
            if (Details == null || !ReferenceEquals(Details.Source, source))
            {
                if (!ReferenceEquals(Details, Card)) Details?.Clear();
                Details = Card != null && ReferenceEquals(Card.Source, source) ? Card : new SSCBondOverviewView(source, map);
            }
            Details.SetMap(map);
            RefreshLocal(Details);
            return Details;
        }

        internal void ReleaseDetails(Pawn source = null)
        {
            if (source != null && !ReferenceEquals(Details?.Source, source)) return;
            if (!ReferenceEquals(Details, Card)) Details?.Clear();
            Details = null;
        }
        internal void ReleaseCard()
        {
            if (!ReferenceEquals(Card, Details)) Card?.Clear();
            Card = null; cardFrame = int.MinValue;
        }
        internal void Clear() { ReleaseCard(); ReleaseDetails(); }
        internal void RequestRefresh(SSCBondOverviewView view)
        {
            if (view != null && (ReferenceEquals(view, Card) || ReferenceEquals(view, Details))) view.Pending = true;
        }

        internal void RefreshLocal(SSCBondOverviewView view, bool force = false)
        {
            if (view == null || !ReferenceEquals(Owner, Current.Game) || Current.ProgramState != ProgramState.Playing ||
                Scribe.mode != LoadSaveMode.Inactive || !SSCBondOverviewUtility.IsValidPawn(view.Source)) return;
            double now = clock();
            if (!view.LocalRetryDue(now)) return;
            try
            {
                if (!force && !view.Pending && !view.DirectChanged()) return;
                // 每次只从当前缰绳重新取候选并核链，不保留诊断发现或历史漏索引引用。
                // 成功后清除局部请求；主人、指派和缰绳引用变化不受周期刷新期限阻挡。
                var next = buildLocal(view.Source, view.Map);
                // 仍按原周期读取全部本地事实；只有内容变化才替换快照、增加代次和清空文字／布局缓存。
                // 保留整份旧快照也保留它的行引用及成员索引，不把新行混进旧展示缓存。
                if (!next.SamePresentation(view.Snapshot))
                { view.Snapshot = next; view.Changed(); }
                view.Pending = view.Failed = false;
                view.DirectChanged(true); view.NextLocalRefresh = clock() + 2;
            }
            catch (Exception ex)
            {
                // 本地异常也受日志节流；保留旧事实，点击仍由现时关系和游戏状态守卫。
                view.NextLocalRefresh = clock() + 2;
                view.Failed = true;
                Report(ex, clock());
            }
        }

        private void Report(Exception ex, double now)
        { if (now >= nextError) { nextError = now + 30; report?.Invoke(ex); } }

        internal void Update(int frame, bool cardEligible, Map map)
        {
            // 非当前游戏的会话绝不继续读取世界；加载期间也不保留可操作旧行。
            if (!ReferenceEquals(Owner, Current.Game) || Current.ProgramState != ProgramState.Playing || Scribe.mode != LoadSaveMode.Inactive)
            { Clear(); return; }
            if (!cardEligible || cardFrame == int.MinValue || frame - cardFrame > 2) ReleaseCard();
            if (Card != null && !SSCBondOverviewUtility.IsValidPawn(Card.Source)) ReleaseCard();
            if (Details != null && !SSCBondOverviewUtility.IsValidPawn(Details.Source))
            { if (Details.Snapshot != null) { Details.Snapshot = null; Details.Changed(); } Details.Pending = false; Details.Failed = true; }
            Card?.SetMap(map); Details?.SetMap(map);
            double now = clock();
            if (now < previousTime)
            {
                nextError = now;
                if (Card != null) Card.NextLocalRefresh = now;
                if (Details != null) Details.NextLocalRefresh = now;
            }
            previousTime = now;
            // 两秒真实时间更新阶段、姓名、死亡与持有位置；同源卡片和窗口只构建一次。
            // 无关系角色仅在直接字段变化时重读，停止过去用于发现漏索引的空结果轮询。
            if (Card != null) RefreshLocal(Card, Card.ShowCard && now >= Card.NextLocalRefresh);
            if (Details != null && !ReferenceEquals(Details, Card))
                RefreshLocal(Details, now >= Details.NextLocalRefresh);
        }
    }
}
