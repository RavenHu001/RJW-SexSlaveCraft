// AI 辅助生成：命中后的定时矛尖泛光，不改变原版动画、伤害或药效。
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCPnaSpearGlowUtility
    {
        internal const string WeaponDefName = "SSC_Weapon_PnaSpikeStaff";
        // 按 60 tick/s：满亮 0.3 秒，再淡出 0.5 秒。允许原版收回后继续跟随矛尖。
        internal const int HoldTicks = 18;
        internal const int FadeTicks = 30;
        internal const int LifetimeTicks = HoldTicks + FadeTicks;
        private static readonly AccessTools.FieldRef<Pawn_DrawTracker, JitterHandler> Jitter =
            AccessTools.FieldRefAccess<Pawn_DrawTracker, JitterHandler>("jitterer");

        private sealed class Hit
        {
            internal System.WeakReference<Thing> Weapon;
            internal System.WeakReference<Pawn> Owner;
            internal System.WeakReference<JitterHandler> Handler;
            internal System.WeakReference<Map> Map;
            internal int RemainingTicks;
        }
        private static readonly ConditionalWeakTable<Thing, Hit> ByWeapon = new ConditionalWeakTable<Thing, Hit>();
        private static readonly ConditionalWeakTable<JitterHandler, Hit> ByHandler = new ConditionalWeakTable<JitterHandler, Hit>();

        /// <summary>每次真实出手先结束上一份反馈；刺空不继承先前命中的亮度。</summary>
        internal static void Clear(Thing weapon)
        {
            if (weapon != null && ByWeapon.TryGetValue(weapon, out Hit hit)) Remove(hit);
        }

        /// <summary>仅专用刺击 Verb 在父类返回命中后调用；完全挡甲仍算接触，不检查施药结果。</summary>
        internal static void Begin(Thing weapon, Pawn pawn)
        {
            Clear(weapon);
            if (Scribe.mode != LoadSaveMode.Inactive || weapon?.def?.defName != WeaponDefName ||
                pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Destroyed || pawn.Drawer == null ||
                weapon.Destroyed || pawn.equipment?.Primary != weapon) return;
            JitterHandler handler = Jitter(pawn.Drawer);
            if (handler == null) return;
            if (ByHandler.TryGetValue(handler, out Hit previous)) Remove(previous);
            var hit = new Hit { Weapon = new System.WeakReference<Thing>(weapon), Owner = new System.WeakReference<Pawn>(pawn),
                Handler = new System.WeakReference<JitterHandler>(handler), Map = new System.WeakReference<Map>(pawn.Map),
                RemainingTicks = LifetimeTicks };
            ByWeapon.Add(weapon, hit);
            ByHandler.Add(handler, hit);
        }

        /// <summary>复用原版视觉 tick 时钟；回位结束后仍会调用，暂停和加速继续遵守游戏时间。</summary>
        internal static void Advance(JitterHandler handler, int ticksPassed)
        {
            if (handler == null || ticksPassed <= 0 || !ByHandler.TryGetValue(handler, out Hit hit)) return;
            hit.RemainingTicks = Math.Max(0, hit.RemainingTicks - ticksPassed);
            if (hit.RemainingTicks == 0) Remove(hit);
        }

        internal static float GetAlpha(Thing weapon)
        {
            if (weapon == null || !ByWeapon.TryGetValue(weapon, out Hit hit)) return 0f;
            if (Scribe.mode != LoadSaveMode.Inactive || weapon.Destroyed ||
                !hit.Owner.TryGetTarget(out Pawn pawn) || pawn.Destroyed || pawn.Dead || !pawn.Spawned ||
                pawn.equipment?.Primary != weapon || !hit.Handler.TryGetTarget(out _) ||
                !hit.Map.TryGetTarget(out Map map) || pawn.Map != map)
            { Remove(hit); return 0f; }
            float alpha = Alpha(hit);
            if (alpha <= 0f) Remove(hit);
            return alpha;
        }

        private static float Alpha(Hit hit)
        {
            // 只由本次命中后的剩余时间决定；身体偏移、提前回位、额外受击均不缩短或延长。
            return Math.Max(0f, Math.Min(1f, hit.RemainingTicks / (float)FadeTicks));
        }

        private static void Remove(Hit hit)
        {
            if (hit.Weapon.TryGetTarget(out Thing weapon) && ByWeapon.TryGetValue(weapon, out Hit current) && current == hit)
                ByWeapon.Remove(weapon);
            if (hit.Handler.TryGetTarget(out JitterHandler handler) && ByHandler.TryGetValue(handler, out current) && current == hit)
                ByHandler.Remove(handler);
        }

    }
}
