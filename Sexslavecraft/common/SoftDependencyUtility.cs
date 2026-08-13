using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace SexSlaveCraft
{
    public static class SoftDependencyUtility
    {
        private static readonly Type ExtendedAnimatorType = FindOptionalType("Rimworld_Animations.CompExtendedAnimator");
        private static readonly PropertyInfo IsAnimatingProperty = ExtendedAnimatorType?.GetProperty("IsAnimating", BindingFlags.Public | BindingFlags.Instance);

        public static Type FindOptionalType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return null;
        }

        public static bool IsAnimating(Pawn pawn)
        {
            if (pawn == null || ExtendedAnimatorType == null || IsAnimatingProperty == null)
                return false;

            List<ThingComp> allComps = pawn.AllComps;
            if (allComps == null)
                return false;

            for (int i = 0; i < allComps.Count; i++)
            {
                ThingComp comp = allComps[i];
                if (comp == null || !ExtendedAnimatorType.IsInstanceOfType(comp))
                    continue;

                object value = IsAnimatingProperty.GetValue(comp, null);
                if (value is bool isAnimating)
                    return isAnimating;
            }

            return false;
        }
    }
}
