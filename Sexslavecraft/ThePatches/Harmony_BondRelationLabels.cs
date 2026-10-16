// AI 辅助生成
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;

namespace SexSlaveCraft
{
    /// <summary>仅在含查看者上下文的显示入口替换称谓；关系数据、排序及其他关系不变。</summary>
    [HarmonyPatch(typeof(SocialCardUtility), "GetRelationsString")]
    public static class Harmony_BondRelationLabels
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => Rewrite(instructions, false);

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, bool tracker)
        {
            // 社交列表同时使用普通名称和首字母大写名称；好感说明使用后者。
            // 只替换这两种调用，保留原版遍历、排版、排序和其他显示逻辑。
            var plain = AccessTools.Method(typeof(PawnRelationDef), nameof(PawnRelationDef.GetGenderSpecificLabel));
            var cap = AccessTools.Method(typeof(PawnRelationDef), nameof(PawnRelationDef.GetGenderSpecificLabelCap));
            foreach (var instruction in instructions)
            {
                bool isCap = instruction.Calls(cap);
                if (isCap || instruction.Calls(plain))
                {
                    // 原调用栈已有关系定义和对方 Pawn，再补入查看者作为第三个参数。
                    // 列表查看者是方法参数 1；好感说明查看者保存在实例的私有 pawn 字段中。
                    var load = new CodeInstruction(tracker ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1);
                    // 分支必须先执行新增的参数装载，故原调用的跳转标签及块边界一并前移。
                    load.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    load.blocks.AddRange(instruction.blocks);
                    instruction.blocks.Clear();
                    yield return load;
                    if (tracker) yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_RelationsTracker), "pawn"));
                    // 静态包装器按实际锁链命名本模组关系，其他关系仍调用原版对应入口。
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(SSCBondRelationLabels),
                        isCap ? nameof(SSCBondRelationLabels.GetLabelCap) : nameof(SSCBondRelationLabels.GetLabel));
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.OpinionExplanation))]
    public static class Harmony_BondRelationOpinionLabels
    {
        // 与社交列表共用同一方向规则，避免悬停的 +100 来源仍显示旧的性别称谓。
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => Harmony_BondRelationLabels.Rewrite(instructions, true);
    }
}
