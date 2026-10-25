// AI 辅助生成：真实 XML／Scribe 阶段与跨 Pawn 引用；只为外部世界提供已加载实体登记。
using System.Xml.Linq;
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>通过真实 XML 往返验证明确选择、旧字段、未知版本及跨角色授权引用。</summary>
    private static void SaveTests()
    {
        foreach (var setting in Enum.GetValues<SSCRomanceSetting>())
        foreach (bool value in new[] { false, true })
            Check($"real Scribe explicit {setting}/{value}", () =>
            {
                var p = Slave(); var v = SSCRomancePreferences.GetView(p, setting);
                Require(SSCRomancePreferences.TrySet(p, setting, value, v.Context, out _));
                var saved = new RomanceFixture(Comp(p)); string path = Save(saved);
                var restored = Slave(); Load(path, Comp(restored)); Equal(value, SSCRomancePreferences.GetView(restored, setting).Saved);
            });
        foreach (bool entireNode in new[] { false, true })
            Check("real Scribe old missing configuration/fields defaults " + entireNode, () =>
            {
                var p = Slave(); Comp(p).romanceConfig = Denied(); string path = Save(new RomanceFixture(Comp(p)));
                Edit(path, doc => { if (entireNode) doc.Descendants("sscRomanceConfig").Remove(); else doc.Descendants("sscRomanceConfig").Single().RemoveNodes(); });
                var restored = Slave(); Load(path, Comp(restored)); foreach (var s in Enum.GetValues<SSCRomanceSetting>()) Equal(s <= SSCRomanceSetting.AcceptProposal || s == SSCRomanceSetting.InitiateToSexSlave, SSCRomancePreferences.GetView(restored, s).Saved);
            });
        Check("old seven-field config preserves choices and defaults new peer fields", () =>
        {
            var p = Slave(); Comp(p).romanceConfig = Denied();
            Comp(p).romanceConfig.allowInitiateToSexSlave = false; Comp(p).romanceConfig.alwaysAcceptSexSlave = true;
            string path = Save(new RomanceFixture(Comp(p)));
            Edit(path, doc => { doc.Descendants("allowInitiateToSexSlave").Remove(); doc.Descendants("alwaysAcceptSexSlave").Remove(); });
            var restored = Slave(); Load(path, Comp(restored)); var cfg = Comp(restored).romanceConfig;
            Require(cfg.allowInitiateToSexSlave && !cfg.alwaysAcceptSexSlave && cfg.initiateOnlyMaster && !cfg.allowInitiateRomance);
        });
        Check("real Scribe unknown version preserved and remains diagnosed", () =>
        {
            var p = Slave(); Comp(p).romanceConfig = Denied(); Comp(p).romanceConfig.version = 999;
            string path = Save(new RomanceFixture(Comp(p))); var restored = Slave(); Load(path, Comp(restored));
            Equal(999, Comp(restored).romanceConfig.version); Require(!Comp(restored).romanceConfig.allowAcceptRomance);
            Equal(SSCRomanceReason.UnsupportedConfig, SSCRomanceResolver.Resolve(restored).Reason);
        });
        Check("real Scribe pending authorization resumes with fresh runtime token, same persistent epoch", () =>
        {
            var a = Slave(); var b = Pawn(); var auth = Authorize(a, b); var owner = SSCBondUtility.GetBoundMaster(a);
            string path = Save(new RomanceFixture(Comp(a), auth.Stamp));
            // 模拟同一 Pawn 从存档重建组件与进程内人格状态；随后真实 Scribe 覆盖保存凭据。
            Require(SSCPersonalityRuntimeUtility.TryBeginMutation(a, out var scope)); using (scope) { }
            var loaded = Load(path, Comp(a), a, b, owner);
            Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance));
            Require(SSCRomanceLifecycle.TryResume(loaded.Stamp, out var resumed)); Require(resumed.IsCurrent(a, b, SSCRomanceAction.Romance));
        });
        Check("real Scribe cannot revive stamp after unnotified same-owner chain replacement", () =>
        {
            var a = Slave(); var b = Pawn(); var owner = SSCBondUtility.GetBoundMaster(a); var auth = Authorize(a, b);
            var oldChain = SSCBondUtility.GetChain(a);
            var newChain = new Hediff_ChainOfSexSlave { def = oldChain.def, pawn = a, target = owner, loadID = ++id };
            newChain.Severity = .9f;
            // 外部绕过通知替换同主锁链，运行时引用校验已拒绝；读档不能丢掉这层归属检查。
            a.health.hediffSet.hediffs.Remove(oldChain); a.health.hediffSet.hediffs.Add(newChain);
            Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance));
            string path = Save(new RomanceFixture(Comp(a), auth.Stamp)); var loaded = Load(path, Comp(a), a, b, owner);
            Require(!SSCRomanceLifecycle.TryResume(loaded.Stamp, out _));
        });
        Check("real Scribe same persisted chain ID resumes after runtime chain object reconstruction", () =>
        {
            var a = Slave(); var b = Pawn(); var owner = SSCBondUtility.GetBoundMaster(a); var auth = Authorize(a, b);
            string path = Save(new RomanceFixture(Comp(a), auth.Stamp)); var oldChain = SSCBondUtility.GetChain(a);
            // 正常读档会产生新的对象实例，但保留原版保存的 loadID；不能把它误判成重绑。
            var restoredChain = new Hediff_ChainOfSexSlave { def = oldChain.def, pawn = a, target = owner, loadID = oldChain.loadID };
            restoredChain.Severity = .9f; a.health.hediffSet.hediffs.Remove(oldChain); a.health.hediffSet.hediffs.Add(restoredChain);
            var loaded = Load(path, Comp(a), a, b, owner);
            Require(!auth.IsCurrent(a, b, SSCRomanceAction.Romance));
            Require(SSCRomanceLifecycle.TryResume(loaded.Stamp, out var resumed)); Require(resumed.IsCurrent(a, b, SSCRomanceAction.Romance));
        });
        foreach (string missing in new[] { "epoch", "chainId", "action", "actor", "target", "master" })
            Check("real Scribe incomplete command cannot resume: " + missing, () =>
            {
                var a = Slave(); var b = Pawn(); var auth = Authorize(a, b); string path = Save(new RomanceFixture(Comp(a), auth.Stamp));
                Edit(path, doc => doc.Descendants("stamp").Elements(missing).Remove());
                var loaded = Load(path, Comp(a), a, b, SSCBondUtility.GetBoundMaster(a)); Require(!SSCRomanceLifecycle.TryResume(loaded.Stamp, out _));
            });
        Check("real Scribe clone keeps own later choice rather than newborn reset", () =>
        {
            var p = Slave(); SSCRomanceLifecycle.ResetNewClone(p); Comp(p).romanceConfig = new() { allowAcceptProposal = false };
            string path = Save(new RomanceFixture(Comp(p))); var restored = Slave(); Load(path, Comp(restored));
            SSCRomanceLifecycle.Notify(restored); Require(!Comp(restored).romanceConfig.allowAcceptProposal);
        });
        Check("gel snapshot real Scribe preserves dormant choices without any authorization", () =>
        {
            var source = new CompPersonalityStore { romanceConfig = Denied() }; var saved = new RomanceFixture(Comp(Slave()), store: source);
            string path = Save(saved); var target = new CompPersonalityStore(); Load(path, Comp(Slave()), store: target);
            Require(!target.romanceConfig.allowInitiateRomance && target.romanceConfig.alwaysAcceptMaster);
            Require(!XDocument.Load(path).Descendants("gel").Single().Descendants("sscRomanceAuthorizationId").Any());
        });
    }
    /// <summary>将当前夹具写入独立 XML 文件，不读取或覆盖玩家存档。</summary>
    private static string Save(RomanceFixture fixture)
    {
        string path = Path.Combine(saveDirectory, "romance-" + ++id + ".xml");
        Scribe.saver.InitSaving(path, "RomanceFixture"); Scribe_Deep.Look(ref fixture, "scene"); Scribe.saver.FinalizeSaving(); return path;
    }
    /// <summary>登记已经恢复的外部 Pawn 后加载生产字段，实际执行交叉引用和 PostLoadInit。</summary>
    private static RomanceFixture Load(string path, CompSexSlaveTraining comp, Pawn actor = null, Pawn target = null, Pawn master = null, CompPersonalityStore store = null)
    {
        RomanceFixture loaded = null; Scribe.loader.InitLoading(path);
        var directory = (LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs);
        foreach (var pawn in new[] { actor, target, master }.Where(p => p != null).Distinct()) directory.RegisterLoaded(pawn);
        Scribe_Deep.Look(ref loaded, "scene", comp, store); Scribe.loader.FinalizeLoading(); return loaded;
    }
    /// <summary>只改本例隔离 XML，构造缺字段或损坏格式，避免绕过真实加载过程。</summary>
    private static void Edit(string path, Action<XDocument> edit) { var doc = XDocument.Load(path); edit(doc); doc.Save(path); }
}
public sealed class RomanceFixture : IExposable
{
    private readonly CompSexSlaveTraining comp;
    private readonly CompPersonalityStore store;
    public SSCRomanceCommandStamp Stamp;
    /// <summary>供读档构造器注入新载体与可选凝胶；载体本身由外部世界夹具提供。</summary>
    public RomanceFixture(CompSexSlaveTraining comp, CompPersonalityStore store) { this.comp = comp; this.store = store; }
    /// <summary>供保存端携带当前载体、命令及可选凝胶，各数据仍委托生产入口序列化。</summary>
    public RomanceFixture(CompSexSlaveTraining comp, SSCRomanceCommandStamp stamp = null, CompPersonalityStore store = null)
    { this.comp = comp; Stamp = stamp; this.store = store; }
    /// <summary>分别保存载体字段、命令及凝胶节点，确保授权不会混入人格快照。</summary>
    public void ExposeData()
    {
        comp.ExposeRomance(); Scribe_Deep.Look(ref Stamp, "stamp");
        if (store != null) { Scribe.EnterNode("gel"); store.ExposeRomanceSnapshot(); Scribe.ExitNode(); }
    }
}
