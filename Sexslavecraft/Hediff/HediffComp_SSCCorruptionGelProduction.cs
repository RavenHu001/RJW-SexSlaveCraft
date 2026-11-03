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
        public float decayMultiplier = 2f;
        public double sceneBoost = 0.10d;
        public HediffCompProperties_SSCCorruptionGelProduction() { compClass = typeof(HediffComp_SSCCorruptionGelProduction); }
    }

    /// <summary>载体Severity恒为1，不承担库存；重复授予保留原组件选择与进度。</summary>
    public sealed class Hediff_SSCCorruptionGelProduction : HediffWithComps
    {
        private static readonly HediffStage NeutralStage = new HediffStage();
        /// <summary>只有当前身体唯一载体正在积累时提供工作代价；库存、选择仍只保存在Comp。</summary>
        public override HediffStage CurStage => this.TryGetComp<HediffComp_SSCCorruptionGelProduction>()?.IsProducing == true
            ? base.CurStage : NeutralStage;
        public override bool TryMergeWith(Hediff other) => other?.def == def;
        public override void PostAdd(DamageInfo? dinfo) { base.PostAdd(dinfo); SSCCorruptionGelProductionUtility.NotifyEffectsChanged(pawn); }
        public override void PostRemoved() { base.PostRemoved(); SSCCorruptionGelProductionUtility.NotifyEffectsChanged(pawn); }
    }

    /// <summary>开关与批次进度唯一保存源；无位置、关闭或失格时不追补，不提前扣除待交付批次。</summary>
    public sealed class HediffComp_SSCCorruptionGelProduction : HediffComp, ICorruptionModifierSource
    {
        private bool productionEnabled;
        private double batchProgress;
        private int retryTicks;
        private bool delivering;
        private bool? observedProducing;
        public HediffCompProperties_SSCCorruptionGelProduction Props => (HediffCompProperties_SSCCorruptionGelProduction)props;
        public bool ProductionEnabled => productionEnabled;
        public double BatchProgress => batchProgress;

        /// <summary>当前身体与位置的共同准入；已完成批次交付不重新要求正生产速率。</summary>
        private bool HasProductionContext => Scribe.mode == LoadSaveMode.Inactive && productionEnabled &&
            ReferenceEquals(SSCCorruptionGelProductionUtility.Find(Pawn), this) &&
            SSCCorruptionGelProductionUtility.ActivationFailure(Pawn) == null && HasDeliveryLocation();
        /// <summary>事件准入允许满批次认领但不给库存；积累和代价另校验合法正速率。</summary>
        internal bool CanReceiveSceneBoost
        {
            get
            {
                if (!HasProductionContext) return false;
                double rate = Props.progressPerDay * (double)RateMultiplier;
                return double.IsFinite(rate) && rate > 0d;
            }
        }
        public bool IsProducing => batchProgress < 1d && CanReceiveSceneBoost;
        public float GetDecayMultiplier() => IsProducing && float.IsFinite(Props.decayMultiplier) && Props.decayMultiplier > 0f
            ? Props.decayMultiplier : 1f;
        public float GetMinCorruption() => 0f;

        /// <summary>派生缓存仅在状态转换或明确生命周期通知时清理，不保存第二份有效选择。</summary>
        internal void RefreshEffects(bool force = false)
        {
            bool active = IsProducing;
            if (force || observedProducing != active)
            {
                observedProducing = active;
                SSCCorruptionGelProductionUtility.ClearWorkSpeedCache(Pawn);
            }
        }

        /// <summary>只能由已认领的真实场次调用；不提前交付、不改Severity、不保留超过整批的余量。</summary>
        internal void ApplySceneBoost()
        {
            if (!CanReceiveSceneBoost || !double.IsFinite(Props.sceneBoost) || Props.sceneBoost <= 0d) return;
            batchProgress = Math.Min(1d, batchProgress + Props.sceneBoost);
            RefreshEffects();
        }

        /// <summary>阶段取当前锁链；0档同基础，不借用调教收益倍率或历史特质。</summary>
        public float RateMultiplier => SSCIdentityUtility.GetSexSlaveStage(Pawn) switch
        {
            1 => Props.stageOne, 2 => Props.stageTwo, 3 => Props.stageThree, 4 => Props.stageFour, _ => 1f
        };

        internal void SetProductionEnabled(bool enabled) { productionEnabled = enabled; retryTicks = 0; RefreshEffects(true); }

        /// <summary>保存原组件选择与归一化库存；不持久化绝对世界时间，读档不补领离线生产。</summary>
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref productionEnabled, "sscCorruptionGelProductionEnabled", false);
            Scribe_Values.Look(ref batchProgress, "sscCorruptionGelBatchProgress", 0d);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                batchProgress = double.IsFinite(batchProgress) ? Math.Clamp(batchProgress, 0d, 1d) : 0d;
                observedProducing = null;
            }
        }

        /// <summary>只消费原生间隔delta；每次实际推进前复查原owner、身体与位置，满批次停止积累。</summary>
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            RefreshEffects();
            if (delta <= 0 || delivering || !HasProductionContext) return;
            if (batchProgress < 1d)
            {
                double rate = Props.progressPerDay * (double)RateMultiplier;
                if (!double.IsFinite(rate) || rate <= 0d) return;
                batchProgress = Math.Min(1d, batchProgress + rate * delta / 60000d);
            }
            RefreshEffects();
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
            finally { delivering = false; RefreshEffects(); }
        }

        private bool HasDeliveryLocation() => Pawn.Spawned && Pawn.Map != null || Pawn.GetCaravan() != null;

        /// <summary>生成一份临时商品；仅按真实放置/入库返回值记账，失败保留库存且清理未持有临时对象。</summary>
        private bool TryDeliverProduct()
        {
            if (Props.product == null) return false;
            Pawn producer = Pawn;
            Pawn_HealthTracker health = producer.health;
            Thing item = ThingMaker.MakeThing(Props.product);
            bool accepted = false;
            try
            {
                // MakeThing/PostMake可触发外部回调；物品尚未交付时必须仍持有原身体owner及有效选择。
                if (!ReferenceEquals(Pawn, producer) || !ReferenceEquals(producer.health, health) || !productionEnabled ||
                    !ReferenceEquals(SSCCorruptionGelProductionUtility.Find(producer), this) ||
                    SSCCorruptionGelProductionUtility.ActivationFailure(producer) != null) return false;
                if (producer.Spawned && producer.Map != null)
                    accepted = GenPlace.TryPlaceThing(item, producer.Position, producer.Map, ThingPlaceMode.Near);
                else
                {
                    Caravan caravan = producer.GetCaravan();
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
