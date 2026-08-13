using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Verse;

// EN: This file reads RJW-PE sex-age settings without taking a hard dependency on RJW-PE.
// EN: It builds a stable snapshot so SSC can explain why a pawn fails age-gated sex-target checks.
// CN: 这个文件用“软兼容”的方式读取 RJW-PE 的性年龄设置。
// CN: 它会组装一个稳定快照，让 SSC 在目标被年龄门槛卡住时能解释失败原因。
namespace SexSlaveCraft
{
    public static class RJWPECompatibility
    {
        public sealed class SexAgeConfigSnapshot
        {
            public bool Detected;
            public bool OverrideAgeCheck;
            public float MinimumAgeSex;
            public float MinimumAgeRape;
            public string Source;
            public string Note;
        }

        public static bool IsRJWPELoaded()
        {
            return GetAssemblyByName("rjwpe") != null;
        }

        public static bool TryGetSexAgeConfigForPawn(Pawn pawn, out SexAgeConfigSnapshot snapshot)
        {
            snapshot = new SexAgeConfigSnapshot
            {
                Detected = IsRJWPELoaded(),
                Source = "unknown",
                Note = ""
            };

            // EN: Step 1: bail out early if the target pawn is null or the RJW-PE assembly is not present.
            // CN: 步骤 1：如果目标 Pawn 为空，或根本没有加载 RJW-PE，就直接提前退出。
            if (pawn == null)
            {
                snapshot.Note = "pawn is null";
                return false;
            }

            if (!snapshot.Detected)
            {
                snapshot.Note = "rjwpe assembly not loaded";
                return false;
            }

            try
            {
                // EN: Step 2: resolve the settings container dynamically, because RJW-PE is optional and may change internals between versions.
                // CN: 步骤 2：动态解析设置容器，因为 RJW-PE 是可选兼容，而且不同版本内部结构可能会变化。
                Type settingsContainerType = FindType("rjwpe.Settings.SettingsContainer");
                Type generalSettingsType = FindType("rjwpe.Settings.GeneralSettings");

                if (settingsContainerType == null || generalSettingsType == null)
                {
                    snapshot.Note = "settings container type not found";
                    return false;
                }

                MethodInfo getGeneric = settingsContainerType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);

                if (getGeneric == null)
                {
                    snapshot.Note = "SettingsContainer.Get<T>() not found";
                    return false;
                }

                object generalSettings = getGeneric.MakeGenericMethod(generalSettingsType).Invoke(null, null);
                if (generalSettings == null)
                {
                    snapshot.Note = "GeneralSettings is null";
                    return false;
                }

                // EN: Step 3: pick the Humanlike or Animals branch first, because RJW-PE stores them in separate groups.
                // CN: 步骤 3：先决定走 Humanlike 还是 Animals 分支，因为 RJW-PE 把它们拆在不同配置组里。
                bool useHumanlike = pawn.RaceProps != null && pawn.RaceProps.Humanlike;
                bool useAnimal = pawn.RaceProps != null && pawn.RaceProps.Animal;
                if (!useHumanlike && !useAnimal)
                {
                    snapshot.Note = "pawn is neither humanlike nor animal";
                    return false;
                }

                PropertyInfo groupProp = generalSettingsType.GetProperty(useHumanlike ? "Humanlike" : "Animals", BindingFlags.Public | BindingFlags.Instance);
                object raceGroup = groupProp?.GetValue(generalSettings, null);
                if (raceGroup == null)
                {
                    snapshot.Note = "race group config not found";
                    return false;
                }

                object selectedRaceConfig = null;
                string raceLabel = pawn.def?.label;

                PropertyInfo overridesProp = raceGroup.GetType().GetProperty("Overrides", BindingFlags.Public | BindingFlags.Instance);
                object overridesObj = overridesProp?.GetValue(raceGroup, null);
                IDictionary nonGenericDict = overridesObj as IDictionary;

                if (!string.IsNullOrEmpty(raceLabel) && nonGenericDict != null && nonGenericDict.Contains(raceLabel))
                {
                    selectedRaceConfig = nonGenericDict[raceLabel];
                    snapshot.Source = "override:" + raceLabel;
                }

                if (selectedRaceConfig == null)
                {
                    // EN: Step 4: if the current race has no explicit override, fall back to the group's global config.
                    // CN: 步骤 4：如果当前种族没有专属覆盖，就回退到该分组的全局配置。
                    PropertyInfo globalProp = raceGroup.GetType().GetProperty("Global", BindingFlags.Public | BindingFlags.Instance);
                    selectedRaceConfig = globalProp?.GetValue(raceGroup, null);
                    snapshot.Source = "global";
                }

                if (selectedRaceConfig == null)
                {
                    snapshot.Note = "selected race config is null";
                    return false;
                }

                PropertyInfo sexProp = selectedRaceConfig.GetType().GetProperty("Sex", BindingFlags.Public | BindingFlags.Instance);
                object sexConfig = sexProp?.GetValue(selectedRaceConfig, null);
                if (sexConfig == null)
                {
                    snapshot.Note = "sex config not found";
                    return false;
                }

                PropertyInfo overrideAgeCheckProp = sexConfig.GetType().GetProperty("OverrideAgeCheck", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo minSexProp = sexConfig.GetType().GetProperty("MinimumAgeSex", BindingFlags.Public | BindingFlags.Instance);
                PropertyInfo minRapeProp = sexConfig.GetType().GetProperty("MinimumAgeRape", BindingFlags.Public | BindingFlags.Instance);

                if (overrideAgeCheckProp == null || minSexProp == null || minRapeProp == null)
                {
                    snapshot.Note = "sex config properties missing";
                    return false;
                }

                snapshot.OverrideAgeCheck = SafeToBool(overrideAgeCheckProp.GetValue(sexConfig, null), true);
                snapshot.MinimumAgeSex = SafeToFloat(minSexProp.GetValue(sexConfig, null), 0f);
                snapshot.MinimumAgeRape = SafeToFloat(minRapeProp.GetValue(sexConfig, null), 0f);
                snapshot.Note = "ok";
                return true;
            }
            catch (Exception ex)
            {
                snapshot.Note = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static float SafeToFloat(object value, float fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToSingle(value); }
            catch { return fallback; }
        }

        private static bool SafeToBool(object value, bool fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToBoolean(value); }
            catch { return fallback; }
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, false, false);
                if (t != null) return t;
            }
            return null;
        }

        private static Assembly GetAssemblyByName(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
            {
                try { return string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        }
    }
}
