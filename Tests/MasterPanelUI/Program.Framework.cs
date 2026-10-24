// AI 辅助生成：直接运行共用查询／提交与缰绳后端；测试后端只提供外部载体与权限变化。
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private const string Sharing = SSCAbilityToggleUtility.MasterDamageSharing;
    /// <summary>执行公共框架与真实缰绳后端回归；GUI、翻译和健康接口仍使用宿主边界，不替代实机结果。</summary>
    private static void RunFrameworkTests()
    {
        // 先验证原字段仍是唯一真值，以及两种后端在真实通用绘制器中的排序与提示。
        Run("分伤注册使用原字段，读取与后台共用入口一致", () =>
        {
            var p = new Pawn(); var bridle = Bind(p);
            Assert(SSCAbilityToggleUtility.Definitions.Count(d => d.Id == Sharing) == 1);
            Assert(SSCAbilityToggleUtility.GetEnabled(p, Sharing) && SSCAbilityToggleUtility.GetMasterDamageSharingEnabled(p));
            bridle.shareDamageEnabled = false;
            Assert(!SSCAbilityToggleUtility.GetEnabled(p, Sharing) && !SSCAbilityToggleUtility.IsActive(p, Sharing));
            Assert(!p.Training.abilityToggles.HasEntries);
        });
        Run("分伤与恩赐使用同一绘制器，排序和原分伤比例提示保持", () =>
        {
            var p = new Pawn(); Bind(p); Tab().Draw(p);
            var labels = Widgets.Items.Where(i => i.Kind == "checkbox").Select(i => i.Text).ToArray();
            Assert(labels.SequenceEqual(new[] { "SSC_MasterShareDamage_Label".Translate(), "SSC_AbilityToggle_MasterDesireInfestation_Label".Translate() }));
            string tip = "SSC_MasterShareDamage_Desc".Translate(SSCMasterBondUtility.DamageShareRatio.ToStringPercent());
            Assert(Widgets.Items.Any(i => i.Kind == "tooltip" && i.Text == tip));
        });
        Run("分伤缺缰绳提供原因且不补建保存状态", () =>
        {
            var p = new Pawn(); var view = SSCAbilityToggleUtility.GetView(p, Sharing);
            Assert(view.Visible && !view.Enabled && !view.CanChange && view.ChangeReason == Key("ShareNoBond"));
            Assert(!SSCAbilityToggleUtility.TrySet(p, Sharing, true, view.Context, out string reason) && !string.IsNullOrEmpty(reason));
            Assert(!p.Training.abilityToggles.HasEntries && p.health.Adds == 0);
        });
        Run("分伤不额外套用恩赐的空壳或身份实际生效条件", () =>
        {
            var p = new Pawn(); Bind(p); p.Training.pawnIdentity = PawnIdentity.Unset;
            p.health.AddHediff(new Hediff { pawn = p, def = SSCDefOf.SSC_PersonalityExcreted_Done });
            Assert(SSCAbilityToggleUtility.GetEnabled(p, Sharing) && SSCAbilityToggleUtility.IsActive(p, Sharing));
            Assert(!SSCAbilityToggleUtility.CanSet(p, Sharing, false, out _));
            Assert(!SSCAbilityToggleUtility.IsActive(p, Infestation));
        });
        // 捕获上下文后分别改变载入状态、人格、角色或载体，确认提交入口不沿用旧授权。
        Run("加载中两种后端都拒绝写入并返回原因", () =>
        {
            var p = new Pawn(); var bridle = Bind(p);
            var share = SSCAbilityToggleUtility.GetView(p, Sharing); var gift = SSCAbilityToggleUtility.GetView(p, Infestation);
            Scribe.mode = LoadSaveMode.LoadingVars;
            Assert(!SSCAbilityToggleUtility.TrySet(p, Sharing, false, share.Context, out string a));
            Assert(!SSCAbilityToggleUtility.TrySet(p, Infestation, true, gift.Context, out string b));
            Assert(a == b && a == "SSC_AbilityToggle_Loading".Translate());
            Assert(bridle.shareDamageEnabled && !p.Training.abilityToggles.HasEntries);
        });
        Run("分伤同组件换人格后旧上下文不能写入", () =>
        {
            var p = new Pawn(); var bridle = Bind(p); var view = SSCAbilityToggleUtility.GetView(p, Sharing);
            Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope)); using (scope) { }
            Assert(!SSCAbilityToggleUtility.TrySet(p, Sharing, false, view.Context, out string reason));
            Assert(reason == "SSC_AbilityToggle_Changed".Translate() && bridle.shareDamageEnabled);
        });
        Run("流转中分伤拒绝提交，结束后新上下文可以关闭", () =>
        {
            var p = new Pawn(); var bridle = Bind(p);
            Assert(SSCPersonalityRuntimeUtility.TryBeginMutation(p, out var scope));
            using (scope) Assert(!SSCAbilityToggleUtility.CanSet(p, Sharing, false, out _));
            var view = SSCAbilityToggleUtility.GetView(p, Sharing);
            Assert(SSCAbilityToggleUtility.TrySet(p, Sharing, false, view.Context, out _));
            Assert(!bridle.shareDamageEnabled && !p.Training.abilityToggles.HasEntries);
        });
        Run("旧人格签名不能为关系载体补拍当前缰绳凭据", () =>
        {
            var p = new Pawn(); var bridle = Bind(p);
            Assert(!SSCAbilityToggleUtility.TrySetEnabled(p, Sharing, false, p.Training, SSCPersonalityRuntimeUtility.Capture(p)));
            Assert(bridle.shareDamageEnabled);
        });
        // 人格字典的清源／恢复不得影响关系保存源，即使字典里有同名未知历史键也不能反推分伤值。
        Run("人格清源与恢复不写缰绳，不从字典伪造分伤偏好", () =>
        {
            var p = new Pawn(); var bridle = Bind(p); var snapshot = new SSCAbilityToggleState(); snapshot.Set(Sharing, false);
            SSCAbilityToggleUtility.RestorePreference(p, snapshot);
            Assert(bridle.shareDamageEnabled && SSCAbilityToggleUtility.GetEnabled(p, Sharing));
            SSCAbilityToggleUtility.ClearPersonalityState(p);
            Assert(bridle.shareDamageEnabled && !p.Training.abilityToggles.HasEntries);
        });
        Run("反复查询及同值分伤提交不触发健康写入", () =>
        {
            var p = new Pawn(); Bind(p); int adds = p.health.Adds, removes = p.health.Removes, changes = p.health.Notifications;
            for (int i = 0; i < 5; i++)
            {
                var view = SSCAbilityToggleUtility.GetView(p, Sharing);
                Assert(SSCAbilityToggleUtility.TrySet(p, Sharing, true, view.Context, out _));
                Tab().Draw(p);
            }
            Assert(p.health.Adds == adds && p.health.Removes == removes && p.health.Notifications == changes && !p.Training.abilityToggles.HasEntries);
        });
        Run("提交凭据不能跨角色或能力复用", () =>
        {
            var p = new Pawn(); var other = new Pawn(); Bind(p); var bridle = Bind(other);
            var view = SSCAbilityToggleUtility.GetView(p, Sharing);
            Assert(!SSCAbilityToggleUtility.TrySet(other, Sharing, false, view.Context, out _));
            Assert(!SSCAbilityToggleUtility.TrySet(p, Infestation, true, view.Context, out _));
            Assert(bridle.shareDamageEnabled && !SSCAbilityToggleUtility.GetEnabled(p, Infestation));
        });
        // 额外定义只注册到当前测试进程，并用可见规则约束目标，游戏生产注册项不因此增加。
        Run("保存开启但暂停的能力显示动态原因，仍允许关闭", () =>
        {
            var p = new Pawn { Downed = true }; const string id = "SSC_TestPaused";
            SSCAbilityToggleUtility.Register(new SSCAbilityToggleDefinition(id, SSCAbilityToggleGroup.Slave, PawnIdentity.Slave,
                "SSC_AbilityToggle_MasterDesireInfestation_Label", "SSC_AbilityToggle_MasterDesireInfestation_Tip",
                defaultEnabled: true, activationRule: pawn => !pawn.Downed, visibleRule: pawn => pawn == p));
            p.Training.pawnIdentity = PawnIdentity.Slave;
            var view = SSCAbilityToggleUtility.GetView(p, id);
            Assert(view.Enabled && !view.Active && view.CanChange && !string.IsNullOrEmpty(view.InactiveReason));
            Tab().DrawToggleGroup(p, SSCAbilityToggleGroup.Slave, 200);
            Assert(Widgets.Items.Any(i => i.Kind == "tooltip" && i.Text.Contains(view.InactiveReason)));
            Assert(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _));
        });
        Run("可见与排序规则只影响展示，不写入状态", () =>
        {
            var p = new Pawn(); bool show = false; const string id = "SSC_TestVisible";
            SSCAbilityToggleUtility.Register(new SSCAbilityToggleDefinition(id, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
                "test first", "test tip", order: -200, visibleRule: pawn => pawn == p && show));
            Tab().Draw(p); Assert(!Widgets.Items.Any(i => i.Text == "test first"));
            show = true; Tab().Draw(p); Assert(Widgets.Items.First(i => i.Kind == "checkbox").Text == "test first");
            Assert(!p.Training.abilityToggles.HasEntries && p.health.Adds == 0);
        });
        RunBackendContractTests();
        // 读取真实四语资源及其源码镜像，避免用人工复制文本掩盖缺键或同步遗漏。
        foreach (string language in Languages)
            Run(language + " 通用原因键完整且资源镜像一致", () =>
            {
                string relative = "Keyed/SSC_AbilityToggles.xml";
                var file = SscTestPaths.Combine(repo, "Languages", language, relative);
                var nodes = XDocument.Load(file).Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
                foreach (string suffix in new[] { "Unavailable", "Identity", "Hollow", "Mutating", "PlayerOnly", "Loading", "Changed" })
                    Assert(nodes.TryGetValue("SSC_AbilityToggle_" + suffix, out string text) && !string.IsNullOrWhiteSpace(text));
                Assert(File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(SscTestPaths.Combine(repo, "Sexslavecraft/Languages", language, relative))));
            });
    }

    /// <summary>仅提供一个可替换的外部保存实例及写入计数，不模拟人格、缰绳或身体业务。</summary>
    private sealed class TestOwner { internal bool Enabled = true; internal int Writes; }
    /// <summary>用外部载体、权限开关和回调驱动场景，公共查询／凭据／规则组合仍执行生产实现。</summary>
    private sealed class TestBackend : SSCAbilityToggleBackend
    {
        internal TestOwner Owner = new TestOwner();
        internal bool Allowed = true;
        internal Action OnCheck;
        /// <summary>只读返回外部实例中的偏好，不落入真实人格字典。</summary>
        public override bool GetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition) => Owner.Enabled;
        /// <summary>该边界默认准入，供用例单独验证定义附加规则，而不重写其组合算法。</summary>
        public override string ActivationFailure(Pawn pawn, SSCAbilityToggleDefinition definition) => null;
        /// <summary>在生产校验调用期间触发外部变化，再返回边界权限，验证回调后的 owner 复查。</summary>
        public override string ChangeFailure(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        { OnCheck?.Invoke(); return Allowed ? null : "permission changed"; }
        /// <summary>提供当前外部实例给生产上下文捕获，替换引用由用例显式控制。</summary>
        public override object CaptureOwner(Pawn pawn) => Owner;
        /// <summary>按实例相等报告归属，不在替身里重新实现公共角色／人格／定义校验。</summary>
        public override bool IsOwnerCurrent(Pawn pawn, object owner) => ReferenceEquals(Owner, owner);
        /// <summary>只统计实际值变化，便于断言被拒绝或重复请求没有落到外部写入。</summary>
        public override void SetEnabled(Pawn pawn, SSCAbilityToggleDefinition definition, bool enabled)
        { if (Owner.Enabled != enabled) { Owner.Enabled = enabled; Owner.Writes++; } }
    }
    /// <summary>验证自定义后端的方向规则、引用替换与回调重入，不接入第二批身体能力。</summary>
    private static void RunBackendContractTests()
    {
        // 同一载体依次经历关闭、替换和权限变化；其结果由后续用例继续断言，执行顺序有意保留。
        var p = new Pawn { Training = new CompSexSlaveTraining { pawnIdentity = PawnIdentity.Unset } };
        var backend = new TestBackend(); const string id = "SSC_TestExternalOwner";
        SSCAbilityToggleUtility.Register(new SSCAbilityToggleDefinition(id, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
            "test owner", "test tip", backend: backend, visibleRule: pawn => pawn == p,
            changeRule: (pawn, enabled) => enabled ? "cannot enable" : null));
        Run("适配后端独立于展示组和人格身份门槛", () =>
        {
            var view = SSCAbilityToggleUtility.GetView(p, id);
            Assert(view.Enabled && view.Active && view.CanChange && !p.Training.abilityToggles.HasEntries);
            Assert(SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _));
            Assert(!backend.Owner.Enabled && backend.Owner.Writes == 1 && !p.Training.abilityToggles.HasEntries);
        });
        Run("目标值规则区分开启与关闭并返回具体原因", () =>
        {
            Assert(SSCAbilityToggleUtility.CanSet(p, id, false, out _));
            Assert(!SSCAbilityToggleUtility.CanSet(p, id, true, out string reason) && reason == "cannot enable");
            var view = SSCAbilityToggleUtility.GetView(p, id);
            Assert(!view.CanChange && view.ChangeReason == reason);
            Assert(!SSCAbilityToggleUtility.TrySet(p, id, true, view.Context, out _) && !backend.Owner.Enabled);
        });
        Run("原载体替换后旧上下文不写新旧实例", () =>
        {
            var view = SSCAbilityToggleUtility.GetView(p, id); var old = backend.Owner; int writes = old.Writes;
            backend.Owner = new TestOwner();
            Assert(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out string reason) && !string.IsNullOrEmpty(reason));
            Assert(old.Writes == writes && backend.Owner.Enabled && backend.Owner.Writes == 0);
        });
        Run("测量后资格变化由提交入口重新拒绝", () =>
        {
            var view = SSCAbilityToggleUtility.GetView(p, id); backend.Allowed = false;
            Assert(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out string reason) && reason == "permission changed");
            Assert(backend.Owner.Enabled && backend.Owner.Writes == 0);
            backend.Allowed = true;
        });
        Run("资格回调替换载体也不能写入新实例", () =>
        {
            var view = SSCAbilityToggleUtility.GetView(p, id); var old = backend.Owner;
            backend.OnCheck = () => backend.Owner = new TestOwner();
            Assert(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _));
            Assert(old.Enabled && old.Writes == 0 && backend.Owner.Enabled && backend.Owner.Writes == 0);
            backend.OnCheck = null;
        });
        Run("构建视图期间替换载体不能授权旧显示值的新提交", () =>
        {
            var old = backend.Owner; backend.OnCheck = () => backend.Owner = new TestOwner();
            var view = SSCAbilityToggleUtility.GetView(p, id); backend.OnCheck = null;
            Assert(!SSCAbilityToggleUtility.TrySet(p, id, false, view.Context, out _));
            Assert(old.Enabled && old.Writes == 0 && backend.Owner.Enabled && backend.Owner.Writes == 0);
        });
        // 后端提供自身准入后，公共层仍须追加定义规则，不能因换后端而跳过激活／编辑限制。
        Run("后端适配仍遵守定义的激活与编辑附加规则", () =>
        {
            const string ruleId = "SSC_TestBackendRules"; bool allow = false;
            SSCAbilityToggleUtility.Register(new SSCAbilityToggleDefinition(ruleId, SSCAbilityToggleGroup.Master, PawnIdentity.Master,
                "test rules", "test tip", backend: backend, activationRule: pawn => allow, editRule: pawn => allow,
                visibleRule: pawn => false));
            Assert(SSCAbilityToggleUtility.GetEnabled(p, ruleId) && !SSCAbilityToggleUtility.IsActive(p, ruleId));
            Assert(!SSCAbilityToggleUtility.CanSet(p, ruleId, false, out _));
            allow = true;
            Assert(SSCAbilityToggleUtility.IsActive(p, ruleId) && SSCAbilityToggleUtility.CanSet(p, ruleId, false, out _));
        });
    }
}
