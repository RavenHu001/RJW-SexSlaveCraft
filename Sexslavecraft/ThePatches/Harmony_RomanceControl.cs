// AI 辅助生成
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
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Harmony_SSCRomanceInteraction
    {
        /// <summary>在原方法的思想、经验、worker、冷却与日志前中止主动禁止；RJW 和原版 Toil 同样经过此处。</summary>
        public static bool Prefix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, ref bool __result, out SSCRomanceExecutionScope.Invocation __state)
        {
            __state = SSCRomanceExecutionScope.Enter(___pawn, recipient, intDef);
            if (SSCRomanceInteractionUtility.CanEnter(___pawn, recipient, intDef)) return true;
            __result = false;
            return false;
        }

        /// <summary>原版和外部补丁异常也释放调用帧；无授权调用及重入不会继承外层票据。</summary>
        public static void Finalizer(SSCRomanceExecutionScope.Invocation __state) => SSCRomanceExecutionScope.Exit(__state);

        /// <summary>在原版可交互性／冷却检查返回后、首次思想或经验前再次检查，堵住外部回调改变规则或关系的窗口。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            var constructor = AccessTools.Constructor(typeof(List<RulePackDef>), Type.EmptyTypes);
            var thought = AccessTools.Field(typeof(InteractionDef), nameof(InteractionDef.initiatorThought));
            var matches = new List<int>();
            // 锚定原生额外语句列表与紧随其后的首个思想检查，不绑定偏移、标签编号或局部变量编号。
            // 当前目标方法没有异常区；包含异常区的外部改写不插入 ret，避免生成非法跨区返回。
            for (int i = 0; i + 3 < codes.Count; i++)
                if (codes[i].opcode == OpCodes.Newobj && Equals(codes[i].operand, constructor) && codes[i + 1].IsStloc() &&
                    codes[i + 2].opcode == OpCodes.Ldarg_2 && codes[i + 3].LoadsField(thought)) matches.Add(i);
            SSCRomanceCommandHooks.common = matches.Count == 1 && !codes.Any(c => c.blocks.Count != 0);
            if (!SSCRomanceCommandHooks.common)
            {
                SSCLog.WarningImportant("[SSC Romance] 共同入口副作用前锚点不匹配，未安装末次复查；入口前缀仍保留。");
                return codes;
            }
            int at = matches[0];
            var begin = new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codes[at]);
            var proceed = generator.DefineLabel(); codes[at].labels.Add(proceed);
            // 原冷却检查有两条分支直达此处；移动入边标签，确保分支不能绕过新增检查。
            // 拒绝直接返回 false，不伪造失败记忆，也不进入原 worker 或更新互动时间。
            codes.InsertRange(at, new[] { begin,
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn")),
                new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SSCRomanceExecutionScope), nameof(SSCRomanceExecutionScope.BeforeEffects))),
                new CodeInstruction(OpCodes.Brtrue, proceed), new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret) });
            return codes;
        }
    }

    [HarmonyPatch]
    public static class Harmony_SSCRomanceRandomSelection
    {
        /// <summary>精确登记三个原 worker；不修改普通聊天、性行为或其他模组自定义浪漫行为。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(InteractionWorker_RomanceAttempt), typeof(InteractionWorker_MarriageProposal), typeof(InteractionWorker_Breakup) })
                yield return AccessTools.DeclaredMethod(type, "RandomSelectionWeight", new[] { typeof(Pawn), typeof(Pawn) });
        }

        /// <summary>主动不准入时不参加随机选择；被动规则不参与过滤，也不把原版为零的权重提高。</summary>
        public static bool Prefix(Pawn initiator, Pawn recipient, MethodBase __originalMethod, ref float __result)
        {
            var action = ActionFor(__originalMethod.DeclaringType);
            if (SSCRomanceInteractionUtility.CanStart(initiator, recipient, action)) return true;
            __result = 0f;
            return false;
        }

        /// <summary>目标来自固定登记表，按真实声明类型还原方向明确的行为。</summary>
        internal static SSCRomanceAction ActionFor(Type type) => type == typeof(InteractionWorker_RomanceAttempt) ? SSCRomanceAction.Romance :
            type == typeof(InteractionWorker_MarriageProposal) ? SSCRomanceAction.Proposal : SSCRomanceAction.Breakup;
    }

    [HarmonyPatch]
    public static class Harmony_SSCRomanceAcceptance
    {
        /// <summary>只接入两个原版接受分支；分手没有接收判定，不能伪造接受分手。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(InteractionWorker_RomanceAttempt), "Interacted");
            yield return AccessTools.DeclaredMethod(typeof(InteractionWorker_MarriageProposal), "Interacted");
        }

        /// <summary>以真实概率调用及相邻比较匹配唯一锚点；不覆盖任何完整关系结算，也不触碰拒婚 40% 比较。</summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var codes = instructions.Select(c => new CodeInstruction(c)).ToList();
            bool romance = __originalMethod.DeclaringType == typeof(InteractionWorker_RomanceAttempt);
            var chance = romance ? AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "SuccessChance", new[] { typeof(Pawn), typeof(Pawn), typeof(float) }) :
                AccessTools.Method(typeof(InteractionWorker_MarriageProposal), "AcceptanceChance", new[] { typeof(Pawn), typeof(Pawn) });
            int[] calls = codes.Select((c, i) => c.Calls(chance) ? i : -1).Where(i => i >= 0).ToArray();
            int index = calls.Length == 1 ? calls[0] + (romance ? 1 : 4) : -1;
            bool matched = index >= 0 && index < codes.Count && (romance ?
                codes[index].opcode == OpCodes.Bge_Un || codes[index].opcode == OpCodes.Bge_Un_S :
                codes[index].opcode == OpCodes.Clt && codes[calls[0] + 1].opcode == OpCodes.Stloc_0 &&
                codes[calls[0] + 2].Calls(AccessTools.PropertyGetter(typeof(Rand), "Value")) && codes[calls[0] + 3].opcode == OpCodes.Ldloc_0);
            if (romance) SSCRomanceCommandHooks.romance = matched; else SSCRomanceCommandHooks.proposal = matched;
            if (!matched)
            {
                // 返回原指令，避免猜改其他判定；明确留下本分支未覆盖的诊断，不能当作完成安装。
                SSCLog.WarningImportant("[SSC Romance] 接受判定锚点不匹配，未安装覆盖：" + __originalMethod.DeclaringType.FullName);
                return codes;
            }
            var tail = new List<CodeInstruction>();
            if (romance)
            {
                // bge.un 失败跳转改为 clt + 同一失败目标；NaN 时同样失败，标签与异常边界随原比较保留。
                var branch = codes[index];
                codes[index] = new CodeInstruction(branch) { opcode = OpCodes.Clt, operand = null };
                tail.Add(new CodeInstruction(OpCodes.Brfalse, branch.operand));
            }
            // 此时栈顶是原版接受 bool；助手不额外抽随机数，只覆盖受控接收者的本次决定。
            var added = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldc_I4, (int)(romance ? SSCRomanceAction.Romance : SSCRomanceAction.Proposal)),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SSCRomanceAcceptanceUtility), nameof(SSCRomanceAcceptanceUtility.Resolve))) };
            added.AddRange(tail);
            codes.InsertRange(index + 1, added);
            return codes;
        }
    }

    [HarmonyPatch(typeof(RelationsUtility), nameof(RelationsUtility.RomanceOption))]
    public static class Harmony_SSCRomanceMenu
    {
        /// <summary>原版资格仍成立才增加主动禁用原因；被动必拒不隐藏、不禁用目标，不改原生概率资格函数。</summary>
        public static void Postfix(Pawn initiator, Pawn romanceTarget, ref FloatMenuOption option, ref float chance, ref bool __result)
        {
            if (!__result || option == null) return;
            string rejection = SSCRomanceInteractionUtility.Rejection(initiator, romanceTarget, SSCRomanceAction.Romance);
            if (rejection != null)
            {
                option = new FloatMenuOption(romanceTarget.LabelShort + " (" + rejection + ")", null);
                chance = 0f;
                __result = false;
                return;
            }
            // 显示当前保证／必拒结果，但保留原可点击动作；这里的 0 不回流原版资格函数。
            var reception = SSCRomancePolicy.Evaluate(initiator, romanceTarget, SSCRomanceAction.Romance).Reception;
            if (reception == SSCRomanceReception.ForceAccept || reception == SSCRomanceReception.ForceReject)
            {
                chance = reception == SSCRomanceReception.ForceAccept ? 1f : 0f;
                option.Label = "SSC_Romance_AcceptancePreview".Translate(romanceTarget.LabelShort, chance.ToStringPercent());
            }
        }
    }

    [HarmonyPatch]
    public static class Harmony_SSCRomanceRjwClick
    {
        /// <summary>RJW 的菜单类型为 internal，按完整类型和签名定位；版本变化不猜测其他入口。</summary>
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("rjw.RMB_Socialize");
            var method = type == null ? null : AccessTools.DeclaredMethod(type, "Socialize", new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(InteractionDef) });
            return method != null && method.IsStatic && method.ReturnType == typeof(void) ? method : null;
        }

        /// <summary>已安装依赖的签名不符时明确报告未覆盖，避免一个菜单适配使整个 PatchAll 中断。</summary>
        public static bool Prepare()
        {
            if (TargetMethod() != null) return true;
            SSCLog.WarningImportant("[SSC Romance] RJW Socialize 签名不匹配，未安装右键反馈适配；共同入口仍保留执行复查。");
            return false;
        }

        /// <summary>右键点击时重新检查并提示主动／关系拒绝；旧菜单不获豁免，普通聊天仍按 RJW 执行。</summary>
        public static bool Prefix(Pawn pawn, LocalTargetInfo target, InteractionDef interaction)
        {
            if (!SSCRomanceInteractionUtility.TryGetAction(interaction, out var action)) return true;
            string rejection = SSCRomanceInteractionUtility.Rejection(pawn, target.Pawn, action);
            if (rejection == null) return true;
            Messages.Message(rejection, pawn, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}
