using System;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static Pawn Combatant(float progress = 0)
    {
        Pawn p = Pawn(); Train(p, progress); return p;
    }
    private static DamageInfo Hit(Thing actor, DamageDef def = null) => new DamageInfo(def ?? DamageDefOf.Bullet, actor);
    private static void RunExperienceCases()
    {
        new Harmony("ssc.tests.combatant").PatchAll(typeof(Program).Assembly);
        Run("日常与仪式使用相同动态公式，仪式分数不使用部位虚拟量纲", () =>
        {
            Equal(60f, SpecializationTrainingProgressUtility.RitualScore(.8f));
            Equal(.03f, CombatantSpecializationProgressUtility.TrainingProgress(
                SpecializationTrainingProgressUtility.RitualScore(.8f), false));
            Equal(0f, SpecializationTrainingProgressUtility.RitualScore(float.NaN));
            Equal(0f, SpecializationTrainingProgressUtility.RitualScore(float.PositiveInfinity));
        });
        Run("公共受训经验只增长当前方向，并遵守完成、终极和资格门槛", () =>
        {
            foreach (SexSlaveSpecializationType type in new[]
            {
                SexSlaveSpecializationType.Bus, SexSlaveSpecializationType.Cow,
                SexSlaveSpecializationType.PetCat, SexSlaveSpecializationType.PetDog,
                SexSlaveSpecializationType.PetRabbit, SexSlaveSpecializationType.TrainerOfficer,
                SexSlaveSpecializationType.Combatant
            })
            {
                Pawn trainer = Pawn(), receiver = Pawn();
                receiver.Training.SetSpecialization(type);
                Equal(.02f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(trainer, receiver, 40f));
                Equal(.02f, receiver.Training.specializationProgress);
                Equal(0f, trainer.Training.specializationProgress);
                receiver.Training.specializationProgress = .999f;
                Equal(0f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(trainer, receiver, 40f));
                receiver.Training.specializationProgress = .25f;
                switch (type)
                {
                    case SexSlaveSpecializationType.Bus:
                        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Bus_Final);
                        break;
                    case SexSlaveSpecializationType.Cow:
                        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Cow_Final);
                        break;
                    case SexSlaveSpecializationType.TrainerOfficer:
                        receiver.TrainerFinal = true;
                        break;
                    case SexSlaveSpecializationType.Combatant:
                        receiver.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
                        break;
                    default:
                        receiver.PetFinal = true;
                        break;
                }
                Equal(0f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(trainer, receiver, 40f));
                Equal(.25f, receiver.Training.specializationProgress);
            }
            Pawn disqualified = Pawn();
            disqualified.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            disqualified.TrainerEligible = false;
            Equal(0f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(Pawn(), disqualified, 40f));
            Pawn noDirection = Pawn();
            Equal(0f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(Pawn(), noDirection, 40f));
        });
        Run("评分动态收益、主人倍率及两个上限", () =>
        {
            foreach (var v in new[] { (0f, 0f), (-20f, 0f), (20f, .01f), (40f, .02f), (60f, .03f), (80f, .04f), (200f, .04f), (float.MaxValue, .04f) })
            {
                Equal(v.Item2, CombatantSpecializationProgressUtility.TrainingProgress(v.Item1, false));
                Equal(v.Item2 * 1.5f, CombatantSpecializationProgressUtility.TrainingProgress(v.Item1, true));
            }
            foreach (float bad in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                Equal(0, CombatantSpecializationProgressUtility.TrainingProgress(bad, false));
                Equal(0, CombatantSpecializationProgressUtility.TrainingProgress(bad, true));
            }
        });
        Run("体型收益无最低奖励且最多百分之三", () =>
        {
            foreach (var v in new[] { (.01f, .0001f), (.25f, .0025f), (.5f, .005f), (1f, .01f), (2f, .02f), (3f, .03f), (100f, .03f), (float.MaxValue, .03f) })
                Equal(v.Item2, CombatantSpecializationProgressUtility.KillProgress(v.Item1));
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
                Equal(0, CombatantSpecializationProgressUtility.KillProgress(bad));
        });
        Run("实际主人加成与其他主人、指定调教员及无绑定基础收益", () =>
        {
            Pawn master = Pawn(PawnIdentity.Master), other = Pawn(PawnIdentity.Master);
            Pawn receiver = Combatant(); receiver.BoundMaster = master; receiver.AssignedTrainer = other;
            Equal(.03f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(master, receiver, 40));
            Equal(.02f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(other, receiver, 40));
            receiver.BoundMaster = null;
            Equal(.02f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(other, receiver, 40));
            receiver.BoundMaster = other;
            Equal(.03f, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(other, receiver, 40));
            Equal(.10f, Ordinary(receiver).Severity);
            Equal(0, master.Training.specializationProgress);
        });
        Run("两类事件仅向 SSC 性奴发奖，无额外研究或进度倍率", () =>
        {
            SSCDefOf.SSC_RES_Combatant.IsFinished = false;
            foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
            foreach (float progress in new[] { 0f, .25f, .75f })
            {
                // 直接构造事件输入验证提交入口，不测试身份切换或旧记录迁移。
                Pawn receiver = Pawn(identity); Train(receiver, progress);
                receiver.IsColonist = false; receiver.IsSlave = true;
                bool eligible = identity == PawnIdentity.Slave;
                Equal(eligible ? .02f : 0, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(Pawn(), receiver, 40));
                Equal(progress + (eligible ? .02f : 0), receiver.Training.specializationProgress);
                new Pawn().Kill(Hit(receiver));
                Equal(progress + (eligible ? .03f : 0), receiver.Training.specializationProgress);
                if (eligible) Equal(progress + .03f, Ordinary(receiver).Severity);
            }
        });
        Run("无效参与者与错误方向不获奖", () =>
        {
            Pawn p = Combatant(), trainer = Pawn();
            Equal(0, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(null, p, 80));
            Equal(0, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(p, p, 80));
            trainer.Dead = true;
            Equal(0, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(trainer, p, 80));
            trainer.Dead = false; trainer.Destroyed = true;
            Equal(0, SpecializationTrainingProgressUtility.NotifyTrainingCompleted(trainer, p, 80));
            foreach (SexSlaveSpecializationType direction in Enum.GetValues<SexSlaveSpecializationType>())
            {
                if (direction == SexSlaveSpecializationType.Combatant) continue;
                p.Training.SetSpecialization(direction);
                Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(p, .01f));
            }
            p = Combatant(); p.Dead = true; Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(p, .01f));
            p.Dead = false; p.Destroyed = true; Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(p, .01f));
            Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(new Pawn(), .01f));
        });
        Run("进度完成容差、剩余空间、异常数值及阶段即时同步", () =>
        {
            foreach (float before in new[] { .19f, .49f, .98f, .999f, 1f })
            {
                Pawn p = Combatant(before);
                float expected = before >= .999f ? before : Math.Min(1, before + .04f);
                Equal(expected - before, CombatantSpecializationProgressUtility.TryGainProgress(p, .04f));
                Equal(expected, p.Training.specializationProgress); Equal(expected, Ordinary(p).Severity);
                Equal(expected, Reload(p).Training.specializationProgress);
            }
            foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Pawn p = Combatant(.3f);
                Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(p, bad));
                Equal(.3f, p.Training.specializationProgress);
                p.Training.specializationProgress = bad;
                Equal(.01f, CombatantSpecializationProgressUtility.TryGainProgress(p, .01f));
                Equal(.01f, Ordinary(p).Severity);
            }
            Pawn over = Combatant(); over.Training.specializationProgress = 2f;
            Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(over, .01f)); Equal(1, Ordinary(over).Severity);
        });
        Run("获得经验后切换保存历史而不跨方向发奖", () =>
        {
            Pawn p = Combatant(.2f);
            CombatantSpecializationProgressUtility.TryGainProgress(p, .04f);
            p.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            Equal(0, CombatantSpecializationProgressUtility.TryGainProgress(p, .03f));
            p.Training.SetSpecialization(SexSlaveSpecializationType.Combatant);
            Equal(.24f, Ordinary(p).Severity);
        });
        Run("真实 Harmony 死亡边界采集死亡前体型，不读取死后体型", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn { ActualBodySize = .25f };
            victim.OnKill = () => victim.ActualBodySize = 10;
            victim.Kill(Hit(killer)); Equal(.0025f, killer.Training.specializationProgress);
        });
        Run("目标种族阵营倒地不改变相同体型奖励", () =>
        {
            foreach (string kind in new[] { "human", "animal", "insect", "mech", "modded" })
            foreach (string faction in new[] { "friendly", "neutral", "hostile" })
            foreach (bool downed in new[] { false, true })
            {
                Pawn killer = Combatant();
                new Pawn { Kind = kind, Faction = faction, Downed = downed }.Kill(Hit(killer));
                Equal(.01f, killer.Training.specializationProgress);
            }
        });
        Run("屠宰排除而处决狩猎补刀及带屠宰任务的其他攻击计入", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn();
            killer.jobs.curDriver = new JobDriver_Slaughter();
            killer.jobs.curJob = new Job { targetA = new LocalTargetInfo { Thing = victim } };
            victim.Kill(Hit(killer, DamageDefOf.ExecutionCut)); Equal(0, killer.Training.specializationProgress);
            victim.Dead = false; victim.Kill(Hit(killer)); Equal(.01f, killer.Training.specializationProgress);
            // 其他目标不能因攻击者刚好正在屠宰而被过滤。
            new Pawn().Kill(Hit(killer, DamageDefOf.ExecutionCut)); Equal(.02f, killer.Training.specializationProgress);
            killer.jobs.curDriver = new JobDriver();
            new Pawn { Downed = true }.Kill(Hit(killer, DamageDefOf.ExecutionCut)); Equal(.03f, killer.Training.specializationProgress);
        });
        Run("没有本次攻击者的失血及陷阱不追溯经验", () =>
        {
            Pawn killer = Combatant();
            new Pawn().Kill(null, new Hediff());
            new Pawn().Kill(Hit(new Thing()));
            new Pawn().Kill(Hit(killer, new DamageDef { harmsHealth = false }));
            Equal(0, killer.Training.specializationProgress);
        });
        Run("保留攻击者的持续燃烧不发奖，直接火焰攻击可发奖", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn();
            new Fire { OnDamage = () => victim.Kill(Hit(killer, DamageDefOf.Flame)) }.DoFireDamage(victim);
            Equal(0, killer.Training.specializationProgress);
            new Pawn().Kill(Hit(killer, DamageDefOf.Flame)); Equal(.01f, killer.Training.specializationProgress);
        });
        Run("原攻击者的分摊伤害不发奖，原目标直接死亡仍有奖励", () =>
        {
            Pawn killer = Combatant();
            SSCMasterBondUtility.IsRedirectingDamage = true;
            try { new Pawn().Kill(Hit(killer)); }
            finally { SSCMasterBondUtility.IsRedirectingDamage = false; }
            Equal(0, killer.Training.specializationProgress);
            new Pawn().Kill(Hit(killer)); Equal(.01f, killer.Training.specializationProgress);
        });
        Run("射弹或武器爆炸计入，保留攻击者的环境连锁不计入", () =>
        {
            Pawn killer = Combatant();
            new Explosion { OnDamage = () => new Pawn().Kill(Hit(killer)) }.AffectCell(default);
            Equal(0, killer.Training.specializationProgress);
            new Explosion { projectile = new ThingDef(), OnDamage = () => new Pawn().Kill(Hit(killer)) }.AffectCell(default);
            new Explosion { weapon = new ThingDef(), OnDamage = () => new Pawn().Kill(Hit(killer)) }.AffectCell(default);
            Equal(.02f, killer.Training.specializationProgress);
        });
        Run("未真正死亡、重复死亡及嵌套死亡仅结算实际一次", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn { PreventDeath = true };
            victim.Kill(Hit(killer)); Equal(0, killer.Training.specializationProgress);
            victim.PreventDeath = false;
            victim.OnKill = () => { victim.OnKill = null; victim.Kill(Hit(killer)); };
            victim.Kill(Hit(killer)); victim.Kill(Hit(killer)); Equal(.01f, killer.Training.specializationProgress);
        });
        Run("同 tick 多目标与复活后的新死亡各自正常结算", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn();
            victim.Kill(Hit(killer)); new Pawn().Kill(Hit(killer));
            victim.Dead = false; victim.Kill(Hit(killer)); Equal(.03f, killer.Training.specializationProgress);
            Pawn loadedDead = new Pawn { Dead = true }; loadedDead.Kill(Hit(killer)); Equal(.03f, killer.Training.specializationProgress);
        });
        Run("原死亡异常保持传播；经验采样失败不阻止死亡且释放保护", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn();
            victim.OnKill = () => throw new InvalidOperationException("death");
            bool thrown = false; try { victim.Kill(Hit(killer)); } catch (InvalidOperationException) { thrown = true; }
            Check(thrown, "死亡异常被吞"); Equal(0, killer.Training.specializationProgress);
            victim.OnKill = null; victim.ReadBodySize = () => throw new InvalidOperationException("size");
            victim.Kill(Hit(killer)); Check(victim.Dead, "额外采样阻止死亡");
            Equal(0, killer.Training.specializationProgress);
            victim.Dead = false; victim.ReadBodySize = null;
            victim.Kill(Hit(killer)); Equal(.01f, killer.Training.specializationProgress);
        });
        Run("燃烧和爆炸异常释放间接来源作用域", () =>
        {
            Pawn killer = Combatant();
            try { new Fire { OnDamage = () => throw new InvalidOperationException() }.DoFireDamage(new Pawn()); } catch (InvalidOperationException) { }
            try { new Explosion { OnDamage = () => throw new InvalidOperationException() }.AffectCell(default); } catch (InvalidOperationException) { }
            new Pawn().Kill(Hit(killer)); Equal(.01f, killer.Training.specializationProgress);
        });
        Run("无效体型、自杀和完成培养不发击杀奖励", () =>
        {
            Pawn killer = Combatant();
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                new Pawn { ActualBodySize = bad }.Kill(Hit(killer));
            killer.Kill(Hit(killer)); Equal(0, killer.Training.specializationProgress);
            killer = Combatant(.999f); new Pawn().Kill(Hit(killer)); Equal(.999f, killer.Training.specializationProgress);
        });
        Run("重复提交同一死亡凭据不能重发，释放后的凭据失效", () =>
        {
            Pawn killer = Combatant(), victim = new Pawn();
            var record = CombatantKillUtility.BeginDeath(victim, Hit(killer));
            victim.Dead = true;
            record.Complete(); record.Complete(); record.Dispose(); record.Dispose(); record.Complete();
            Equal(.01f, killer.Training.specializationProgress);
        });
    }
}
