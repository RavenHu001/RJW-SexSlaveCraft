// AI 辅助生成：使用原版倒计时组件管理发作和冷却。
using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>关联本场精神状态，向原版健康页提供当前属性和剩余时间。</summary>
    public sealed class Hediff_SSCSlaveBreakdown : HediffWithComps
    {
        internal const string DefName = "SSC_Hediff_SlaveBreakdown";
        private const string HeartFleckDefName = "SSC_SlaveBreakdownHeart";
        // 地图粒子只在本次运行中排程。读档后先等一次随机延迟，再开始新一轮。
        private long nextHeartAt = -1L;
        private int heartsRemaining;
        internal HediffComp_Disappears Timer => GetComp<HediffComp_Disappears>();
        // 只用于旧档转换。读档解析出精神状态时，才能知道老期限是否已包含延长时间。
        internal long LegacyExpiresAt = -1L;
        private static readonly HediffStage NeutralStage = new HediffStage();

        // 读档解析引用和状态启动期间保留空阶段。到期时先停止属性，实例留给精神状态完成结算。
        private bool IsActive => Scribe.mode == LoadSaveMode.Inactive && Timer?.ticksToDisappear > 0 &&
            MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.EffectIsActive(this) == true;

        public override HediffStage CurStage => IsActive ? base.CurStage : NeutralStage;

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (pawn?.Spawned != true) return;
            int now = SSCSlaveBreakdown.Now;
            if (now < nextHeartAt || !IsActive || !pawn.health.hediffSet.hediffs.Contains(this)) return;

            try
            {
                // 隔离视觉排程和粒子初始化的随机数。镜头移动不会改变玩法的随机结果。
                Rand.PushState(Gen.HashCombineInt(pawn.thingIDNumber, now));
                try
                {
                    if (nextHeartAt < 0)
                    {
                        nextHeartAt = (long)now + Rand.RangeInclusive(12, 72);
                        return;
                    }

                    // 一轮的 1～3 颗依次出现。每次只处理一颗，按当前 tick 安排下一颗，画面切回时也不补发。
                    if (heartsRemaining == 0) heartsRemaining = Rand.RangeInclusive(1, 3);
                    heartsRemaining--;
                    nextHeartAt = (long)now + (heartsRemaining > 0
                        ? Rand.RangeInclusive(8, 25)
                        : Rand.RangeInclusive(55, 125));
                    TryDrawHeart();
                }
                finally { Rand.PopState(); }
            }
            catch (Exception error)
            {
                // 原版会移除刷新中抛出异常的 Hediff。把显示故障留在粒子分支，保留本场发作。
                Log.ErrorOnce("[SSC] Could not show the slave breakdown heart: " + error, 194706057);
            }
        }

        private void TryDrawHeart()
        {
            Map map = pawn.Map;
            if (map == null || map != Find.CurrentMap || pawn.Position.Fogged(map)) return;

            Vector3 position = pawn.DrawPos + new Vector3(Rand.Range(-.05f, .62f), 0f, Rand.Range(.55f, .78f));
            if (!position.ShouldSpawnMotesAt(map, drawOffscreen: false)) return;
            var def = DefDatabase<FleckDef>.GetNamedSilentFail(HeartFleckDefName);
            if (def == null) return;

            var data = FleckMaker.GetDataStatic(position, map, def, .45f);
            data.instanceColor = new Color(.85f, .50f, 1.25f, .92f);
            data.rotation = 180f;
            data.velocity = new Vector3(Rand.Range(-.16f, .16f), 0f, Rand.Range(.24f, .42f));
            map.flecks.CreateFleck(data);
        }

        // 原版到期移除。额外兜底清掉失去持有者的实例，读档期间等待交叉引用解析。
        public override bool ShouldRemove => base.ShouldRemove || (Scribe.mode == LoadSaveMode.Inactive &&
            MentalState_SSCSlaveBreakdown.CurrentFor(pawn)?.OwnsEffect(this) != true);

        // 禁止原版按 Def 合并，保证 Scribe 引用和退出清理一直指向同一个实例。
        public override bool TryMergeWith(Hediff other) => false;

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref LegacyExpiresAt, "sscBreakdownEffectExpiresAt", -1L);
        }
    }

    /// <summary>隐藏的原版定时后效，保留原类型以便读取旧档。</summary>
    public sealed class Hediff_SSCSlaveBreakdownCooldown : HediffWithComps
    {
        internal const string DefName = "SSC_Hediff_SlaveBreakdownCooldown";
        internal HediffComp_Disappears Timer => GetComp<HediffComp_Disappears>();
        private long legacyExpiresAt;

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyExpiresAt, "sscBreakdownCooldownExpiresAt", 0L);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyExpiresAt > 0 && SSCSlaveBreakdown.Now >= 0 && Timer is { } timer)
            {
                timer.disappearsAfterTicks = timer.Props.disappearsAfterTicks.max;
                timer.ticksToDisappear = (int)Math.Clamp(legacyExpiresAt - SSCSlaveBreakdown.Now, 0L, timer.disappearsAfterTicks);
                legacyExpiresAt = 0;
            }
        }
    }
}
