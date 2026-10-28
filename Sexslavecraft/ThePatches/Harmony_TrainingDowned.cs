// AI 辅助生成
#if SSC_RW17
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>整体安装两处局部倒地适配；锚点不匹配时返回原代码并关闭全部新增保护。</summary>
    internal static class SSCTrainingDownedPatches
    {
        private const string Owner = "SexSlaveCraft.training.downed";
        private static bool installed, healthMatched, abortMatched, disabled;
        internal static bool Ready { get; private set; }

        /// <summary>使用当前游戏及RJW实际成员，全部安装成功后才开放业务桥；不影响冻结版。</summary>
        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var harmony = new Harmony(Owner);
            MethodInfo health = AccessTools.DeclaredMethod(typeof(Pawn_HealthTracker), "MakeDowned",
                new[] { typeof(DamageInfo?), typeof(Hediff) });
            MethodInfo abort = AccessTools.DeclaredMethod(typeof(JobDriver_Sex), nameof(JobDriver_Sex.ConditionsToAbortSex), Type.EmptyTypes);
            try
            {
                if (health == null || abort == null) throw new MissingMethodException("training downed targets");
                harmony.Patch(health, transpiler: new HarmonyMethod(typeof(SSCTrainingDownedPatches), nameof(HealthTranspiler)) { priority = Priority.Last });
                harmony.Patch(abort, transpiler: new HarmonyMethod(typeof(SSCTrainingDownedPatches), nameof(AbortTranspiler)) { priority = Priority.Last });
                if (!healthMatched || !abortMatched || disabled) throw new InvalidOperationException("required anchors unavailable");
                Ready = true;
            }
            catch (Exception error)
            {
                Ready = false;
                if (health != null) harmony.Unpatch(health, HarmonyPatchType.All, Owner);
                if (abort != null) harmony.Unpatch(abort, HarmonyPatchType.All, Owner);
                Disable("installation: " + error.Message);
            }
        }

        /// <summary>第三方重新生成包装后若原语义锚点失效，也不能继续保留半套保护。</summary>
        private static void Disable(string reason)
        {
            Ready = false;
            if (!disabled) Log.Warning("[SSC TrainingDowned] Disabled; original rules retained: " + reason);
            disabled = true;
        }

        /// <summary>只替换MakeDowned内部一个职责查询；原ClearMind分支和其他健康／Lord通知保持原样。</summary>
        private static IEnumerable<CodeInstruction> HealthTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = instructions.ToList();
            try
            {
                var code = original.Select(c => new CodeInstruction(c)).ToList();
                var duty = AccessTools.DeclaredMethod(typeof(PawnUtility), nameof(PawnUtility.DutyActiveWhenDown), new[] { typeof(Pawn), typeof(bool) });
                int[] positions = code.Select((c, i) => new { c, i }).Where(x => x.c.Calls(duty)).Select(x => x.i).ToArray();
                if (positions.Length != 1 || positions[0] + 1 >= code.Count ||
                    (code[positions[0] + 1].opcode != OpCodes.Brtrue && code[positions[0] + 1].opcode != OpCodes.Brtrue_S))
                    throw new InvalidOperationException("MakeDowned duty branch");
                code[positions[0]].opcode = OpCodes.Call;
                code[positions[0]].operand = AccessTools.DeclaredMethod(typeof(SSCTrainingDownedUtility), nameof(SSCTrainingDownedUtility.KeepDutyWhenDowned));
                healthMatched = true;
                return code;
            }
            catch (Exception error) { Disable(error.Message); return original; }
        }

        /// <summary>只修改两次初始Downed快照读取，闭包、死亡和器官变化条件继续由RJW生成。</summary>
        private static IEnumerable<CodeInstruction> AbortTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = instructions.ToList();
            try
            {
                var code = original.Select(c => new CodeInstruction(c)).ToList();
                var downed = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Downed));
                int[] positions = code.Select((c, i) => new { c, i }).Where(x => x.c.Calls(downed)).Select(x => x.i).ToArray();
                if (positions.Length != 2 || positions.Any(i => i + 1 >= code.Count || code[i + 1].opcode != OpCodes.Stfld ||
                    !(code[i + 1].operand is FieldInfo field) || field.FieldType != typeof(bool)))
                    throw new InvalidOperationException("RJW initial Downed snapshots");
                for (int n = positions.Length - 1; n >= 0; n--)
                {
                    int index = positions[n];
                    var owner = new CodeInstruction(OpCodes.Ldarg_0);
                    owner.labels.AddRange(code[index].labels);
                    code[index].labels.Clear();
                    code.Insert(index, owner);
                    code[index + 1].opcode = OpCodes.Call;
                    code[index + 1].operand = AccessTools.DeclaredMethod(typeof(SSCTrainingDownedUtility), nameof(SSCTrainingDownedUtility.InitialDownedForAbort));
                }
                abortMatched = true;
                return code;
            }
            catch (Exception error) { Disable(error.Message); return original; }
        }
    }
}
#endif
