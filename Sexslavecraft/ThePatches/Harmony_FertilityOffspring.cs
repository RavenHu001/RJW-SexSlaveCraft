// AI 辅助生成：局部生成调用包装，保留HAR选种及月经预生成取出补丁。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SexSlaveCraft
{
    /// <summary>以实际GeneratePawn调用为锚；不全局补丁PawnGenerator，不改遗传或胎数循环。</summary>
    internal static class SSCFertilityGenerationPatches
    {
        internal const string MenstruationType = "RJW_Menstruation.HediffComp_PregeneratedBabies";
        internal const string MenstruationBirthType = "RJW_Menstruation.ApplyBirthOutcome_PregeneratedBabies_Patch";
        private static readonly MethodInfo generate = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) });
        internal static bool MenstruationInstalled { get; private set; }

        /// <summary>原生出生观察和婴儿生成改写使用同一目标签名及亲代参数校验。</summary>
        internal static MethodInfo NativeTarget()
        {
            var method = AccessTools.DeclaredMethod(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome), new[]
            {
                typeof(RitualOutcomePossibility), typeof(float), typeof(Precept_Ritual), typeof(List<GeneDef>),
                typeof(Pawn), typeof(Thing), typeof(Pawn), typeof(Pawn), typeof(LordJob_Ritual),
                typeof(RitualRoleAssignments), typeof(bool)
            });
            return method?.IsStatic == true && method.ReturnType == typeof(Thing) &&
                HasArguments(method, (5, "birtherThing", typeof(Thing)), (6, "father", typeof(Pawn))) ? method : null;
        }

        /// <summary>成对安装预生成和即时回退两条入口。已有婴儿仍由月经模组的原方法取出。</summary>
        internal static void RegisterMenstruation(Harmony harmony)
        {
            MenstruationInstalled = false;
            MenstruationInstalled = SSCOptionalPatchGroup.Install(harmony, "RJW Menstruation", () =>
            {
                var preGeneration = AccessTools.TypeByName(MenstruationType);
                var birth = AccessTools.TypeByName(MenstruationBirthType);
                if (preGeneration == null && birth == null) return null;
                var add = SSCOptionalPatchGroup.RequireTarget(preGeneration, "AddNewBaby", typeof(void), false, typeof(Pawn), typeof(Pawn));
                var take = SSCOptionalPatchGroup.RequireTarget(birth, "GetPregeneratedBaby", typeof(Pawn), true, typeof(PawnGenerationRequest), typeof(Thing));
                // 两个亲代均为 Pawn。仅按类型无法发现换位，名字与位置不符时保留外部生成路径。
                if (!HasArguments(add, (0, "mother", typeof(Pawn)), (1, "father", typeof(Pawn))) ||
                    !HasArguments(take, (1, "birtherThing", typeof(Thing))))
                    throw new InvalidOperationException("Unexpected birth argument layout for AddNewBaby or GetPregeneratedBaby");
                return new[]
                {
                    (add, typeof(Harmony_FertilityMenstruationGeneration), HarmonyPatchType.Transpiler),
                    (take, typeof(Harmony_FertilityMenstruationFallback), HarmonyPatchType.Transpiler)
                };
            });
        }

        /// <summary>原请求压栈后添加本入口上下文，包装器内部只调用一次原生成；匹配失败保留整段原IL。</summary>
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, string wrapper,
            CodeInstruction[] arguments, bool allowPregeneratedReplacement = false, bool multiple = false)
        {
            var codes = instructions.ToList();
            MethodInfo replacement = AccessTools.DeclaredMethod(typeof(SSCFertilityGenerationPatches), wrapper);
            int count = generate == null ? 0 : codes.Count(c => c.Calls(generate));
            // 月经接管原版生成时，当前调用负责取出预生成婴儿。其即时生成分支由独立入口处理。
            if (count == 0 && allowPregeneratedReplacement && codes.Any(c => c.operand is MethodInfo method &&
                method.DeclaringType?.FullName == MenstruationBirthType && method.Name == "GetPregeneratedBaby")) return codes;
            if (replacement == null || count < 1 || (!multiple && count != 1))
            {
                Log.Error("[SSC] 婴儿生成接入失败：" + wrapper + "的GeneratePawn匹配数为" + count + "；保留原生成。");
                return codes;
            }
            var result = new List<CodeInstruction>();
            foreach (var code in codes)
            {
                if (!code.Calls(generate)) { result.Add(code); continue; }
                // 旧调用上的分支/异常块必须先进入新增参数加载，避免跳过栈参数。
                var loads = arguments.Select(c => new CodeInstruction(c)).ToArray();
                loads[0].MoveLabelsFrom(code).MoveBlocksFrom(code);
                result.AddRange(loads);
                result.Add(new CodeInstruction(OpCodes.Call, replacement));
            }
            return result;
        }

        /// <summary>安装前核对参数位置、名字和类型。确认语义后，Transpiler 才能使用对应的参数槽。</summary>
        private static bool HasArguments(MethodBase method, params (int Index, string Name, Type Type)[] expected)
        {
            var parameters = method.GetParameters();
            return expected.All(p => p.Index < parameters.Length && parameters[p.Index].Name == p.Name &&
                parameters[p.Index].ParameterType == p.Type);
        }

        /// <summary>真实怀胎Pawn提供身体设置；生长舱不借遗传母方资格套用这些设置。</summary>
        internal static Pawn GenerateNative(PawnGenerationRequest request, Thing birtherThing, Pawn father)
            => SSCFertilityOffspringUtility.Generate(request, birtherThing as Pawn, father);

        /// <summary>只适配RJW类人妊娠；动物和其他BasePregnancy派生路径保持原样。</summary>
        internal static Pawn GenerateRjw(PawnGenerationRequest request, rjw.Hediff_BasePregnancy pregnancy)
            => pregnancy is rjw.Hediff_HumanlikePregnancy
                ? SSCFertilityOffspringUtility.Generate(request, pregnancy.pawn, pregnancy.father)
                : PawnGenerator.GeneratePawn(request);

        /// <summary>月经真实预生成使用其现有亲代参数，其余前后置处理仍由原方法执行。</summary>
        /// <remarks>组安装失败时，残留包装器直接调用原生成，以原参数继续。</remarks>
        internal static Pawn GenerateMenstruation(PawnGenerationRequest request, Pawn mother, Pawn father)
            => MenstruationInstalled ? SSCFertilityOffspringUtility.Generate(request, mother, father) : PawnGenerator.GeneratePawn(request);

        /// <summary>仅月经内部即时生成回退调用；已有Pawn返回路径不经过这里，不追补。</summary>
        internal static Pawn GenerateFallback(PawnGenerationRequest request, Thing birtherThing)
            => MenstruationInstalled
                ? SSCFertilityOffspringUtility.Generate(request, birtherThing as Pawn, SSCFertilityBirthUtility.CurrentFather(birtherThing as Pawn))
                : PawnGenerator.GeneratePawn(request);
    }

    [HarmonyPatch]
    [HarmonyAfter("RJW_Menstruation", "rimworld.erdelf.alien_race.main")]
    internal static class Harmony_FertilityNativeGeneration
    {
        internal static MethodBase TargetMethod() => SSCFertilityGenerationPatches.NativeTarget();
        internal static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            Log.WarningOnce("[SSC Fertility] ApplyBirthOutcome signature or birth arguments changed. Keeping original birth.", 0x5353464E);
            return false;
        }

        /// <summary>月经已替换为取出已有婴儿时不覆盖调用，其回退由独立局部补丁接通。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateNative),
                new[] { CodeInstruction.LoadArgument(5), CodeInstruction.LoadArgument(6) }, true);
    }

    [HarmonyPatch]
    internal static class Harmony_FertilityRjwGeneration
    {
        internal static MethodBase TargetMethod()
        {
            var target = AccessTools.DeclaredMethod(typeof(rjw.Hediff_BasePregnancy), "GenerateBabies",
                new[] { typeof(rjw.Hediff_BasePregnancy.DnaGivingParent) });
            return target?.IsStatic == false && target.ReturnType == typeof(void) ? target : null;
        }

        internal static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            Log.WarningOnce("[SSC Fertility] RJW GenerateBabies signature changed. Keeping original generation.", 0x53534652);
            return false;
        }

        /// <summary>生成循环和遗传计算不动，仅向生成包装器传当前妊娠实例。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateRjw), new[] { new CodeInstruction(OpCodes.Ldarg_0) });
    }

    internal static class Harmony_FertilityMenstruationGeneration
    {
        // AddNewBaby 是实例方法，arg.0 为 this。已验证的两位亲代对应 arg.1 和 arg.2。
        /// <summary>只包裹AddNewBaby内部生成，不改月经原双胎或遗传处理。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateMenstruation),
                new[] { CodeInstruction.LoadArgument(1), CodeInstruction.LoadArgument(2) });
    }

    internal static class Harmony_FertilityMenstruationFallback
    {
        // GetPregeneratedBaby 是静态方法，arg.1 为实际分娩对象。只在即时生成调用处追加该参数。
        /// <summary>只包裹取出器的即时生成分支，已有婴儿返回分支保持不变。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateFallback),
                new[] { CodeInstruction.LoadArgument(1) }, multiple: true);
    }
}
