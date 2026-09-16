using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只在床位分配的姓名区域绘制标记，不修改全局 Pawn 名称或其他建筑的界面。</summary>
    [HarmonyPatch]
    public static class Harmony_SSC_SharedBedAssignmentUI
    {
        /// <summary>为原版窗口的已分配行和未分配行注册相同的姓名绘制补丁。</summary>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DrawAssignedRow");
            yield return AccessTools.Method(typeof(Dialog_AssignBuildingOwner), "DrawUnassignedRow");
        }

        /// <summary>将原版省略姓名的绘制调用替换为带标记的绘制入口，保留其余行逻辑。</summary>
        /// <param name="instructions">Harmony 提供的原始行绘制指令。</param>
        /// <remarks>补入当前 Pawn 和分配组件，并将跳转标签、异常块边界移到替换序列的入口。</remarks>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.LabelEllipses), new[] { typeof(Rect), typeof(string) });
            MethodInfo replacement = AccessTools.Method(typeof(Harmony_SSC_SharedBedAssignmentUI), nameof(DrawPawnLabel));
            FieldInfo assignable = AccessTools.Field(typeof(Dialog_AssignBuildingOwner), "assignable");
            foreach (CodeInstruction instruction in instructions)
            {
                if (!instruction.Calls(label))
                {
                    yield return instruction;
                    continue;
                }
                yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, assignable);
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
        }

        /// <summary>为床位列表中的 SSC 性奴绘制标记、悬停说明和姓名，并恢复调用方的绘制状态。</summary>
        /// <param name="rect">标记与姓名共同使用的区域；调用方负责预留头像和按钮位置。</param>
        /// <param name="label">原界面提供的姓名文本，可包含不可分配原因。</param>
        /// <param name="pawn">当前行角色，用于判断 SSC 身份并生成提示。</param>
        /// <param name="assignable">分配组件；其他建筑或非性奴角色使用原来的姓名绘制。</param>
        public static void DrawPawnLabel(Rect rect, string label, Pawn pawn, CompAssignableToPawn assignable)
        {
            if (!(assignable is CompAssignableToPawn_Bed) || !SSCIdentityUtility.IsSexSlave(pawn))
            {
                Widgets.LabelEllipses(rect, label);
                return;
            }

            string badge = "SSC_SharedBedBadge".Translate();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Rect badgeRect;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                float width = Mathf.Min(Text.CalcSize(badge).x + 8f, rect.width * 0.45f);
                badgeRect = new Rect(rect.x, rect.y + (rect.height - 22f) * 0.5f, width, 22f);
                Widgets.DrawBoxSolid(badgeRect, new Color(0.65f, 0.3f, 0.6f, 0.25f));
                GUI.color = new Color(1f, 0.65f, 0.83f, oldColor.a);
                Widgets.Label(badgeRect, badge);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }

            TooltipHandler.TipRegion(badgeRect, GetTooltip(pawn));
            rect.xMin = badgeRect.xMax + 5f;
            Widgets.LabelEllipses(rect, label);
        }

        /// <summary>根据当前绑定主人或指定调教员、恶堕门槛生成本地化提示，不执行床位分配。</summary>
        /// <param name="pawn">需要显示同床规则的角色。</param>
        /// <returns>包含对象和门槛状态的完整说明；无有效对象时显示对应提示。</returns>
        public static string GetTooltip(Pawn pawn)
        {
            string target = "SSC_SharedBedNoPartner".Translate();
            if (SSCSharedBedUtility.TryGetPartner(pawn, out Pawn partner, out bool isMaster))
                target = (isMaster ? "SSC_SharedBedMaster" : "SSC_SharedBedTrainer").Translate(partner.LabelShortCap);
            string threshold = SSCSharedBedUtility.HasMeaningfulCorruption(pawn)
                ? "SSC_SharedBedThresholdMet".Translate().ToString()
                : "SSC_SharedBedThresholdUnmet".Translate().ToString();
            return "SSC_SharedBedTooltip".Translate(target, threshold);
        }
    }

    /// <summary>Dubs Mint Menus 使用自己的角色行；反射接入，不将它变成必需依赖。</summary>
    [HarmonyPatch]
    public static class Harmony_SSC_MintSharedBedAssignmentUI
    {
        /// <summary>仅在找到兼容的 Mint 行方法时启用补丁，避免未安装 Mint 时 Harmony 因目标为空报错。</summary>
        public static bool Prepare() => TargetMethods().Any();

        /// <summary>通过类型名、方法签名和组件字段类型定位 Mint 的角色行，不引入程序集硬依赖。</summary>
        /// <returns>兼容的 DoRow 方法；Mint 未加载或接口不匹配时返回空序列。</returns>
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("DubsMintMenus.Dialog_AssignBuildingOwner");
            if (type == null) yield break;
            var method = AccessTools.Method(type, "DoRow", new[] { typeof(Rect), typeof(Pawn), typeof(bool) });
            var comp = AccessTools.Field(type, "comp");
            if (method != null && comp?.FieldType == typeof(CompAssignableToPawn)) yield return method;
        }

        /// <summary>替换 Mint 两个分支中的姓名绘制，保留按钮、分配行为和独立的意识形态说明。</summary>
        /// <param name="instructions">Mint DoRow 的原始指令，用于确认两个 string Label 调用仍然存在。</param>
        /// <param name="__originalMethod">实际补丁目标，用于读取其所属窗口的 comp 字段。</param>
        /// <returns>注入 Pawn、组件和整行区域后的指令；调用数量不匹配时告警并返回原指令。</returns>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var codes = instructions.ToList();
            MethodInfo label = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) });
            // 1.6 的两个 string Label 分别绘制已分配/未分配角色；意识形态说明使用 TaggedString 重载。
            if (codes.Count(code => code.Calls(label)) != 2)
            {
                Log.Warning("[SexSlaveCraft] Dubs Mint Menus 角色行结构已变化，未添加同床标记。请更新兼容补丁。");
                return codes;
            }
            MethodInfo replacement = AccessTools.Method(typeof(Harmony_SSC_MintSharedBedAssignmentUI), nameof(DrawPawnLabel));
            FieldInfo comp = AccessTools.Field(__originalMethod.DeclaringType, "comp");
            var result = new List<CodeInstruction>();
            foreach (var instruction in codes)
            {
                if (!instruction.Calls(label))
                {
                    result.Add(instruction);
                    continue;
                }
                result.Add(new CodeInstruction(OpCodes.Ldarg_2).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
                result.Add(new CodeInstruction(OpCodes.Ldarg_0));
                result.Add(new CodeInstruction(OpCodes.Ldfld, comp));
                result.Add(new CodeInstruction(OpCodes.Ldarg_1));
                result.Add(new CodeInstruction(OpCodes.Call, replacement));
            }
            return result;
        }

        /// <summary>为 Mint 床位姓名预留右侧按钮空间，再复用原版窗口的性奴标记绘制。</summary>
        /// <param name="rect">Mint 原始姓名区域，可能延伸到按钮下方。</param>
        /// <param name="label">Mint 生成的姓名及可选拒绝原因，完整传给共享绘制函数。</param>
        /// <param name="pawn">当前行角色。</param>
        /// <param name="comp">当前分配组件；非床位或非性奴直接使用 Mint 原本的 Label 调用。</param>
        /// <param name="rowRect">整行区域，用于计算右侧 170px 按钮及 5px 间距的位置。</param>
        public static void DrawPawnLabel(Rect rect, string label, Pawn pawn, CompAssignableToPawn comp, Rect rowRect)
        {
            if (!(comp is CompAssignableToPawn_Bed) || !SSCIdentityUtility.IsSexSlave(pawn))
            {
                Widgets.Label(rect, label);
                return;
            }
            // Mint 的姓名区域延伸至整行右端；预留它原有的 170px 按钮及 5px 间距。
            rect.width = Mathf.Max(0f, Mathf.Min(rect.width, rowRect.xMax - 175f - rect.x));
            Harmony_SSC_SharedBedAssignmentUI.DrawPawnLabel(rect, label, pawn, comp);
        }
    }
}
