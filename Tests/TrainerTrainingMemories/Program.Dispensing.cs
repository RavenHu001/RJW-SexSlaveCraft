using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;
using UnityEngine;

// 记忆发放与数值换算用例：驱动真实 ConditioningUtility 与 LegacyTrainingUtility。
internal static partial class Program
{
    /// <summary>弱值换算必须为整数、四舍五入，并对非零基准保底绝对值 1。</summary>
    private static void WeakValueRule()
    {
        Equal(-5, TrainerTrainingMemoryUtility.WeakValue(-10), "反感心情");
        Equal(1, TrainerTrainingMemoryUtility.WeakValue(2), "动摇心情");
        Equal(4, TrainerTrainingMemoryUtility.WeakValue(8), "适应心情");
        Equal(8, TrainerTrainingMemoryUtility.WeakValue(15), "沉沦心情");
        Equal(-10, TrainerTrainingMemoryUtility.WeakValue(-20), "旧模式反感社交");
        Equal(3, TrainerTrainingMemoryUtility.WeakValue(5), "旧模式适应社交取整");
        Equal(8, TrainerTrainingMemoryUtility.WeakValue(15), "旧模式沉沦社交");

        // 半值边界：基准 ×0.5 恰好落在 .5 上，必须“五入”而不是“取偶”。
        // 这里对应设计文档 4.7 节数值表；曾经因为使用 Mathf.RoundToInt（取偶）
        // 而把 +5 舍成 +2、+9 舍成 +4、-5 舍成 -2，与约定不符。
        Equal(3, TrainerTrainingMemoryUtility.WeakValue(5), "半值 +5 应入到 +3");
        Equal(5, TrainerTrainingMemoryUtility.WeakValue(9), "半值 +9 应入到 +5");
        Equal(-3, TrainerTrainingMemoryUtility.WeakValue(-5), "半值 -5 应入到 -3");
        Equal(-5, TrainerTrainingMemoryUtility.WeakValue(-9), "半值 -9 应入到 -5");
        Equal(2, TrainerTrainingMemoryUtility.WeakValue(3), "半值 +3 应入到 +2");
        Equal(4, TrainerTrainingMemoryUtility.WeakValue(7), "半值 +7 应入到 +4");
        Equal(6, TrainerTrainingMemoryUtility.WeakValue(11), "半值 +11 应入到 +6");
        Equal(7, TrainerTrainingMemoryUtility.WeakValue(13), "半值 +13 应入到 +7");
        Equal(-2, TrainerTrainingMemoryUtility.WeakValue(-3), "半值 -3 应入到 -2");
        Equal(-4, TrainerTrainingMemoryUtility.WeakValue(-7), "半值 -7 应入到 -4");

        // 替身必须与真实引擎一致：Unity 的 Mathf.RoundToInt 在 .5 上取偶。
        // 用替身自身再确认一次，避免以后有人把替身改回“远离零”而掩盖同类问题。
        Equal(2, Mathf.RoundToInt(2.5f), "替身的 Mathf.RoundToInt 必须复现 Unity 的取偶语义");

        // 非零基准被舍入成零时必须保底 ±1，不能变成零效果。
        Equal(1, TrainerTrainingMemoryUtility.WeakValue(1), "正一保底");
        Equal(-1, TrainerTrainingMemoryUtility.WeakValue(-1), "负一保底");
        Equal(0, TrainerTrainingMemoryUtility.WeakValue(0), "零基准保持零");

        Assert(Math.Abs(TrainerTrainingMemoryUtility.WeakFactor - 0.5f) < 0.0001f, "弱化系数应为 0.5");
    }

    /// <summary>新模式社交偏移：先对 D 取整成基准，再套用同一套弱值换算。</summary>
    private static void WeakOpinionOffsetRule()
    {
        Equal(-1, TrainerTrainingMemoryUtility.WeakOpinionOffset(-1f), "D=-1 保底");
        Equal(1, TrainerTrainingMemoryUtility.WeakOpinionOffset(1f), "D=1 保底");
        Equal(3, TrainerTrainingMemoryUtility.WeakOpinionOffset(5f), "D=5");
        Equal(3, TrainerTrainingMemoryUtility.WeakOpinionOffset(6f), "D=6");
        Equal(5, TrainerTrainingMemoryUtility.WeakOpinionOffset(10f), "D=10");
        Equal(6, TrainerTrainingMemoryUtility.WeakOpinionOffset(11f), "D=11");
        Equal(6, TrainerTrainingMemoryUtility.WeakOpinionOffset(12f), "D=12");

        // D 由原算法产生，理论上不会出现半值；这里确认即便传入半值也不会产生小数结果。
        Equal(1, TrainerTrainingMemoryUtility.WeakOpinionOffset(0.6f), "半值输入仍然取整");
    }

    /// <summary>套别分流：有效训导官、主人、失格性奴与空对象各自归类。</summary>
    private static void MemorySetResolution()
    {
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        Equal(1, (int)TrainerTrainingMemoryUtility.Resolve(officer), "有效训导官");

        // 主人固定具备调教员身份，但不属于训导官，必须继续走原套。
        Pawn master = NewPawn(PawnIdentity.Master);
        Equal(0, (int)TrainerTrainingMemoryUtility.Resolve(master), "主人");

        // 性奴但从未担任训导官：原本合法的执行者，继续走原套，
        // 不能被误判成失格训导官而丢失既有反馈。
        Pawn ordinarySlave = NewPawn(PawnIdentity.Slave);
        Equal(0, (int)TrainerTrainingMemoryUtility.Resolve(ordinarySlave), "从未任职的普通性奴");

        // 关闭任职开关的性奴：结算时已不满足有效任职，不回退领取强套。
        Pawn disabled = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerIdentityInitialized: true);
        disabled.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled = false;
        Equal(2, (int)TrainerTrainingMemoryUtility.Resolve(disabled), "关闭任职的性奴");
        Pawn unqualified = NewPawn(PawnIdentity.Slave, trainerEnabled: true, trainerIdentityInitialized: true);
        Equal(2, (int)TrainerTrainingMemoryUtility.Resolve(unqualified), "失格性奴");

        // 死亡、销毁与被丢弃的角色不能产生新反馈。
        Pawn dead = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        dead.Dead = true;
        Assert(!TrainerOfficerFeedback.IsActiveOfficer(dead), "死亡角色不再有效任职");
        Pawn destroyed = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        destroyed.Destroyed = true;
        Assert(!TrainerOfficerFeedback.IsActiveOfficer(destroyed), "销毁角色不再有效任职");

        // 未选择身份的角色属于原本合法的其他执行者，继续走原套而不是弱套。
        Pawn unset = NewPawn(PawnIdentity.Unset);
        Equal(0, (int)TrainerTrainingMemoryUtility.Resolve(unset), "未选择身份");

        // 空对象不能获得任何套别，避免无效任务发放奖励。
        Equal(2, (int)TrainerTrainingMemoryUtility.Resolve(null), "空执行者");
    }

    /// <summary>套别分流跟随结算当次的真实身份，不保存历史分类。</summary>
    private static void MemorySetFollowsCurrentLegality()
    {
        // 同一角色先作为有效训导官，之后合法变成主人：两次结算必须走不同套别。
        Pawn changing = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        Pawn firstReceiver = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(changing, firstReceiver, 60f, 12f);
        Equal(1, MemoriesWithDef(firstReceiver, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count, "首席身份为训导官");

        var comp = changing.TryGetComp<CompSexSlaveTraining>();
        comp.pawnIdentity = PawnIdentity.Master;
        comp.slaveTrainerEnabled = false;
        Pawn secondReceiver = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(changing, secondReceiver, 60f, 12f);
        Equal(1, MemoriesWithDef(secondReceiver, SSCDefOf.SSC_Training_OpinionDynamic).Count, "改成主人后走原套");
        Equal(0, MemoriesWithDef(secondReceiver, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count, "改成主人后不再发弱套");

        // 身份切换本身不发放任何记忆，也不会清理已经生成的历史记忆。
        Assert(MemoriesWithDef(firstReceiver, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count == 1,
            "身份切换不清理历史弱套");
    }

    /// <summary>
    /// 复现日常结算的记忆发放：结算入口只读判定一次套别，再交给对应套别的发放路径。
    /// 恶堕、意志、经验与冷却等正式收益不在本套件范围内。
    /// </summary>
    private static void ExecuteDaily(Pawn executor, Pawn receiver, float score, float opinionChange)
    {
        TrainingMemorySet memorySet = TrainerTrainingMemoryUtility.Resolve(executor);
        if (SSCMod.settings?.useOldScoring ?? false)
        {
            // 旧模式沿用生产等级判定，再按套别发放固定档位。
            int level = LegacyTrainingUtility.DetermineLevel_Legacy(executor, score);
            LegacyTrainingUtility.ApplyMemories_Legacy(level, executor, receiver, true, memorySet);
            return;
        }

        // 新模式在结算入口按原算法取整得到整数基准。
        int opinionDelta = (int)opinionChange;
        TrainerTrainingMemoryUtility.ApplyDynamicMemories(memorySet, opinionDelta, executor, receiver);
    }

    /// <summary>把主人社交技能设为固定等级，使旧模式等级判定确定可复现。</summary>
    private static void SetSocialLevel(Pawn pawn, int level)
    {
        pawn.skills.skills.Clear();
        pawn.skills.skills.Add(new SkillRecord { def = SkillDefOf.Social, Level = level });
    }

    private static void SetLegacyScoring(bool enabled) => SSCMod.settings.useOldScoring = enabled;

    /// <summary>新模式主人路径：原套 Def、原值与零变化不发全部保持。</summary>
    private static void MasterDynamicDispensing()
    {
        Pawn master = NewPawn(PawnIdentity.Master);
        Pawn slave = NewPawn(PawnIdentity.Slave);

        // D=12 属于沉沦档，主人社交实际偏移就是 12。
        ExecuteDaily(master, slave, 60f, 12f);

        var masterMoods = MemoriesWithDef(slave, SSCDefOf.SSC_Training_MoodDynamic);
        var masterSocials = MemoriesWithDef(slave, SSCDefOf.SSC_Training_OpinionDynamic);
        Equal(1, masterMoods.Count, "主人心情一份");
        Equal(1, masterSocials.Count, "主人社交一份");
        Equal(3, masterMoods[0].CurStageIndex, "主人心情档位为沉沦");
        Equal(12, (int)((Thought_MemorySocial)masterSocials[0]).opinionOffset, "主人社交实际偏移保持原值");

        // 主人路径绝不能出现弱套；两者是不同 Def 家族。
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_MoodDynamic).Count == 0, "主人不发弱心情");
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count == 0, "主人不发弱社交");

        // 负向档位同样是原值，不因弱套存在而改变。
        ClearMemories(slave);
        ExecuteDaily(master, slave, 10f, -4f);
        var negative = MemoriesWithDef(slave, SSCDefOf.SSC_Training_OpinionDynamic);
        Equal(1, negative.Count, "主人负向社交一份");
        Equal(-4, (int)((Thought_MemorySocial)negative[0]).opinionOffset, "主人负向偏移为原值");
        Equal(0, MemoriesWithDef(slave, SSCDefOf.SSC_Training_MoodDynamic)[0].CurStageIndex, "负向为反感档");

        // 零变化不发任何记忆，主人与训导官一致。
        ClearMemories(slave);
        ExecuteDaily(master, slave, 0f, 0f);
        Equal(0, MemoriesOf(slave).Count, "零变化不发记忆");
    }

    /// <summary>新模式训导官路径：只发弱套，档位由未弱化的 D 决定，实际偏移为整数弱值。</summary>
    private static void OfficerDynamicDispensing()
    {
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        Pawn slave = NewPawn(PawnIdentity.Slave);

        // D=12：档位仍为沉沦，实际偏移为 12 的弱值 6。
        ExecuteDaily(officer, slave, 60f, 12f);

        var weakMoods = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_MoodDynamic);
        var weakSocials = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(1, weakMoods.Count, "训导官心情一份");
        Equal(1, weakSocials.Count, "训导官社交一份");
        Equal(3, weakMoods[0].CurStageIndex, "训导官心情档位保留沉沦");
        Equal(6, (int)((Thought_MemorySocial)weakSocials[0]).opinionOffset, "训导官社交为弱值 +6");
        Assert(weakMoods[0] is Thought_Memory, "弱心情使用原生普通记忆类");
        Assert(weakSocials[0] is Thought_MemoryTrainerTraining, "弱社交使用训导官动态社交类");

        // 单次训导官完成不得同时生成本次原套，也不能把好感转给执行者的主人。
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_Training_MoodDynamic).Count == 0, "训导官不发原套心情");
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_Training_OpinionDynamic).Count == 0, "训导官不发原套社交");
        Assert(ReferenceEquals(weakSocials[0].otherPawn, officer), "社交对象必须是实际施教训导官");

        // D 的档位边界与主人一致：0 不发，负数保底，±1 不被舍入成零效果。
        ClearMemories(slave);
        ExecuteDaily(officer, slave, 40f, 0f);
        Equal(0, MemoriesOf(slave).Count, "D=0 不发");

        ClearMemories(slave);
        ExecuteDaily(officer, slave, 8f, 1f);
        var wavering = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(1, wavering.Count, "D=1 发一份");
        Equal(1, wavering[0].CurStageIndex, "D=1 为动摇档");
        Equal(1, (int)((Thought_MemorySocial)wavering[0]).opinionOffset, "D=1 弱值保底 +1");

        ClearMemories(slave);
        ExecuteDaily(officer, slave, 8f, -1f);
        var resentful = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(0, resentful[0].CurStageIndex, "D=-1 为反感档");
        Equal(-1, (int)((Thought_MemorySocial)resentful[0]).opinionOffset, "D=-1 弱值保底 -1");

        // 适应档边界：D=6 仍是适应，弱值为 3。
        ClearMemories(slave);
        ExecuteDaily(officer, slave, 40f, 6f);
        var adapted = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(2, adapted[0].CurStageIndex, "D=6 为适应档");
        Equal(3, (int)((Thought_MemorySocial)adapted[0]).opinionOffset, "D=6 弱值 +3");

        // 弱值与主人原值必须可区分，否则弱化没有意义。
        Assert(TrainerTrainingMemoryUtility.WeakValue(15) < 15, "弱值必须小于主人原值");
    }

    /// <summary>失格执行者：本次不发任何套，也不回退领取较强的原套。</summary>
    private static void InvalidOfficerDispensing()
    {
        // 结算瞬间关闭任职开关：执行者曾开启训导官身份，分类应落在 None。
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true,
            trainerIdentityInitialized: true);
        Pawn slave = NewPawn(PawnIdentity.Slave);
        officer.TryGetComp<CompSexSlaveTraining>().slaveTrainerEnabled = false;
        ExecuteDaily(officer, slave, 60f, 12f);
        Equal(0, MemoriesOf(slave).Count, "失格执行者不发受训者记忆");

        // 从未担任训导官的普通性奴执行者仍属原本合法的执行者，继续走原套。
        Pawn slaveExecutor = NewPawn(PawnIdentity.Slave);
        Pawn receiver = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(slaveExecutor, receiver, 60f, 12f);
        Equal(1, MemoriesWithDef(receiver, SSCDefOf.SSC_Training_OpinionDynamic).Count, "普通性奴仍走原套");

        // 死亡执行者同样不能产生新记忆。
        Pawn dead = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        dead.Dead = true;
        Pawn deadReceiver = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(dead, deadReceiver, 60f, 12f);
        Equal(0, MemoriesOf(deadReceiver).Count, "死亡执行者不发记忆");
    }

    /// <summary>旧模式：等级不变，主人走原套，训导官走数值等价的弱套。</summary>
    private static void LegacyLevelDispensing()
    {
        SetLegacyScoring(true);

        // 社交技能 15 直接判定为等级 3，避免随机项影响断言。
        Pawn master = NewPawn(PawnIdentity.Master);
        SetSocialLevel(master, 15);
        Pawn slave = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(master, slave, 100f, 0f);

        Equal(1, MemoriesWithDef(slave, SSCDefOf.SSC_Training_Mood_Lvl3).Count, "主人等级 3 心情");
        Equal(1, MemoriesWithDef(slave, SSCDefOf.SSC_Training_Social_Lvl3).Count, "主人等级 3 社交");
        Equal(15, (int)SSCDefOf.SSC_Training_Social_Lvl3.stages[0].baseOpinionOffset, "主人等级 3 运行值为 15");
        Equal(0, MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_Social_Lvl3).Count, "主人不发弱套");

        // 同一等级下有效训导官改发弱套，数值为运行值的一半取整。
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        SetSocialLevel(officer, 15);
        Pawn weakReceiver = NewPawn(PawnIdentity.Slave);
        ExecuteDaily(officer, weakReceiver, 100f, 0f);

        Equal(1, MemoriesWithDef(weakReceiver, SSCDefOf.SSC_TrainerTraining_Mood_Lvl3).Count, "训导官等级 3 弱心情");
        Equal(1, MemoriesWithDef(weakReceiver, SSCDefOf.SSC_TrainerTraining_Social_Lvl3).Count, "训导官等级 3 弱社交");
        Equal(8, (int)SSCDefOf.SSC_TrainerTraining_Social_Lvl3.stages[0].baseOpinionOffset, "弱社交等级 3 为 8");
        Equal(0, MemoriesWithDef(weakReceiver, SSCDefOf.SSC_Training_Mood_Lvl3).Count, "训导官不发原套心情");

        // 等级 1 与等级 2 的弱值分别为 -10 与 +3，心情为负向与正向且均为整数。
        AssertWeakLevel(1, 5f, -10, SSCDefOf.SSC_TrainerTraining_Mood_Lvl1, SSCDefOf.SSC_TrainerTraining_Social_Lvl1);
        AssertWeakLevel(2, 50f, 3, SSCDefOf.SSC_TrainerTraining_Mood_Lvl2, SSCDefOf.SSC_TrainerTraining_Social_Lvl2);

        SetLegacyScoring(false);
    }

    /// <summary>用指定社交等级与分数产生旧模式等级，校验该等级实际发放的弱套定义与数值。</summary>
    private static void AssertWeakLevel(int expectedLevel, float score, int expectedSocial, ThoughtDef moodDef, ThoughtDef socialDef)
    {
        // 社交技能 5 时：分数 5 判定为等级 1，分数 50 判定为等级 2；
        // 固定随机项为 0 且不触发幸运倍率，使等级判定确定可复现。
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        SetSocialLevel(officer, 5);
        Pawn receiver = NewPawn(PawnIdentity.Slave);
        Rand.RangeValue = 0f;
        Rand.ChanceResult = false;
        ExecuteDaily(officer, receiver, score, 0f);

        Equal(1, MemoriesWithDef(receiver, moodDef).Count, $"等级 {expectedLevel} 的弱心情一份");
        Equal(1, MemoriesWithDef(receiver, socialDef).Count, $"等级 {expectedLevel} 的弱社交一份");
        // 弱心情必须等于同等级主人基准的整数弱值。
        ThoughtDef masterMood = expectedLevel == 1
            ? SSCDefOf.SSC_Training_Mood_Lvl1
            : (expectedLevel == 2 ? SSCDefOf.SSC_Training_Mood_Lvl2 : SSCDefOf.SSC_Training_Mood_Lvl3);
        Equal(TrainerTrainingMemoryUtility.WeakValue((int)masterMood.stages[0].baseMoodEffect),
            (int)moodDef.stages[0].baseMoodEffect, $"等级 {expectedLevel} 的弱心情值");
        Equal(expectedSocial, (int)((Thought_MemorySocial)MemoriesWithDef(receiver, socialDef)[0]).opinionOffset,
            $"等级 {expectedLevel} 的弱社交实例偏移");
        Assert(moodDef.stages[0].baseMoodEffect != 0, "弱心情必须非零");
        Assert(MemoriesWithDef(receiver, SSCDefOf.SSC_Training_Mood_Lvl1).Count == 0
            && MemoriesWithDef(receiver, SSCDefOf.SSC_Training_Mood_Lvl2).Count == 0,
            "训导官不得发放原套心情");
    }

    /// <summary>旧模式仪式：维持原套且不发社交记忆，不新增弱套。</summary>
    private static void LegacyRitualDispensing()
    {
        SetLegacyScoring(true);
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        Pawn receiver = NewPawn(PawnIdentity.Slave);

        // 兼容调用保持原套默认值；仪式路径沿用 includeSocialThought=false。
        LegacyTrainingUtility.ApplyMemories_Legacy(2, officer, receiver, false);
        Equal(1, MemoriesWithDef(receiver, SSCDefOf.SSC_Training_Mood_Lvl2).Count, "兼容调用仍发原套心情");
        Equal(0, MemoriesWithDef(receiver, SSCDefOf.SSC_Training_Social_Lvl2).Count, "仪式不发社交记忆");
        Equal(0, MemoriesWithDef(receiver, SSCDefOf.SSC_TrainerTraining_Mood_Lvl2).Count, "仪式不新增弱套");

        // 即使显式传入弱套，仪式调用也不发社交记忆，弱心情仍按等级发放。
        ClearMemories(receiver);
        LegacyTrainingUtility.ApplyMemories_Legacy(1, officer, receiver, false, TrainingMemorySet.Officer);
        Equal(1, MemoriesWithDef(receiver, SSCDefOf.SSC_TrainerTraining_Mood_Lvl1).Count, "弱套心情按等级发放");
        Equal(0, MemoriesOf(receiver).Count(m => m is Thought_MemorySocial), "弱套仪式不发社交记忆");

        SetLegacyScoring(false);
    }

    /// <summary>动态社交记忆：显示与分组按保存阶段，而不是按弱化后的偏移反推。</summary>
    private static void DynamicSocialBehaviour()
    {
        Pawn slave = NewPawn(PawnIdentity.Slave);
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);

        // D=12 的弱值为 +6；若按偏移反推会得到“适应”，这里必须仍显示沉沦并分在沉沦组。
        ExecuteDaily(officer, slave, 60f, 12f);
        var submission = (Thought_MemorySocial)MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Single();
        Equal(3, submission.CurStageIndex, "沉沦档位被保存");
        Assert(submission.LabelCap.Contains("沉沦"), "标签按保存阶段取沉沦，实际=" + submission.LabelCap);

        // 适应档（D=6 → +3）与沉沦档不得互相分组，否则两档会合并成一份。
        ExecuteDaily(officer, slave, 40f, 6f);
        var adaptation = (Thought_MemorySocial)MemoriesOf(slave)
            .Single(m => m.def == SSCDefOf.SSC_TrainerTraining_OpinionDynamic && m.CurStageIndex == 2);
        Assert(!submission.GroupsWith(adaptation), "不同档位不得同组");

        // 同档位、同对象的两份记忆必须同组，保证重复工作时走原生刷新路径。
        ExecuteDaily(officer, slave, 40f, 6f);
        var adaptationSecond = (Thought_MemorySocial)MemoriesOf(slave)
            .Where(m => m.def == SSCDefOf.SSC_TrainerTraining_OpinionDynamic && m.CurStageIndex == 2).Last();
        Assert(adaptation.GroupsWith(adaptationSecond), "同档位同对象必须同组");

        // 不同对象的同档位记忆也不得同组。
        Pawn otherOfficer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true,
            trainerIdentityInitialized: true);
        ExecuteDaily(otherOfficer, slave, 40f, 6f);
        var otherAdaptation = MemoriesOf(slave)
            .OfType<Thought_MemoryTrainerTraining>()
            .Where(m => ReferenceEquals(m.def, SSCDefOf.SSC_TrainerTraining_OpinionDynamic)
                && m.CurStageIndex == 2)
            .Last();
        Assert(ReferenceEquals(otherAdaptation.otherPawn, otherOfficer), "对象为第二名训导官");
        // 原版分组在人物不同时会退回标签比较；社交标签会把对象短名格式化进去，
        // 因此这里断言社交标签可区分不同人物，再断言分组结果。
        Assert(adaptation.LabelCapSocial != otherAdaptation.LabelCapSocial,
            $"不同对象的社交标签必须可区分：left=[{adaptation.LabelCapSocial}]"
            + $" right=[{otherAdaptation.LabelCapSocial}]");
        Assert(adaptation.LabelCapSocial.Contains(adaptation.otherPawn.LabelShort),
            "社交标签必须包含实际执行者短名");
        Assert(!adaptation.GroupsWith(otherAdaptation), "不同对象不得同组");

        // 与主人原套的同档位记忆绝不互相分组。
        ExecuteDaily(NewPawn(PawnIdentity.Master), slave, 60f, 12f);
        var masterSocial = (Thought_MemorySocial)MemoriesWithDef(slave, SSCDefOf.SSC_Training_OpinionDynamic).Single();
        Assert(!submission.GroupsWith(masterSocial), "两套 Def 不得同组");
    }

    /// <summary>
    /// 动态社交记忆的容量语义：同档未到上限时各自新增；
    /// 超过同组上限时由原生契约移除最老一份，而不是刷新年龄或无限累计。
    /// </summary>
    private static void DynamicSocialStacking()
    {
        // 每个场景使用独立受训者，避免与前一场景的记忆累计互相干扰。
        Pawn slave = NewPawn(PawnIdentity.Slave);
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true,
            trainerIdentityInitialized: true);
        ClearMemories(slave);

        // 同一档位重复工作：未到上限时各自新增，不做同档刷新。
        ExecuteDaily(officer, slave, 40f, 6f);
        ExecuteDaily(officer, slave, 40f, 6f);
        Equal(2, MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count, "未到上限时同档各自新增");

        // 达到同组上限后继续工作：总数保持在上限。
        int limit = SSCDefOf.SSC_TrainerTraining_OpinionDynamic.stackLimit;
        for (int i = 2; i < limit; i++) ExecuteDaily(officer, slave, 40f, 6f);
        var atLimit = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(limit, atLimit.Count, "到达同组上限");
        Assert(atLimit.All(m => ReferenceEquals(m.otherPawn, officer)), "上限内全部指向实际训导官");

        // 推老全部年龄，再完成一次：最老一份被移除，总数维持在容量。
        foreach (var memory in atLimit) memory.age = 1000;
        ExecuteDaily(officer, slave, 40f, 6f);
        var afterCap = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(limit, afterCap.Count, "超过同组上限后总数维持在容量");
        Assert(afterCap.All(m => m.CurStageIndex == 2), "容量维护不改变感受档位");
        Assert(afterCap.All(m => ReferenceEquals(m.otherPawn, officer)), "容量维护不改变对象方向");
        Assert(afterCap.Count(m => m.age < 1000) == 1, "新发放的一份进入列表，最老一份被移除");

        // 主人原套与训导官弱套共存：互不删除、互不刷新。
        Pawn master = NewPawn(PawnIdentity.Master);
        ExecuteDaily(master, slave, 60f, 12f);
        var masterMood = MemoriesWithDef(slave, SSCDefOf.SSC_Training_MoodDynamic).Single();
        masterMood.age = 500;
        int weakBefore = MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count;
        ExecuteDaily(officer, slave, 40f, 6f);
        Equal(500, masterMood.age, "主人记忆年龄不被弱套刷新");
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_Training_MoodDynamic).Count == 1, "主人记忆未被删除");
        Assert(MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count == weakBefore,
            "弱套容量独立维护，不受原套影响");

        // 弱动态社交的容量与主人原套同口径。
        Equal(SSCDefOf.SSC_Training_OpinionDynamic.stackLimit, SSCDefOf.SSC_TrainerTraining_OpinionDynamic.stackLimit, "动态社交单套容量");
        Equal(SSCDefOf.SSC_Training_OpinionDynamic.stackLimitForSameOtherPawn,
            SSCDefOf.SSC_TrainerTraining_OpinionDynamic.stackLimitForSameOtherPawn, "动态社交同对象容量");
        Equal(SSCDefOf.SSC_Training_OpinionDynamic.durationDays.ToString(),
            SSCDefOf.SSC_TrainerTraining_OpinionDynamic.durationDays.ToString(), "动态社交时长");
    }

    /// <summary>人格链路：弱套的定义、阶段与整数偏移经过采集、复制与恢复后完整保留。</summary>
    private static void PersonalityRoundTrip()
    {
        Pawn slave = NewPawn(PawnIdentity.Slave);
        Pawn officer = NewPawn(PawnIdentity.Slave, trainerQualification: true, trainerEnabled: true);
        ExecuteDaily(officer, slave, 60f, 12f);
        var original = (Thought_MemorySocial)MemoriesWithDef(slave, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Single();
        original.age = 1234;

        // 采集与复制必须保存定义、阶段与实例偏移。
        StoredMemoryData captured = PersonalityMemoryUtility.Capture(original);
        StoredMemoryData copied = PersonalityMemoryUtility.Copy(captured);
        Equal(3, copied.stageIndex, "采集阶段");
        Assert(Math.Abs(copied.opinionOffset - 6f) < 0.0001f, "采集到的实际弱偏移为 6");
        Assert(copied.hasOpinionOffset, "社交记忆必须标记偏移存在");
        Equal(1234, copied.age, "采集年龄");

        // 存读档往返：字典模型只在加载阶段回填。
        Scribe_Values.Data.Clear();
        Scribe_Values.Loading = false;
        copied.ExposeData();
        var reloaded = new StoredMemoryData();
        Scribe_Values.Loading = true;
        reloaded.ExposeData();
        Scribe_Values.Loading = false;
        Equal(3, reloaded.stageIndex, "存读档后的阶段");
        Assert(Math.Abs(reloaded.opinionOffset - 6f) < 0.0001f, "存读档后的偏移");
        Assert(ReferenceEquals(reloaded.def, SSCDefOf.SSC_TrainerTraining_OpinionDynamic), "存读档后的定义");

        // 恢复到另一具身体：先按保存阶段创建，再写回实例偏移。
        Pawn host = NewPawn(PawnIdentity.Unset);
        PersonalityMemoryUtility.Restore(host, new List<StoredMemoryData> { copied });
        var restored = (Thought_MemoryTrainerTraining)MemoriesOf(host)
            .Single(m => m.def == SSCDefOf.SSC_TrainerTraining_OpinionDynamic);
        Equal(3, restored.CurStageIndex, "恢复后的阶段");
        Assert(Math.Abs(restored.opinionOffset - 6f) < 0.0001f, "恢复后的实际偏移");
        Equal(1234, restored.age, "恢复后的年龄");
        Assert(ReferenceEquals(restored.otherPawn, officer), "恢复后的对象为原训导官");
        Assert(restored.LabelCap.Contains("沉沦"), "恢复后仍按保存阶段显示");

        // 主人原套记忆与弱套同时存在时必须各自保留，不能按新身体重新分类。
        ExecuteDaily(NewPawn(PawnIdentity.Master), slave, 60f, 12f);
        var all = MemoriesOf(slave).Where(m => m.def == SSCDefOf.SSC_Training_OpinionDynamic
            || m.def == SSCDefOf.SSC_TrainerTraining_OpinionDynamic).ToList();
        Equal(2, all.Count, "两套社交记忆共存");
        var snapshot = all.Select(PersonalityMemoryUtility.Capture).ToList();
        Pawn secondHost = NewPawn(PawnIdentity.Unset);
        PersonalityMemoryUtility.Restore(secondHost, snapshot.Select(PersonalityMemoryUtility.Copy).ToList());
        Assert(MemoriesWithDef(secondHost, SSCDefOf.SSC_Training_OpinionDynamic).Count == 1, "原套恢复一份");
        Assert(MemoriesWithDef(secondHost, SSCDefOf.SSC_TrainerTraining_OpinionDynamic).Count == 1, "弱套恢复一份");
    }
}
