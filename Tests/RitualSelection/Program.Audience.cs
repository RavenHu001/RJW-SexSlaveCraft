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
        RunAudienceCollectorTests(root);
        Run("实机回归：阶段进度始终为零，四名全程观众仍贡献 12.5%", () =>
        {
            var f = new Fixture(4); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => true;
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int i = 0; i < 20000; i++) { comp.Tick(ritual, data, 1f); ritual.ElapsedTicks++; }
            Near(0, ritual.TicksPassedWithProgress); Near(20000, RitualOutcomeComp_BindingSpectatorCount.AttendanceDurationTicks(ritual));
            Near(4, comp.Count(ritual, data)); Near(0.125f, comp.QualityOffset(ritual, data));
        });
        Run("真实计时包含无观众的时段，末尾短暂观看不算半场出席", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => false;
            var pawn = f.Map.Pawns[2]; pawn.Position = new IntVec3(100, 100);
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int i = 0; i < 1000; i++)
            {
                if (i == 800) pawn.Position = pawn.mindState.duty.focus.Cell;
                comp.Tick(ritual, data, 1f); ritual.ElapsedTicks++;
            }
            Near(200, data.presentForTicks[pawn]); Near(0, comp.Count(ritual, data));
        });
        Run("已保存出席记录可配合 Lord 实际计时结算，不要求原版进度非零", () =>
        {
            var f = new Fixture(4); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 20000 };
            var data = new RitualOutcomeComp_DataThingPresence();
            float[] recorded = { 19124, 19367, 19440, 19249 };
            for (int i = 0; i < recorded.Length; i++) data.presentForTicks[f.Map.Pawns[i + 2]] = recorded[i];
            Near(4, comp.Count(ritual, data)); Near(0.125f, comp.QualityOffset(ritual, data));
            ritual.TicksPassedWithProgress = 99999999;
            Near(4, comp.Count(ritual, data));
        });
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
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            // Positive control reproduces the original configured-duration mismatch.
            Near(0, new RitualOutcomeComp_ParticipantCount().Count(ritual, data));
            Near(3, comp.Count(ritual, data)); Near(0.10f, comp.QualityOffset(ritual, data));
            Assert(comp.GetDesc(ritual, data).StartsWith("3 / 10 "));
            Assert(comp.GetDesc(ritual, data).EndsWith("10 %"));
        });
        Run("观众至少出席实际时长的一半，短暂加入不算", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6001 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 3000.5f;
            data.presentForTicks[f.Map.Pawns[3]] = 3000f;
            data.presentForTicks[f.Map.Pawns[4]] = 6001f;
            Near(2, comp.Count(ritual, data)); Near(0.075f, comp.QualityOffset(ritual, data));
        });
        Run("已离场但出席过半的观众仍保留贡献", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 4000;
            f.A.RemoveParticipant(f.Map.Pawns[2]); Near(1, comp.Count(ritual, data));
        });
        Run("旧出席数据中的主角、动物和其他物体不提供助兴", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            f.Map.Pawns[2].RaceProps.Humanlike = false; data.presentForTicks[new Thing()] = 6000;
            Near(0, comp.Count(ritual, data)); Near(0, comp.QualityOffset(ritual, data));
        });
        Run("零观众的预览与结算均无最低 5% 加成", () =>
        {
            var f = new Fixture(); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            Near(0, comp.GetQualityFactor(f.Ritual, f.Spot, null, f.A, null).quality);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            Near(0, comp.QualityOffset(ritual, comp.MakeData()));
        });
        Run("没有实际计时或出席记录时不会误算观众", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            data.presentForTicks[f.Map.Pawns[2]] = 0;
            Near(0, comp.Count(ritual, data)); ritual.ElapsedTicks = 100;
            Near(0, comp.Count(ritual, data)); Near(0, comp.Count(ritual, null)); Near(0, comp.Count(null, data));
        });
        Run("十人以上观众加成封顶 20%，预览和结算一致", () =>
        {
            var f = new Fixture(12); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            foreach (var pawn in f.Map.Pawns) data.presentForTicks[pawn] = 6000;
            Near(0.20f, comp.GetQualityFactor(f.Ritual, f.Spot, null, f.A, null).quality);
            Near(10, comp.Count(ritual, data)); Near(0.20f, comp.QualityOffset(ritual, data));
        });
        Run("出席数据沿用原版格式且两场仪式数据互不影响", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = new LordJob_Ritual { assignments = f.A, ElapsedTicks = 6000 };
            var first = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            var second = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            first.presentForTicks[f.Map.Pawns[2]] = 6000;
            Near(1, comp.Count(ritual, first)); Near(0, comp.Count(ritual, second));
            first.Reset(); Near(0, comp.Count(ritual, first));
        });
    }

    private static LordJob_Ritual AudienceLord(Fixture f)
    {
        var ritual = new LordJob_Ritual { assignments = f.A, Map = f.Map, Spot = new IntVec3(127, 134) };
        foreach (Pawn pawn in f.Map.Pawns)
        {
            pawn.Map = f.Map;
            pawn.Position = new IntVec3(130, 134);
            pawn.mindState.duty = new Verse.AI.PawnDuty
            {
                def = DutyDefOf.Spectate, focus = new LocalTargetInfo(pawn.Position)
            };
            ritual.PawnsToCountTowardsPresence.Add(pawn);
        }
        return ritual;
    }

    private static void RunAudienceCollectorTests(string root)
    {
        Run("逐帧采集：通用聚会区域外的指定观众站位仍计入六阶段结算", () =>
        {
            var f = new Fixture(3); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f);
            GatheringsUtility.Area = (cell, spot, map) => false;
            var original = new RitualOutcomeComp_ParticipantCount();
            var originalData = (RitualOutcomeComp_DataThingPresence)original.MakeData();
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int phase = 0; phase < 6; phase++)
                for (int tick = 0; tick < 100; tick++)
                {
                    // SSC has job-driven stage endings: elapsed ticks advance, duration progress stays zero.
                    original.Tick(ritual, originalData, 1f); comp.Tick(ritual, data, 1f);
                    ritual.ElapsedTicks++;
                }
            Assert(originalData.presentForTicks.Count == 0);
            Assert(data.presentForTicks.Count == 3);
            foreach (Pawn spectator in f.A.SpectatorsForReading) Near(600, data.presentForTicks[spectator]);
            Near(3, comp.Count(ritual, data)); Near(0.10f, comp.QualityOffset(ritual, data));
        });
        Run("逐帧采集：聚会区域与指定站位同时满足时出席不重复累加", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => true;
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int i = 0; i < 20; i++) { comp.Tick(ritual, data, 0.25f); ritual.ElapsedTicks++; }
            Near(20f, data.presentForTicks[f.Map.Pawns[2]]); Near(1, comp.Count(ritual, data));
            Assert(!data.presentForTicks.ContainsKey(f.Host) && !data.presentForTicks.ContainsKey(f.Target));
        });
        Run("逐帧采集：未抵达观看位置或执行无关职责不记出席", () =>
        {
            var f = new Fixture(2); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => false;
            f.Map.Pawns[2].Position = new IntVec3(100, 100);
            f.Map.Pawns[3].mindState.duty.def = new Verse.AI.DutyDef();
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int i = 0; i < 100; i++) { comp.Tick(ritual, data, 1f); ritual.ElapsedTicks++; }
            Assert(data.presentForTicks.Count == 0); Near(0, comp.QualityOffset(ritual, data));
        });
        Run("逐帧采集：非本场成员、其他地图、非参与、非人类与倒地观众不记出席", () =>
        {
            var f = new Fixture(6); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => true;
            var pawns = f.Map.Pawns.ToArray(); // RemoveParticipant reorders the shared candidate list.
            ritual.PawnsToCountTowardsPresence.Remove(pawns[2]);
            pawns[3].Map = new Map(); f.A.RemoveParticipant(pawns[4]);
            pawns[5].RaceProps.Humanlike = false; pawns[6].Downed = true; pawns[7].Spawned = false;
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            comp.Tick(ritual, data, 1f); Assert(data.presentForTicks.Count == 0);
        });
        Run("逐帧采集：看完一半才离场保留贡献，较早离场不提供加成", () =>
        {
            var f = new Fixture(2); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); GatheringsUtility.Area = (cell, spot, map) => false;
            var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            for (int i = 0; i < 600; i++)
            {
                if (i == 200) ritual.PawnsToCountTowardsPresence.Remove(f.Map.Pawns[2]);
                if (i == 300) ritual.PawnsToCountTowardsPresence.Remove(f.Map.Pawns[3]);
                comp.Tick(ritual, data, 1f); ritual.ElapsedTicks++;
            }
            Near(200, data.presentForTicks[f.Map.Pawns[2]]); Near(300, data.presentForTicks[f.Map.Pawns[3]]);
            Near(1, comp.Count(ritual, data)); Near(0.05f, comp.QualityOffset(ritual, data));
        });
        Run("逐帧采集：缺失数据与非正进度不累计出席", () =>
        {
            var f = new Fixture(1); f.Open(); f.SelectBoth(); var comp = AudienceComp(root);
            var ritual = AudienceLord(f); var data = (RitualOutcomeComp_DataThingPresence)comp.MakeData();
            comp.Tick(ritual, data, 0f); comp.Tick(ritual, data, -1f);
            comp.Tick(ritual, null, 1f); comp.Tick(null, data, 1f);
            Assert(data.presentForTicks.Count == 0);
        });
    }
}
