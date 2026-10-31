// AI 辅助生成：局部生成调用包装，保留HAR选种及月经预生成取出补丁。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>以实际GeneratePawn调用为锚；不全局补丁PawnGenerator，不改遗传或胎数循环。</summary>
    internal static class SSCFertilityGenerationPatches
    {
        internal const string MenstruationType = "RJW_Menstruation.HediffComp_PregeneratedBabies";
        internal const string MenstruationBirthType = "RJW_Menstruation.ApplyBirthOutcome_PregeneratedBabies_Patch";
        private static readonly MethodInfo generate = AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) });

        /// <summary>原请求压栈后添加本入口上下文，包装器内部只调用一次原生成；匹配失败保留整段原IL。</summary>
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, string wrapper,
            CodeInstruction[] arguments, bool allowPregeneratedReplacement = false, bool multiple = false)
        {
            var codes = instructions.ToList();
            int count = codes.Count(c => c.Calls(generate));
            if (count == 0 && allowPregeneratedReplacement && codes.Any(c => c.operand is MethodInfo method &&
                method.DeclaringType?.FullName == MenstruationBirthType && method.Name == "GetPregeneratedBaby")) return codes;
            if (count < 1 || (!multiple && count != 1))
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
                result.Add(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SSCFertilityGenerationPatches), wrapper)));
            }
            return result;
        }

        /// <summary>按已核对的参数名加载，不把局部变量编号或IL偏移写成兼容契约。</summary>
        internal static CodeInstruction Argument(MethodBase method, string name)
        {
            int index = Array.FindIndex(method.GetParameters(), p => p.Name == name);
            if (index < 0) throw new InvalidOperationException("[SSC] Missing birth argument: " + name);
            return CodeInstruction.LoadArgument(index + (method.IsStatic ? 0 : 1));
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
        internal static Pawn GenerateMenstruation(PawnGenerationRequest request, Pawn mother, Pawn father)
            => SSCFertilityOffspringUtility.Generate(request, mother, father);

        /// <summary>仅月经内部即时生成回退调用；已有Pawn返回路径不经过这里，不追补。</summary>
        internal static Pawn GenerateFallback(PawnGenerationRequest request, Thing birtherThing)
            => SSCFertilityOffspringUtility.Generate(request, birtherThing as Pawn,
                SSCFertilityBirthUtility.CurrentFather(birtherThing as Pawn));
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    [HarmonyAfter("RJW_Menstruation", "rimworld.erdelf.alien_race.main")]
    internal static class Harmony_FertilityNativeGeneration
    {
        /// <summary>月经已替换为取出已有婴儿时不覆盖调用，其回退由独立局部补丁接通。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateNative),
                new[] { SSCFertilityGenerationPatches.Argument(original, "birtherThing"), SSCFertilityGenerationPatches.Argument(original, "father") }, true);
    }

    [HarmonyPatch(typeof(rjw.Hediff_BasePregnancy), "GenerateBabies")]
    internal static class Harmony_FertilityRjwGeneration
    {
        /// <summary>生成循环和遗传计算不动，仅向生成包装器传当前妊娠实例。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateRjw), new[] { new CodeInstruction(OpCodes.Ldarg_0) });
    }

    [HarmonyPatch]
    internal static class Harmony_FertilityMenstruationGeneration
    {
        /// <summary>按可选类型和已核对签名查找；未安装月经时不发空类型查询警告。</summary>
        internal static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(SSCFertilityGenerationPatches.MenstruationType);
            return type == null ? null : AccessTools.Method(type, "AddNewBaby", new[] { typeof(Pawn), typeof(Pawn) });
        }
        /// <summary>可选依赖不存在时跳过安装，不将其变成硬依赖。</summary>
        internal static bool Prepare() => TargetMethod() != null;
        /// <summary>只包裹AddNewBaby内部生成，不改月经原双胎或遗传处理。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateMenstruation),
                new[] { SSCFertilityGenerationPatches.Argument(original, "mother"), SSCFertilityGenerationPatches.Argument(original, "father") });
    }

    [HarmonyPatch]
    internal static class Harmony_FertilityMenstruationFallback
    {
        /// <summary>可选月经出生取出器的真实方法；没有该类型则不安装。</summary>
        internal static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(SSCFertilityGenerationPatches.MenstruationBirthType);
            return type == null ? null : AccessTools.Method(type, "GetPregeneratedBaby", new[] { typeof(PawnGenerationRequest), typeof(Thing) });
        }
        /// <summary>缺依赖时跳过，不添加空目标补丁。</summary>
        internal static bool Prepare() => TargetMethod() != null;
        /// <summary>只包裹取出器的即时生成分支，已有婴儿返回分支保持不变。</summary>
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            => SSCFertilityGenerationPatches.Rewrite(instructions, nameof(SSCFertilityGenerationPatches.GenerateFallback),
                new[] { SSCFertilityGenerationPatches.Argument(original, "birtherThing") }, multiple: true);
    }
}
