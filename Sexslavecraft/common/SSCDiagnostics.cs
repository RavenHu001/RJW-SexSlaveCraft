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
        /// <summary>一次读取生成的显示数据。概况分区、注册行和完整文本来自同一轮采集。</summary>
        internal sealed class Snapshot
        {
            internal string Report;
            internal (string Title, string Body)[] Overview;
#if SSC_RW17
            internal PatchRow[] Registrations;
#endif
        }

#if SSC_RW17
        /// <summary>State 用于筛选，Status 是本地化标签，Notes 汇总可供汇报的诊断详情。</summary>
        internal sealed class PatchRow
        {
            internal string Label, Status, Notes;
            internal SSCPatchState State;
        }
#endif

        public static string BuildReport() => Capture().Report;

        internal static Snapshot Capture()
        {
            var report = new StringBuilder();
            var overview = new List<(string Title, string Body)>();
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
            }, overview);
            AppendSection(report, "SSC_Diagnostics_Dependencies", () =>
            {
                AppendAssembly(report, "Harmony", "0Harmony");
                AppendAssembly(report, "RimJobWorld", "RJW");
                AppendAssembly(report, "RimWorld Animations", "Rimworld-Animations");
            }, overview);
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
            }, overview);
#if SSC_RW17
            var registrations = CaptureRegistrations();
            AppendSection(report, "SSC_Diagnostics_PatchRegistration", () =>
            {
                if (registrations.Length == 0) report.AppendLine("SSC_Diagnostics_PatchWaiting".Translate());
                foreach (var row in registrations)
                {
                    report.AppendLine(row.Label + " — " + row.Status);
                    if (row.Notes.Length != 0) report.AppendLine("  " + row.Notes);
                }
            });
#endif
            report.AppendLine("SSC_Diagnostics_ReportNote".Translate());
            return new Snapshot
            {
                Report = report.ToString(),
                Overview = overview.ToArray(),
#if SSC_RW17
                Registrations = registrations
#endif
            };
        }

#if SSC_RW17
        // 模块与复制报告共用同一批已格式化的行，刷新时只读取状态。
        private static PatchRow[] CaptureRegistrations()
        {
            // 优先看调教能否派发和武器绘制是否回退。其余先列出失败与回退，同类沿用稳定的名称顺序。
            return SSCPatchRegistration.Snapshot()
                .OrderBy(entry => entry.Key == SSCPatchRegistration.DailyTraining ? 0 :
                    entry.Key == nameof(Harmony_SSCPnaSpearGlowDraw) ? 1 :
                    entry.Value.State == SSCPatchState.Failed ? 2 :
                    entry.Value.State == SSCPatchState.Fallback ? 3 : 4)
                .Select(entry =>
                {
                    var (state, target, stage, detail, reason) = entry.Value;
                    string label = entry.Key;
                    if (entry.Key == SSCPatchRegistration.DailyTraining)
                    {
                        label = "SSC_Diagnostics_PatchDailyTraining".Translate();
                        if (state == SSCPatchState.Failed)
                            detail = string.Join(" · ", new[] { detail, "SSC_Diagnostics_TrainingHooksUnavailable".Translate().ToString() }
                                .Where(value => !string.IsNullOrEmpty(value)));
                    }
                    if (entry.Key == nameof(Harmony_SSCPnaSpearGlowDraw))
                    {
                        // 已登记的 Transpiler 仍可能原样透传。窗口和复制报告使用同一个修正后的状态。
                        label = "SSC_Diagnostics_PatchWeaponDrawing".Translate();
                        target = "Verse.PawnRenderUtility.DrawEquipmentAiming";
                        if (state == SSCPatchState.Ready && !Harmony_SSCPnaSpearGlowDraw.Matched)
                        {
                            state = SSCPatchState.Fallback;
                            stage = "Transpiler";
                            detail = "SSC_Diagnostics_PatchWeaponDrawingFallback".Translate();
                        }
                    }
                    // 跳过原因是注册端记录的分类，在这里才按当前语言生成文字。
                    string skip = state == SSCPatchState.Skipped && reason != SSCPatchSkipReason.None
                        ? ("SSC_Diagnostics_Skip" + reason).Translate().ToString() : null;
                    return new PatchRow
                    {
                        Label = label,
                        State = state,
                        Status = ("SSC_Diagnostics_Patch" + state).Translate(),
                        Notes = string.Join(" · ", new[] { skip, target, stage, detail }.Where(value => !string.IsNullOrEmpty(value)))
                    };
                }).ToArray();
        }
#endif

        private static void AppendSection(StringBuilder report, string titleKey, Action collect,
            List<(string Title, string Body)> overview = null)
        {
            string title = titleKey.Translate();
            report.AppendLine("[" + title + "]");
            int start = report.Length;
            try { collect(); }
            catch (Exception exception)
            {
                // Exception messages can contain local paths. Keep the report limited to the error type.
                report.AppendLine("SSC_Diagnostics_CheckFailed".Translate(exception.GetType().Name));
            }
            // 概况只取本分区刚写入的正文，和复制报告共用采集结果及分区级容错。
            overview?.Add((title, report.ToString(start, report.Length - start).TrimEnd()));
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
