// AI 辅助生成：原版职位资格 + 生产快照的工作量/生命周期契约。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Precept_RoleSingle Role(Ideo ideo, Pawn pawn, bool leader = true)
    {
        // 使用真实职位和任职槽，直接布置状态以隔离授职通知；资格检查仍走原版 RequirementsMet。
        var role = new Precept_RoleSingle { ideo = ideo, def = new PreceptDef { defName = "FixtureRole", leaderRole = leader, roleCanBeChild = true, roleRequirements = new() } };
        var chosen = new IdeoRoleInstance(role) { pawn = pawn };
        Set(role, "chosenPawn", chosen);
        // 原版 RolesListForReading 依赖戒律缓存，夹具也按原版路径重建，而非替换职位枚举结果。
        ideo.PreceptsListForReading.Add(role); ideo.RecachePrecepts();
        return role;
    }
    private static void LeaderTests()
    {
        Check("formal leader three states and moral-guide/faction pointer exclusion", () =>
        {
            // 仅有非领袖职位及 faction.leader 指针不足以成为正式领袖。
            Context(); var master = MakePawn(PawnIdentity.Master); var nonMaster = MakePawn(); MakeMap(master, nonMaster);
            var ideo = new Ideo(); player.ideos.SetPrimary(ideo);
            Role(ideo, master, false); player.leader = master;
            Equal(SSCCultureLeaderState.None, SSCCultureLeaderUtility.Query(Collect()).State);
            // 在同一任职槽上依次验证主人、非主人和空缺三态，身份改变不能等同于职位空缺。
            var role = Role(ideo, master);
            Equal(SSCCultureLeaderState.Master, SSCCultureLeaderUtility.Query(Collect()).State);
            master.TryGetComp<CompSexSlaveTraining>().pawnIdentity = PawnIdentity.Unset;
            Equal(SSCCultureLeaderState.NonMaster, SSCCultureLeaderUtility.Query(Collect()).State);
            Set(role, "chosenPawn", null);
            Equal(SSCCultureLeaderState.None, SSCCultureLeaderUtility.Query(Collect()).State);
        });
        Check("minor ideology incumbent and conflicting valid incumbents are read-only", () =>
        {
            // 次文化的正式任职者应可被发现；主文化出现另一有效任职者后须显式返回冲突。
            Context(); var a = MakePawn(PawnIdentity.Master); var b = MakePawn(); MakeMap(a, b);
            var primary = new Ideo(); var minor = new Ideo(); player.ideos.SetPrimary(primary);
            ((List<Ideo>)AccessTools.Field(typeof(FactionIdeosTracker), "ideosMinor").GetValue(player.ideos)).Add(minor);
            var ra = Role(minor, a); Equal(a, SSCCultureLeaderUtility.Query(Collect()).Pawn);
            var rb = Role(primary, b); var result = SSCCultureLeaderUtility.Query(Collect());
            Require(result.Conflict); Equal(SSCCultureLeaderState.None, result.State);
            // 冲突判定不能自行挑选领袖或卸职，两个真实任职槽保持原值。
            Equal(a, ra.ChosenPawnSingle()); Equal(b, rb.ChosenPawnSingle());
        });
        Check("leader requires member and vanilla free non-slave eligibility plus actual requirements", () =>
        {
            // 同一主人从隐藏世界人物进入地图后，才具备已确定的名册资格。
            Context(); var p = MakePawn(PawnIdentity.Master); var ideo = new Ideo(); player.ideos.SetPrimary(ideo);
            var role = Role(ideo, p); Equal(SSCCultureLeaderState.None, SSCCultureLeaderUtility.Query(Collect()).State);
            MakeMap(p); Equal(SSCCultureLeaderState.Master, SSCCultureLeaderUtility.Query(Collect()).State);
            // 名册保留法律奴隶，但原版领袖资格排除；恢复自由后再验证实际同文化要求仍被执行。
            Set(p.guest, "guestStatusInt", GuestStatus.Slave);
            Require(Collect().Contains(p)); Equal(SSCCultureLeaderState.None, SSCCultureLeaderUtility.Query(Collect()).State);
            Set(p.guest, "guestStatusInt", GuestStatus.Guest);
            role.def.roleRequirements.Add(new RoleRequirement_SameIdeo());
            Equal(SSCCultureLeaderState.None, SSCCultureLeaderUtility.Query(Collect()).State);
        });
    }
    private static SSCCultureGameComponent Cache(bool initialize = true)
    {
        // 直接创建生产组件，允许用例刻意不初始化；计数从初始化结束后开始，不混入夹具准备工作。
        var cache = new SSCCultureGameComponent(Current.Game);
        if (initialize) cache.FinalizeInit();
        calls.Clear(); return cache;
    }
    // 未调用入口视作零次，断言的是实际工作次数而非计时器精度或机器运行速度。
    private static int CallCount(string name) => calls.GetValueOrDefault(name);
    private static void SnapshotTests()
    {
        Check("no query and personal queries perform no population work", () =>
        {
            // 个人只读属性不触发人口准备；同时用反射确认组件没有主动逐 tick 扫描入口。
            Context(); var p = MakePawn(); var cache = Cache();
            SSCCultureStateUtility.HasChain(p); SSCCultureStateUtility.IsMaster(p); SSCCultureStateUtility.TryGetActualMaster(p, out _);
            Equal(0, calls.Count); tick += 500; Equal(0, calls.Count);
            Require(!typeof(SSCCultureGameComponent).GetMethods().Any(m => m.DeclaringType == typeof(SSCCultureGameComponent) && m.Name == "GameComponentTick"));
        });
        Check("same-generation roster shared with leader; 99/100 tick invalidation", () =>
        {
            // 同代反复请求主人数与领袖，必须共用一次名册，且不能连带计算环境人数。
            Context(); var p = MakePawn(PawnIdentity.Master); MakeMap(p); var cache = Cache();
            Require(cache.TryGetMasterCount(out var n)); Equal(1, n);
            for (int i = 0; i < 20; i++) { Require(cache.TryGetMasterCount(out n)); cache.TryGetLeader(out _); }
            Equal(1, CallCount("CollectMembers")); Equal(1, CallCount("CountMasters")); Equal(1, CallCount("Query"));
            // 起点为 1000：1099 仍命中，1100 首次请求才重建，覆盖有效期的闭开边界。
            Equal(0, CallCount("CollectManagedChained")); tick = 1099; cache.TryGetMasterCount(out _); Equal(1, CallCount("CollectMembers"));
            tick = 1100; cache.TryGetMasterCount(out _); Equal(2, CallCount("CollectMembers"));
        });
        Check("late feature does not extend generation; bound counts are per requested owner", () =>
        {
            // 主人 a 的个人统计开启代但不建名册；同一主人的重复请求只执行一次绑定遍历。
            Context(); var a = MakePawn(PawnIdentity.Master); var b = MakePawn(PawnIdentity.Master); var slave = MakePawn();
            Chain(slave, a); BindCandidates(a, slave, slave); var cache = Cache();
            cache.TryGetBoundSlaveCount(a, out var n); Equal(1, n); Equal(0, CallCount("CollectMembers"));
            cache.TryGetBoundSlaveCount(a, out _); Equal(1, CallCount("CountBoundSlaves"));
            // 环境与主人 b 直到后期才被请求，仍沿用 1000 的共同起点，1100 整代一起失效。
            tick = 1090; cache.TryGetManagedChainedCount(out _); Equal(1, CallCount("CollectManagedChained"));
            tick = 1099; cache.TryGetBoundSlaveCount(b, out n); Equal(0, n); Equal(2, CallCount("CountBoundSlaves"));
            tick = 1100; cache.TryGetManagedChainedCount(out _); Equal(2, CallCount("CollectManagedChained"));
            cache.TryGetBoundSlaveCount(a, out _); Equal(3, CallCount("CountBoundSlaves"));
        });
        Check("within-generation tick rollback clears all features", () =>
        {
            // 回退后仍大于代起点，必须与最近读取时刻比较才能发现，不能只比较 generationTick。
            Context(); MakeMap(MakePawn(PawnIdentity.Master)); var cache = Cache();
            cache.TryGetMasterCount(out _); tick = 1090; cache.TryGetMasterCount(out _);
            tick = 1050; cache.TryGetMasterCount(out _); Equal(2, CallCount("CollectMembers"));
        });
        Check("native source recursion is measured separately from one SSC preparation", () =>
        {
            // 两张地图下同时记录 SSC 收集入口与原版来源/容器递归入口，展示各自实际工作量。
            Context(); var p = MakePawn(PawnIdentity.Master); Chain(p, MakePawn(PawnIdentity.Master)); MakeMap(p); MakeMap();
            var cache = Cache(); cache.TryGetMasterCount(out _);
            Equal(1, CallCount("CollectMembers")); Equal(2, CallCount("get_FreeColonists")); Equal(2, CallCount("get_ColonySubhumansControllable"));
            int rosterRecursions = CallCount("get_AllPawnsUnspawned"); Require(rosterRecursions >= 4);
            cache.TryGetManagedChainedCount(out var count); Equal(1, count);
            int allRecursions = CallCount("get_AllPawnsUnspawned"); Require(allRecursions > rosterRecursions);
            // 准备完成后连续读取两个项目，底层递归也不得增长，避免只验证表层缓存命中。
            for (int i = 0; i < 50; i++) { cache.TryGetMasterCount(out _); cache.TryGetManagedChainedCount(out _); }
            Equal(allRecursions, CallCount("get_AllPawnsUnspawned"));
            Console.WriteLine($"WORK roster SSC=1, native recursive entries={rosterRecursions}; roster+environment recursive entries={allRecursions}; 50 repeated reads add 0");
        });
        Check("unready/loading/DLC-off reads are unavailable, not cached zero; old Game cannot read", () =>
        {
            // 未初始化、Scribe 读取中及 DLC 关闭都返回不可查询；恢复后必须真正收集，不能命中伪零值。
            Context(); MakeMap(MakePawn(PawnIdentity.Master)); var cache = Cache(false);
            Require(!cache.TryGetMasterCount(out _)); Equal(0, calls.Count);
            cache.FinalizeInit(); Scribe.mode = LoadSaveMode.LoadingVars;
            Require(!cache.TryGetMasterCount(out _)); Scribe.mode = LoadSaveMode.Inactive;
            SetStatic(typeof(ModsConfig), "ideologyActive", false); Require(!cache.TryGetMasterCount(out _));
            SetStatic(typeof(ModsConfig), "ideologyActive", true); Require(cache.TryGetMasterCount(out var n)); Equal(1, n);
            // LoadedGame 清除旧代；随后切换 Game，旧组件拒绝查询，新组件才可返回有效的零人数。
            cache.LoadedGame(); cache.TryGetMasterCount(out _); Equal(2, CallCount("CollectMembers"));
            Context(); Require(!cache.TryGetMasterCount(out _));
            var fresh = Cache(); Require(fresh.TryGetMasterCount(out n)); Equal(0, n);
        });
    }

    private static void SerializationTests()
    {
        Check("real Scribe component roundtrip saves no snapshot fields and first load read rebuilds", () =>
        {
            // 先填充生产快照，再用真实 Scribe 保存，确认缓存结果和 Pawn 引用没有落入组件节点。
            Context(); MakeMap(MakePawn(PawnIdentity.Master)); var source = Cache(); source.TryGetMasterCount(out _);
            string path = Path.Combine(saveDirectory, "cache.xml");
            try
            {
                Scribe.saver.InitSaving(path, "CacheFixture"); Scribe_Deep.Look(ref source, "cache"); Scribe.saver.FinalizeSaving();
                Require(!System.Xml.Linq.XDocument.Load(path).Root.Element("cache").Elements().Any());
                // 反序列化只构造组件；必须经过 LoadedGame 初始化后，首次读取才重建当前人口。
                SSCCultureGameComponent loaded = null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "cache", Current.Game); Scribe.loader.FinalizeLoading();
                Require(!loaded.TryGetMasterCount(out _)); loaded.LoadedGame();
                Require(loaded.TryGetMasterCount(out var n)); Equal(1, n);
            }
            // 保存/载入任一断言失败时也恢复全局 Scribe 模式，避免下一例被误判为加载中。
            finally { Scribe.mode = LoadSaveMode.Inactive; }
        });
        Check("real Scribe culture roundtrip preserves zero-weight choices without generation reset", () =>
        {
            // 手动选择零生成权重的合法档位，并提供原版序列化必需的最小文化字段及唯一 ID。
            Context(); Ideo source = Culture(mainMeme); Add(source, "SexSlave_Envy"); Add(source, "Trainer_Hostile");
            Add(source, "Master_Deference"); Add(source, "MasterCount_One"); Add(source, "SlaveCount_Unlimited");
            Add(source, "SlaveGender_FemaleRequired");
            source.culture = new CultureDef { defName = "FixtureCulture" }; DefDatabase<CultureDef>.Add(source.culture);
            source.description = "[TODO:FixtureCulture.description]"; source.memberName = "[TODO:member]";
            Set(source, "id", 1);
            foreach (var precept in source.PreceptsListForReading) precept.Init(source);
            string path = Path.Combine(saveDirectory, "culture.xml");
            try
            {
                Scribe.saver.InitSaving(path, "CultureFixture"); Scribe_Deep.Look(ref source, "ideo"); Scribe.saver.FinalizeSaving();
                Ideo loaded = null;
                Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "ideo"); Scribe.loader.FinalizeLoading();
                // 同时核对五组合法性与完整 Def 名集合，不能把读档后的自动重随机误判为保留成功。
                VerifyGroups(loaded);
                Equal(string.Join(',', source.PreceptsListForReading.Select(p => p.def.defName).Order()),
                    string.Join(',', loaded.PreceptsListForReading.Select(p => p.def.defName).Order()));
            }
            // 无论 Scribe 过程是否成功，后续测试都应回到非存读档状态。
            finally { Scribe.mode = LoadSaveMode.Inactive; }
        });
    }
}
