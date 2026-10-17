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
        internal double NextRefresh;
        internal SSCBondOverviewView(Pawn source, Map map) { Source = source; Map = map; }

        internal bool ShowCard => Snapshot?.State == SSCBondOverviewQueryState.Ready
            ? Snapshot.Identity != PawnIdentity.Unset || Snapshot.Master != null || Snapshot.TotalSlaveCount > 0 ||
                Snapshot.MasterState == SSCBondOverviewMasterState.InvalidTarget
            : (Source?.TryGetComp<CompSexSlaveTraining>()?.pawnIdentity ?? PawnIdentity.Unset) != PawnIdentity.Unset || SSCBondUtility.GetChain(Source) != null;

        internal void SetMap(Map map)
        {
            if (ReferenceEquals(Map, map)) return;
            Map = map; Snapshot = null; Pending = true; Changed();
        }
        partial void ResetPresentation();
        internal void Changed() { Revision++; ResetPresentation(); }
        internal void Clear() { Snapshot = null; Pending = false; ResetPresentation(); }
    }

    /// <summary>最多两个活跃使用位置；GUI 只登记访问，完整查询仅由 Update 合并执行。</summary>
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
            Owner = owner; this.clock = clock; this.build = build ?? SSCBondOverviewUtility.Build; this.report = report;
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
                if (Card != null) Card.NextRefresh = now;
                if (Details != null) Details.NextRefresh = now;
            }
            previousTime = now;
            if (frame == buildFrame || now < nextBuild) return;
            bool cardDue = Due(Card, now), detailsDue = Due(Details, now);
            if (!cardDue && !detailsDue) return;
            // 两个来源都到期时轮换；同源共享对象，因此一次构建同时服务卡片与窗口。
            SSCBondOverviewView view = detailsDue && (!cardDue || preferDetails) ? Details : Card;
            preferDetails = !ReferenceEquals(view, Details);
            buildFrame = frame;
            view.Pending = false;
            double started = now;
            try { view.Snapshot = build(view.Source, view.Map); view.Failed = false; }
            catch (Exception ex)
            {
                view.Snapshot = null; view.Failed = true;
                if (now >= nextError) { nextError = now + 30; report?.Invoke(ex); }
            }
            view.Changed();
            double finished = clock();
            if (finished < started) finished = started;
            double baseline = ReferenceEquals(view, Details) || view.ShowCard || view.Failed ? 2 : 5;
            view.NextRefresh = finished + Math.Min(30, Math.Max(baseline, (finished - started) * 10));
            nextBuild = finished + .25;
        }

        private static bool Due(SSCBondOverviewView view, double now)
            => view != null && SSCBondOverviewUtility.IsValidPawn(view.Source) && (view.Pending || now >= view.NextRefresh);
    }
}
