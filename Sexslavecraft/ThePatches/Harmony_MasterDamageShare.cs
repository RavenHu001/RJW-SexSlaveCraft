using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

// EN: This patch redirects part of the master's incoming damage before it lands.
// EN: It asks SSCMasterBondUtility for a plan, then applies redirected hits.
// CN: 这个补丁会在主人的伤害真正落地前，先重定向其中一部分。
// CN: 它向 SSCMasterBondUtility 请求方案，然后施加分摊伤害。
namespace SexSlaveCraft
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Harmony_SSC_MasterDamageShare
    {
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            if (!SSCMasterBondUtility.CanRedirectDamageFrom(__instance, dinfo))
            {
                if (__instance?.MapHeld != null && dinfo.Def != null && SSCMasterBondUtility.ShouldBlockDamageDef(dinfo.Def))
                {
                    Log.Message("SSC_MasterShareDamage_LogBlocked".Translate(__instance.LabelShort, dinfo.Def.label).ToString());
                }
                return;
            }

            if (!SSCMasterBondUtility.TryCreateDamageRedirectPlan(__instance, dinfo, out SSCMasterBondUtility.DamageRedirectPlan plan))
            {
                Log.Message("SSC_MasterShareDamage_LogNoCandidate".Translate(__instance.LabelShort, dinfo.Def.label, dinfo.Amount.ToString("F1")).ToString());
                return;
            }

            Log.Message("SSC_MasterShareDamage_LogStart".Translate(__instance.LabelShort, dinfo.Def.label, plan.OriginalAmount.ToString("F1"), plan.RedirectAmount.ToString("F1"), plan.Candidates.Count.ToString(), SSCMasterBondUtility.CountTemporaryGelatinizationCandidates(plan.Candidates).ToString()).ToString());

            using (SSCMasterBondUtility.BeginDamageRedirectScope())
            {
                for (int i = 0; i < plan.Candidates.Count; i++)
                {
                    Pawn slave = plan.Candidates[i];
                    float share = SSCMasterBondUtility.CalculateSlaveDamageShare(slave, plan);
                    if (share <= 0f) continue;

                    DamageInfo redirected = SSCMasterBondUtility.CreateRedirectedDamageInfo(dinfo, slave, share, out BodyPartRecord part);
                    slave.TakeDamage(redirected);

                    string partLabel = part?.LabelCap ?? "SSC_MasterShareDamage_PartFallback".Translate().ToString();
                    Log.Message("SSC_MasterShareDamage_Log".Translate(__instance.LabelShort, slave.LabelShort, share.ToString("F1"), partLabel).ToString());
                    if (slave.Spawned)
                    {
                        MoteMaker.ThrowText(slave.DrawPos, slave.Map, "SSC_MasterShareDamage_Float".Translate(share.ToString("F1")).ToString(), 3f);
                    }
                }
            }

            dinfo.SetAmount(plan.RemainingAmount);

            Log.Message("SSC_MasterShareDamage_LogRemain".Translate(__instance.LabelShort, plan.RemainingAmount.ToString("F1")).ToString());

            if (__instance.Spawned)
            {
                Messages.Message("SSC_MasterShareDamage_Message".Translate(__instance.LabelShort, plan.RedirectAmount.ToString("F1"), plan.Candidates[0].LabelShort), __instance, MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
