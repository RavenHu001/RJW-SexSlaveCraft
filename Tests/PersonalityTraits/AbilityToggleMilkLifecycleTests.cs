// AI 辅助生成：运行真实提取、凝胶保存／加工及植入入口，保留泌乳旧字段的历史行为。
using System.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>将人格开关与原泌乳字段交叉组合，验证独立快照、明确 false 覆盖和提取后的原体残值。</summary>
    private static void RunAbilityToggleMilkLifecycleCases()
    {
        // 二元成对覆盖，并保留完整覆盖宿主的锚点。
        foreach (var (gift, milk, hostGift, hostMilk) in new (bool, bool, bool, bool)[]
        {
            (false, false, false, false),
            (false, false, true, false),
            (false, false, true, true),
            (false, true, false, true),
            (false, true, true, true),
            (true, false, false, true),
            (true, false, true, true),
            (true, true, false, false),
            (true, true, true, false),
            (true, true, true, true),
        })
            Run($"生命周期：来源恩赐={gift}泌乳={milk}／宿主恩赐={hostGift}泌乳={hostMilk}", () =>
            {
                var source = Body("Source"); var original = MasterTraining(source, PawnIdentity.Master, false);
                original.abilityToggles.Set(ToggleId, gift); original.abilityToggles.Set("SSC_UnknownFuture", false);
                original.milkProductionEnabled = milk;
                var gel = Excrete(source);
                // 提取只清通用人格字典；原泌乳残值不被这次框架改造顺带清除。
                Assert(!original.abilityToggles.HasEntries && original.milkProductionEnabled == milk, "清源边界");
                Assert(gel.abilityToggles.Get(ToggleId) == gift && gel.milkProductionEnabled == milk, "保存来源选择");
                Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
                var loaded = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; loaded.PostExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; loaded.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                var processed = Gel(); processed.CopyFrom(loaded);
                loaded.abilityToggles.Set(ToggleId, !gift); loaded.milkProductionEnabled = !milk;
                var host = Body("Host"); var receiver = MasterTraining(host, PawnIdentity.Master, false);
                receiver.abilityToggles.Set(ToggleId, hostGift); receiver.milkProductionEnabled = hostMilk;
                object before = SSCPersonalityRuntimeUtility.Capture(host);
                Assert(ExcretionUtility.InheritEverything(host, processed), "实际植入");
                Assert(receiver.abilityToggles.Get(ToggleId) == gift && receiver.milkProductionEnabled == milk, "来源精确覆盖宿主");
                Assert(!receiver.abilityToggles.Get("SSC_UnknownFuture", true), "未知明确关闭保留");
                Assert(!SSCPersonalityRuntimeUtility.IsCurrent(host, before) && !SSCPersonalityRuntimeUtility.IsMutating(host), "旧授权失效且作用域释放");
                receiver.abilityToggles.Set(ToggleId, !gift); receiver.milkProductionEnabled = !milk;
                Assert(gel.abilityToggles.Get(ToggleId) == gift && gel.milkProductionEnabled == milk, "源凝胶不被宿主回写");
            });
        foreach (bool hostOn in new[] { false, true })
            Run("生命周期：旧凝胶缺字典／泌乳字段覆盖宿主 " + hostOn, () =>
            {
                var gel = Gel(); Scribe.mode = LoadSaveMode.Saving; gel.PostExposeData();
                Scribe.Values.Remove("sscAbilityToggles"); Scribe.Values.Remove("milkProductionEnabled");
                var legacy = Gel(); Scribe.mode = LoadSaveMode.LoadingVars; legacy.PostExposeData();
                Scribe.mode = LoadSaveMode.PostLoadInit; legacy.PostExposeData(); Scribe.mode = LoadSaveMode.Inactive;
                var host = Body("Host"); var comp = MasterTraining(host, PawnIdentity.Master, false);
                comp.abilityToggles.Set(ToggleId, hostOn); comp.milkProductionEnabled = hostOn;
                Assert(ExcretionUtility.InheritEverything(host, legacy), "旧来源可植入");
                Assert(!comp.abilityToggles.Get(ToggleId) && comp.milkProductionEnabled, "恩赐关闭／泌乳开启的各自旧默认");
                Assert(!comp.abilityToggles.HasEntries, "不补写整个注册表");
            });
        Run("生命周期：复用凝胶采集缺 Comp 的身体保留泌乳历史例外", () =>
        {
            var gel = Gel(); gel.milkProductionEnabled = false; gel.abilityToggles.Set(ToggleId, true);
            gel.StorePawnData(Body("No training comp"));
            Assert(!gel.milkProductionEnabled && !gel.abilityToggles.HasEntries, "原字段条件复制与人格空快照各自沿旧规则");
        });
    }
}
