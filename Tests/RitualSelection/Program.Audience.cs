using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static RitualOutcomeComp_BindingSpectatorCount AudienceComp(string root)
    {
        var xml = XDocument.Load(Path.Combine(root, "Defs", "RitualDefs", "Ritual_Outcome.xml"));
        var comp = xml.Descendants("comps").Elements("li").First();
        Assert((string)comp.Attribute("Class") == typeof(RitualOutcomeComp_BindingSpectatorCount).FullName);
        return new RitualOutcomeComp_BindingSpectatorCount
        {
            label = (string)comp.Element("label"),
            curve = new SimpleCurve(comp.Descendants("points").Elements("li").Select(p =>
            {
                var xy = p.Value.Trim('(', ')').Split(',');
                return new CurvePoint(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
            }))
        };
    }

    private static void Near(float expected, float actual)
        => Assert(Math.Abs(expected - actual) < 0.00001f);

    private static void RunAudienceTests(string root)
    {
        root ??= Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        Run("观众预览随两个主角分配从 5 人降到 3 人", () =>
        {
            var f = new Fixture(3); f.Open(); var comp = AudienceComp(root);
            QualityFactor Preview() => comp.GetQualityFactor(f.Ritual, f.Spot, null, f.A, null);
            Assert(Preview().count == "5 / 10"); Near(0.15f, Preview().quality);
            Assert(f.HostSlot()); Assert(Preview().count == "4 / 10");
            Assert(f.TargetSlot()); Assert(Preview().count == "3 / 10"); Near(0.10f, Preview().quality);
            // Replacing a protagonist returns the former one to the audience, preserving its size.
            f.Map.Pawns[2].BoundMaster = f.Host;
            Assert(f.Window.Replace(f.Map.Pawns[2], new RitualRole[] { f.TargetRole }, f.Target));
            Assert(Preview().count == "3 / 10");
        });
        Run("不参与者和非人类不提供观众预览加成", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth();
            f.A.RemoveParticipant(f.Map.Pawns[2]); f.Map.Pawns[3].RaceProps.Humanlike = false;
            var preview = AudienceComp(root).GetQualityFactor(f.Ritual, f.Spot, null, f.A, null);
            Assert(preview.count == "1 / 10"); Near(0.05f, preview.quality);
        });
        Run("强制角色即使留在观众列表也不重复计数", () =>
        {
            var f = new Fixture(3); f.Open();
            f.A.ForcedRolesForReading["master"] = f.Host;
            f.A.ForcedRolesForReading["slave"] = f.Target;
            Assert(AudienceComp(root).GetQualityFactor(f.Ritual, f.Spot, null, f.A, null).count == "3 / 10");
        });
        Run("实际出席三名观众在超长配置时长下仍获得 10% 结算加成", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            // Positive control reproduces the original configured-duration mismatch.
            Near(0, new RitualOutcomeComp_ParticipantCount().Count(ritual, data));
            Near(3, comp.Count(ritual, data)); Near(0.10f, comp.QualityOffset(ritual, data));
            Assert(comp.GetDesc(ritual, data).StartsWith("3 / 10 "));
            Assert(comp.GetDesc(ritual, data).EndsWith("10 %"));
        });
        Run("观众至少出席实际进度的一半，短暂加入不算", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000.5f };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 3000.25f;
            data.presentForTicks[f.Map.Pawns[3]] = 3000f;
            data.presentForTicks[f.Map.Pawns[4]] = 6000.5f;
            Near(2, comp.Count(ritual, data)); Near(0.075f, comp.QualityOffset(ritual, data));
        });
        Run("已离场但出席过半的观众仍保留贡献", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 4000;
            f.A.RemoveParticipant(f.Map.Pawns[2]); Near(1, comp.Count(ritual, data));
        });
        Run("旧出席数据中的主角、动物和其他物体不提供助兴", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            f.Map.Pawns[2].RaceProps.Humanlike = false; data.presentForTicks[new Thing()] = 6000;
            Near(0, comp.Count(ritual, data)); Near(0, comp.QualityOffset(ritual, data));
        });
        Run("零观众的预览与结算均无最低 5% 加成", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            Near(0, comp.GetQualityFactor(f.Ritual, f.Spot, null, f.A, null).quality);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            Near(0, comp.QualityOffset(ritual, comp.MakeData()));
        });
        Run("没有实际进度或出席记录时不会误算观众", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 0;
            Near(0, comp.Count(ritual, data)); ritual.TicksPassedWithProgress = 100;
            Near(0, comp.Count(ritual, data)); Near(0, comp.Count(ritual, null)); Near(0, comp.Count(null, data));
        });
        Run("十人以上观众加成封顶 20%，预览和结算一致", () =>
        {
            var f = new Fixture(12); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            Near(0.20f, comp.GetQualityFactor(f.Ritual, f.Spot, null, f.A, null).quality);
            Near(10, comp.Count(ritual, data)); Near(0.20f, comp.QualityOffset(ritual, data));
        });
        Run("出席数据沿用原版格式且两场仪式数据互不影响", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, TicksPassedWithProgress = 6000 };
            var first = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            var second = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            first.presentForTicks[f.Map.Pawns[2]] = 6000;
            Near(1, comp.Count(ritual, first)); Near(0, comp.Count(ritual, second));
            first.Reset(); Near(0, comp.Count(ritual, first));
        });
    }
}
