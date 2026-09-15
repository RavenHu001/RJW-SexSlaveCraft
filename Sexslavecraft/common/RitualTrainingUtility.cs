using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using rjw;
using Verse;

// EN: This file provides ritual-specific training helpers.
// EN: It maps ritual phases to sex props and handles fallback animation startup.
// CN: 这个文件提供仪式专用的调教辅助逻辑。
// CN: 它负责把仪式阶段映射到 sex props，并处理动画兜底启动。
namespace SexSlaveCraft
{
    public static class RitualTrainingUtility
    {
        private static readonly Type AnimationUtilityType = FindOptionalType("Rimworld_Animations.AnimationUtility");
        private static readonly Type GroupAnimationDefType = FindOptionalType("Rimworld_Animations.GroupAnimationDef");
        private static readonly Type ExtendedAnimatorType = FindOptionalType("Rimworld_Animations.CompExtendedAnimator");
        private static readonly PropertyInfo IsAnimatingProperty = ExtendedAnimatorType?.GetProperty("IsAnimating", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo StartGroupAnimationMethod = FindStartGroupAnimationMethod();
        private static readonly MethodInfo GetAnimationLengthMethod = FindGetAnimationLengthMethod();

        public static void ApplyPhaseToSexProps(SexProps sexProps, Pawn initiator, Pawn slave, int phaseIndex)
        {
            if (sexProps == null || initiator == null || slave == null) return;

            (xxx.rjwSextype sexType, string interactionDefName) phaseData = GetPhaseData(phaseIndex);
            RJWSexPropsUtility.ApplySexType(sexProps, initiator, slave, phaseData.sexType, phaseData.interactionDefName);
            Log.Message($"[SSC Ritual] 仪式进入阶段 {phaseIndex + 1}，当前体位已切换为：{phaseData.interactionDefName}");
        }

        /// <summary>从动画框架的定义库筛选可用动画并备用启动，成功后回传动画时长；框架缺省或无匹配项时返回 false。</summary>
        public static bool TryStartFallbackAnimation(Pawn initiator, Pawn slave, Building bed, Action<int> applyAnimationTicks)
        {
            try
            {
                if (AnimationUtilityType == null || GroupAnimationDefType == null || StartGroupAnimationMethod == null || GetAnimationLengthMethod == null)
                {
                    SSCLog.Verbose("[SSC Ritual] Manual animation fallback unavailable: Rimworld_Animations API not found.");
                    return false;
                }

                JobDriver_SexBaseReciever receiverDriver = slave?.jobs?.curDriver as JobDriver_SexBaseReciever;
                if (receiverDriver == null)
                {
                    SSCLog.WarningImportant("[SSC Ritual] Manual: slave is not in a SexBaseReciever job!");
                    return false;
                }

                List<Pawn> participants = receiverDriver.parteners.ToList();
                if (!participants.Contains(slave))
                {
                    participants.Add(slave);
                }

                SSCLog.Verbose($"[SSC Ritual] Manual: participants count = {participants.Count}");

                List<(Def def, List<Pawn> order, int priority)> animations = new List<(Def, List<Pawn>, int)>();
                // 定义库按泛型类型独立存储；使用运行时类型读取动画库，同时保留框架的可选依赖。
                foreach (Def animationDef in GenDefDatabase.GetAllDefsInDatabaseForDef(GroupAnimationDefType))
                {
                    if (!GroupAnimationDefType.IsInstanceOfType(animationDef)) continue;
                    if (TryCanAnimationBeUsed(animationDef, participants, out List<Pawn> order, out int priority))
                    {
                        animations.Add((animationDef, order, priority));
                    }
                }

                SSCLog.Verbose($"[SSC Ritual] Manual: found {animations.Count} matching animations");
                if (animations.Count == 0)
                {
                    SSCLog.WarningImportant("[SSC Ritual] Manual: no matching animations found for these pawns!");
                    return false;
                }

                // Group animation priority is unreliable here, so use the filtered set and pick one directly.
                (Def def, List<Pawn> order, int priority) selected = animations.RandomElement();
                SSCLog.Verbose($"[SSC Ritual] Manual: selected {selected.def.defName}");

                StartGroupAnimationMethod.Invoke(null, new object[] { selected.order, selected.def, (Thing)bed ?? slave });

                object tickResult = GetAnimationLengthMethod.Invoke(null, new object[] { initiator });
                int animationTicks = tickResult is int ticks ? ticks : 0;
                SSCLog.Verbose($"[SSC Ritual] Manual: animation started, length = {animationTicks}");
                applyAnimationTicks?.Invoke(animationTicks);

                SSCLog.Verbose($"[SSC Ritual] Manual: pawn IsAnimating = {IsAnimating(initiator)}");
                SSCLog.Verbose($"[SSC Ritual] Manual: slave IsAnimating = {IsAnimating(slave)}");
                return true;
            }
            catch (Exception ex)
            {
                SSCLog.Error($"[SSC Ritual] Manual animation start failed: {ex}");
                return false;
            }
        }

        private static Type FindOptionalType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryCanAnimationBeUsed(Def animationDef, List<Pawn> participants, out List<Pawn> order, out int priority)
        {
            order = null;
            priority = 0;

            if (animationDef == null) return false;

            MethodInfo method = FindCanAnimationBeUsedMethod(animationDef.GetType());
            if (method == null) return false;

            object[] args = { participants, null, 0 };
            object result = method.Invoke(animationDef, args);
            if (!(result is bool canUse) || !canUse) return false;

            order = args[1] as List<Pawn>;
            if (args[2] is int resolvedPriority)
            {
                priority = resolvedPriority;
            }

            return order != null;
        }

        public static bool IsAnimating(Pawn pawn)
        {
            if (pawn == null || ExtendedAnimatorType == null || IsAnimatingProperty == null) return false;

            IEnumerable<ThingComp> allComps = pawn.AllComps;
            if (allComps == null) return false;

            foreach (ThingComp comp in allComps)
            {
                if (comp == null || !ExtendedAnimatorType.IsInstanceOfType(comp)) continue;

                object value = IsAnimatingProperty.GetValue(comp, null);
                if (value is bool isAnimating)
                {
                    return isAnimating;
                }
            }

            return false;
        }

        private static (xxx.rjwSextype sexType, string interactionDefName) GetPhaseData(int phaseIndex)
        {
            switch (phaseIndex)
            {
                case 0: return (xxx.rjwSextype.Handjob, "Sex_Handjob");
                case 1: return (xxx.rjwSextype.Footjob, "Sex_Footjob");
                case 2: return (xxx.rjwSextype.Sixtynine, "Sex_Sixtynine");
                case 3: return (xxx.rjwSextype.Boobjob, "Sex_Breastjob");
                case 4: return (xxx.rjwSextype.Anal, "Sex_Anal");
                default: return (xxx.rjwSextype.Vaginal, "Sex_Vaginal");
            }
        }

        private static MethodInfo FindStartGroupAnimationMethod()
        {
            if (AnimationUtilityType == null || GroupAnimationDefType == null) return null;

            Type listPawnType = typeof(List<Pawn>);
            return AnimationUtilityType.GetMethod(
                "StartGroupAnimation",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { listPawnType, GroupAnimationDefType, typeof(Thing) },
                null);
        }

        private static MethodInfo FindGetAnimationLengthMethod()
        {
            if (AnimationUtilityType == null) return null;

            return AnimationUtilityType.GetMethod(
                "GetAnimationLength",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Pawn) },
                null);
        }

        private static MethodInfo FindCanAnimationBeUsedMethod(Type animationDefType)
        {
            if (animationDefType == null) return null;

            Type byRefListPawnType = typeof(List<Pawn>).MakeByRefType();
            Type byRefIntType = typeof(int).MakeByRefType();
            return animationDefType.GetMethod(
                "CanAnimationBeUsed",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(List<Pawn>), byRefListPawnType, byRefIntType },
                null);
        }
    }
}
