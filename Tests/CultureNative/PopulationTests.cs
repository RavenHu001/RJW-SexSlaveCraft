// AI 辅助生成：真实地图人口/商队/ThingOwner 容器与生产查询，不模拟筛选返回值。
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn MakePawn(PawnIdentity identity = PawnIdentity.Unset, Faction faction = null)
    {
        // ThingDef 的默认图形会启动 Waylith shader；只绕过 Def 的图形构造，人口与 Pawn 属性仍为原版。
        var def = Bare<ThingDef>(); def.defName = "FixtureHuman"; def.category = ThingCategory.Pawn; def.stackLimit = 1;
        def.race = new RaceProperties { intelligence = Intelligence.Humanlike };
        var pawn = new Pawn { def = def };
        // 补齐原版人口属性访问的真实 tracker；displayOrder 哨兵用于发现意外的人物栏排序写入。
        Set(pawn, "factionInt", faction ?? player);
        pawn.health = new Pawn_HealthTracker(pawn);
        pawn.guest = new Pawn_GuestTracker(pawn);
        pawn.playerSettings = Bare<Pawn_PlayerSettings>();
        Set(pawn.playerSettings, "pawn", pawn); pawn.playerSettings.displayOrder = -9999999;
        pawn.ageTracker = new Pawn_AgeTracker(pawn);
        pawn.mindState = new Verse.AI.Pawn_MindState(pawn);
        pawn.ideo = new Pawn_IdeoTracker(pawn);
        // 身份放在实际训练组件中，避免把身份工具函数替换成测试预设答案。
        var comp = new CompSexSlaveTraining { parent = pawn, pawnIdentity = identity };
        Set(pawn, "comps", new List<ThingComp> { comp });
        return pawn;
    }

    private static Hediff_ChainOfSexSlave Chain(Pawn slave, Pawn master)
    {
        // 直接布置实际健康列表，隔离绑定事务的通知/奖励；被测读取仍走生产 GetChain/ValidTargets。
        var chain = new Hediff_ChainOfSexSlave { pawn = slave, def = SSCDefOf.ChainOfSexSlave, target = master };
        slave.health.hediffSet.hediffs.Add(chain);
        return chain;
    }

    private static void BindCandidates(Pawn master, params Pawn[] slaves)
    {
        // 直接保留输入中的重复、空项和陈旧引用，交由实际绑定迭代器执行校验。
        var bridle = new Hediff_BridleOfSexSlave { pawn = master, def = SSCDefOf.BridleOfSexSlave, targets = slaves.ToList() };
        master.health.hediffSet.hediffs.Add(bridle);
    }

    private static Map MakeMap(params Pawn[] pawns)
    {
        // 建立真实 MapPawns 和持有物来源，仅绕过生成/出生流程；不替换任何人口属性结果。
        var map = new Map();
        map.listerThings = new ListerThings(ListerThingsUse.Global);
        map.passingShipManager = new PassingShipManager(map);
        map.mapPawns = new MapPawns(map);
        ((List<Pawn>)AccessTools.Field(typeof(MapPawns), "pawnsSpawned").GetValue(map.mapPawns)).AddRange(pawns);
        maps.Add(map);
        return map;
    }

    private static Game Context()
    {
        // 每例重建阵营、世界和管理器，避免地图、任务或正式职位残留影响下一个断言。
        maps = new(); worldObjects = new WorldObjectsHolder();
        player = new Faction { def = new FactionDef { defName = "FixturePlayer", isPlayer = true } };
        player.ideos = new FactionIdeosTracker(player);
        var game = Bare<Game>();
        game.tickManager = new TickManager();
        game.uniqueIDsManager = new UniqueIDsManager();
        game.questManager = new QuestManager();
        game.World = Bare<World>();
        game.World.factionManager = new FactionManager();
        game.World.factionManager.AllFactionsListForReading.Add(player);
        // 统一从可查询的 1000 tick 开始，边界用例再显式切换 DLC、Scribe 或时间。
        Current.Game = game;
        Current.ProgramState = ProgramState.Playing;
        SetStatic(typeof(ModsConfig), "ideologyActive", true);
        tick = 1000;
        return game;
    }

    private static HashSet<Pawn> Collect()
    {
        // 测试集合与生产快照一样按引用去重，人口来源和筛选全部交给实际生产入口。
        var set = new HashSet<Pawn>(ReferenceEqualityComparer.Instance);
        SSCCulturePopulationUtility.CollectMembers(set);
        return set;
    }

    private static void PopulationTests()
    {
        Check("chain identity is independent of pathway preselection; actual owner has no trainer fallback", () =>
        {
            // 先验证仅预选 Slave 不等于持链，再撤销预选、直接持链验证两个口径独立。
            Context(); var p = MakePawn(PawnIdentity.Slave); var master = MakePawn(PawnIdentity.Master);
            Require(!SSCCultureStateUtility.HasChain(p)); Require(!SSCCultureStateUtility.TryGetActualMaster(p, out _));
            p.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            var chain = Chain(p, master);
            Require(SSCCultureStateUtility.HasChain(p)); Require(SSCCultureStateUtility.TryGetActualMaster(p, out var owner)); Equal(master, owner);
            // 主人失去身份或链指向本人都应查询失败，不能退回调教员/预选身份作为实际主人。
            master.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Slave;
            Require(!SSCCultureStateUtility.TryGetActualMaster(p, out _));
            chain.target = p; Require(!SSCCultureStateUtility.TryGetActualMaster(p, out _));
            Require(!SSCCultureStateUtility.IsActiveOfficer(master));
        });
        Check("real map sources: cross-map dedup, hidden player excluded, legal slave retained", () =>
        {
            // 同一对象放入两个地图来源；隐藏对象只有玩家阵营，不应被主动补入，法律奴隶仍可在名册。
            Context(); var a = MakePawn(PawnIdentity.Master); var b = MakePawn(); var hidden = MakePawn(PawnIdentity.Master);
            var slave = MakePawn(PawnIdentity.Master); Set(slave.guest, "guestStatusInt", GuestStatus.Slave);
            MakeMap(a, b, slave); MakeMap(a);
            var set = Collect(); Equal(3, set.Count); Require(set.Contains(b) && set.Contains(slave) && !set.Contains(hidden));
            Equal(2, SSCCulturePopulationUtility.CountMasters(set));
        });
        Check("managed chained prisoners are environmental population, not members; visitors excluded", () =>
        {
            // 两名外方人物同有 HostFaction 和锁链，只有实际囚犯进入环境人数，二者都不充当殖民地成员。
            Context(); var master = MakePawn(PawnIdentity.Master); var prisoner = MakePawn(faction: new Faction());
            Set(prisoner.guest, "guestStatusInt", GuestStatus.Prisoner); Set(prisoner.guest, "hostFactionInt", player);
            var visitor = MakePawn(faction: new Faction()); Set(visitor.guest, "hostFactionInt", player);
            Chain(prisoner, master); Chain(visitor, master); MakeMap(master, prisoner, visitor);
            Require(!Collect().Contains(prisoner)); Equal(1, SSCCulturePopulationUtility.CountManagedChained(new()));
        });
        Check("real nested holder collection includes unspawned members without UI recache", () =>
        {
            // 使用真实地图持有物递归，先验证未出生角色，再增加冷冻舱中的第二层持有物。
            Context(); var map = MakeMap(); var held = MakePawn(PawnIdentity.Master);
            var holder = new FixtureMapHolder(map); map.components.Add(holder);
            holder.GetDirectlyHeldThings().TryAdd(held);
            Require(map.mapPawns.FreeColonists.Contains(held)); Require(Collect().Contains(held));
            var casketDef = Bare<ThingDef>(); casketDef.defName = "FixtureCasket"; casketDef.category = ThingCategory.Building; casketDef.stackLimit = 1;
            var casket = new Building_CryptosleepCasket { def = casketDef };
            holder.GetDirectlyHeldThings().TryAdd(casket);
            var inner = MakePawn(PawnIdentity.Master); casket.GetDirectlyHeldThings().TryAdd(inner);
            Equal(2, SSCCulturePopulationUtility.CountMasters(Collect()));
            // 只读统计不得借人物栏重排改变原始排序哨兵。
            Equal(-9999999, inner.playerSettings.displayOrder);
        });
        Check("real player caravan membership is independent of loaded maps", () =>
        {
            // 不创建地图，只把成员放入真实玩家商队，验证离图和纯商队存档口径。
            Context(); var a = MakePawn(PawnIdentity.Master); var caravan = new Caravan(); Set(caravan, "factionInt", player);
            caravan.pawns.TryAdd(a); worldObjects.Caravans.Add(caravan);
            Require(caravan.IsPlayerControlled); Require(Collect().Contains(a)); Equal(1, SSCCulturePopulationUtility.CountMasters(Collect()));
        });
        Check("native quest extra-faction lodger is excluded from member and environment collections", () =>
        {
            // 用实际任务 ExtraFaction 部件制造借住资格，不伪造 IsQuestLodger 的返回值。
            Context(); var p = MakePawn(PawnIdentity.Master); Chain(p, MakePawn(PawnIdentity.Master)); MakeMap(p);
            var quest = new Quest { acceptanceTick = 0 };
            var part = new QuestPart_ExtraFaction { quest = quest, extraFaction = new ExtraFaction(new Faction(), ExtraFactionType.HomeFaction), affectedPawns = new() { p } };
            Current.Game.questManager.ExtraFactionQuestParts.Add(part);
            Require(p.IsQuestLodger()); Require(!Collect().Contains(p));
            Equal(0, SSCCulturePopulationUtility.CountManagedChained(new()));
        });
        Check("travelling transporter is an environment source but never expands the member roster", () =>
        {
            // 放入实际在途运输容器；原版广域人口能发现它，但人物栏名册不能因此扩大。
            Context(); var p = MakePawn(PawnIdentity.Master); Chain(p, MakePawn(PawnIdentity.Master));
            var transport = new TravellingTransporters(); var info = new ActiveTransporterInfo(transport);
            info.innerContainer.TryAdd(p);
            ((List<ActiveTransporterInfo>)AccessTools.Field(typeof(TravellingTransporters), "transporters").GetValue(transport)).Add(info);
            worldObjects.TravellingTransporters.Add(transport);
            Require(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.Contains(p));
            Require(!Collect().Contains(p)); Equal(1, SSCCulturePopulationUtility.CountManagedChained(new()));
        });
        Check("native subhuman sources retain the map/caravan distinction", () =>
        {
            // 仅此例开放 Anomaly，使用真实 mutant 属性复现地图可控制亚人与商队资格的差别。
            Context(); SetStatic(typeof(ModsConfig), "anomalyActive", true);
            try
            {
                var p = MakePawn(PawnIdentity.Master); p.mutant = new Pawn_MutantTracker(p);
                Set(p.mutant, "def", new MutantDef { consideredSubhuman = true, canBeDrafted = true });
                var map = MakeMap(); var holder = new FixtureMapHolder(map); map.components.Add(holder); holder.GetDirectlyHeldThings().TryAdd(p);
                Require(p.IsSubhuman && !p.IsColonist && !p.IsColonySubhumanPlayerControlled);
                Require(map.mapPawns.ColonySubhumansControllable.Contains(p)); Require(Collect().Contains(p));
                maps.Clear(); var caravan = new Caravan(); Set(caravan, "factionInt", player); worldObjects.Caravans.Add(caravan);
                // 直接布置来源列表，避免移动通知；商队仍使用真实 IsColonySubhumanPlayerControlled。
                caravan.PawnsListForReading.Add(p); Require(!Collect().Contains(p));
            }
            // 无论断言是否通过，都恢复全局 DLC 状态供后续用例使用。
            finally { SetStatic(typeof(ModsConfig), "anomalyActive", false); }
        });
        Check("bound iterator rejects stale, duplicate, dead, discarded and self candidates without writes", () =>
        {
            // 混合有效外方绑定与各种无效缰绳候选，覆盖有向链核对、存活过滤和引用去重。
            Context(); var master = MakePawn(PawnIdentity.Master); var a = MakePawn(faction: new Faction());
            var stale = MakePawn(); var dead = MakePawn(); var discarded = MakePawn();
            Chain(a, master); Chain(stale, MakePawn(PawnIdentity.Master)); Chain(dead, master); Chain(discarded, master); Chain(master, master);
            Set(dead.health, "healthState", PawnHealthState.Dead); Set(discarded, "mapIndexOrState", (sbyte)-3);
            BindCandidates(master, a, a, stale, dead, discarded, master, null);
            // 查询前后对比原始候选与链目标，证明计数没有顺便清理或修复绑定数据。
            var before = SSCBondUtility.GetBridle(master).targets.ToArray();
            Equal(1, SSCCulturePopulationUtility.CountBoundSlaves(master, new()));
            Require(before.SequenceEqual(SSCBondUtility.GetBridle(master).targets));
            Equal(master, SSCBondUtility.GetBoundMaster(a));
        });
    }

    private sealed class FixtureMapHolder : MapComponent, IThingHolder
    {
        // 让真实地图持有物发现路径进入 ThingOwner；此夹具不直接返回任何 Pawn 收集结果。
        private readonly ThingOwner things;
        public FixtureMapHolder(Map map) : base(map) { things = new ThingOwner<Thing>(this); }
        public IThingHolder ParentHolder => map;
        public ThingOwner GetDirectlyHeldThings() => things;
        // 子容器仍由原版工具递归展开，验证冷冻舱等嵌套持有物而非测试侧手动展平。
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, things);
    }
}
