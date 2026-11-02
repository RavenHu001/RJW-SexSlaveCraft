// AI 辅助生成
using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>1.7独立生产参数；每批固定一份，四阶段只改变后续积累速度。</summary>
    public sealed class HediffCompProperties_SSCCorruptionGelProduction : HediffCompProperties
    {
        public ThingDef product;
        public float progressPerDay = 0.5f;
        public float stageOne = 1f, stageTwo = 1.25f, stageThree = 1.5f, stageFour = 2f;
        public HediffCompProperties_SSCCorruptionGelProduction() { compClass = typeof(HediffComp_SSCCorruptionGelProduction); }
    }

    /// <summary>载体Severity恒为1，不承担库存；重复授予保留原组件选择与进度。</summary>
    public sealed class Hediff_SSCCorruptionGelProduction : HediffWithComps
    {
        public override bool TryMergeWith(Hediff other) => other?.def == def;
    }

    /// <summary>开关与批次进度唯一保存源；无位置、关闭或失格时不追补，不提前扣除待交付批次。</summary>
    public sealed class HediffComp_SSCCorruptionGelProduction : HediffComp
    {
        private bool productionEnabled;
        private double batchProgress;
        private int retryTicks;
        private bool delivering;
        public HediffCompProperties_SSCCorruptionGelProduction Props => (HediffCompProperties_SSCCorruptionGelProduction)props;
        public bool ProductionEnabled => productionEnabled;
        public double BatchProgress => batchProgress;

        /// <summary>阶段取当前锁链；0档同基础，不借用调教收益倍率或历史特质。</summary>
        public float RateMultiplier => SSCIdentityUtility.GetSexSlaveStage(Pawn) switch
        {
            1 => Props.stageOne, 2 => Props.stageTwo, 3 => Props.stageThree, 4 => Props.stageFour, _ => 1f
        };

        internal void SetProductionEnabled(bool enabled) { productionEnabled = enabled; retryTicks = 0; }

        /// <summary>保存原组件选择与归一化库存；不持久化绝对世界时间，读档不补领离线生产。</summary>
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref productionEnabled, "sscCorruptionGelProductionEnabled", false);
            Scribe_Values.Look(ref batchProgress, "sscCorruptionGelBatchProgress", 0d);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                batchProgress = double.IsFinite(batchProgress) ? Math.Clamp(batchProgress, 0d, 1d) : 0d;
        }

        /// <summary>只消费原生间隔delta；每次实际推进前复查原owner、身体与位置，满批次停止积累。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (delta <= 0 || delivering || Scribe.mode != LoadSaveMode.Inactive || !productionEnabled ||
                !ReferenceEquals(SSCCorruptionGelProductionUtility.Find(Pawn), this) ||
                SSCCorruptionGelProductionUtility.ActivationFailure(Pawn) != null || !HasDeliveryLocation()) return;
            if (batchProgress < 1d)
            {
                double rate = Props.progressPerDay * (double)RateMultiplier;
                if (!double.IsFinite(rate) || rate <= 0d) return;
                batchProgress = Math.Min(1d, batchProgress + rate * delta / 60000d);
            }
            if (batchProgress < 1d) return;
            retryTicks -= delta;
            if (retryTicks > 0) return;
            retryTicks = 250;
            delivering = true;
            try
            {
                // 交付成功即记账，即使库存通知改变资格也不能保留这一批再次领取。
                if (TryDeliverProduct()) batchProgress = 0d;
            }
            finally { delivering = false; }
        }

        private bool HasDeliveryLocation() => Pawn.Spawned && Pawn.Map != null || Pawn.GetCaravan() != null;

        /// <summary>生成一份临时商品；仅按真实放置/入库返回值记账，失败保留库存且清理未持有临时对象。</summary>
        private bool TryDeliverProduct()
        {
            if (Props.product == null) return false;
            Thing item = ThingMaker.MakeThing(Props.product);
            bool accepted = false;
            try
            {
                if (Pawn.Spawned && Pawn.Map != null)
                    accepted = GenPlace.TryPlaceThing(item, Pawn.Position, Pawn.Map, ThingPlaceMode.Near);
                else
                {
                    Caravan caravan = Pawn.GetCaravan();
                    if (caravan != null)
                    {
                        Pawn receiver = CaravanInventoryUtility.FindPawnToMoveInventoryTo(item, caravan.PawnsListForReading, null);
                        accepted = receiver?.inventory?.innerContainer?.TryAdd(item) == true;
                    }
                }
                return accepted;
            }
            finally
            {
                if (!accepted && !item.Destroyed && !item.Spawned && item.holdingOwner == null) item.Destroy();
            }
        }

        /// <summary>UI与健康页只读同一进度和暂停原因；查询不推进、不交付。</summary>
        public string StorageDetail
        {
            get
            {
                string key = !productionEnabled ? "SSC_CorruptionGel_Disabled"
                    : SSCCorruptionGelProductionUtility.ActivationFailure(Pawn) != null ? "SSC_CorruptionGel_Inactive"
                    : batchProgress >= 1d ? "SSC_CorruptionGel_Waiting"
                    : !HasDeliveryLocation() ? "SSC_CorruptionGel_NoLocation" : "SSC_CorruptionGel_Progress";
                return key.Translate(((float)batchProgress).ToStringPercent());
            }
        }
        public override string CompLabelInBracketsExtra => StorageDetail;
    }
}
