// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>单台设备保存的运行选择，供电和补料另由基类判定。</summary>
    public enum SSCPnaMistMode { Off, Low, High }

    /// <summary>配置 PNA 投送速率和高档消耗倍率，复用原发烟器的半径、供电与雾效参数。</summary>
    public sealed class CompProperties_SSCPnaMistEmitter : CompProperties_SSCAphrodisiacEmitter
    {
        // 以游戏日和归一化负荷为单位，0.10 表示毛输入 10 个百分点。
        public float lowLoadPerDay = .10f, highLoadPerDay = 25f;
        public float highPowerMultiplier = 2f, highFuelMultiplier = 12f;
        /// <summary>PNA 负荷定义由公共投送入口按当前身体和锁链选择。</summary>
        protected override bool RequiresHediff => false;

        /// <summary>将 XML 参数绑定到 PNA 发烟器运行组件。</summary>
        public CompProperties_SSCPnaMistEmitter() => compClass = typeof(CompSSCPnaMistEmitter);

        /// <summary>核查两档速率能实现预期平衡，并验证档位控件和高档消耗参数。</summary>
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            // 0.224 是 97% 处的低段日消退率，0.25 是末期日消退率。
            if (!SSCPnaMath.IsFinite(lowLoadPerDay) || lowLoadPerDay <= 0f || lowLoadPerDay >= .224f ||
                !SSCPnaMath.IsFinite(highLoadPerDay) || highLoadPerDay <= .25f)
                yield return "PNA mist requires a low rate below the terminal range and a high rate above terminal decay.";
            if (!hasSwitch || !SSCPnaMath.IsFinite(highPowerMultiplier) || highPowerMultiplier < 1f ||
                !SSCPnaMath.IsFinite(highFuelMultiplier) || highFuelMultiplier < 1f)
                yield return "PNA mist requires a mode control and valid high mode consumption multipliers.";
        }
    }

    /// <summary>以独立档位控制 PNA 投送，沿用发烟器的运行准入和实际覆盖。</summary>
    public sealed class CompSSCPnaMistEmitter : CompSSCAphrodisiacEmitter
    {
        private SSCPnaMistMode mode;
        // 存档保存尚未达到首次入账阈值的剂量。角色已经入账的负荷仍由 Hediff 保存。
        private Dictionary<Pawn, double> startupDose = new();
        private List<Pawn> savedTargets;
        private List<double> savedDoses;
        // 前次覆盖用于判定连续暴露。本次集合用于去重，列表用于在健康回调前固定遍历顺序。
        private readonly HashSet<Pawn> previousTargets = new();
        private readonly HashSet<Pawn> currentTargets = new();
        private readonly List<Pawn> targets = new();
        private int lastSampleTick = -1;
        private bool sampling;

        private CompProperties_SSCPnaMistEmitter Props => (CompProperties_SSCPnaMistEmitter)props;
        /// <summary>对外提供保存的档位选择，供控件和检查栏读取。</summary>
        public SSCPnaMistMode Mode => mode;
        /// <summary>从档位派生设备的运行意愿，关闭档同时关闭投送与发烟。</summary>
        public override bool Enabled => mode != SSCPnaMistMode.Off;
        /// <summary>以当前半径的基础功率计算高档电力需求。</summary>
        public override float DemandWatts => base.DemandWatts * (mode == SSCPnaMistMode.High ? Props.highPowerMultiplier : 1f);
        /// <summary>将高档倍率应用到按覆盖半径计算的基础日耗。</summary>
        public override float FuelPerDay => !Enabled ? 0f : base.FuelPerDay * (mode == SSCPnaMistMode.High ? Props.highFuelMultiplier : 1f);
        /// <summary>通过基类普通 Tick 每 60 个游戏 Tick 采样一次。</summary>
        protected override int PulseInterval => 60;
        /// <summary>关闭状态由档位保存和提交。</summary>
        protected override bool HasSeparateSwitch => false;
        internal override string RadiusTooltipKey => "SSC_PnaMistEmitter_RadiusDesc";
        protected override string NoFuelKey => "SSC_PnaMistEmitter_NoFuel";
        protected override string FuelRateKey => "SSC_PnaMistEmitter_FuelRate";

        /// <summary>校验当前设备与档位，切换后从新的采样窗口开始并刷新功率。</summary>
        public bool TrySetMode(SSCPnaMistMode value)
        {
            if (!CanConfigure || value < SSCPnaMistMode.Off || value > SSCPnaMistMode.High) return false;
            if (mode == value) return true;
            mode = value;
            NotifySettingsChanged();
            return true;
        }

        /// <summary>保存档位和首次入账余额，读档后重新确认覆盖。</summary>
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mode, "sscPnaMistMode", SSCPnaMistMode.Off);
            Scribe_Collections.Look(ref startupDose, "sscPnaMistStartupDose", LookMode.Reference, LookMode.Value,
                ref savedTargets, ref savedDoses);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 引用完成解析后才能丢弃 Scribe 过渡列表。采样时钟从新窗口开始。
                startupDose ??= new Dictionary<Pawn, double>();
                if (mode < SSCPnaMistMode.Off || mode > SSCPnaMistMode.High) mode = SSCPnaMistMode.Off;
                if (!Enabled) startupDose.Clear();
                previousTargets.Clear();
                lastSampleTick = -1;
                savedTargets = null;
                savedDoses = null;
            }
        }

        /// <summary>当设备停机、离图或设置变更时清除旧覆盖与未入账剂量。</summary>
        protected override void ResetExposure()
        {
            startupDose.Clear();
            previousTargets.Clear();
            lastSampleTick = -1;
        }

        /// <summary>计算一段连续覆盖的 PNA 剂量，并将符合条件的目标交给公共入口。</summary>
        public override void ApplyExposurePulse()
        {
            if (!CanRunNow || sampling) return;
            int now = Find.TickManager.TicksGame;
            if (lastSampleTick == now) return;

            Map map = parent.Map;
            Room room = parent.GetRoom();
            int revision = ExposureRevision;
            bool consecutive = lastSampleTick >= 0 && now - lastSampleTick == PulseInterval;
            double dose = (mode == SSCPnaMistMode.High ? Props.highLoadPerDay : Props.lowLoadPerDay) *
                PulseInterval / (double)SSCPnaMath.TicksPerDay;
            sampling = true;
            try
            {
                // 先固定当下的覆盖名单，之后的健康回调可以安全修改地图角色列表。
                var pawns = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (InCoverage(pawn, map, room) && SSCPnaExposureUtility.IsValidTarget(pawn) &&
                        !SSCIdentityUtility.IsMaster(pawn) && currentTargets.Add(pawn)) targets.Add(pawn);
                }

                // 首次采样只确认覆盖。读档恢复的余额也必须先通过当前覆盖检查。
                // .NET 10 的 Dictionary 允许在枚举期间移除当前键。
                foreach (var entry in startupDose)
                    if (!currentTargets.Contains(entry.Key)) startupDose.Remove(entry.Key);

                // 相邻采样都覆盖同一目标时才计入该段暴露，间隔中断后先重新取样。
                if (consecutive)
                {
                    for (int i = 0; i < targets.Count && PulseStillValid(map, revision); i++)
                    {
                        Pawn pawn = targets[i];
                        if (!previousTargets.Contains(pawn)) continue;
                        Pawn_HealthTracker health = pawn.health;
                        // 公共入口在健康回调前后复查原设备、原健康载体和当前范围。
                        bool CanContinue() => CanReceiveDose(pawn, health, map, revision);
                        if (!CanContinue()) { startupDose.Remove(pawn); continue; }

                        // 合并上次暂缓的剂量。公共入口会先结算自然消退，再决定入账或继续暂缓。
                        startupDose.Remove(pawn, out double pending);
                        if (!double.IsFinite(pending) || pending < 0d || pending >= SSCPnaExposureUtility.MinimumMistStartDose)
                            pending = 0d;
                        double requested = pending + dose;
                        var result = SSCPnaExposureUtility.ApplyExposure(pawn, (float)requested,
                            SSCPnaExposureSource.MistEmitter, CanContinue);
                        if (result.Deferred && CanContinue()) startupDose[pawn] = requested;
                    }
                }

                // 回调修改档位或范围后，由变更入口建立新的采样窗口。
                if (PulseStillValid(map, revision))
                {
                    previousTargets.Clear();
                    previousTargets.UnionWith(currentTargets);
                    lastSampleTick = now;
                }
            }
            finally
            {
                targets.Clear();
                currentTargets.Clear();
                sampling = false;
            }
        }

        /// <summary>创建关闭、低档和高档菜单，每个选择都提交给原设备。</summary>
        protected override Command CreateOperatingCommand() => new Command_Action
        {
            defaultLabel = "SSC_PnaMistEmitter_Mode".Translate(ModeLabel(mode)),
            defaultDesc = "SSC_PnaMistEmitter_ModeDesc".Translate(),
            icon = TexCommand.DesirePower,
            action = () =>
            {
                if (!CanConfigure) return;
                var options = new List<FloatMenuOption>(3);
                foreach (SSCPnaMistMode option in System.Enum.GetValues<SSCPnaMistMode>())
                    options.Add(new FloatMenuOption(ModeLabel(option), () => TrySetMode(option)));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        };

        /// <summary>在共用供电与耗材状态前显示当前选定档位。</summary>
        public override string CompInspectStringExtra() =>
            "SSC_PnaMistEmitter_Mode".Translate(ModeLabel(mode)) + "\n" + base.CompInspectStringExtra();

        /// <summary>提供模式菜单、命令标签和检查栏共用的档位名称。</summary>
        private static TaggedString ModeLabel(SSCPnaMistMode value) => value switch
        {
            SSCPnaMistMode.Low => "SSC_PnaMistEmitter_Low".Translate(),
            SSCPnaMistMode.High => "SSC_PnaMistEmitter_High".Translate(),
            _ => "SSC_Emitter_Off".Translate()
        };
    }
}
