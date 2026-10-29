// AI 辅助生成
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    /// <summary>只读真实课堂配对；学生自己的印记与学习属性仍由原LearningUtility计算。</summary>
    public static class FertilityCareUtility
    {
        /// <summary>核对双方任务、同一课桌和准备状态，等待与其他学习活动不取得教师增益。</summary>
        public static Pawn LessonTeacher(Pawn student)
        {
            if (!ModsConfig.BiotechActive || student?.CurJobDef != JobDefOf.Lessontaking ||
                student.jobs.curDriver is not JobDriver_Lessontaking taking || !taking.isReadyToLearn) return null;
            Pawn teacher = student.CurJob.GetTarget(TargetIndex.B).Pawn;
            if (teacher?.CurJobDef != JobDefOf.Lessongiving || teacher.jobs.curDriver is not JobDriver_Lessongiving giving ||
                !giving.isReadyToTeach || giving.taughtSkill == null || teacher.CurJob.GetTarget(TargetIndex.B).Pawn != student ||
                teacher.CurJob.GetTarget(TargetIndex.A) != student.CurJob.GetTarget(TargetIndex.A) ||
                teacher.Faction == null || teacher.Faction != student.Faction) return null;
            return teacher;
        }

        /// <summary>在本轮原活动回调后复查原人格，不把学生回调另算一份教师时间。</summary>
        public static IEnumerable<Toil> WrapLessons(IEnumerable<Toil> source, JobDriver_Lessongiving driver)
        {
            foreach (var toil in source)
            {
                // 取证的实际授课Toil是Never且技能为Social；移动、等待Toil保持原样。
                if (toil.defaultCompleteMode == ToilCompleteMode.Never && toil.activeSkill?.Invoke() == SkillDefOf.Social && toil.tickIntervalAction != null)
                {
                    var original = toil.tickIntervalAction;
                    toil.tickIntervalAction = delta =>
                    {
                        Pawn teacher = driver.pawn;
                        var comp = teacher.TryGetComp<CompSexSlaveTraining>();
                        object token = SSCPersonalityRuntimeUtility.Capture(teacher);
                        Job job = teacher.CurJob;
                        Pawn student = job?.GetTarget(TargetIndex.B).Pawn;
                        // 原回调在首Tick选定授课技能；完成后核对是否仍在实际课堂。
                        // 培养只要求正在上课，不以学生技能经验是否增长作为门槛。
                        original(delta);
                        if (teacher.CurJob == job && LessonTeacher(student) == teacher)
                            comp?.GainFertilityCare(delta, token);
                    };
                }
                yield return toil;
            }
        }
    }

    [HarmonyPatch(typeof(LearningUtility), nameof(LearningUtility.LearningRateFactor))]
    internal static class Harmony_FertilityLessonRate
    {
        /// <summary>同一实际倍率由学生需求及课堂技能经验消费者读取，不重复乘到老师自身属性上。</summary>
        public static void Postfix(Pawn pawn, ref float __result)
        {
            Pawn teacher = FertilityCareUtility.LessonTeacher(pawn);
            if (teacher != null) __result *= FertilitySpecializationUtility.CareFactor(teacher);
        }
    }
    [HarmonyPatch(typeof(JobDriver_Lessongiving), "MakeNewToils")]
    internal static class Harmony_FertilityLessonTime
    {
        /// <summary>惰性包装原Toil序列，保留原授课与任务失败规则。</summary>
        public static void Postfix(JobDriver_Lessongiving __instance, ref IEnumerable<Toil> __result)
            => __result = FertilityCareUtility.WrapLessons(__result, __instance);
    }
    [HarmonyPatch(typeof(BabyPlayUtility), nameof(BabyPlayUtility.PlayTickCheckEnd))]
    internal static class Harmony_FertilityBabyPlay
    {
        internal sealed class Context
        {
            internal CompSexSlaveTraining Comp;
            internal object Token;
            internal Need_Play Need;
            internal float Before;
        }
        /// <summary>只增强己方婴儿实际玩耍恢复，不改变成人娱乐收益或设施属性。</summary>
        public static void Prefix(Pawn baby, Pawn adult, ref float playFactor, int delta, out Context __state)
        {
            __state = null;
            if (!ModsConfig.BiotechActive || baby?.needs?.play == null || adult?.Faction == null || baby.Faction != adult.Faction ||
                delta <= 0 || !float.IsFinite(playFactor) || playFactor <= 0f || !FertilitySpecializationUtility.MeetsConditions(adult, out _)) return;
            __state = new Context { Comp = adult.TryGetComp<CompSexSlaveTraining>(), Token = SSCPersonalityRuntimeUtility.Capture(adult),
                Need = baby.needs.play, Before = baby.needs.play.CurLevel };
            playFactor *= FertilitySpecializationUtility.CareFactor(adult);
        }
        /// <summary>缺需求或已满且未恢复时不计时；原回调内换人格也不能给新人格补领。</summary>
        public static void Postfix(Pawn baby, int delta, Context __state)
        {
            if (__state != null && ReferenceEquals(baby?.needs?.play, __state.Need) && __state.Need.CurLevel > __state.Before)
                __state.Comp?.GainFertilityCare(delta, __state.Token);
        }
    }
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.Notify_HediffChanged))]
    internal static class Harmony_FertilityBodyChange
    {
        /// <summary>子宫移除、完成或空壳变化立即刷新；重入由协调器阻止。</summary>
        public static void Postfix(Hediff hediff)
        {
            if (hediff?.def?.defName == SSCUterusDevelopmentUtility.DefName || hediff?.def == SSCDefOf.SSC_PersonalityExcreted_Done)
                FertilitySpecializationUtility.Sync(hediff?.pawn);
        }
    }
}
