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
    [System.Flags]
    public enum SSCSharedBedBadge
    {
        None = 0,
        SexSlave = 1,
        Master = 2,
        Trainer = 4
    }

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

        /// <summary>按当前床的分配关系绘制身份标记、说明和姓名，并恢复调用方的绘制状态。</summary>
        /// <param name="rect">标记与姓名共同使用的区域；调用方负责预留头像和按钮位置。</param>
        /// <param name="label">原界面提供的姓名文本，可包含不可分配原因。</param>
        /// <param name="pawn">当前行角色，用于判断 SSC 身份并生成提示。</param>
        /// <param name="assignable">分配组件；其他建筑或不显示标记的角色使用原来的姓名绘制。</param>
        public static void DrawPawnLabel(Rect rect, string label, Pawn pawn, CompAssignableToPawn assignable)
        {
            Building_Bed bed = (assignable as CompAssignableToPawn_Bed)?.parent as Building_Bed;
            SSCSharedBedBadge roles = GetAssignmentBadge(pawn, bed);
            if (roles == SSCSharedBedBadge.None)
            {
                Widgets.LabelEllipses(rect, label);
                return;
            }

            var badges = GetIndividualBadges(roles).ToArray();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Rect groupRect = rect;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                float[] widths = badges.Select(role => Text.CalcSize(GetBadgeLabel(role)).x + 8f).ToArray();
                // 最多占姓名区域的 55%，为姓名保留原起点；长翻译按比例压缩，并通过提示保留全文。
                float fixedWidth = badges.Length * 4f + 7f;
                float groupWidth = Mathf.Min(widths.Sum() + fixedWidth, Mathf.Max(0f, rect.width * 0.55f));
                float scale = Mathf.Min(1f, Mathf.Max(0f, groupWidth - fixedWidth) / widths.Sum());
                groupRect = new Rect(rect.xMax - groupWidth, rect.y, groupWidth, rect.height);
                float x = groupRect.x;
                for (int i = 0; i < badges.Length && scale > 0f; i++)
                {
                    Color color = GetBadgeColor(badges[i]);
                    Rect badgeRect = new Rect(x, rect.y + (rect.height - 20f) * 0.5f, widths[i] * scale, 20f);
                    Widgets.DrawBoxSolid(badgeRect, new Color(color.r, color.g, color.b, 0.22f));
                    GUI.color = new Color(color.r, color.g, color.b, oldColor.a);
                    Widgets.LabelEllipses(badgeRect, GetBadgeLabel(badges[i]));
                    x = badgeRect.xMax + 4f;
                }
                GUI.color = oldColor;
                if (groupWidth >= fixedWidth)
                    Widgets.DrawBoxSolid(new Rect(groupRect.xMax - 7f, rect.y + (rect.height - 7f) * 0.5f, 7f, 7f),
                        GetStatusColor(GetSharingStatus(pawn, bed)));
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }

            rect.width = Mathf.Max(0f, groupRect.x - 5f - rect.x);
            Widgets.LabelEllipses(rect, label);
            // 原版拒绝原因也包含在 label 中；省略后仍可完整查看。
            TooltipHandler.TipRegion(rect, label);
            TooltipHandler.TipRegion(groupRect, GetTooltip(pawn, bed));
        }

        /// <summary>空床显示全部身份；有人时仅标记已分配者及其直接 SSC 床伴，保留这些人的全部实际身份。</summary>
        /// <remarks>多人床取直接关系并集，不递归扩散；该结果只控制标签，不过滤角色行或强制拒绝分配。</remarks>
        public static SSCSharedBedBadge GetAssignmentBadge(Pawn pawn, Building_Bed bed)
        {
            if (pawn == null || bed == null) return SSCSharedBedBadge.None;
            var owners = bed.OwnersForReading;
            if (owners.Count > 0 && !owners.Contains(pawn)
                && !owners.Any(owner => SSCSharedBedUtility.HasSharedBedRelation(pawn, owner))) return SSCSharedBedBadge.None;
            SSCSharedBedBadge roles = SSCSharedBedBadge.None;
            if (SSCIdentityUtility.IsSexSlave(pawn)) roles |= SSCSharedBedBadge.SexSlave;
            if (SSCIdentityUtility.IsMaster(pawn)) roles |= SSCSharedBedBadge.Master;
            if (SSCIdentityUtility.IsTrainer(pawn)) roles |= SSCSharedBedBadge.Trainer;
            return roles;
        }

        /// <summary>按固定顺序枚举独立身份；兼任者分别绘制，不将多个身份拼进一个色块。</summary>
        public static IEnumerable<SSCSharedBedBadge> GetIndividualBadges(SSCSharedBedBadge roles)
        {
            if ((roles & SSCSharedBedBadge.Master) != 0) yield return SSCSharedBedBadge.Master;
            if ((roles & SSCSharedBedBadge.SexSlave) != 0) yield return SSCSharedBedBadge.SexSlave;
            if ((roles & SSCSharedBedBadge.Trainer) != 0) yield return SSCSharedBedBadge.Trainer;
        }

        /// <summary>返回身份的简短本地化名称；组合名称仅用于悬停标题。</summary>
        public static string GetBadgeLabel(SSCSharedBedBadge roles)
        {
            if (roles == SSCSharedBedBadge.Master) return "SSC_SharedBedMasterBadge".Translate();
            if (roles == SSCSharedBedBadge.SexSlave) return "SSC_SharedBedBadge".Translate();
            if (roles == SSCSharedBedBadge.Trainer) return "SSC_SharedBedTrainerBadge".Translate();
            return string.Join(" · ", GetIndividualBadges(roles).Select(GetBadgeLabel));
        }

        /// <summary>用金色、紫粉色、青蓝色分别区分主人、性奴和调教员，不以身份颜色表达许可状态。</summary>
        public static Color GetBadgeColor(SSCSharedBedBadge role)
        {
            if (role == SSCSharedBedBadge.Master) return new Color(1f, 0.8f, 0.35f, 1f);
            if (role == SSCSharedBedBadge.Trainer) return new Color(0.35f, 0.8f, 1f, 1f);
            return new Color(1f, 0.65f, 0.83f, 1f);
        }

        /// <summary>用独立状态标记区分有效许可、等待床伴/保留床位和当前无额外许可。</summary>
        public static Color GetStatusColor(SSCSharedBedStatus status)
        {
            if (status == SSCSharedBedStatus.Permitted) return new Color(0.4f, 0.9f, 0.45f, 1f);
            if (status == SSCSharedBedStatus.Waiting || status == SSCSharedBedStatus.Retained)
                return new Color(1f, 0.75f, 0.25f, 1f);
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        /// <summary>复用实际 SSC 许可；空床只表示等待床伴，已有归属例外不冒充有效床伴关系。</summary>
        public static SSCSharedBedStatus GetSharingStatus(Pawn pawn, Building_Bed bed)
        {
            if (SSCSharedBedUtility.HasPartnerBedPermission(bed, pawn)) return SSCSharedBedStatus.Permitted;
            if (SSCSharedBedUtility.HasAssignedBedPermission(bed, pawn)) return SSCSharedBedStatus.Retained;
            if (!SSCSharedBedUtility.IsOrdinarySharedBed(bed) || pawn == null || pawn.IsPrisoner)
                return SSCSharedBedStatus.Unavailable;
            if (bed.OwnersForReading.Count == 0 && (SSCIdentityUtility.IsMaster(pawn) || SSCIdentityUtility.IsTrainer(pawn)
                || (SSCSharedBedUtility.HasMeaningfulCorruption(pawn) && SSCSharedBedUtility.GetAllowedPartners(pawn).Any())))
                return SSCSharedBedStatus.Waiting;
            return SSCSharedBedStatus.Unavailable;
        }

        /// <summary>显示当前恶堕比例和严格大于的门槛，数值格式避免把常见边界值四舍五入成相同显示。</summary>
        private static string GetThresholdText(Pawn pawn)
        {
            return (SSCSharedBedUtility.HasMeaningfulCorruption(pawn)
                ? "SSC_SharedBedThresholdMet" : "SSC_SharedBedThresholdUnmet")
                .Translate((SSCSharedBedUtility.GetCorruption(pawn) * 100f).ToString("0.#####"));
        }

        /// <summary>返回本床中确实获得 SSC 双向许可的对象名称；复用同一对角色的生产判断。</summary>
        private static string GetPermittedPartnerNames(Pawn pawn, Building_Bed bed)
        {
            return string.Join(", ", bed.OwnersForReading.Where(owner =>
                SSCSharedBedUtility.CanShareWithPartner(bed, pawn, owner)).Select(owner => owner.LabelShortCap.ToString()));
        }

        /// <summary>按字段展示关系、门槛及本床 SSC 许可；原版环境检查和合法配偶同床继续由原版负责。</summary>
        public static string GetTooltip(Pawn pawn, Building_Bed bed)
        {
            var lines = new List<string> { GetBadgeLabel(GetAssignmentBadge(pawn, bed)) };
            if (SSCIdentityUtility.IsSexSlave(pawn))
            {
                Pawn master = SSCBondUtility.GetChain(pawn)?.LinkedPawn;
                Pawn trainer = pawn.TryGetComp<CompSexSlaveTraining>()?.selectedTrainer;
                string none = "SSC_SharedBedNone".Translate();
                lines.Add("SSC_SharedBedMaster".Translate(master == null ? none : master.LabelShortCap.ToString()));
                string trainerName = trainer == null ? none : trainer.LabelShortCap.ToString();
                if (trainer != null && TrainerAssignmentUtility.GetActiveAssignedTrainer(pawn) == null)
                    trainerName = "SSC_SharedBedInactiveTrainer".Translate(trainerName);
                lines.Add("SSC_SharedBedTrainer".Translate(trainerName));
                lines.Add(GetThresholdText(pawn));
            }
            if (bed != null)
            {
                // 主人/调教员查看的是相关性奴的门槛；性奴兼任调教员时也分别列明，避免误用其自身恶堕。
                foreach (Pawn owner in bed.OwnersForReading.Where(owner => owner != pawn
                    && SSCSharedBedUtility.GetAllowedPartners(owner).Contains(pawn)))
                    lines.Add("SSC_SharedBedPartnerThreshold".Translate(owner.LabelShortCap, GetThresholdText(owner)));
            }
            SSCSharedBedStatus status = GetSharingStatus(pawn, bed);
            string reason;
            if (status == SSCSharedBedStatus.Permitted)
                reason = "SSC_SharedBedStatusAllowed".Translate(GetPermittedPartnerNames(pawn, bed));
            else if (status == SSCSharedBedStatus.Retained) reason = "SSC_SharedBedStatusRetained".Translate();
            else if (status == SSCSharedBedStatus.Waiting) reason = "SSC_SharedBedStatusWaiting".Translate();
            else if (!SSCSharedBedUtility.IsOrdinarySharedBed(bed)) reason = "SSC_SharedBedStatusBedType".Translate();
            else if (pawn == null || pawn.IsPrisoner) reason = "SSC_SharedBedStatusPrisoner".Translate();
            else if (SSCIdentityUtility.IsSexSlave(pawn) && !SSCSharedBedUtility.HasMeaningfulCorruption(pawn)
                && !SSCIdentityUtility.IsTrainer(pawn)) reason = "SSC_SharedBedStatusThreshold".Translate();
            else reason = "SSC_SharedBedStatusNoPartner".Translate();
            lines.Add("SSC_SharedBedStatus".Translate(reason));
            if (status == SSCSharedBedStatus.Waiting && pawn.IsSlave)
                lines.Add("SSC_SharedBedAssignPartnerFirst".Translate());
            lines.Add("SSC_SharedBedNativeChecks".Translate());
            return string.Join("\n", lines);
        }
    }

    /// <summary>仅描述 SSC 额外许可，不代表原版最终分配或实际使用结果。</summary>
    public enum SSCSharedBedStatus
    {
        Unavailable,
        Waiting,
        Permitted,
        Retained
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

        /// <summary>为 Mint 床位姓名预留右侧按钮空间，再复用原版窗口的独立身份和状态标记绘制。</summary>
        /// <param name="rect">Mint 原始姓名区域，可能延伸到按钮下方。</param>
        /// <param name="label">Mint 生成的姓名及可选拒绝原因，完整传给共享绘制函数。</param>
        /// <param name="pawn">当前行角色。</param>
        /// <param name="comp">当前分配组件；非床位或无本床标记时直接使用 Mint 原本的 Label 调用。</param>
        /// <param name="rowRect">整行区域，用于计算右侧 170px 按钮及 5px 间距的位置。</param>
        public static void DrawPawnLabel(Rect rect, string label, Pawn pawn, CompAssignableToPawn comp, Rect rowRect)
        {
            Building_Bed bed = (comp as CompAssignableToPawn_Bed)?.parent as Building_Bed;
            if (Harmony_SSC_SharedBedAssignmentUI.GetAssignmentBadge(pawn, bed) == SSCSharedBedBadge.None)
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
