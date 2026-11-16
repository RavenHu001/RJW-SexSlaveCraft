using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>记录实际完成定型或取得定型印记的业务入口，供发信时选择相应叙述。</summary>
    internal enum SSCImprintAcquisition { Seed, Binding, FullCorruption, Implantation, Copy }

    /// <summary>将已提交的资格和印记结果送入信件栏。授予资格、写入印记及建立绑定由各调用方完成。</summary>
    internal static class SSCImprintLetters
    {
        // 按本次挂载的 Hediff 实例去重。同一实例由多个完成回调报告时只认领一次，同一身体成功植入另一份人格则会获得新实例。
        // 弱键只在本次运行中追踪实际发信对象。印记移除后可回收，读档也不会通过这张表重放历史信件。
        private static readonly ConditionalWeakTable<Hediff, object> notified = new();
        private static readonly object notifiedMarker = new();

        /// <summary>由首次获得永久主人资格的用种成功出口调用。研究提示只随本次实际补齐的蓝图一起发送。</summary>
        internal static void SendQualification(Pawn pawn, bool researchUnlocked)
        {
            if (GetLetterStack() is not LetterStack stack || pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded) return;
            Send(stack, pawn, "Qualification", researchUnlocked);
        }

        /// <summary>为仍挂在原目标身上的定型印记发送取得信件。</summary>
        /// <param name="pawn">取得印记的小人，也是信件定位目标。</param>
        /// <param name="imprint">业务提交时取得的实例。若后续回调移除或替换它，旧结果失效。</param>
        /// <param name="source">实际取得途径。它与印记形态共同决定标题和正文。</param>
        internal static void SendAcquired(Pawn pawn, Hediff imprint, SSCImprintAcquisition source)
        {
            if (imprint == null || pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded ||
                imprint.pawn != pawn || SSCImprintUtility.Find(pawn) != imprint ||
                GetLetterStack() is not LetterStack stack) return;

            // 种子与绑定各有两种定型方向。满值、植入和复制只为淫欲之奴发信，初始态没有取得定型结果。
            string key = (source, SSCImprintUtility.Kind(imprint.def)) switch
            {
                (SSCImprintAcquisition.Seed, SSCImprintKind.Master) => "SeedMaster",
                (SSCImprintAcquisition.Seed, SSCImprintKind.Slave) => "SeedSlave",
                (SSCImprintAcquisition.Binding, SSCImprintKind.Master) => "BindingMaster",
                (SSCImprintAcquisition.Binding, SSCImprintKind.Slave) => "BindingSlave",
                (SSCImprintAcquisition.FullCorruption, SSCImprintKind.Slave) => "FullCorruption",
                (SSCImprintAcquisition.Implantation, SSCImprintKind.Slave) => "ImplantedSlave",
                (SSCImprintAcquisition.Copy, SSCImprintKind.Slave) => "CopiedSlave",
                _ => null
            };
            if (key == null || notified.TryGetValue(imprint, out _)) return;

            // 原版投递或外部投递回调可能重入同一入口，先认领实例再发信。
            notified.Add(imprint, notifiedMarker);
            Send(stack, pawn, key);
        }

        /// <summary>由已完成的公开复制入口调用，并核对新个体仍在地图上、继承的印记仍然有效。</summary>
        internal static void SendVisibleCopy(Pawn copy, Hediff inheritedImprint)
        {
            if (copy?.Spawned == true) SendAcquired(copy, inheritedImprint, SSCImprintAcquisition.Copy);
        }

        // 正常游戏结算才有信件投递目标。反序列化可以恢复印记和已保存的信件。
        private static LetterStack GetLetterStack()
            => Scribe.mode == LoadSaveMode.Inactive && Current.Game != null ? Find.LetterStack : null;

        private static void Send(LetterStack stack, Pawn pawn, string key, bool researchUnlocked = false)
        {
            try
            {
                // 小人名称用于正文，原版信件的定位目标使用小人本身。
                TaggedString text = ("SSC_Imprint_" + key + "LetterText").Translate(pawn.LabelShort);
                if (researchUnlocked) text += "\n\n" + "SSC_Imprint_QualificationResearch".Translate();
                stack.ReceiveLetter(("SSC_Imprint_" + key + "LetterLabel").Translate(), text, LetterDefOf.NeutralEvent, pawn);
            }
            catch (Exception ex)
            {
                // 调用方的取得结果已落地。投递异常只记日志，避免上层当作结算失败并重试业务动作。
                Log.Warning($"[SSC] Could not send the imprint event letter for {pawn}: {ex}");
            }
        }
    }

    /// <summary>只接入原版公开复制的成功出口。通用复制也用于异常尸体和其他尚未公开的对象。</summary>
    [HarmonyPatch(typeof(AnomalyUtility), nameof(AnomalyUtility.TryDuplicatePawn))]
    internal static class Harmony_SSCImprintVisibleDuplicate
    {
        // 在原版生成及阵营回调之前记录来源方向，完成后再根据复制体实际保留的印记发信。
        private static void Prefix(Pawn originalPawn, out bool __state)
            => __state = SSCImprintUtility.GetKind(originalPawn) == SSCImprintKind.Slave;

        private static void Postfix(bool __result, Pawn duplicatePawn, bool __state)
        {
            if (__result && __state)
                SSCImprintLetters.SendVisibleCopy(duplicatePawn, SSCImprintUtility.Find(duplicatePawn));
        }
    }

    /// <summary>在异常尸体首次觉醒并公开出现时宣布印记继承。</summary>
    [HarmonyPatch(typeof(UnnaturalCorpseTracker), "AwakeningTick")]
    internal static class Harmony_SSCImprintAwokenDuplicate
    {
        // 只接受本次调用中从未觉醒到已觉醒的变化。提前预告、读档后的已觉醒状态和之后替换追猎身体都没有这一变化。
        private static void Prefix(bool ___awoken, out bool __state) => __state = ___awoken;

        private static void Postfix(bool ___awoken, Pawn ___awokenPawn, bool __state)
        {
            if (!__state && ___awoken)
                SSCImprintLetters.SendVisibleCopy(___awokenPawn, SSCImprintUtility.Find(___awokenPawn));
        }
    }
}
