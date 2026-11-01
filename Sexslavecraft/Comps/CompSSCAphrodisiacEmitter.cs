// AI 辅助生成：单格发烟器；开关和半径属于建筑，药效沿实际环境的RJW Def。
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>引用当前模组环境中的药效和烟雾定义，不在设备内重建药效阶段。</summary>
    public class CompProperties_SSCAphrodisiacEmitter : CompProperties
    {
        public HediffDef hediff;
        public FleckDef smoke;
        public FleckDef sourceSmoke;
        /// <summary>绑定设备运行组件，供XML实例化。</summary>
        public CompProperties_SSCAphrodisiacEmitter() { compClass = typeof(CompSSCAphrodisiacEmitter); }
        /// <summary>在Def检查阶段报告缺失药效或电力组件；烟雾允许缺省。</summary>
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
        /// <summary>实例显示当前请求功率；没有实例的建造信息卡显示默认半径功率。</summary>
        public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
        {
            var emitter = (req.Thing as ThingWithComps)?.GetComp<CompSSCAphrodisiacEmitter>();
            float watts = emitter?.DemandWatts ?? CompSSCAphrodisiacEmitter.WattsForRadius(10);
            yield return new StatDrawEntry(StatCategoryDefOf.Building, "PowerConsumption".Translate(),
                watts.ToString("F0") + " W", "SSC_Emitter_PowerDesc".Translate(), 5000);
        }
    }

    /// <summary>保存单台设备的设置，协调原版电网、周期药效与独立烟雾表现。</summary>
    public class CompSSCAphrodisiacEmitter : CompAutoPowered
    {
        public const int MinRadius = 5, MaxRadius = 20, DefaultRadius = 10, ExposureInterval = 120;
        public const float ExposurePerPulse = .01f;
        private const int SmokeTileSize = 5, SmokeCyclePulses = 24;
        // 唯一持久状态：功率由半径与开关推导，缓存和结算上下文均不入档。
        private bool enabled = true;
        private int radius = DefaultRadius;
        private readonly List<Pawn> targets = new List<Pawn>();
        // 单片最多25个坐标，仅在视觉脉冲内复用，结束即清空；不是房间拓扑缓存。
        private readonly List<IntVec3> smokeCells = new List<IntVec3>(SmokeTileSize * SmokeTileSize);
        private Gizmo_SSCAphrodisiacRadius radiusGizmo;
        // 健康回调可能重入或修改设备；重入锁与版本号分别阻止重复结算和旧脉冲继续提交。
        private bool applyingExposure;
        private int revision;
        private CompProperties_SSCAphrodisiacEmitter Props => (CompProperties_SSCAphrodisiacEmitter)props;
        private CompPowerTrader Power => compPowerCached ?? (compPowerCached = parent.TryGetComp<CompPowerTrader>());
        public bool Enabled => enabled;
        public int Radius => radius;
        /// <summary>设备请求的负载；未通电仍保留请求值，关闭时才归零。</summary>
        public float DemandWatts => enabled ? WattsForRadius(radius) : 0f;
        /// <summary>将玩家保存的开关选择交给CompAutoPowered的电网启停流程。</summary>
        public override bool WantsToBeOn => enabled;
        /// <summary>按已确认的整数半径计算瓦数；调用方负责半径范围校验。</summary>
        public static float WattsForRadius(int value) => 100f + 4f * value * value;

        /// <summary>手动提交仅限当前玩家建筑的现役组件；此所有权限制不用于药效目标。</summary>
        public bool CanConfigure => Scribe.mode == LoadSaveMode.Inactive && parent != null &&
            parent.Spawned && !parent.Destroyed && parent.Faction == Faction.OfPlayer &&
            ReferenceEquals(parent.TryGetComp<CompSSCAphrodisiacEmitter>(), this);

        /// <summary>检查此刻能否运行；保存选择、有效组件、电网供电和停电条件须同时满足。</summary>
        public bool CanRunNow => Scribe.mode == LoadSaveMode.Inactive && enabled &&
            parent != null && parent.Spawned && !parent.Destroyed &&
            ReferenceEquals(parent.TryGetComp<CompSSCAphrodisiacEmitter>(), this) &&
            Power?.PowerOn == true && Power.PowerNet != null &&
            !parent.Map.gameConditionManager.ElectricityDisabled(parent.Map);

        /// <summary>由玩家直接切换设备，提交时复查资格并立即更新负载，无需小人执行任务。</summary>
        public bool TrySetEnabled(bool value)
        {
            if (!CanConfigure) return false;
            if (enabled == value) return true;
            enabled = value;
            revision++;
            RefreshPowerDemand();
            return true;
        }

        /// <summary>拒绝非有限输入，将半径限制为5～20的整数；有效变更立即更新负载。</summary>
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
            // 关闭时通知电力组件退出通电状态，避免零负载却仍显示运行。
            if (!enabled && Power.PowerOn) parent.BroadcastCompSignal(AutoPoweredWantsOffSignal);
            // 原版以负输出表示耗电；不能因暂时缺电清零，否则电网会失去恢复供电的需求。
            float output = -DemandWatts;
            if (Power.powerOutputInt != output) Power.PowerOutput = output;
        }

        /// <summary>生成或读档后重取组件引用，清除临时上下文并恢复本机负载。</summary>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            revision++;
            compPowerCached = parent.TryGetComp<CompPowerTrader>();
            targets.Clear();
            radiusGizmo = null;
            RefreshPowerDemand();
        }

        /// <summary>保存开关与半径；读档结束时修正越界值，其他状态由运行环境重新建立。</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref enabled, "sscEmitterEnabled", true);
            Scribe_Values.Look(ref radius, "sscEmitterRadius", DefaultRadius);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) radius = Math.Max(MinRadius, Math.Min(MaxRadius, radius));
        }

        /// <summary>离图时使当前脉冲失效，并释放目标快照和界面缓存。</summary>
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            revision++;
            targets.Clear();
            radiusGizmo = null;
            base.PostDeSpawn(map, mode);
        }

        /// <summary>换图后废弃原地图上下文，并重新取得电力组件与负载。</summary>
        public override void PostSwapMap()
        {
            base.PostSwapMap();
            revision++;
            targets.Clear();
            radiusGizmo = null;
            compPowerCached = parent.TryGetComp<CompPowerTrader>();
            RefreshPowerDemand();
        }

        /// <summary>以普通Tick为唯一自动结算入口，按建筑散列错开药效和烟雾的周期工作。</summary>
        public override void CompTick()
        {
            // 电力组件重置可恢复静态默认值；每tick仅做一次廉价差值核对。
            RefreshPowerDemand();
            if (!CanRunNow) return;
            // 不补算断电期间的暴露；药效回调可能停机，烟雾调度前须再查运行条件。
            if (parent.IsHashIntervalTick(ExposureInterval)) ApplyExposurePulse();
            if (CanRunNow && parent.IsHashIntervalTick(15)) TryEmitSmoke();
        }

        /// <summary>只检查有效健康对象、同图、半径及同房间，不额外按年龄、种族或阵营筛选。</summary>
        private bool InCoverage(Pawn pawn, Map map, Room room)
            => room != null && pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned &&
                pawn.Map == map && pawn.health?.hediffSet != null &&
                pawn.Position.DistanceToSquared(parent.Position) <= radius * radius && pawn.GetRoom() == room;

        /// <summary>确认源设备仍属于本次脉冲的地图和配置版本；中途变更后不续算旧请求。</summary>
        private bool PulseStillValid(Map map, int pulseRevision)
            => CanRunNow && revision == pulseRevision && parent.Map == map;

        /// <summary>在回调后复查源、范围与原健康载体，防止旧请求写入被替换的Tracker。</summary>
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
                // 复用列表收集当次候选，避免健康回调修改原版角色列表而破坏遍历。
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                    if (InCoverage(pawns[i], map, room)) targets.Add(pawns[i]);
                for (int i = 0; i < targets.Count; i++)
                {
                    // 快照仅表示候选资格；每个目标提交前仍复查源设备和当前房间。
                    if (!PulseStillValid(map, pulseRevision)) break;
                    Pawn pawn = targets[i];
                    if (!InCoverage(pawn, map, parent.GetRoom())) continue;
                    Pawn_HealthTracker health = pawn.health;
                    // 复用环境最终定义的同一药效，严重度上限、阶段与自然衰减交给Hediff处理。
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
                    // 添加或增强药效同样可触发回调；失效后不再追加用药时间和需求收益。
                    if (!CanReceiveDose(pawn, health, map, pulseRevision)) continue;
                    // 与原发烟器同类的娱乐用药暴露；不调用摄入Doer，不另加PNA负荷。
                    if (pawn.mindState != null) pawn.mindState.lastTakeRecreationalDrugTick = Find.TickManager.TicksGame;
                    if (pawn.needs?.drugsDesire != null) pawn.needs.drugsDesire.CurLevel += .2f;
                }
            }
            // 即使外部健康回调抛错，也不让缓存持有角色或使重入锁永久阻止后续结算。
            finally { targets.Clear(); applyingExposure = false; }
        }

        /// <summary>仅在当前地图生成源头喷烟，按六秒轮巡把范围烟云铺到各片有效空间。</summary>
        private void TryEmitSmoke()
        {
            if (!CanRunNow) return;
            Map map = parent.Map;
            if (map != Find.CurrentMap) return;
            Room room = parent.GetRoom();
            if (room == null) return;
            // 两层烟雾共用独立的表现随机序列，不改变玩法随机流。
            Rand.PushState(Gen.HashCombineInt(parent.thingIDNumber, Find.TickManager.TicksGame));
            try
            {
                // 每15 tick在设备出口喷一朵小烟，确保小房间或范围采样失败时仍能看见运行状态。
                if (Props.sourceSmoke != null)
                    SpawnSmoke(parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead) + new Vector3(0f, 0f, .5f), map,
                        Props.sourceSmoke, Rand.Range(.8f, 1.2f), true);

                if (Props.smoke == null) return;
                // 将包围方框切成5×5小片，24次视觉脉冲恰好遍历一次；不积攒停机或离图期间的补发。
                // 最大半径只有81片，每次至多处理4片／100格，不在单次发烟时扫描整个房间。
                int columns = (radius * 2 + SmokeTileSize) / SmokeTileSize;
                int tiles = columns * columns;
                int phase = (int)((Find.TickManager.TicksGame / 15 + parent.thingIDNumber) % SmokeCyclePulses);
                for (int index = phase * tiles / SmokeCyclePulses; index < (phase + 1) * tiles / SmokeCyclePulses; index++)
                {
                    int minX = parent.Position.x - radius + index % columns * SmokeTileSize;
                    int minZ = parent.Position.z - radius + index / columns * SmokeTileSize;
                    int validCells = CollectSmokeCells(map, room, minX, minZ);
                    // 每6个有效格每轮平均生成一朵；21秒寿命下约0.58朵／格，接近文化自动烟罐的密度。
                    // 随机取整保持小片面积比例，单片最多5朵／单次最多20朵，不以放大纹理代替密度。
                    int count = validCells / 6;
                    if (Rand.Chance((validCells % 6) / 6f)) count++;
                    for (int i = 0; i < count; i++)
                    {
                        // 部分洗牌使同轮烟点不重复；下轮重新查询当前房间，不复用旧拓扑。
                        int pick = Rand.Range(i, validCells);
                        IntVec3 cell = smokeCells[pick];
                        smokeCells[pick] = smokeCells[i];
                        smokeCells[i] = cell;
                        SpawnSmoke(cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MoteOverhead), map,
                            Props.smoke, 4f, false);
                    }
                }
            }
            // 包含提前返回与异常路径，始终恢复进入前的玩法随机状态。
            finally { smokeCells.Clear(); Rand.PopState(); }
        }

        /// <summary>收集至多25格的当前合法烟点，供本片无放回抽样；复用临时缓冲，不缓存房间拓扑。</summary>
        private int CollectSmokeCells(Map map, Room room, int minX, int minZ)
        {
            smokeCells.Clear();
            for (int x = minX; x < minX + SmokeTileSize; x++)
                for (int z = minZ; z < minZ + SmokeTileSize; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(map) || cell.DistanceToSquared(parent.Position) > radius * radius ||
                        cell.GetRoom(map) != room) continue;
                    smokeCells.Add(cell);
                }
            return smokeCells.Count;
        }

        /// <summary>沿原版可见性检查创建浅色烟朵；源头带轻微运动，范围保持原烟纹理的颗粒层次。</summary>
        private void SpawnSmoke(Vector3 position, Map map, FleckDef fleck, float scale, bool atSource)
        {
            // 跳过的烟雾不补发；保留完整实例透明度，由纹理和生命周期负责淡入淡出。
            if (!position.ShouldSpawnMotesAt(map, false)) return;
            FleckCreationData data = FleckMaker.GetDataStatic(position, map, fleck, scale);
            // 主体保留接近原版白烟的亮度；深紫色和过大尺寸会把纹理压成暗色平滑斑块。
            data.instanceColor = atSource ? new Color(1f, .88f, 1f, 1f) : new Color(1f, .97f, 1f, 1f);
            if (atSource)
            {
                data.rotationRate = Rand.Range(-10f, 10f);
                data.velocityAngle = Rand.Range(-15f, 15f);
                data.velocitySpeed = Rand.Range(.1f, .35f);
            }
            map.flecks.CreateFleck(data);
        }

        /// <summary>按帧提交原版关机图标，暂停时也随直接开关刷新，不保存额外开关或图标句柄。</summary>
        public override void PostDraw()
        {
            // MapMeshAndRealTime保证静态建筑也调用此入口；绘制不依赖通电或选中状态。
            if (parent.Spawned && !enabled && !parent.Fogged() && !parent.IsBrokenDown())
                parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.PowerOff);
        }

        /// <summary>为本台玩家设备提供直接开关和缓存滑条，提交由setter再次校验当前资格。</summary>
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

        /// <summary>显示保存的开关选择、实际运行状态及请求功率，不在绘制时推进药效。</summary>
        public override string CompInspectStringExtra()
        {
            string state = !enabled ? "SSC_Emitter_Off" : CanRunNow ? "SSC_Emitter_Running" : "SSC_Emitter_NoPower";
            return "SSC_Emitter_Status".Translate(state.Translate()) + "\n" +
                "SSC_Emitter_RadiusPower".Translate(radius, DemandWatts.ToString("F0"));
        }

        /// <summary>选中时显示半径上界；实际生效区域还须满足同房间条件。</summary>
        public override void PostDrawExtraSelectionOverlays()
        {
            if (parent.Spawned) GenDraw.DrawRadiusRing(parent.Position, radius);
        }
    }
}
