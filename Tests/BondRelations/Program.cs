// AI 辅助生成
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Extensions = Verse.Extensions;

internal static partial class Program
{
    static int cases, failures;
    static PawnRelationDef customCapRelation;
    /// <summary>模拟第三方对原版大写称谓入口的后置修改，检查包装器是否完整保留它。</summary>
    static void CustomCap(PawnRelationDef __instance, ref string __result)
    {
        if (__instance == customCapRelation) __result = "custom: " + __result;
    }
    /// <summary>把失败条件转换为用例异常，不中断后续独立场景。</summary>
    static void Require(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    /// <summary>清空世界角色快照后独立执行用例，累计通过数量及失败详情。</summary>
    static void Check(string name, Action action)
    {
        cases++; SSCRestrictionGameComponent.Pawns.Clear();
        ResetOverviewWorld();
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    /// <summary>构造具有主人身份但尚无绑定的角色，不在构造中授予任何社交奖励。</summary>
    static Pawn Master() => new Pawn { Training = { pawnIdentity = PawnIdentity.Master } };
    /// <summary>调用生产 Bind 建立锁链及缰绳，让测试观察真实生命周期入口。</summary>
    static (Pawn master, Pawn slave) Bound()
    {
        var master = Master(); var slave = new Pawn();
        Require(SSCBondUtility.Bind(master, slave));
        return (master, slave);
    }
    /// <summary>将边界好感设为恰好 80，调用生产仪式奖励服务准备已赚取的关系。</summary>
    static void Award(Pawn master, Pawn slave)
    {
        slave.relations.BaseOpinion = 80;
        Require(SSCBondRelationUtility.TryCreateAfterRitual(master, slave));
    }
    /// <summary>只查询社交存储结果，不根据锁链推算测试应当得到的结果。</summary>
    static bool Has(Pawn first, Pawn second) => first.relations.DirectRelationExists(SSCDefOf.SSC_FlawedLovers, second);
    /// <summary>读取仓库的真实翻译文件，使称谓断言同时检查键名和发布目录镜像。</summary>
    static void LoadLanguage(string root, string lang)
    {
        Extensions.Translations.Clear();
        var xml = XDocument.Load(SscTestPaths.Combine(root, "Languages", lang, "Keyed/SSC_BondRelations.xml"));
        foreach (var e in xml.Root.Elements()) Extensions.Translations[e.Name.LocalName] = e.Value;
    }
    /// <summary>运行关系授予、生命周期、旧档清理及真实 Harmony 显示回归。</summary>
    static int Main(string[] args)
    {
        if (args.Contains("--benchmark")) return BondOverviewBenchmark.Run();
        string root = SscTestPaths.RepositoryRoot(args.Length > 0 ? args[0] : null);
        // 健康通知经实际 Harmony 生产补丁进入离体维护，CE 兼容测试不直接调用清理方法。
        var harmony = new Harmony("ssc.tests.bond-relations");
        harmony.CreateClassProcessor(typeof(SSCBondHealthChanged)).Patch();
        // 先验证授予规则：实际主人仍需达到原门槛，第三方主持资格不能变成所有权。
        foreach (int opinion in new[] { 79, 80, 100 })
            Check("bound master ritual threshold " + opinion, () =>
            {
                var f = Bound(); f.slave.relations.BaseOpinion = opinion;
                Require(SSCBondRelationUtility.TryCreateAfterRitual(f.master, f.slave) == (opinion >= 80));
                Require(Has(f.master, f.slave) == (opinion >= 80) && Has(f.slave, f.master) == (opinion >= 80));
            });
        foreach (PawnIdentity identity in new[] { PawnIdentity.Master, PawnIdentity.Slave, PawnIdentity.Unset })
            Check("third-party host cannot gain ownership relation: " + identity, () =>
            {
                var f = Bound(); Award(f.master, f.slave);
                var trainer = new Pawn { Training = { pawnIdentity = identity } };
                f.slave.Training.selectedTrainer = trainer; f.slave.relations.BaseOpinion = 100;
                Require(!SSCBondRelationUtility.TryCreateAfterRitual(trainer, f.slave));
                Require(!Has(trainer, f.slave) && Has(f.master, f.slave));
                Require(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
            });
        Check("high opinion without a chain cannot create a relation", () =>
        { var m = Master(); var s = new Pawn(); s.relations.BaseOpinion = 100; Require(!SSCBondRelationUtility.TryCreateAfterRitual(m, s)); });
        Check("reversed ritual arguments cannot borrow reciprocal validity", () =>
        { var f = Bound(); f.master.relations.BaseOpinion = 100; Require(!SSCBondRelationUtility.TryCreateAfterRitual(f.slave, f.master)); });
        // 绑定和维护只整理归属，不能补发仪式奖励；已经合法获得的奖励也不持续要求 80 好感。
        Check("binding and repair never award the ritual reward", () =>
        { var f = Bound(); f.slave.relations.BaseOpinion = 100; SSCBondUtility.RepairReciprocalLink(f.slave); Require(!Has(f.master, f.slave)); });
        Check("repeat ritual does not duplicate; later opinion loss does not revoke valid reward", () =>
        {
            var f = Bound(); Award(f.master, f.slave); f.slave.relations.BaseOpinion = -100;
            Require(!SSCBondRelationUtility.TryCreateAfterRitual(f.master, f.slave));
            Require(SSCBondRelationUtility.RemoveInvalidRelations(f.slave) == 0 && Has(f.master, f.slave));
            Require(f.slave.relations.DirectRelations.Count == 1);
        });
        // 使用生产绑定服务和真实锁链类验证关系移除时机，并观察双方列表及缓存通知。
        Check("unbind removes both directions and notifies both caches", () =>
        {
            var f = Bound(); Award(f.master, f.slave);
            Require(SSCBondUtility.Unbind(f.slave));
            Require(!Has(f.master, f.slave) && !Has(f.slave, f.master));
            Require(f.master.relations.CacheNotifications == 2 && f.slave.relations.CacheNotifications == 2);
        });
        Check("rebind removes old relation without transferring reward", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var next = Master();
            Require(SSCBondUtility.Bind(next, f.slave, true));
            Require(!Has(f.master, f.slave) && !Has(next, f.slave));
            Require(SSCBondUtility.GetBoundMaster(f.slave) == next);
            Require(SSCBondUtility.GetBridle(f.master) == null);
        });
        Check("rejected takeover preserves valid existing reward", () =>
        { var f = Bound(); Award(f.master, f.slave); Require(!SSCBondUtility.Bind(Master(), f.slave)); Require(Has(f.master, f.slave)); });
        Check("external removal of real chain cleans relations through PostRemoved", () =>
        { var f = Bound(); Award(f.master, f.slave); f.slave.health.RemoveHediff(SSCBondUtility.GetChain(f.slave)); Require(!Has(f.master, f.slave) && !Has(f.slave, f.master)); });
        // 本机 CE 1.6 的 HealthTool.RemoveHediff 直接删集合后通知，完全不调用 PostRemoved。
        Check("CE removal without PostRemoved immediately revokes both social records", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var chain = SSCBondUtility.GetChain(f.slave);
            f.master.health.AddHediff(new Hediff { def = SSCDefOf.SSC_MasterBondEmpowerment });
            f.slave.relations.BaseOpinion = -20;
            Require(f.slave.relations.OpinionOf(f.master) == 80);
            f.slave.health.hediffSet.hediffs.Remove(chain);
            f.slave.health.Notify_HediffChanged(chain);
            Require(!Has(f.master, f.slave) && !Has(f.slave, f.master), "CE left stored relations after deleting chain");
            Require(SSCBondUtility.GetBridle(f.master) == null, "CE left empty bridle");
            Require(f.master.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_MasterBondEmpowerment) == null);
            Require(f.slave.relations.OpinionOf(f.master) == -20, "removed relation still contributes opinion");
            int notifications = f.slave.relations.CacheNotifications;
            f.slave.health.Notify_HediffChanged(chain);
            Require(f.slave.relations.CacheNotifications == notifications, "repeat notification removed twice");
        });
        Check("CE removal preserves other slaves and ordinary social relations", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var second = new Pawn();
            Require(SSCBondUtility.Bind(f.master, second)); Award(f.master, second);
            var friend = new PawnRelationDef { label = "friend", opinionOffset = 5 };
            f.master.relations.AddDirectRelation(friend, f.slave);
            var chain = SSCBondUtility.GetChain(f.slave);
            f.slave.health.hediffSet.hediffs.Remove(chain);
            f.slave.health.Notify_HediffChanged(chain);
            Require(!Has(f.master, f.slave) && !Has(f.slave, f.master));
            Require(Has(f.master, second) && Has(second, f.master));
            Require(SSCBondUtility.GetBridle(f.master).ValidTargets.SequenceEqual(new[] { second }));
            Require(f.master.relations.DirectRelationExists(friend, f.slave) && f.slave.relations.DirectRelationExists(friend, f.master));
        });
        Check("CE severity notifications preserve an attached chain and its reward", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var chain = SSCBondUtility.GetChain(f.slave);
            int notifications = f.slave.relations.CacheNotifications;
            chain.Severity = 0.7f; f.slave.health.Notify_HediffChanged(chain);
            Require(Has(f.master, f.slave) && Has(f.slave, f.master));
            Require(SSCBondUtility.GetBridle(f.master).ValidTargets.Single() == f.slave);
            Require(f.slave.relations.CacheNotifications == notifications);
        });
        Check("unrelated health notifications do not run SSC bond maintenance", () =>
        {
            var f = Bound(); Award(f.master, f.slave);
            f.slave.health.hediffSet.hediffs.Remove(SSCBondUtility.GetChain(f.slave));
            f.slave.health.Notify_HediffChanged(new Hediff { pawn = f.slave });
            f.slave.health.Notify_HediffChanged(null);
            Require(Has(f.master, f.slave), "unrelated hediff triggered bond cleanup");
        });
        Check("CE removal notification also cleans a dead master's empty bridle", () =>
        {
            var f = Bound(); Award(f.master, f.slave); f.master.Dead = true;
            var chain = SSCBondUtility.GetChain(f.slave);
            f.slave.health.hediffSet.hediffs.Remove(chain);
            f.slave.health.Notify_HediffChanged(chain);
            Require(SSCBondUtility.GetBridle(f.master) == null && !Has(f.master, f.slave) && !Has(f.slave, f.master));
        });
        Check("late removal notification preserves a replacement chain to the same master", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var oldChain = SSCBondUtility.GetChain(f.slave);
            var bridle = SSCBondUtility.GetBridle(f.master); bridle.shareDamageEnabled = false;
            f.slave.health.hediffSet.hediffs.Remove(oldChain);
            var replacement = Hediff_ChainOfSexSlave.AddToPawn(f.slave, f.master);
            f.slave.health.Notify_HediffChanged(oldChain);
            Require(SSCBondUtility.GetChain(f.slave) == replacement && Has(f.master, f.slave));
            Require(SSCBondUtility.GetBridle(f.master) == bridle && !bridle.shareDamageEnabled);
            Require(bridle.ValidTargets.Single() == f.slave);
        });
        Check("direct chain replacement also cleans prior reward", () =>
        { var f = Bound(); Award(f.master, f.slave); Hediff_ChainOfSexSlave.AddToPawn(f.slave, Master()); Require(!Has(f.master, f.slave)); });
        // 实机反馈路径：暂停中先手动删性奴锁链，再杀主人，清理不得依赖后续健康 Tick。
        Check("manual last-chain deletion immediately removes dead master's bridle and empowerment", () =>
        {
            var f = Bound(); Award(f.master, f.slave);
            f.master.health.AddHediff(new Hediff { def = SSCDefOf.SSC_MasterBondEmpowerment });
            f.slave.health.RemoveHediff(SSCBondUtility.GetChain(f.slave));
            f.master.Dead = true;
            Require(SSCBondUtility.GetBridle(f.master) == null, "empty bridle survived without a tick");
            Require(f.master.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_MasterBondEmpowerment) == null);
            Require(!Has(f.master, f.slave) && !Has(f.slave, f.master));
        });
        Check("removing one slave's chain retains the master's other real bond", () =>
        {
            var f = Bound(); var second = new Pawn(); Require(SSCBondUtility.Bind(f.master, second));
            Award(f.master, f.slave); Award(f.master, second);
            f.slave.health.RemoveHediff(SSCBondUtility.GetChain(f.slave));
            Require(SSCBondUtility.GetBridle(f.master).ValidTargets.SequenceEqual(new[] { second }));
            Require(!Has(f.master, f.slave) && Has(f.master, second));
        });
        // 公开字段被外部直接改写时，生产锁链的 Tick 必须清旧关系并补齐新反向索引，不能授予新奖励。
        Check("direct target mutation follows the new chain on its next tick", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var next = Master(); var chain = SSCBondUtility.GetChain(f.slave);
            chain.target = next; chain.Tick();
            Require(SSCBondUtility.GetBridle(f.master) == null && !Has(f.master, f.slave));
            Require(SSCBondUtility.GetBridle(next).ValidTargets.Single() == f.slave && !Has(next, f.slave));
            int notifications = f.slave.relations.CacheNotifications; chain.Tick();
            Require(f.slave.relations.CacheNotifications == notifications, "stable tick repeated relation writes");
        });
        Check("clearing chain target removes social reward and old bridle", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var chain = SSCBondUtility.GetChain(f.slave);
            chain.target = null; chain.Tick();
            Require(SSCBondUtility.GetBridle(f.master) == null && !Has(f.master, f.slave));
        });
        Check("target mutation followed by same-frame removal cleans both old indices", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var chain = SSCBondUtility.GetChain(f.slave); var next = Master();
            chain.target = next; f.slave.health.RemoveHediff(chain);
            Require(SSCBondUtility.GetBridle(f.master) == null && !Has(f.master, f.slave));
        });
        Check("direct AddToPawn replacement immediately removes old master bridle", () =>
        {
            var f = Bound(); Award(f.master, f.slave);
            Hediff_ChainOfSexSlave.AddToPawn(f.slave, Master());
            Require(SSCBondUtility.GetBridle(f.master) == null && !Has(f.master, f.slave));
        });
        Check("detached old chain tick cannot replace a new bridle or reset its preference", () =>
        {
            var f = Bound(); var old = SSCBondUtility.GetChain(f.slave);
            f.slave.health.RemoveHediff(old); Require(SSCBondUtility.Bind(f.master, f.slave));
            var current = SSCBondUtility.GetBridle(f.master); current.shareDamageEnabled = false;
            old.target = Master(); old.Tick();
            Require(SSCBondUtility.GetBridle(f.master) == current && !current.shareDamageEnabled);
        });
        Check("stale bridle cannot unbind a slave belonging to another master", () =>
        {
            var f = Bound(); var staleMaster = Master();
            var staleBridle = Hediff_BridleOfSexSlave.AddToPawn(staleMaster, f.slave);
            Require(!staleBridle.ValidTargets.Any());
            Require(SSCBondUtility.UnbindAllFromMaster(staleMaster) == 0);
            Require(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Check("load prunes dead owner's stale bridle without waiting for a tick", () =>
        {
            var f = Bound(); var staleMaster = Master(); staleMaster.Dead = true;
            Hediff_BridleOfSexSlave.AddToPawn(staleMaster, f.slave);
            SSCRestrictionGameComponent.Pawns.AddRange(new[] { staleMaster, f.master, f.slave });
            new SSCBondRelationGameComponent(new Game()).FinalizeInit();
            Require(SSCBondUtility.GetBridle(staleMaster) == null && SSCBondUtility.GetBridle(f.master) != null);
            Require(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        // 模拟加载后的混合历史：正确主人、错误主人、无链关系及普通朋友同时存在。
        // 直接执行生产 GameComponent，确认清理顺序、重复执行及反向记录删除均安全。
        Check("old save migration removes false masters from either endpoint, including off-map and dead pawns", () =>
        {
            var f = Bound(); Award(f.master, f.slave); var falseMaster = Master(); falseMaster.Dead = true;
            falseMaster.relations.AddDirectRelation(SSCDefOf.SSC_FlawedLovers, f.slave);
            var orphan = Master(); var noChain = new Pawn(); orphan.relations.AddDirectRelation(SSCDefOf.SSC_FlawedLovers, noChain);
            var friend = new PawnRelationDef { label = "friend", labelFemale = "friend", opinionOffset = 5 };
            falseMaster.relations.AddDirectRelation(friend, f.slave);
            SSCRestrictionGameComponent.Pawns.AddRange(new[] { falseMaster, orphan, f.master, f.slave, noChain });
            var game = new SSCBondRelationGameComponent(new Game()); game.FinalizeInit(); game.FinalizeInit();
            Require(Has(f.master, f.slave) && !Has(falseMaster, f.slave) && !Has(f.slave, falseMaster) && !Has(orphan, noChain));
            Require(falseMaster.relations.DirectRelationExists(friend, f.slave));
            Require(SSCBondUtility.GetBoundMaster(f.slave) == f.master);
        });
        Check("load without reward does not auto-award at 100 opinion", () =>
        {
            var f = Bound(); f.slave.relations.BaseOpinion = 100;
            SSCRestrictionGameComponent.Pawns.AddRange(new[] { f.master, f.slave });
            new SSCBondRelationGameComponent(new Game()).FinalizeInit(); Require(!Has(f.master, f.slave));
        });
        // 死亡与解绑分开验证；销毁和丢弃则按锁链原有失效规则清理。
        Check("dead but still bound master keeps valid reward", () =>
        { var f = Bound(); Award(f.master, f.slave); f.master.Dead = true; Require(!SSCBondUtility.GetChain(f.slave).ShouldRemove); Require(SSCBondRelationUtility.RemoveInvalidRelations(f.slave) == 0 && Has(f.master, f.slave)); });
        Check("destroyed master loses invalid reward", () =>
        { var f = Bound(); Award(f.master, f.slave); f.master.Destroyed = true; Require(SSCBondRelationUtility.RemoveInvalidRelations(f.slave) == 1 && !Has(f.master, f.slave)); });
        Check("discarded master loses invalid reward", () =>
        { var f = Bound(); Award(f.master, f.slave); f.master.Discarded = true; Require(SSCBondRelationUtility.RemoveInvalidRelations(f.slave) == 1 && !Has(f.master, f.slave)); });
        // 唯一主人约束作用于每个性奴，不能错误限制主人只能拥有一名性奴。
        Check("master-side cleanup preserves multiple genuinely bound slaves", () =>
        {
            var f = Bound(); var second = new Pawn(); Require(SSCBondUtility.Bind(f.master, second));
            Award(f.master, f.slave); Award(f.master, second);
            Require(SSCBondRelationUtility.RemoveInvalidRelations(f.master) == 0);
            Require(Has(f.master, f.slave) && Has(f.master, second));
            SSCBondUtility.Unbind(f.slave); Require(!Has(f.master, f.slave) && Has(f.master, second));
        });
        Check("null/self pair cannot create a reward", () =>
        { var pawn = Master(); Require(!SSCBondRelationUtility.IsValidPair(null, pawn) && !SSCBondRelationUtility.IsValidPair(pawn, pawn)); Require(!SSCBondRelationUtility.TryCreateAfterRitual(null, pawn)); });

        // 安装生产显示补丁后再查询名称，确保验证真实 IL 参数装载而非直接调用包装器。
        harmony.CreateClassProcessor(typeof(Harmony_BondRelationLabels)).Patch();
        harmony.CreateClassProcessor(typeof(Harmony_BondRelationOpinionLabels)).Patch();
        // 四语及主从双方的性别组合共用实际翻译；同时检查列表、悬停和混合关系中的普通名称分支。
        foreach (string lang in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            foreach (Gender masterGender in new[] { Gender.Male, Gender.Female })
                foreach (Gender slaveGender in new[] { Gender.Male, Gender.Female })
                    Check($"actual Harmony labels and tooltip: {lang}, master={masterGender}, slave={slaveGender}", () =>
                    {
                        LoadLanguage(root, lang); var f = Bound(); Award(f.master, f.slave);
                        f.master.gender = masterGender; f.slave.gender = slaveGender;
                        string m = Extensions.Translations["SSC_BondRelation_Master" + (masterGender == Gender.Female ? "Female" : "")];
                        string s = Extensions.Translations["SSC_BondRelation_Slave" + (slaveGender == Gender.Female ? "Female" : "")];
                        var def = SSCDefOf.SSC_FlawedLovers;
                        Require(SocialCardUtility.Render(f.slave, f.master, def) == m.CapitalizeFirst(), "master label");
                        Require(SocialCardUtility.Render(f.master, f.slave, def) == s.CapitalizeFirst(), "slave label");
                        Require(f.slave.relations.OpinionExplanation(f.master) == m.CapitalizeFirst(), "master tooltip");
                        Require(f.master.relations.OpinionExplanation(f.slave) == s.CapitalizeFirst(), "slave tooltip");
                        var friend = new PawnRelationDef { label = "friend", labelFemale = "friend" };
                        Require(SocialCardUtility.Render(f.slave, f.master, friend, def) == "Friend, " + m, "secondary uncapitalized relation");
                        Require(SocialCardUtility.Render(f.slave, f.master, friend) == "Friend", "other relation unchanged");
                        Require(File.ReadAllBytes(SscTestPaths.Combine(root, "Languages", lang, "Keyed/SSC_BondRelations.xml"))
                            .SequenceEqual(File.ReadAllBytes(SscTestPaths.Combine(root, "Sexslavecraft/Languages", lang, "Keyed/SSC_BondRelations.xml"))), "language mirror");
                    });
        // 再叠加一个第三方补丁，防止 SSC 包装器绕开其他关系原有的大写入口。
        Check("other relationship retains a third-party Cap postfix", () =>
        {
            customCapRelation = new PawnRelationDef { label = "friend", labelFemale = "friend" };
            harmony.Patch(AccessTools.Method(typeof(PawnRelationDef), nameof(PawnRelationDef.GetGenderSpecificLabelCap)),
                postfix: new HarmonyMethod(typeof(Program), nameof(CustomCap)));
            Require(SocialCardUtility.Render(new Pawn(), new Pawn(), customCapRelation) == "custom: Friend");
        });
        RunBondOverviewTests();
        RunBondOverviewPerformanceTests();
        Console.WriteLine($"{cases - failures}/{cases} passed");
        return failures == 0 ? 0 : 1;
    }
}
