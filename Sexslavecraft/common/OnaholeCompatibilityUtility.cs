using System;
using System.Reflection;
using RimWorld;
using rjw;
using Verse;

// EN: This file bridges SSC training scenes with the RJW Onahole receiver flow.
// EN: It detects the BeOnahole driver through reflection, registers partners, and checks whether the special receiver state is still valid.
// CN: 这个文件负责把 SSC 调教场景桥接到 RJW Onahole 的接收者流程上。
// CN: 它通过反射识别 BeOnahole driver、注册 partner，并检查这种特殊 receiver 状态是否仍然有效。
namespace SexSlaveCraft
{
    public static class OnaholeCompatibilityUtility
    {
        // Current Onahole versions place the driver in RJW_Onahole.Jobs.
        // Resolve by both assembly-qualified name and loaded-assembly scan so a harmless
        // assembly rename or visibility change does not disable all SSC training support.
        private static readonly Type BeOnaholeDriverType = ResolveBeOnaholeDriverType();
        private static readonly MethodInfo AddPartnerMethod = ResolvePawnMethod(BeOnaholeDriverType, "AddPartner");
        private static readonly MethodInfo RemovePartnerMethod = ResolvePawnMethod(BeOnaholeDriverType, "RemovePartner");

        private static Type ResolveBeOnaholeDriverType()
        {
            string[] typeNames =
            {
                "RJW_Onahole.Jobs.JobDriver_BeOnahole",
                "RJW_Onahole.JobDriver_BeOnahole"
            };

            for (int i = 0; i < typeNames.Length; i++)
            {
                Type resolved = Type.GetType(typeNames[i] + ", RJW_Onahole");
                if (resolved != null) return resolved;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                for (int typeIndex = 0; typeIndex < typeNames.Length; typeIndex++)
                {
                    Type resolved = assemblies[assemblyIndex].GetType(typeNames[typeIndex], false);
                    if (resolved != null) return resolved;
                }
            }

            return null;
        }

        private static MethodInfo ResolvePawnMethod(Type declaringType, string methodName)
        {
            if (declaringType == null) return null;
            return declaringType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(Pawn) },
                null);
        }

        public static bool IsOnaholeLoaded => BeOnaholeDriverType != null;

        public static bool IsBeOnaholeDriver(object driver)
        {
            return driver != null && BeOnaholeDriverType != null && BeOnaholeDriverType.IsInstanceOfType(driver);
        }

        public static bool IsPawnOnOnahole(Pawn pawn)
        {
            // EN: This check is the fast path used by training jobs before they decide whether SSC_TrainingReceiver can be replaced.
            // CN: 这个检查是调教 Job 的快速入口，用来判断当前能不能把 SSC_TrainingReceiver 换成 Onahole 的接收流程。
            return IsBeOnaholeDriver(pawn?.jobs?.curDriver);
        }

        public static bool TryRegisterOnaholePartner(Pawn onaholePawn, Pawn partner)
        {
            object driver = onaholePawn?.jobs?.curDriver;
            if (!IsBeOnaholeDriver(driver) || AddPartnerMethod == null || partner == null)
            {
                return false;
            }

            try
            {
                // EN: Compatibility path: register the current trainer as partner without hard-linking against the Onahole assembly.
                // CN: 兼容分支：在不硬依赖 Onahole 程序集的前提下，把当前调教师注册成 partner。
                AddPartnerMethod.Invoke(driver, new object[] { partner });
                if (driver is rjw.JobDriver_SexBaseReciever reciever)
                {
                    // Mirror Onahole's own same-tick race guard. A bound pawn can
                    // service only one initiator at a time.
                    if (reciever.parteners != null)
                    {
                        foreach (Pawn registeredPartner in reciever.parteners)
                        {
                            if (registeredPartner != null && registeredPartner != partner)
                            {
                                RemovePartnerMethod?.Invoke(driver, new object[] { partner });
                                SSCLog.WarningImportant(
                                    $"[SSC Onahole] Receiver is already occupied: pawn={onaholePawn.LabelShort}, " +
                                    $"existingPartner={registeredPartner.LabelShort}, rejectedPartner={partner.LabelShort}");
                                return false;
                            }
                        }
                    }

                    reciever.asleep = false;
                }
                return true;
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant($"[SSC Onahole] Failed to register partner on onahole driver: {ex}");
                return false;
            }
        }

        public static bool TrySynchronizeOnaholeSexProps(Pawn onaholePawn, SexProps initiatorProps)
        {
            object driver = onaholePawn?.jobs?.curDriver;
            if (!IsBeOnaholeDriver(driver))
            {
                return true;
            }

            if (!(driver is JobDriver_SexBaseReciever receiver) ||
                initiatorProps == null ||
                initiatorProps.dictionaryKey == null)
            {
                SSCLog.WarningImportant(
                    $"[SSC Onahole] Cannot synchronize receiver SexProps: pawn={onaholePawn?.LabelShort ?? "null"}, " +
                    $"driver={driver?.GetType().Name ?? "null"}, interaction={initiatorProps?.dictionaryKey?.defName ?? "null"}");
                return false;
            }

            try
            {
                // EN: RJW ticks the receiver's Orgasm as well as the initiator's. BeOnahole's idle SexProps
                // have no interaction, so SSC must provide the same reversed props that JobDriver_UseOnahole uses.
                // CN: RJW 会同时调用发起者和接收者的 Orgasm。BeOnahole 的待机 SexProps 没有 interaction，
                // 因此 SSC 必须像 JobDriver_UseOnahole 一样给接收者同步一份反向 SexProps。
                receiver.Sexprops = initiatorProps.GetForPartner();
                SSCLog.Important(
                    $"[SSC Onahole] Receiver SexProps synchronized: pawn={onaholePawn.LabelShort}, " +
                    $"interaction={receiver.Sexprops.dictionaryKey?.defName ?? "null"}");
                return true;
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant($"[SSC Onahole] Failed to synchronize receiver SexProps: {ex}");
                return false;
            }
        }

        public static void TryUnregisterOnaholePartner(Pawn onaholePawn, Pawn partner)
        {
            object driver = onaholePawn?.jobs?.curDriver;
            if (!IsBeOnaholeDriver(driver) || RemovePartnerMethod == null || partner == null)
            {
                return;
            }

            try
            {
                // EN: BeOnahole does not run the normal receiver cleanup path. Mirror its own
                // JobDriver_UseOnahole finish action so a completed SSC scene leaves no stale partner.
                // CN: BeOnahole 不走普通 receiver 的清理流程。这里复用它自己的 RemovePartner，
                // 避免 SSC 场景结束后留下失效 partner。
                RemovePartnerMethod.Invoke(driver, new object[] { partner });
                SSCLog.Verbose(
                    $"[SSC Onahole] Partner unregistered: receiver={onaholePawn.LabelShort}, partner={partner.LabelShort}");
            }
            catch (Exception ex)
            {
                SSCLog.WarningImportant($"[SSC Onahole] Failed to unregister partner: {ex}");
            }
        }

        public static string GetOnaholeReceiverStateReport(Pawn pawn, JobDef fallbackJob)
        {
            // EN: This debug report is intentionally verbose so Onahole compatibility fallback can be diagnosed from logs alone.
            // CN: 这个调试报告故意写得比较啰嗦，这样光看日志就能诊断 Onahole 兼容回退发生在哪里。
            if (pawn?.jobs?.curDriver == null) return "receiver=null";

            if (IsBeOnaholeDriver(pawn.jobs.curDriver))
            {
                Building_Bed currentBed = pawn.CurrentBed();
                return $"receiver=BeOnahole, job={pawn.CurJobDef?.defName ?? "null"}, inBed={(currentBed != null)}, currentBed={currentBed?.def?.defName ?? "null"}";
            }

            return $"receiver={pawn.jobs.curDriver.GetType().Name}, job={pawn.CurJobDef?.defName ?? "null"}, expected={fallbackJob?.defName ?? "null"}";
        }

        public static bool IsValidTrainingReceiver(Pawn pawn, JobDef fallbackJob)
        {
            if (pawn?.jobs?.curDriver == null) return false;
            // EN: BeOnahole is accepted as a valid receiver even though it does not use SSC's fallback job.
            // CN: BeOnahole 虽然不用 SSC 的 fallback job，但这里仍视为合法 receiver。
            if (IsBeOnaholeDriver(pawn.jobs.curDriver)) return true;
            return pawn.CurJob?.def == fallbackJob;
        }
    }
}
