using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) })]
    internal static class SSCBondOwnerDeath
    {
        internal static void Prefix(Pawn __instance, out (SSCBondUtility.ReleaseContext[] Pending, bool Notify) __state)
        {
            __state = default;
            if (__instance.Dead || Scribe.mode != LoadSaveMode.Inactive) return;
            var bridle = SSCBondUtility.GetBridle(__instance);
            if (bridle == null) return;
            // 死亡过程可调整健康、容器和旅队归属。提前保存候选和玩家通知资格，
            // 由提交入口复核每个从属者的实际锁链和人格。
            var pending = bridle.ValidTargets.Distinct().Select(pawn => new SSCBondUtility.ReleaseContext(pawn)).ToArray();
            if (pending.Length > 0) __state = (pending, PawnUtility.ShouldSendNotificationAbout(__instance));
        }

        internal static void Postfix(Pawn __instance, (SSCBondUtility.ReleaseContext[] Pending, bool Notify) __state)
        {
            if (!__instance.Dead || __state.Pending == null) return;
            var released = new List<Pawn>();
            foreach (var context in __state.Pending)
            {
                if (context.Master != __instance) continue;
                try
                {
                    if (SSCBondUtility.TryReleaseUnavailableOwner(context)) released.Add(context.Subject);
                }
                catch (Exception error)
                {
                    Log.Error($"[SSC] Could not release {context.Subject} after the death of {__instance}: {error}");
                }
            }

            if (released.Count > 0 && __state.Notify)
                Messages.Message("SSC_BondRelease_DeathNotice".Translate(__instance.LabelShort,
                    string.Join(", ", released.Select(pawn => pawn.LabelShort.ToString()))), new LookTargets(released.Cast<Thing>()),
                    MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
