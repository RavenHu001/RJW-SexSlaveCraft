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
        private bool localFailed;
        internal int Revision;
        internal int Page;
        internal double NextRefresh;
        internal double NextLocalRefresh;
        internal bool LocalRetryDue(double now) => !localFailed || now >= NextLocalRefresh;
        internal void LocalResult(bool failed) { localFailed = failed; }
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

    /// <summary>最多两个活跃视图；登记时读取直接关系，完整补漏仅由 Update 按统一预算执行。</summary>
    internal sealed class SSCBondOverviewSession
    {
        internal readonly Game Owner;
        private readonly Func<double> clock;
        private readonly Func<Pawn, Map, SSCBondOverviewSnapshot> build;
        private readonly Action<Exception> report;
        internal SSCBondOverviewView Card { get; private set; }
        internal SSCBondOverviewView Details { get; private set; }
        private int cardFrame = int.MinValue, buildFrame = int.MinValue;
        private bool preferDetails;
        private double previousTime = double.NegativeInfinity, nextBuild, nextError;

        internal SSCBondOverviewSession(Game owner, Func<double> clock,
            Func<Pawn, Map, SSCBondOverviewSnapshot> build = null, Action<Exception> report = null)
        {
            Owner = owner; this.clock = clock; this.build = build; this.report = report;
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
                if (!force && !view.DirectChanged()) return;
                view.Snapshot = SSCBondOverviewUtility.BuildLocal(view.Source, view.Map, view.Snapshot);
                view.LocalResult(false);
                view.DirectChanged(true); view.NextLocalRefresh = clock() + 2; view.Changed();
            }
            catch (Exception ex)
            {
                // 本地异常也受日志节流；保留旧事实，点击仍由现时关系和游戏状态守卫。
                view.NextLocalRefresh = clock() + 2;
                view.LocalResult(true);
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
                nextBuild = now; nextError = now;
                if (Card != null) Card.NextRefresh = Card.NextLocalRefresh = now;
                if (Details != null) Details.NextRefresh = Details.NextLocalRefresh = now;
            }
            previousTime = now;
            // 全局退避只限制补漏；另一来源的本地显示、直接关系变化和本地定时刷新不必等它。
            if (frame != buildFrame && now >= nextBuild) Supplement(frame, now);
            if (Card != null) RefreshLocal(Card, now >= Card.NextLocalRefresh);
            if (Details != null && !ReferenceEquals(Details, Card)) RefreshLocal(Details, now >= Details.NextLocalRefresh);
        }

        private void Supplement(int frame, double now)
        {
            bool cardDue = Due(Card, now), detailsDue = Due(Details, now);
            if (!cardDue && !detailsDue) return;
            // 两个来源都到期时轮换；同源共享对象，因此一次构建同时服务卡片与窗口。
            SSCBondOverviewView view = detailsDue && (!cardDue || preferDetails) ? Details : Card;
            preferDetails = !ReferenceEquals(view, Details);
            buildFrame = frame;
            view.Pending = false;
            double started = now;
            try
            {
                view.Snapshot = build != null ? build(view.Source, view.Map)
                    : SSCBondOverviewUtility.BuildWithKnown(view.Source, view.Map, view.Snapshot);
                view.Failed = false;
                view.DirectChanged(true);
            }
            catch (Exception ex)
            {
                view.Failed = true;
                Report(ex, now);
            }
            view.Changed();
            double finished = clock();
            if (finished < started) finished = started;
            view.NextLocalRefresh = finished + 2;
            double baseline = ReferenceEquals(view, Details) || view.ShowCard || view.Failed ? 2 : 5;
            // 所有入口共用预算：手动、旧行请求、换来源、开窗口都只排队，不能绕过慢查询退避。
            // 从完成时刻起至少间隔 2 秒，耗时按十倍退避并封顶 30 秒；空摘要自身仍等 5 秒。
            // 预算留在会话，关闭重开也不重置；候选扫描和点击导航的实时复查保持原路径。
            double budget = Math.Min(30, Math.Max(2, (finished - started) * 10));
            view.NextRefresh = finished + Math.Max(baseline, budget);
            nextBuild = finished + budget;
        }

        private static bool Due(SSCBondOverviewView view, double now)
            => view != null && SSCBondOverviewUtility.IsValidPawn(view.Source) && now >= view.NextRefresh;
    }
}
