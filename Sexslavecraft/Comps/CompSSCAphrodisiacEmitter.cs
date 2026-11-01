// AI 辅助生成：单格发烟器；开关和半径属于建筑，药效沿实际环境的RJW Def。
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    public class CompProperties_SSCAphrodisiacEmitter : CompProperties
    {
        public HediffDef hediff;
        public FleckDef smoke;
        public CompProperties_SSCAphrodisiacEmitter() { compClass = typeof(CompSSCAphrodisiacEmitter); }
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (hediff == null) yield return "SSC emitter requires a hediff.";
            if (!parentDef.HasComp(typeof(CompPowerTrader))) yield return "SSC emitter requires a power trader.";
        }
    }

    /// <summary>动态负载的信息卡展示；不写入共享的Power配置。</summary>
    public class CompProperties_SSCAphrodisiacPower : CompProperties_Power
    {
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            var emitter = (req.Thing as ThingWithComps)?.GetComp<CompSSCAphrodisiacEmitter>();
            float watts = emitter?.DemandWatts ?? CompSSCAphrodisiacEmitter.WattsForRadius(10);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "PowerConsumption".Translate(),
                watts.ToString("F0") + " W", "SSC_Emitter_PowerDesc".Translate(), 5000);
        }
    }

    public class CompSSCAphrodisiacEmitter : CompAutoPowered
    {
        public const int MinRadius = 5, MaxRadius = 20, DefaultRadius = 10, ExposureInterval = 120;
        public const float ExposurePerPulse = .01f;
        private bool enabled = true;
        private int radius = DefaultRadius;
        private readonly List<Pawn> targets = new List<Pawn>();
        private Gizmo_SSCAphrodisiacRadius radiusGizmo;
        private bool applyingExposure;
        private int revision;
        private CompProperties_SSCAphrodisiacEmitter Props => (CompProperties_SSCAphrodisiacEmitter)props;
        private CompPowerTrader Power => compPowerCached ?? (compPowerCached = parent.TryGetComp<CompPowerTrader>());
        public bool Enabled => enabled;
        public int Radius => radius;
        public float DemandWatts => enabled ? WattsForRadius(radius) : 0f;
        public override bool WantsToBeOn => enabled;
        public static float WattsForRadius(int value) => 100f + 4f * value * value;

        // 手动提交仅限当前玩家建筑；不把这项所有权检查套到药效接收角色。
        public bool CanConfigure => Scribe.mode == LoadSaveMode.Inactive && parent != null &&
            parent.Spawned && !parent.Destroyed && parent.Faction == Faction.OfPlayer &&
            ReferenceEquals(parent.TryGetComp<CompSSCAphrodisiacEmitter>(), this);

        public bool CanRunNow => Scribe.mode == LoadSaveMode.Inactive && enabled &&
            parent != null && parent.Spawned && !parent.Destroyed &&
            ReferenceEquals(parent.TryGetComp<CompSSCAphrodisiacEmitter>(), this) &&
            Power?.PowerOn == true && Power.PowerNet != null &&
            !parent.Map.gameConditionManager.ElectricityDisabled(parent.Map);

        public bool TrySetEnabled(bool value)
        {
            if (!CanConfigure) return false;
            if (enabled == value) return true;
            enabled = value;
            revision++;
            RefreshPowerDemand();
            return true;
        }

        public bool TrySetRadius(float value)
        {
            if (!CanConfigure || float.IsNaN(value) || float.IsInfinity(value)) return false;
            int next = Mathf.RoundToInt(Mathf.Clamp(value, MinRadius, MaxRadius));
            if (radius == next) return true;
            radius = next;
            revision++;
            RefreshPowerDemand();
            return true;
        }

        /// <summary>保留未通电时的请求负载，由原版电网分配；关闭意愿阻止电网重启。</summary>
        public void RefreshPowerDemand()
        {
            if (Power == null) return;
            if (!enabled && Power.PowerOn) parent.BroadcastCompSignal(AutoPoweredWantsOffSignal);
            float output = -DemandWatts;
            if (Power.powerOutputInt != output) Power.PowerOutput = output;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            revision++;
            compPowerCached = parent.TryGetComp<CompPowerTrader>();
            targets.Clear();
            radiusGizmo = null;
            RefreshPowerDemand();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, "sscEmitterEnabled", true);
            Scribe_Values.Look(ref radius, "sscEmitterRadius", DefaultRadius);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) radius = Math.Max(MinRadius, Math.Min(MaxRadius, radius));
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            revision++;
            targets.Clear();
            radiusGizmo = null;
            base.PostDeSpawn(map, mode);
        }

        public override void PostSwapMap()
        {
            base.PostSwapMap();
            revision++;
            targets.Clear();
            radiusGizmo = null;
            compPowerCached = parent.TryGetComp<CompPowerTrader>();
            RefreshPowerDemand();
        }

        public override void CompTick()
        {
            // 电力组件重置可恢复静态默认值；每tick仅做一次廉价差值核对。
            RefreshPowerDemand();
            if (!CanRunNow) return;
            if (parent.IsHashIntervalTick(ExposureInterval)) ApplyExposurePulse();
            if (CanRunNow && parent.IsHashIntervalTick(60)) TryEmitSmoke();
        }

        private bool InCoverage(Pawn pawn, Map map, Room room)
            => room != null && pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned &&
                pawn.Map == map && pawn.health?.hediffSet != null &&
                pawn.Position.DistanceToSquared(parent.Position) <= radius * radius && pawn.GetRoom() == room;

        private bool PulseStillValid(Map map, int pulseRevision)
            => CanRunNow && revision == pulseRevision && parent.Map == map;

        private bool CanReceiveDose(Pawn pawn, Pawn_HealthTracker health, Map map, int pulseRevision)
            => PulseStillValid(map, pulseRevision) && ReferenceEquals(pawn.health, health) &&
                InCoverage(pawn, map, parent.GetRoom());

        /// <summary>只从普通Tick结算；角色列表先快照，健康回调换图／移除不会打坏枚举。</summary>
        public void ApplyExposurePulse()
        {
            if (applyingExposure || !CanRunNow || Props.hediff == null) return;
            Map map = parent.Map;
            int pulseRevision = revision;
            Room room = parent.GetRoom();
            if (room == null) return;
            targets.Clear();
            applyingExposure = true;
            try
            {
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (InCoverage(pawns[i], map, room)) targets.Add(pawns[i]);
                for (int i = 0; i < targets.Count; i++)
                {
                    if (!PulseStillValid(map, pulseRevision)) break;
                    Pawn pawn = targets[i];
                    if (!InCoverage(pawn, map, parent.GetRoom())) continue;
                    Pawn_HealthTracker health = pawn.health;
                    Hediff effect = health.hediffSet.GetFirstHediffOfDef(Props.hediff);
                    if (effect == null)
                    {
                        effect = HediffMaker.MakeHediff(Props.hediff, pawn);
                        // PostMake和阶段回调可以停机、移走角色或换健康载体；旧输入不能借新状态续权。
                        if (!CanReceiveDose(pawn, health, map, pulseRevision)) continue;
                        effect.Severity = ExposurePerPulse;
                        if (!CanReceiveDose(pawn, health, map, pulseRevision)) continue;
                        health.AddHediff(effect);
                    }
                    else effect.Severity += ExposurePerPulse;
                    if (!CanReceiveDose(pawn, health, map, pulseRevision)) continue;
                    // 与原发烟器同类的娱乐用药暴露；不调用摄入Doer，不另加PNA负荷。
                    if (pawn.mindState != null) pawn.mindState.lastTakeRecreationalDrugTick = Find.TickManager.TicksGame;
                    if (pawn.needs?.drugsDesire != null) pawn.needs.drugsDesire.CurLevel += .2f;
                }
            }
            finally { targets.Clear(); applyingExposure = false; }
        }

        private void TryEmitSmoke()
        {
            if (!CanRunNow) return;
            Map map = parent.Map;
            if (Props.smoke == null || map != Find.CurrentMap) return;
            Room room = parent.GetRoom();
            if (room == null) return;
            // 仅表现使用的随机序列不改变玩法随机流；每朵烟最多八次找点。
            Rand.PushState(Gen.HashCombineInt(parent.thingIDNumber, Find.TickManager.TicksGame));
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 cell = parent.Position + new IntVec3(Rand.RangeInclusive(-radius, radius), 0, Rand.RangeInclusive(-radius, radius));
                    if (!cell.InBounds(map) || cell.DistanceToSquared(parent.Position) > radius * radius ||
                        cell.GetRoom(map) != room) continue;
                    Vector3 position = cell.ToVector3Shifted();
                    if (!position.ShouldSpawnMotesAt(map, false)) return;
                    FleckCreationData data = FleckMaker.GetDataStatic(position, map, Props.smoke, 2f);
                    data.instanceColor = new Color(.8f, .5f, 1f, .45f);
                    data.rotation = Rand.Range(0f, 360f);
                    map.flecks.CreateFleck(data);
                    return;
                }
            }
            finally { Rand.PopState(); }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!CanConfigure) yield break;
            yield return new Command_Toggle
            {
                defaultLabel = "SSC_Emitter_Toggle".Translate(),
                defaultDesc = "SSC_Emitter_ToggleDesc".Translate(),
                icon = TexCommand.DesirePower,
                isActive = () => enabled,
                toggleAction = () => TrySetEnabled(!enabled)
            };
            yield return radiusGizmo ?? (radiusGizmo = new Gizmo_SSCAphrodisiacRadius(this));
        }

        public override string CompInspectStringExtra()
        {
            string state = !enabled ? "SSC_Emitter_Off" : CanRunNow ? "SSC_Emitter_Running" : "SSC_Emitter_NoPower";
            return "SSC_Emitter_Status".Translate(state.Translate()) + "\n" +
                "SSC_Emitter_RadiusPower".Translate(radius, DemandWatts.ToString("F0"));
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            if (parent.Spawned) GenDraw.DrawRadiusRing(parent.Position, radius);
        }
    }
}
