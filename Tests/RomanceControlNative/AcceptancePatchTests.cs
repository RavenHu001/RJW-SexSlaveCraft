// AI 辅助生成：实际原版 IL 唯一锚点与失败回退，以及同一游戏 Harmony 3 的补丁组合记录。
using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接执行生产 Transpiler；验证匹配数、标签、默认浮点语义和未覆盖时的原样回退。</summary>
    private static void AcceptancePatchTests(Harmony harmony, string repo)
    {
        var resolve = AccessTools.Method(typeof(SSCRomanceAcceptanceUtility), nameof(SSCRomanceAcceptanceUtility.Resolve));
        foreach (var method in Harmony_SSCRomanceAcceptance.TargetMethods())
        {
            Check("batch2 actual IL replaces exactly one acceptance decision " + method.DeclaringType.Name, () =>
            {
                var original = PatchProcessor.GetOriginalInstructions(method).ToList();
                var rewritten = Harmony_SSCRomanceAcceptance.Transpiler(original, method).ToList();
                Equal(1, rewritten.Count(c => c.Calls(resolve)));
                Equal(original.Count(c => c.Calls(AccessTools.PropertyGetter(typeof(Rand), "Value"))), rewritten.Count(c => c.Calls(AccessTools.PropertyGetter(typeof(Rand), "Value"))));
                if (method.DeclaringType == typeof(InteractionWorker_MarriageProposal)) Require(rewritten.Any(c => c.opcode == OpCodes.Ldc_R4 && Equals(c.operand, .4f)));
            });
            foreach (bool duplicate in new[] { false, true })
                Check($"batch2 unmatched/nonunique acceptance anchor keeps full original {method.DeclaringType.Name}/{duplicate}", () =>
                {
                    var original = duplicate ? PatchProcessor.GetOriginalInstructions(method).ToList() : new List<CodeInstruction> { new(OpCodes.Ret) };
                    if (duplicate) original.AddRange(PatchProcessor.GetOriginalInstructions(method));
                    var rewritten = Harmony_SSCRomanceAcceptance.Transpiler(original, method).ToList(); Equal(original.Count, rewritten.Count);
                    Require(original.Zip(rewritten).All(p => p.First.opcode == p.Second.opcode && Equals(p.First.operand, p.Second.operand)));
                    Equal(0, rewritten.Count(c => c.Calls(resolve)));
                });
        }
        Check("batch2 romance comparison retains incoming label and exception marker", () =>
        {
            var method = Harmony_SSCRomanceAcceptance.TargetMethods().First(); var original = PatchProcessor.GetOriginalInstructions(method).ToList();
            int at = original.FindIndex(c => c.Calls(AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "SuccessChance"))) + 1;
            var label = new DynamicMethod("romance_labels", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel(); original[at].labels.Add(label);
            original[at].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            var rewritten = Harmony_SSCRomanceAcceptance.Transpiler(original, method).ToList();
            Equal(OpCodes.Clt, rewritten[at].opcode); Require(rewritten[at].labels.Contains(label)); Equal(1, rewritten[at].blocks.Count);
            Equal(original[at].operand, rewritten[at + 5].operand);
        });
        Check("batch2 actual host patch combination is exported with assembly identities", () =>
        {
            var targets = Harmony_SSCRomanceRandomSelection.TargetMethods().Concat(Harmony_SSCRomanceAcceptance.TargetMethods())
                .Append(AccessTools.Method(typeof(Pawn_InteractionsTracker), "TryInteractWith")).Append(Harmony_SSCRomanceRjwClick.TargetMethod())
                .Append(AccessTools.Method(typeof(RelationsUtility), "RomanceOption"));
            var entries = targets.Select(m => new { Method = m.DeclaringType.FullName + "." + m.Name, Patches = new[] { Harmony.GetPatchInfo(m).Prefixes, Harmony.GetPatchInfo(m).Postfixes,
                Harmony.GetPatchInfo(m).Transpilers, Harmony.GetPatchInfo(m).Finalizers }.SelectMany(p => p).Select(p => new { p.owner, p.priority, p.before, p.after, Method = p.PatchMethod.DeclaringType.FullName + "." + p.PatchMethod.Name }).ToArray() }).ToArray();
            string directory = Path.Combine(repo, ".builds/romance-batch2"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "HostPatchCombination.json"), JsonSerializer.Serialize(new { Scope = "isolated native host, not running game mod combination", GameMvid = typeof(Pawn).Module.ModuleVersionId,
                Harmony = typeof(Harmony).Assembly.FullName, Production = production.Location, Entries = entries }, new JsonSerializerOptions { WriteIndented = true }));
            Equal(8, entries.Length); Require(entries.All(e => e.Patches.Length > 0));
        });
    }
}
