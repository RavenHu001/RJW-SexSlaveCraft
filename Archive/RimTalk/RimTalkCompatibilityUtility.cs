// Archived 2026-09-15: excluded from the runtime build pending a complete redesign.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

// EN: This file bridges SSC training jobs to RimTalk without taking a hard assembly dependency.
// CN: 这个文件通过软依赖把 SSC 调教 Job 接入 RimTalk，不会强制玩家安装 RimTalk。
namespace SexSlaveCraft
{
    public static class RimTalkCompatibilityUtility
    {
        private sealed class TrainingDialogueReservation
        {
            public Pawn master;
            public Pawn slave;
            public string bridgeText;
            public string sexType;
            public Guid bridgeResponseId;
            public int createdTick;
            public bool sceneStarted;
            public bool requestStarted;
        }

        private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private const int ReservationTimeoutTicks = 5000;
        private static readonly List<TrainingDialogueReservation> TrainingReservations = new List<TrainingDialogueReservation>();

        private static Type cacheType;
        private static Type pawnStateType;
        private static Type pawnSelectorType;
        private static Type talkResponseType;
        private static Type talkRequestType;
        private static Type talkTypeType;
        private static Type talkServiceType;
        private static Type aiServiceType;
        private static Type commonUtilType;
        private static Type apiHistoryType;
        private static Type talkHistoryType;
        private static Type overlayType;

        private static MethodInfo cacheGetMethod;
        private static MethodInfo cacheGetAllMethod;
        private static MethodInfo addTalkRequestMethod;
        private static MethodInfo getNextTalkRequestMethod;
        private static MethodInfo markRequestSpokenMethod;
        private static MethodInfo ignoreAllTalkResponsesMethod;
        private static MethodInfo generateTalkMethod;
        private static MethodInfo isBusyMethod;
        private static MethodInfo shouldAiBeActiveOnSpeedMethod;
        private static MethodInfo addUserHistoryMethod;
        private static MethodInfo addIgnoredMethod;
        private static MethodInfo overlayNotifyUpdatedMethod;
        private static FieldInfo pawnStatePawnField;
        private static FieldInfo talkResponsesField;
        private static FieldInfo talkRequestsField;

        private static object eventTalkType;
        private static object userTalkType;
        private static bool initialized;
        private static bool failureLogged;
        private static bool patchesInstalled;
        private static bool bypassReservationBlock;
        private static bool reservedGenerationInFlight;

        public static void TryInstallPatches(Harmony harmony)
        {
            if (harmony == null || patchesInstalled || !TryInitialize())
                return;

            try
            {
                MethodInfo canGenerateTalk = AccessTools.Method(pawnStateType, "CanGenerateTalk");
                if (canGenerateTalk != null)
                {
                    harmony.Patch(
                        canGenerateTalk,
                        postfix: new HarmonyMethod(
                            typeof(RimTalkCompatibilityUtility),
                            nameof(CanGenerateTalkPostfix)));
                }

                MethodInfo generateTalk = AccessTools.Method(talkServiceType, "GenerateTalk");
                if (generateTalk != null)
                {
                    harmony.Patch(
                        generateTalk,
                        prefix: new HarmonyMethod(
                            typeof(RimTalkCompatibilityUtility),
                            nameof(GenerateTalkPrefix)));
                }

                MethodInfo getNearbyPawns = AccessTools.Method(pawnSelectorType, "GetAllNearByPawns");
                if (getNearbyPawns != null)
                {
                    harmony.Patch(
                        getNearbyPawns,
                        postfix: new HarmonyMethod(
                            typeof(RimTalkCompatibilityUtility),
                            nameof(GetAllNearByPawnsPostfix)));
                }

                MethodInfo selectNextPawn = AccessTools.Method(pawnSelectorType, "SelectNextAvailablePawn");
                if (selectNextPawn != null)
                {
                    harmony.Patch(
                        selectNextPawn,
                        postfix: new HarmonyMethod(
                            typeof(RimTalkCompatibilityUtility),
                            nameof(SelectNextAvailablePawnPostfix)));
                }

                patchesInstalled = true;
                SSCLog.Important("[SSC RimTalk] Training reservation patches installed.");
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }
        }

        public static void ReserveTrainingJob(Pawn master, Pawn slave)
        {
            if (!CompatibilityEnabled || master == null || slave == null || !TryInitialize())
                return;

            TrainingDialogueReservation existing = FindReservation(master, slave);
            if (existing != null)
                return;

            try
            {
                TrainingDialogueReservation reservation = new TrainingDialogueReservation
                {
                    master = master,
                    slave = slave,
                    bridgeText = BuildInterruptText(master, slave),
                    sexType = slave.TryGetComp<CompSexSlaveTraining>()?.selectedMode.ToString() ?? "Auto",
                    createdTick = GenTicks.TicksGame
                };

                // EN: The scheduled scene owns the next lines for these pawns. Discard every old queued response,
                // then insert one deterministic interruption line without spending an extra AI request.
                // CN: 排班场景接管双方接下来的台词。先丢弃旧回复，再插入一条不消耗额外 AI 请求的固定打断句。
                IgnoreAllPendingResponses(Guid.Empty);
                ClearPawnRequests(master);
                ClearPawnRequests(slave);
                reservation.bridgeResponseId = AddBridgeResponse(reservation);
                TrainingReservations.Add(reservation);

                SSCLog.Important(
                    $"[SSC RimTalk] Training dialogue reserved: master={master.LabelShort}, " +
                    $"slave={slave.LabelShort}, bridge=\"{reservation.bridgeText}\"");
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }
        }

        public static void NotifyTrainingSceneStarted(Pawn master, Pawn slave, string sexType)
        {
            if (!CompatibilityEnabled || master == null || slave == null || !TryInitialize())
                return;

            TrainingDialogueReservation reservation = FindReservation(master, slave);
            if (reservation == null)
            {
                ReserveTrainingJob(master, slave);
                reservation = FindReservation(master, slave);
            }
            if (reservation == null)
                return;

            reservation.sceneStarted = true;
            reservation.sexType = string.IsNullOrEmpty(sexType) ? reservation.sexType : sexType;
            TryStartReservedDialogue(reservation);
        }

        public static void ReleaseTrainingJob(Pawn master, Pawn slave)
        {
            for (int i = TrainingReservations.Count - 1; i >= 0; i--)
            {
                TrainingDialogueReservation reservation = TrainingReservations[i];
                if ((reservation.master == master && reservation.slave == slave) ||
                    reservation.master == null ||
                    reservation.slave == null)
                {
                    TrainingReservations.RemoveAt(i);
                }
            }
        }

        // EN: Non-scheduled SSC scenes keep the lightweight direct event path.
        // CN: 非排班类 SSC 场景继续使用轻量的直接事件路径。
        public static void NotifySexStarted(Pawn initiator, Pawn recipient, string sceneName, string sexType)
        {
            if (!CompatibilityEnabled || initiator == null || recipient == null || !TryInitialize())
                return;

            string prompt =
                $"[SexSlaveCraft scene started] Scene: {sceneName ?? "SSC sex scene"}. " +
                $"Participants: {initiator.LabelShort} and {recipient.LabelShort}. " +
                $"Sex type: {sexType ?? "unspecified"}. Generate an immediate multi-turn, " +
                "in-character exchange between these two pawns that directly reacts to the scene.";
            TryGenerateEvent(initiator, recipient, prompt, false);
        }

        public static void Tick()
        {
            if (reservedGenerationInFlight && !IsRimTalkBusy())
                reservedGenerationInFlight = false;

            if (TrainingReservations.Count == 0 || GenTicks.TicksGame % 15 != 0)
                return;

            for (int i = TrainingReservations.Count - 1; i >= 0; i--)
            {
                TrainingDialogueReservation reservation = TrainingReservations[i];
                if (!ReservationStillValid(reservation))
                {
                    TrainingReservations.RemoveAt(i);
                    continue;
                }

                if (!reservation.requestStarted && IsRimTalkBusy() &&
                    !reservedGenerationInFlight && !AnyReservedRequestStarted())
                {
                    IgnoreAllPendingResponses(reservation.bridgeResponseId);
                }

                if (reservation.sceneStarted)
                {
                    TryStartReservedDialogue(reservation);
                }
            }
        }

        public static void CanGenerateTalkPostfix(object __instance, ref bool __result)
        {
            if (!__result || bypassReservationBlock || __instance == null || pawnStatePawnField == null)
                return;

            Pawn pawn = pawnStatePawnField.GetValue(__instance) as Pawn;
            if (IsReservedPawn(pawn))
                __result = false;
        }

        public static bool GenerateTalkPrefix(object talkRequest, ref bool __result)
        {
            if (bypassReservationBlock || talkRequest == null)
                return true;

            Pawn initiator = GetMember(talkRequest, "Initiator") as Pawn;
            Pawn recipient = GetMember(talkRequest, "Recipient") as Pawn;
            if (!IsReservedPawn(initiator) && !IsReservedPawn(recipient))
                return true;

            object initiatorState = GetPawnState(initiator);
            try { markRequestSpokenMethod?.Invoke(initiatorState, new[] { talkRequest }); }
            catch { }
            __result = false;
            return false;
        }

        public static void GetAllNearByPawnsPostfix(Pawn pawn1, Pawn pawn2, ref List<Pawn> __result)
        {
            if (bypassReservationBlock || __result == null || IsReservedPawn(pawn1))
                return;

            __result.RemoveAll(IsReservedPawn);
        }

        public static void SelectNextAvailablePawnPostfix(ref Pawn __result)
        {
            // EN: RimTalk has one global AI channel. While a claimed training job is approaching its scene,
            // reserve the next generation slot instead of letting an unrelated random conversation take it.
            // CN: RimTalk 只有一个全局 AI 通道。调教 Job 已领取但尚未开场时，预留下一次生成槽，
            // 避免无关的随机对话抢先占用它。
            if (!bypassReservationBlock && TrainingReservations.Count > 0)
                __result = null;
        }

        private static bool CompatibilityEnabled =>
            SSCMod.settings == null || SSCMod.settings.enableRimTalkSexDialogue;

        private static bool TryInitialize()
        {
            if (initialized)
                return cacheGetMethod != null && talkTypeType != null;

            initialized = true;
            try
            {
                cacheType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.Cache");
                pawnStateType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.PawnState");
                pawnSelectorType = SoftDependencyUtility.FindOptionalType("RimTalk.Service.PawnSelector");
                talkResponseType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.TalkResponse");
                talkRequestType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.TalkRequest");
                talkTypeType = SoftDependencyUtility.FindOptionalType("RimTalk.Source.Data.TalkType");
                talkServiceType = SoftDependencyUtility.FindOptionalType("RimTalk.Service.TalkService");
                aiServiceType = SoftDependencyUtility.FindOptionalType("RimTalk.Service.AIService");
                commonUtilType = SoftDependencyUtility.FindOptionalType("RimTalk.Util.CommonUtil");
                apiHistoryType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.ApiHistory");
                talkHistoryType = SoftDependencyUtility.FindOptionalType("RimTalk.Data.TalkHistory");
                overlayType = SoftDependencyUtility.FindOptionalType("RimTalk.UI.Overlay");

                if (cacheType == null || pawnStateType == null || talkResponseType == null ||
                    talkRequestType == null || talkTypeType == null || talkServiceType == null)
                {
                    return false;
                }

                cacheGetMethod = cacheType.GetMethod("Get", PublicStatic, null, new[] { typeof(Pawn) }, null);
                cacheGetAllMethod = cacheType.GetMethod("GetAll", PublicStatic);
                addTalkRequestMethod = pawnStateType.GetMethod("AddTalkRequest", PublicInstance);
                getNextTalkRequestMethod = pawnStateType.GetMethod("GetNextTalkRequest", PublicInstance);
                markRequestSpokenMethod = pawnStateType.GetMethod("MarkRequestSpoken", PublicInstance);
                ignoreAllTalkResponsesMethod = pawnStateType.GetMethod("IgnoreAllTalkResponses", PublicInstance);
                generateTalkMethod = talkServiceType.GetMethod(
                    "GenerateTalk",
                    PublicStatic,
                    null,
                    new[] { talkRequestType },
                    null);
                isBusyMethod = aiServiceType?.GetMethod("IsBusy", PublicStatic);
                shouldAiBeActiveOnSpeedMethod = commonUtilType?.GetMethod(
                    "ShouldAiBeActiveOnSpeed",
                    PublicStatic);
                addUserHistoryMethod = apiHistoryType?.GetMethod("AddUserHistory", PublicStatic);
                addIgnoredMethod = talkHistoryType?.GetMethod("AddIgnored", PublicStatic);
                overlayNotifyUpdatedMethod = overlayType?.GetMethod("NotifyLogUpdated", PublicStatic);
                pawnStatePawnField = pawnStateType.GetField("Pawn", PublicInstance);
                talkResponsesField = pawnStateType.GetField("TalkResponses", PublicInstance);
                talkRequestsField = pawnStateType.GetField("TalkRequests", PublicInstance);
                eventTalkType = Enum.Parse(talkTypeType, "Event");
                userTalkType = Enum.Parse(talkTypeType, "User");

                SSCLog.Important("[SSC RimTalk] RimTalk detected; scheduled training dialogue enabled.");
                return cacheGetMethod != null && generateTalkMethod != null;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return false;
            }
        }

        private static TrainingDialogueReservation FindReservation(Pawn master, Pawn slave)
        {
            for (int i = 0; i < TrainingReservations.Count; i++)
            {
                TrainingDialogueReservation reservation = TrainingReservations[i];
                if (reservation.master == master && reservation.slave == slave)
                    return reservation;
            }
            return null;
        }

        private static bool IsReservedPawn(Pawn pawn)
        {
            if (pawn == null)
                return false;

            for (int i = 0; i < TrainingReservations.Count; i++)
            {
                TrainingDialogueReservation reservation = TrainingReservations[i];
                if (reservation.master == pawn || reservation.slave == pawn)
                    return true;
            }
            return false;
        }

        private static bool AnyReservedRequestStarted()
        {
            for (int i = 0; i < TrainingReservations.Count; i++)
            {
                if (TrainingReservations[i].requestStarted)
                    return true;
            }
            return false;
        }

        private static bool ReservationStillValid(TrainingDialogueReservation reservation)
        {
            if (reservation == null || reservation.master == null || reservation.slave == null)
                return false;
            if (reservation.master.DestroyedOrNull() || reservation.slave.DestroyedOrNull())
                return false;
            if (!reservation.master.Spawned || !reservation.slave.Spawned ||
                reservation.master.Map != reservation.slave.Map)
                return false;
            if (GenTicks.TicksGame - reservation.createdTick > ReservationTimeoutTicks)
                return false;
            return reservation.master.CurJobDef == SSCDefOf.TrainingSexSlave;
        }

        private static string BuildInterruptText(Pawn master, Pawn slave)
        {
            string template = SSCMod.settings?.rimTalkTrainingInterruptTemplate;
            if (string.IsNullOrWhiteSpace(template))
                template = "SSC_RimTalk_DefaultInterrupt".Translate();

            string act = slave.TryGetComp<CompSexSlaveTraining>()?.selectedMode.ToString() ?? "Auto";
            return template
                .Replace("{MASTER}", master.LabelShort)
                .Replace("{SLAVE}", slave.LabelShort)
                .Replace("{ACT}", act)
                .Replace("{TARGET}", slave.LabelShort);
        }

        private static Guid AddBridgeResponse(TrainingDialogueReservation reservation)
        {
            object masterState = GetPawnState(reservation.master);
            if (masterState == null || talkResponsesField == null)
            {
                Messages.Message(
                    $"{reservation.master.LabelShort}: {reservation.bridgeText}",
                    reservation.master,
                    MessageTypeDefOf.NeutralEvent,
                    false);
                return Guid.Empty;
            }

            object response = Activator.CreateInstance(
                talkResponseType,
                new[] { userTalkType, reservation.master.LabelShort, reservation.bridgeText });
            SetMember(response, "TargetName", reservation.slave.LabelShort);

            object apiLog = addUserHistoryMethod?.Invoke(
                null,
                new object[] { reservation.master, reservation.slave, reservation.bridgeText });
            object apiLogId = GetMember(apiLog, "Id");
            if (apiLogId is Guid id)
                SetMember(response, "Id", id);

            IList responses = talkResponsesField.GetValue(masterState) as IList;
            responses?.Insert(0, response);
            overlayNotifyUpdatedMethod?.Invoke(null, null);

            object responseId = GetMember(response, "Id");
            return responseId is Guid bridgeId ? bridgeId : Guid.Empty;
        }

        private static void TryStartReservedDialogue(TrainingDialogueReservation reservation)
        {
            if (reservation.requestStarted || !CanGenerateAtCurrentSpeed() ||
                IsRimTalkBusy() || HasPendingResponses())
                return;

            string prompt =
                "[SexSlaveCraft scheduled training]\n" +
                $"The following interruption line has already been spoken by {reservation.master.LabelShort}: " +
                $"\"{reservation.bridgeText}\"\n" +
                $"Master/inviter: {reservation.master.LabelShort}\n" +
                $"Sex slave/partner: {reservation.slave.LabelShort}\n" +
                $"Training act: {reservation.sexType ?? "Auto"}\n" +
                "Do not repeat the interruption line. Continue immediately with a multi-turn, in-character " +
                "training exchange between these two pawns. Keep the dialogue synchronized with the active sex scene.";

            reservation.requestStarted = TryGenerateEvent(
                reservation.master,
                reservation.slave,
                prompt,
                true);
            if (reservation.requestStarted)
                reservedGenerationInFlight = true;
        }

        private static bool TryGenerateEvent(Pawn initiator, Pawn recipient, string prompt, bool bypassBlock)
        {
            if (!CanGenerateAtCurrentSpeed())
            {
                SSCLog.Verbose(
                    $"[SSC RimTalk] Dialogue request held or skipped at speed " +
                    $"{Find.TickManager.CurTimeSpeed}.");
                return false;
            }

            object pawnState = GetPawnState(initiator);
            if (pawnState == null)
                return false;

            object request = null;
            try
            {
                addTalkRequestMethod.Invoke(
                    pawnState,
                    new[] { (object)prompt, recipient, eventTalkType });
                request = getNextTalkRequestMethod.Invoke(pawnState, null);
                if (request == null)
                    return false;

                bypassReservationBlock = bypassBlock;
                object result = generateTalkMethod.Invoke(null, new[] { request });
                if (result is bool started && started)
                {
                    SSCLog.Verbose(
                        $"[SSC RimTalk] Dialogue generation started: initiator={initiator.LabelShort}, " +
                        $"recipient={recipient.LabelShort}");
                    return true;
                }

                markRequestSpokenMethod?.Invoke(pawnState, new[] { request });
                return false;
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    try { markRequestSpokenMethod?.Invoke(pawnState, new[] { request }); }
                    catch { }
                }
                LogFailureOnce(ex);
                return false;
            }
            finally
            {
                bypassReservationBlock = false;
            }
        }

        private static object GetPawnState(Pawn pawn)
        {
            return pawn == null ? null : cacheGetMethod?.Invoke(null, new object[] { pawn });
        }

        private static bool IsRimTalkBusy()
        {
            object result = isBusyMethod?.Invoke(null, null);
            return result is bool busy && busy;
        }

        private static bool CanGenerateAtCurrentSpeed()
        {
            int maxSpeed = SSCMod.settings?.rimTalkMaxGenerationSpeed ?? 0;
            if (maxSpeed > 0 && (int)Find.TickManager.CurTimeSpeed > maxSpeed)
                return false;

            if (shouldAiBeActiveOnSpeedMethod == null)
                return true;

            try
            {
                object result = shouldAiBeActiveOnSpeedMethod.Invoke(null, null);
                return !(result is bool active) || active;
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
                return true;
            }
        }

        private static bool HasPendingResponses()
        {
            IEnumerable states = cacheGetAllMethod?.Invoke(null, null) as IEnumerable;
            if (states == null || talkResponsesField == null)
                return false;

            foreach (object state in states)
            {
                IList responses = talkResponsesField.GetValue(state) as IList;
                if (responses != null && responses.Count > 0)
                    return true;
            }
            return false;
        }

        private static void IgnoreAllPendingResponses(Guid preserveId)
        {
            IEnumerable states = cacheGetAllMethod?.Invoke(null, null) as IEnumerable;
            if (states == null || talkResponsesField == null)
                return;

            foreach (object state in states)
            {
                IList responses = talkResponsesField.GetValue(state) as IList;
                if (responses == null)
                    continue;

                for (int i = responses.Count - 1; i >= 0; i--)
                {
                    object response = responses[i];
                    object idValue = GetMember(response, "Id");
                    Guid responseId = idValue is Guid id ? id : Guid.Empty;
                    if ((preserveId != Guid.Empty && responseId == preserveId) ||
                        IsReservationBridgeResponse(responseId))
                        continue;

                    if (responseId != Guid.Empty)
                        addIgnoredMethod?.Invoke(null, new object[] { responseId });
                    responses.RemoveAt(i);
                }
            }
        }

        private static bool IsReservationBridgeResponse(Guid responseId)
        {
            if (responseId == Guid.Empty)
                return false;

            for (int i = 0; i < TrainingReservations.Count; i++)
            {
                if (TrainingReservations[i].bridgeResponseId == responseId)
                    return true;
            }
            return false;
        }

        private static void ClearPawnRequests(Pawn pawn)
        {
            object state = GetPawnState(pawn);
            object requests = state == null ? null : talkRequestsField?.GetValue(state);
            requests?.GetType().GetMethod("Clear", PublicInstance)?.Invoke(requests, null);
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
                return null;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, PublicInstance);
            if (property != null)
                return property.GetValue(instance, null);

            return type.GetField(name, PublicInstance)?.GetValue(instance);
        }

        private static void SetMember(object instance, string name, object value)
        {
            if (instance == null)
                return;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, PublicInstance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value, null);
                return;
            }

            type.GetField(name, PublicInstance)?.SetValue(instance, value);
        }

        private static void LogFailureOnce(Exception exception)
        {
            if (failureLogged)
                return;

            failureLogged = true;
            Exception actual = exception is TargetInvocationException && exception.InnerException != null
                ? exception.InnerException
                : exception;
            SSCLog.WarningImportant(
                $"[SSC RimTalk] Compatibility action failed; duplicate failure logs will be suppressed: {actual}");
        }
    }
}
