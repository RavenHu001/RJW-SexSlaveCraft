// AI 辅助生成：执行生产姿态计时和真实 Harmony 近战／绘制／卸装入口。
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static class WhipPoseTests
{
    private sealed class CompatVerb : Verb_MeleeAttackSSCPnaWhip {internal bool Cast()=>TryCastShot();}
    private sealed class WhipVerb : RimWorld.Verb_MeleeAttackDamage
    { internal bool Cast() => TryCastShot(); }

    private sealed class Fixture
    {
        internal readonly Map Map = new Map();
        internal readonly Pawn Owner = new Pawn();
        internal readonly Pawn Target = new Pawn();
        internal readonly ThingWithComps Weapon = new ThingWithComps {
            def = new ThingDef { defName = SSCPnaWhipPoseUtility.WeaponDefName }
        };
        internal readonly WhipVerb Verb = new WhipVerb();
        internal readonly TickManager Clock;
        internal readonly Material Original = new Material {
            mainTexture = new Texture2D { Path = "item" }, shader = new Shader(),
            color = new Color(.3f, .4f, .5f, 1f)
        };
        internal Fixture(TickManager clock = null)
        {
            Clock = clock ?? new TickManager { TicksGame = 1000 };
            Find.TickManager = Clock; Find.CurrentMap = Map; Scribe.mode = LoadSaveMode.Inactive;
            Owner.Map = Target.Map = Weapon.MapHeld = Map;
            Owner.equipment.Primary = Weapon;
            Verb.CasterPawn = Owner; Verb.EquipmentSource = Weapon; Verb.SetTarget(Target);
            // 姿态专项默认打空，以单独检查实体姿态绘制；命中光带由 WhipHitTests 覆盖。
            Verb.NativeCast = () => false;
            Graphics.Sent.Clear();
            PawnRenderUtility.NextMaterial = Original;
            PawnRenderUtility.NextMesh = MeshPool.plane10;
            PawnRenderUtility.NextMatrix = new Matrix4x4 {
                m00 = 2f, m02 = .4f, m03 = 4f, m13 = 6f,
                m20 = -.4f, m22 = 2f, m23 = 8f, m33 = 1f
            };
        }
        internal SSCPnaWhipPose Pose => SSCPnaWhipPoseUtility.GetPose(Weapon);
        internal void Draw() => PawnRenderUtility.DrawEquipmentAiming(Weapon, default, 0);
    }

    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
    private static void Near(double a, double b)
        => Check(Math.Abs(a - b) < .0001, $"{a} != {b}");

    private static void CheckIdle(Matrix4x4 before, Matrix4x4 after, bool flip)
    {
        float anchor = (50f / 256f - .5f) * (flip ? -1f : 1f);
        Near(before.m03 + before.m00 * anchor, after.m03 + after.m00 * anchor);
        Near(before.m23 + before.m20 * anchor, after.m23 + after.m20 * anchor);
        Near(before.m13, after.m13); Near(before.m11, after.m11); Near(before.m33, after.m33);
        Near(before.m00 * before.m00 + before.m20 * before.m20, after.m00 * after.m00 + after.m20 * after.m20);
        Near(before.m02 * before.m02 + before.m22 * before.m22, after.m02 * after.m02 + after.m22 * after.m22);
        Near(Math.Abs(after.m00), Math.Abs(after.m20));
        Near(0, after.m00 * after.m02 + after.m20 * after.m22);
        Check(after.m20 * (flip ? 1f : -1f) < 0, "butt points down at 45 degrees");
    }

    internal static void Register(Action<string, Action> run, string root)
    {
        foreach (bool hit in new[] { true, false }) run("鞭击命中与打空均播放且保留父类返回值 " + hit, () => {
            var f = new Fixture(); f.Verb.NativeCast = () => hit;
            Check(f.Verb.Cast() == hit, "return unchanged"); Near(1, f.Verb.BaseCalls);
            Check(f.Pose == SSCPnaWhipPose.Attack, "attack begins independently of hit");
        });
        foreach (var row in new[] { (0, SSCPnaWhipPose.Attack), (5, SSCPnaWhipPose.Attack),
            (6, SSCPnaWhipPose.Recovery), (17, SSCPnaWhipPose.Recovery), (18, SSCPnaWhipPose.Idle) })
            run("短鞭姿态时间边界 " + row.Item1, () => {
                var f = new Fixture(); f.Verb.Cast(); f.Clock.TicksGame += row.Item1;
                Check(f.Pose == row.Item2, "pose boundary");
            });
        run("短鞭暂停和重复绘制查询不推进时间", () => {
            var f = new Fixture(); f.Verb.Cast();
            for (int i = 0; i < 20; i++) { f.Draw(); Check(f.Pose == SSCPnaWhipPose.Attack, "paused"); }
            Near(1000, f.Clock.TicksGame);
        });
        run("加速跨阶段不会延长短鞭动作", () => {
            var f = new Fixture(); f.Verb.Cast(); f.Clock.TicksGame += 16;
            Check(f.Pose == SSCPnaWhipPose.Recovery, "batched recovery");
            f.Clock.TicksGame += 9; Check(f.Pose == SSCPnaWhipPose.Idle, "expired");
        });
        foreach (string gate in new[] { "busy", "despawned", "saving", "wrong-weapon", "not-primary", "dead", "destroyed", "no-clock" })
            run("短鞭不适用出手不播放 " + gate, () => {
                var f = new Fixture();
                switch (gate) {
                    case "busy": f.Owner.stances.FullBodyBusy = true; break;
                    case "despawned": f.Owner.Spawned = false; break;
                    case "saving": Scribe.mode = LoadSaveMode.Saving; break;
                    case "wrong-weapon": f.Weapon.def.defName = "Other"; break;
                    case "not-primary": f.Owner.equipment.Primary = new Thing(); break;
                    case "dead": f.Owner.Dead = true; break;
                    case "destroyed": f.Weapon.Destroyed = true; break;
                    case "no-clock": Find.TickManager = null; break;
                }
                f.Verb.Cast(); Near(1, f.Verb.BaseCalls);
                Check(f.Pose == SSCPnaWhipPose.Idle, "visual guard only; base still called");
            });
        run("短鞭父类异常传播并清除上次动作", () => {
            var f = new Fixture(); f.Verb.Cast();
            f.Verb.NativeCast = () => throw new InvalidOperationException("native");
            bool threw = false; try { f.Verb.Cast(); } catch (InvalidOperationException) { threw = true; }
            Check(threw, "native exception preserved");
            Check(f.Pose == SSCPnaWhipPose.Idle, "no premature animation");
        });
        run("新一次打空重新开始短鞭动作", () => {
            var f = new Fixture(); f.Verb.Cast(); f.Clock.TicksGame += 10;
            f.Verb.NativeCast = () => false; f.Verb.Cast();
            Check(f.Pose == SSCPnaWhipPose.Attack, "restart on missed swing");
        });
        run("目标在结算中销毁不取消已经发生的鞭击", () => {
            var f = new Fixture(); f.Verb.NativeCast = () => { f.Target.Destroyed = true; return true; };
            f.Verb.Cast(); Check(f.Pose == SSCPnaWhipPose.Attack, "attempt already occurred");
        });
        foreach (string gate in new[] { "dead", "pawn-destroyed", "despawned", "map", "weapon-destroyed",
            "primary", "saving", "clock", "clock-backward" })
            run("短鞭过期持有或时钟状态清理 " + gate, () => {
                var f = new Fixture(); f.Verb.Cast();
                switch (gate) {
                    case "dead": f.Owner.Dead = true; break;
                    case "pawn-destroyed": f.Owner.Destroyed = true; break;
                    case "despawned": f.Owner.Spawned = false; break;
                    case "map": f.Owner.Map = new Map(); break;
                    case "weapon-destroyed": f.Weapon.Destroyed = true; break;
                    case "primary": f.Owner.equipment.Primary = null; break;
                    case "saving": Scribe.mode = LoadSaveMode.Saving; break;
                    case "clock": Find.TickManager = new TickManager { TicksGame = 1001 }; break;
                    case "clock-backward": f.Clock.TicksGame--; break;
                }
                Check(f.Pose == SSCPnaWhipPose.Idle, "invalidated");
                f.Owner.Dead = f.Owner.Destroyed = f.Weapon.Destroyed = false; f.Owner.Spawned = true;
                f.Owner.Map = f.Map; f.Owner.equipment.Primary = f.Weapon;
                Scribe.mode = LoadSaveMode.Inactive; Find.TickManager = f.Clock; f.Clock.TicksGame = 1001;
                Check(f.Pose == SSCPnaWhipPose.Idle, "old pose cannot reappear");
            });
        run("短鞭真实卸装后缀清理同tick重新装备", () => {
            var f = new Fixture(); f.Verb.Cast(); f.Owner.equipment.Primary = null;
            f.Owner.equipment.Notify_EquipmentRemoved(f.Weapon);
            Near(1, f.Owner.equipment.RemoveCalls); Check(f.Owner.equipment.Primary == null, "native removal");
            f.Owner.equipment.Primary = f.Weapon;
            Check(f.Pose == SSCPnaWhipPose.Idle, "no stale swing");
        });
        run("多个短鞭的动作独立，转交不继承", () => {
            var a = new Fixture(); a.Verb.Cast(); a.Clock.TicksGame += 6;
            var b = new Fixture(a.Clock); b.Verb.Cast();
            Check(a.Pose == SSCPnaWhipPose.Recovery && b.Pose == SSCPnaWhipPose.Attack, "independent start times");
            a.Owner.equipment.Primary = null; a.Owner.equipment.Notify_EquipmentRemoved(a.Weapon);
            b.Owner.equipment.Primary = a.Weapon;
            Check(a.Pose == SSCPnaWhipPose.Idle, "transfer clears old owner pose");
        });
        foreach (bool flip in new[] { false, true })
            foreach (var row in new[] { (false, 0, "Idle"), (true, 0, "Attack"), (true, 6, "Recovery"), (true, 18, "Idle") })
                run($"短鞭单次绘制与最终变换保持 {flip}/{row.Item3}/{row.Item2}", () => {
                    var f = new Fixture(); if (row.Item1) f.Verb.Cast(); f.Clock.TicksGame += row.Item2;
                    PawnRenderUtility.NextMesh = flip ? MeshPool.plane10Flip : MeshPool.plane10;
                    f.Draw(); Near(1, Graphics.Sent.Count);
                    var r = Graphics.Sent.Single(); Check(!r.Overlay, "no additive glow");
                    Check(ReferenceEquals(r.Mesh, PawnRenderUtility.NextMesh), "original flip");
                    if (row.Item3 == "Idle") CheckIdle(PawnRenderUtility.NextMatrix, r.Matrix, flip);
                    else Check(r.Matrix.Equals(PawnRenderUtility.NextMatrix), "attack/recovery retain original matrix");
                    Near(4, r.Layer);
                    Check(((Texture2D)r.Material.mainTexture).Path == SSCPnaWhipRenderer.TexturePrefix + row.Item3, "correct pose");
                    Check(ReferenceEquals(r.Material.shader, f.Original.shader), "native shader");
                    Near(.3, r.Material.color.r); Check(!ReferenceEquals(r.Material, f.Original), "isolated material");
                    Check(((Texture2D)f.Original.mainTexture).Path == "item", "shared item material untouched");
                });
        run("未知短鞭网格不猜测，保留原材质绘制", () => {
            var f = new Fixture(); PawnRenderUtility.NextMesh = new Mesh(); f.Draw();
            Near(1, Graphics.Sent.Count); Check(ReferenceEquals(Graphics.Sent.Single().Material, f.Original), "fallback");
        });
        run("普通武器不替换为短鞭，不触发额外绘制", () => {
            var f = new Fixture(); f.Weapon.def.defName = "Other"; f.Draw();
            Near(1, Graphics.Sent.Count); Check(ReferenceEquals(Graphics.Sent.Single().Material, f.Original), "untouched");
        });
        run("短鞭不修改共享材质且不会串姿态", () => {
            var f = new Fixture(); f.Draw(); var idle = Graphics.Sent.Last().Material;
            f.Verb.Cast(); f.Draw(); var attack = Graphics.Sent.Last().Material;
            Check(((Texture2D)idle.mainTexture).Path.EndsWith("Idle"), "idle material remains idle");
            Check(((Texture2D)attack.mainTexture).Path.EndsWith("Attack"), "attack material");
            f.Clock.TicksGame += 18; f.Draw(); Check(ReferenceEquals(idle, Graphics.Sent.Last().Material), "cached idle reused");
        });
        run("短鞭空闲查询不产生泛光或药效", () => {
            var f = new Fixture(); f.Verb.Cast(); Near(0, SSCPnaSpearGlowUtility.GetAlpha(f.Weapon));
            Near(0, f.Map.flecks.Sent.Count);
        });
        foreach (bool hit in new[] { true, false })
            foreach (bool unarmedTool in new[] { true, false })
                run($"持鞭的柄击或拳击均播放 {hit}/{unarmedTool}", () => {
                    var f = new Fixture();
                    f.Verb.EquipmentSource = unarmedTool ? null : f.Weapon;
                    f.Verb.NativeCast = () => hit;
                    Check(f.Verb.Cast() == hit, "native result unchanged"); Near(1, f.Verb.BaseCalls);
                    Check(f.Pose == SSCPnaWhipPose.Attack, "primary whip triggers even if chosen tool is not whip");
                });
        run("普通近战出手没有短鞭时不生成姿态", () => {
            var f = new Fixture(); f.Weapon.def.defName = "Other"; f.Verb.Cast();
            Check(f.Pose == SSCPnaWhipPose.Idle, "unrelated melee untouched");
        });
        run("受击或灭火共用的绘制通知不单独触发鞭击", () => {
            var f = new Fixture(); f.Owner.Drawer.Notify_MeleeAttackOn(f.Target);
            Check(f.Pose == SSCPnaWhipPose.Idle, "only actual melee entry triggers");
        });
        foreach (bool flip in new[] { false, true })
            foreach (int angle in new[] { 0, 90, 180, 270 })
                run($"短鞭四方向待机柄尾向下45度且柄心不跳 {flip}/{angle}", () => {
                    var f = new Fixture(); double r = angle * Math.PI / 180;
                    var source = new Matrix4x4 {m00=(float)Math.Cos(r)*1.2f, m20=-(float)Math.Sin(r)*1.2f,
                        m02=(float)Math.Sin(r)*.8f, m22=(float)Math.Cos(r)*.8f, m03=7, m23=9, m13=.05f,m33=1};
                    PawnRenderUtility.NextMatrix = source;
                    PawnRenderUtility.NextMesh = flip ? MeshPool.plane10Flip : MeshPool.plane10;
                    f.Draw(); Near(1, Graphics.Sent.Count); CheckIdle(source, Graphics.Sent.Single().Matrix, flip);
                });
        run("短鞭保留存档类型但继承原版出手且柄击仍为Blunt", () => {
            var maneuver = XDocument.Load(Path.Combine(root, "1.7/Defs/ManeuverDefs/SSC_PnaIndustrialManeuvers.xml"));
            Check(maneuver.Descendants("verbClass").Single().Value == "SexSlaveCraft.Verb_MeleeAttackSSCPnaWhip", "saved verb type unchanged");
            Check(maneuver.Descendants("meleeDamageDef").Single().Value == "SSC_Damage_PnaWhipCut", "physical path unchanged");
            Check(typeof(Verb_MeleeAttackSSCPnaWhip).GetMethod("TryCastShot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).DeclaringType == typeof(RimWorld.Verb_MeleeAttack), "compatibility class does not override native attack");
            var f = new Fixture(); var compat = new CompatVerb {CasterPawn=f.Owner,EquipmentSource=f.Weapon,NativeCast=()=>false};
            Check(!compat.Cast() && f.Pose == SSCPnaWhipPose.Attack, "saved-type verb also enters common native hook");
            var whip = XDocument.Load(Path.Combine(root, "1.7/Defs/ThingsDefs/SSC_PnaIndustrialWeapons.xml"))
                .Root.Elements("ThingDef").Single(d => (string)d.Element("defName") == SSCPnaWhipPoseUtility.WeaponDefName);
            Check(whip.Descendants("texPath").Single().Value == "Things/Item/Equipment/SSC_PnaShortWhip", "item graphic");
            Check(whip.Element("tools").Elements().Last().Descendants("li").Single().Value == "Blunt", "plain handle strike");
        });
    }
}
