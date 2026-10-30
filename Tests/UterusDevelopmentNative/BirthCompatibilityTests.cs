// AI 辅助生成：显式可选DLL适配核验，--birth-compat独立执行，不计入默认核心分区。
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>读取本机真实HAR／月经DLL；不启动这些Mod的整体初始化，不把适配核验当作实机。</summary>
    private static void BirthCompatibilityTests(Harmony harmony)
    {
        InitializeBirth(harmony);
        string game = Path.GetDirectoryName(typeof(Pawn).Assembly.Location);
        string harPath = Environment.GetEnvironmentVariable("SSC_TEST_HAR_PATH") ?? Path.GetFullPath(Path.Combine(game, "../../workshop/content/294100/839005762/1.7/Assemblies/AlienRace.dll"));
        string menstruationPath = Environment.GetEnvironmentVariable("SSC_TEST_MENSTRUATION_PATH") ?? Path.Combine(game, "Mods/RJW-玩法-月经周期（RJW Menstruation Cycle）/1.7/Assemblies/RJW_Menstruation.dll");
        Require(File.Exists(harPath) && File.Exists(menstruationPath), "Explicit HAR and menstruation DLLs are required for --birth-compat.");
        var har = AssemblyLoadContext.Default.LoadFromAssemblyPath(harPath);
        var menstruation = AssemblyLoadContext.Default.LoadFromAssemblyPath(menstruationPath);
        Console.WriteLine($"HAR: {harPath}, MVID: {har.ManifestModule.ModuleVersionId:N}");
        Console.WriteLine($"Menstruation: {menstruationPath}, MVID: {menstruation.ManifestModule.ModuleVersionId:N}");

        Check("birth compatibility actual HAR child kind is honored only for the selected species", () =>
        {
            var source = ImprintPawn();
            var race = (ThingDef)RuntimeHelpers.GetUninitializedObject(har.GetType("AlienRace.ThingDef_AlienRace", true));
            race.defName = "BirthHarBoundary"; race.race = source.def.race; race.category = ThingCategory.Pawn;
            source.def = race; source.kindDef.race = race;
            object reproduction = race;
            foreach (string name in new[] { "alienRace", "generalSettings", "reproduction" })
            {
                var field = AccessTools.Field(reproduction.GetType(), name);
                var value = RuntimeHelpers.GetUninitializedObject(field.FieldType); field.SetValue(reproduction, value); reproduction = value;
            }
            var child = new PawnKindDef { defName = "BirthHarChild", race = race };
            var childField = AccessTools.Field(reproduction.GetType(), "childKindDef"); childField.SetValue(reproduction, child);
            var original = ImprintPawn().kindDef;
            Require(SSCFertilityOffspringUtility.TryResolveKind(source, original, out var kind)); Equal(child, kind);
            childField.SetValue(reproduction, original);
            Require(SSCFertilityOffspringUtility.TryResolveKind(source, original, out kind)); Equal(source.kindDef, kind);
        });
        Check("birth compatibility actual menstruation replacement retains existing babies and fallback applies current controls", () =>
        {
            var native = AccessTools.Method(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome));
            var replacementType = menstruation.GetType(SSCFertilityGenerationPatches.MenstruationBirthType, true);
            var transpiler = AccessTools.Method(replacementType, "Transpiler");
            var optionalOwner = new Harmony("RJW_Menstruation");
            try
            {
                optionalOwner.Patch(native, transpiler: new HarmonyMethod(transpiler));
                foreach (Type type in new[] { typeof(Harmony_FertilityNativeGeneration), typeof(Harmony_FertilityNativeBirth),
                    typeof(Harmony_FertilityMenstruationGeneration), typeof(Harmony_FertilityMenstruationFallback) })
                    harmony.CreateClassProcessor(type).Patch();
                var take = AccessTools.Method(replacementType, "GetPregeneratedBaby");
                var instructions = PatchProcessor.GetCurrentInstructions(native).ToList();
                Equal(1, instructions.Count(c => c.Calls(take)));
                Equal(0, instructions.Count(c => c.Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateNative"))));
                Equal(4, PatchProcessor.GetCurrentInstructions(take).Count(c => c.Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateFallback"))));
                var add = AccessTools.Method(menstruation.GetType(SSCFertilityGenerationPatches.MenstruationType, true), "AddNewBaby");
                Equal(1, PatchProcessor.GetCurrentInstructions(add).Count(c => c.Calls(AccessTools.Method(typeof(SSCFertilityGenerationPatches), "GenerateMenstruation"))));

                var mother = BirthMother(); var father = ImprintPawn();
                Require(SSCFertilityPreconceptionUtility.TrySetSpeciesSource(SSCFertilityPreconceptionUtility.Capture(mother),
                    SSCOffspringSpeciesSource.Partner, out string reason), reason);
                var scope = SSCFertilityBirthUtility.Begin(mother, father, true);
                try
                {
                    var request = NewbornRequest(mother.kindDef);
                    var baby = (Pawn)take.Invoke(null, new object[] { request, mother });
                    Equal(father.def, baby.def); Equal(Gender.Male, baby.gender); Equal(SSCImprintKind.Initial, SSCImprintUtility.GetKind(baby));
                    // 现有列表是月经自己的载体；SSC不保存/搬运胎儿，只验证原取出路径未被覆盖。
                    var compType = menstruation.GetType(SSCFertilityGenerationPatches.MenstruationType, true);
                    var comp = (HediffComp)Activator.CreateInstance(compType);
                    var labor = new Hediff_LaborPushing { pawn = mother, def = HediffDefOf.PregnancyLaborPushing };
                    comp.parent = labor;
                    AccessTools.Field(typeof(HediffWithComps), "comps").SetValue(labor, new List<HediffComp> { comp });
                    var existing = ImprintPawn(age: 0); existing.gender = Gender.Female;
                    AccessTools.Field(compType, "babies").SetValue(comp, new List<Pawn> { existing });
                    mother.health.hediffSet.hediffs.Add(labor);
                    harmony.Patch(AccessTools.Method(menstruation.GetType("RJW_Menstruation.MenstruationUtility", true), "ShouldCycle"),
                        prefix: new HarmonyMethod(typeof(Program), nameof(BirthCycleBoundary)));
                    var taken = (Pawn)take.Invoke(null, new object[] { request, mother });
                    Require(ReferenceEquals(existing, taken)); Equal(Gender.Female, taken.gender);
                    Equal(SSCImprintKind.None, SSCImprintUtility.GetKind(taken));
                }
                finally { scope.Finish(null); }
            }
            finally { optionalOwner.UnpatchAll(optionalOwner.Id); }
        });
    }
    /// <summary>隔离月经周期资格，不测试其周期算法；实际已有婴儿取出及SSC不追补路径继续执行。</summary>
    private static bool BirthCycleBoundary(ref bool __result) { __result = true; return false; }
}
