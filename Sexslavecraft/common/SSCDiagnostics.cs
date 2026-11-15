using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // Read-only snapshots: inspect existing registrations without running compatibility initializers.
    internal static class SSCDiagnostics
    {
        public static string BuildReport()
        {
            var report = new StringBuilder();
            Assembly assembly = typeof(SSCDiagnostics).Assembly;
            report.AppendLine("SSC_Diagnostics_Title".Translate());
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine();
            AppendSection(report, "SSC_Diagnostics_Environment", () =>
            {
                report.AppendLine("SSC: " + AssemblyVersion(assembly));
                report.AppendLine("SSC MVID: " + assembly.ManifestModule.ModuleVersionId);
                report.AppendLine("RimWorld: " + VersionControl.CurrentVersionStringWithRev);
                report.AppendLine("SSC_Diagnostics_Language".Translate(LanguageDatabase.activeLanguage?.folderName ?? "?"));
            });
            AppendSection(report, "SSC_Diagnostics_Dependencies", () =>
            {
                AppendAssembly(report, "Harmony", "0Harmony");
                AppendAssembly(report, "RimJobWorld", "RJW");
                AppendAssembly(report, "RimWorld Animations", "Rimworld-Animations");
            });
            AppendSection(report, "SSC_Diagnostics_Compatibility", () =>
            {
                List<Patch> patches = RegisteredPatches(assembly);
                report.AppendLine("SSC_Diagnostics_PatchCount".Translate(patches.Count));
                AppendCompatibility(report, patches, "Dubs Mint Menus", "DubsMintMenus.Dialog_AssignBuildingOwner",
                    "Harmony_SSC_MintSharedBedAssignmentUI");
                AppendCompatibility(report, patches, "Equal Milking", "EqualMilking.Helpers.ExtensionHelper",
                    "Patch_IsLactating", "Patch_LactatingHediff", "Patch_LactatingHediffComp", "Patch_GreedyConsume");
                AppendCompatibility(report, patches, "Human Cattle", "HumanCattle.HediffComp_MilkedTracker",
                    "Patch_HumanCattle_DoopsOwnTickPrefix", "Patch_HumanCattle_DoopsOwnPostMakePrePrefix",
                    "Patch_HumanCattle_DoopsOwnPostMakePostPrefix");
                report.AppendLine("RimTalk: " + "SSC_RimTalk_Paused".Translate());
                report.AppendLine("SSC_Diagnostics_PatchNote".Translate());
            });
#if SSC_RW17
            AppendSection(report, "SSC_Diagnostics_PatchRegistration", () =>
            {
                // 窗口打开及点击刷新时生成报告，复制沿用当前文本。读取过程只观察启动记录，不重新注册补丁。
                var entries = SSCPatchRegistration.Snapshot();
                if (entries.Length == 0) report.AppendLine("SSC_Diagnostics_PatchWaiting".Translate());
                foreach (var entry in entries)
                {
                    var (state, target, stage, detail) = entry.Value;
                    string label = entry.Key;
                    if (entry.Key == SSCPatchRegistration.DailyTraining)
                    {
                        // 核心组失败会拦截日常调教派发。把可见影响和失败信息放在同一条记录。
                        label = "SSC_Diagnostics_PatchDailyTraining".Translate();
                        if (state == SSCPatchState.Failed)
                            detail = string.Join(" · ", new[] { detail, "SSC_Diagnostics_TrainingHooksUnavailable".Translate().ToString() }
                                .Where(value => !string.IsNullOrEmpty(value)));
                    }
                    if (entry.Key == nameof(Harmony_SSCPnaSpearGlowDraw))
                    {
                        // 原样透传的 Transpiler 仍在 Harmony 清单里。读取最新匹配结果，展示实际绘制回退。
                        label = "SSC_Diagnostics_PatchWeaponDrawing".Translate();
                        target = "Verse.PawnRenderUtility.DrawEquipmentAiming";
                        if (state == SSCPatchState.Ready && !Harmony_SSCPnaSpearGlowDraw.Matched)
                        {
                            state = SSCPatchState.Fallback;
                            stage = "Transpiler";
                            detail = "SSC_Diagnostics_PatchWeaponDrawingFallback".Translate();
                        }
                    }
                    report.AppendLine(label + " — " + ("SSC_Diagnostics_Patch" + state).Translate());
                    var notes = new[] { target, stage, detail }.Where(value => !string.IsNullOrEmpty(value)).ToArray();
                    if (notes.Length != 0) report.AppendLine("  " + string.Join(" · ", notes));
                }
            });
#endif
            report.AppendLine("SSC_Diagnostics_ReportNote".Translate());
            return report.ToString();
        }

        private static void AppendSection(StringBuilder report, string titleKey, Action collect)
        {
            report.AppendLine("[" + titleKey.Translate() + "]");
            try { collect(); }
            catch (Exception exception)
            {
                // Exception messages can contain local paths. Keep the report limited to the error type.
                report.AppendLine("SSC_Diagnostics_CheckFailed".Translate(exception.GetType().Name));
            }
            report.AppendLine();
        }

        private static void AppendAssembly(StringBuilder report, string label, string assemblyName)
        {
            Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies()
                .Where(candidate => candidate.GetName().Name == assemblyName).ToArray();
            report.AppendLine(label + ": " + (matches.Length == 0
                ? "SSC_Diagnostics_AssemblyMissing".Translate().ToString()
                : string.Join(", ", matches.Select(AssemblyVersion))));
        }

        private static string AssemblyVersion(Assembly assembly)
        {
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
                ?? assembly.GetName().Version?.ToString() ?? "?";
        }

        private static List<Patch> RegisteredPatches(Assembly assembly)
        {
            var result = new List<Patch>();
            foreach (MethodBase target in Harmony.GetAllPatchedMethods().ToArray())
            {
                Patches info = Harmony.GetPatchInfo(target);
                if (info == null) continue;
                result.AddRange(info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                    .Where(patch => patch.PatchMethod?.DeclaringType?.Assembly == assembly));
            }
            return result;
        }

        private static void AppendCompatibility(StringBuilder report, List<Patch> patches, string label,
            string interfaceType, params string[] patchTypes)
        {
            report.AppendLine(label + ":");
            try
            {
                Type type = SoftDependencyUtility.FindOptionalType(interfaceType);
                report.AppendLine("  " + (type == null
                    ? "SSC_Diagnostics_InterfaceMissing".Translate()
                    : "SSC_Diagnostics_InterfaceFound".Translate(AssemblyVersion(type.Assembly))));
                string[] missing = patchTypes.Where(name => !patches.Any(patch =>
                    patch.PatchMethod.DeclaringType.FullName == "SexSlaveCraft." + name)).ToArray();
                int found = patchTypes.Length - missing.Length;
                report.AppendLine("  " + "SSC_Diagnostics_Registered".Translate(found, patchTypes.Length));
                if (type != null && missing.Length > 0)
                {
                    report.AppendLine("  " + "SSC_Diagnostics_Incomplete".Translate());
                    report.AppendLine("  " + string.Join(", ", missing));
                }
            }
            catch (Exception exception)
            {
                report.AppendLine("  " + "SSC_Diagnostics_CheckFailed".Translate(exception.GetType().Name));
            }
        }
    }
}
