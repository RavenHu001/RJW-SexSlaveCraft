using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;

// EN: This file rewrites RJW SexProps for SSC scenes.
// EN: It forces the chosen act type, interaction def, and reflection fallback used by daily training, personality excretion, and the Binding Ritual.
// CN: 这个文件负责为 SSC 场景改写 RJW 的 SexProps。
// CN: 它会强制写入选定的 act type、interaction def，以及日常调教、人格排泄和绑定仪式都会用到的反射兼容逻辑。
namespace SexSlaveCraft
{
    public static class RJWSexPropsUtility
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly FieldInfo SexTypeField = typeof(SexProps).GetField("_sexType", InstanceFlags)
            ?? typeof(SexProps).GetField("sexType", InstanceFlags)
            ?? typeof(SexProps).GetField("<sexType>k__BackingField", InstanceFlags);

        public static bool ApplyTrainingAct(SexProps sexProps, Pawn initiator, Pawn partner, TrainingActType actType)
            => ApplyTrainingAct(sexProps, initiator, partner, actType, out _);

        /// <summary>区分有效指定动作的部位失配与定义或解析结果异常。</summary>
        /// <remarks>只有原 Def 与双方归属有效、且 RJW 未返回部位组合时，才向日常调用方报告可自动回退。</remarks>
        internal static bool ApplyTrainingAct(SexProps sexProps, Pawn initiator, Pawn partner, TrainingActType actType,
            out bool noCompatibleParts)
            => ApplySexType(sexProps, initiator, partner, ConvertToRJWType(actType), ConvertToInteractionDefName(actType),
                out noCompatibleParts);

        /// <summary>开始前同步强制动作及部位；固定仪式／人格排泄可保留空接触结果，不增加解剖准入条件。</summary>
        public static bool ApplySexType(SexProps sexProps, Pawn initiator, Pawn partner, xxx.rjwSextype sexType,
            string interactionDefName, bool allowEmptyParts = false)
            => ApplySexType(sexProps, initiator, partner, sexType, interactionDefName, out _, allowEmptyParts);

        private static bool ApplySexType(SexProps sexProps, Pawn initiator, Pawn partner, xxx.rjwSextype sexType,
            string interactionDefName, out bool noCompatibleParts, bool allowEmptyParts = false)
        {
            noCompatibleParts = false;
            // 缺少指定 Def 或不支持的字段布局时不冒用原选择；先验证再改写。
            if (sexProps == null || initiator == null || partner == null || initiator == partner ||
                SexTypeField == null || string.IsNullOrEmpty(interactionDefName)) return false;
            InteractionDef def = DefDatabase<InteractionDef>.GetNamedSilentFail(interactionDefName);
            if (def == null) return false;
            var interaction = new SexInteraction(def);
            // SSC 的口交模式沿用 Oral 总类，本机 Sex_Fellatio 使用更细的 Fellatio 类型。
            // 保留既有结算分类，同时只允许这一对已核对的兼容映射。
            if (interaction.Sextype != sexType &&
                !(sexType == xxx.rjwSextype.Oral && interaction.Sextype == xxx.rjwSextype.Fellatio)) return false;

            sexProps.pawn = initiator;
            sexProps.partner = partner;
            sexProps.isReceiver = false;
            sexProps.isRevese = interaction.HasInteractionTag(SexInteractionTag.Reverse);
            // RJW 的 interaction setter 不清旧 resolved，且预置 SexProps 的 Start 不重设反向标记。
            // 显式同步其已有字段，不另建缓存；原强迫、避孕套等场景标记继续保留。
            sexProps.resolved = null;
            sexProps.interaction = interaction;
            SexTypeField.SetValue(sexProps, (xxx.rjwSextype?)sexType);

            // 只解析指定交互，不重新随机挑动作；不得在结束阶段临时选部位充当接触证据。
            SexInteractionResolved resolved = SexInteractionHelper.ResolveInteraction(sexProps);
            // 原生解析可能经过兼容补丁。归属或 Def 变化按异常结果处理，避免借自动模式接受串场数据。
            if (sexProps.initiator != initiator || sexProps.recipient != partner || sexProps.dictionaryKey != def)
                return false;
            if (resolved == null && !allowEmptyParts)
            {
                noCompatibleParts = true;
                return false;
            }

            // 仪式及人格排泄使用固定场景，不要求每种动作都满足 RJW 的选性交部位条件。
            // 解析不到时保留当前动作和双方，明确记录无可确认接触；不能沿用上个随机动作的部位。
            // 这里只填写 RJW 已有解析载体，空列表让按真实插入遍历的怀孕／体液逻辑自然跳过。
            if (resolved == null)
                resolved = new SexInteractionResolved
                {
                    Initiator = initiator,
                    Recipient = partner,
                    Interaction = interaction,
                    InitiatorParts = new List<ILewdablePart>(),
                    RecipientParts = new List<ILewdablePart>(),
                    Penetrations = new List<Tuple<ILewdablePart, ILewdablePart>>()
                };
            if (!MatchesResolution(resolved, def, initiator, partner)) return false;
            sexProps.resolved = resolved;
            return true;
        }

#if SSC_RW17
        /// <summary>检查自动回退是否选到了本次双方的有效动作，并同步接收任务所用的方向标记。</summary>
        internal static bool TryPrepareAutomaticTrainingAct(SexProps sexProps, Pawn initiator, Pawn partner)
        {
            // SelectSextype 即使找不到候选，也会返回 SexProps。直接检查缓存，避免 getter 再次解析。
            if (sexProps?.dictionaryKey == null || sexProps.isReceiver || sexProps.pawn != initiator || sexProps.partner != partner)
                return false;
            var resolved = sexProps._resolved;
            if (!MatchesResolution(resolved, sexProps.dictionaryKey, initiator, partner) ||
                resolved.InitiatorParts.Count == 0 || resolved.RecipientParts.Count == 0) return false;

            // RJW Start 会直接使用预置 SexProps。同步 Reverse 供接收方使用，实际动作类型保留给 RJW 和 SSC 完成结算。
            sexProps.isRevese = resolved.Interaction.HasInteractionTag(SexInteractionTag.Reverse);
            return true;
        }
#endif

        /// <summary>核对 Def、部位归属及配对两端。动作可包含反向配对，也可只有参与部位。</summary>
        private static bool MatchesResolution(SexInteractionResolved resolved, InteractionDef def, Pawn initiator, Pawn partner)
        {
            if (resolved?.Interaction?.Def != def || resolved.Initiator != initiator || resolved.Recipient != partner ||
                resolved.InitiatorParts == null || resolved.RecipientParts == null || resolved.Penetrations == null) return false;
            foreach (ILewdablePart part in resolved.InitiatorParts)
                if (part == null || part.Owner != initiator) return false;
            foreach (ILewdablePart part in resolved.RecipientParts)
                if (part == null || part.Owner != partner) return false;
            foreach (var contact in resolved.Penetrations)
                if (contact == null || !((contact.Item1?.Owner == initiator && contact.Item2?.Owner == partner) ||
                    (contact.Item1?.Owner == partner && contact.Item2?.Owner == initiator))) return false;
            return true;
        }

        public static xxx.rjwSextype ConvertToRJWType(TrainingActType actType)
        {
            switch (actType)
            {
                case TrainingActType.Vaginal: return xxx.rjwSextype.Vaginal;
                case TrainingActType.Anal: return xxx.rjwSextype.Anal;
                case TrainingActType.Oral: return xxx.rjwSextype.Oral;
                case TrainingActType.Boobjob: return xxx.rjwSextype.Boobjob;
                case TrainingActType.Handjob: return xxx.rjwSextype.Handjob;
                case TrainingActType.Footjob: return xxx.rjwSextype.Footjob;
                case TrainingActType.Fingering: return xxx.rjwSextype.Fingering;
                case TrainingActType.MutualMasturbation: return xxx.rjwSextype.MutualMasturbation;
                case TrainingActType.Fisting: return xxx.rjwSextype.Fisting;
                case TrainingActType.Rimming: return xxx.rjwSextype.Rimming;
                case TrainingActType.Sixtynine: return xxx.rjwSextype.Sixtynine;
                default: return xxx.rjwSextype.Vaginal;
            }
        }

        public static string ConvertToInteractionDefName(TrainingActType actType)
        {
            switch (actType)
            {
                case TrainingActType.Vaginal: return "Sex_Vaginal";
                case TrainingActType.Anal: return "Sex_Anal";
                case TrainingActType.Oral: return "Sex_Fellatio";
                case TrainingActType.Boobjob: return "Sex_Breastjob";
                case TrainingActType.Handjob: return "Sex_Handjob";
                case TrainingActType.Footjob: return "Sex_Footjob";
                case TrainingActType.Fingering: return "Sex_Fingering";
                case TrainingActType.MutualMasturbation: return "Sex_MutualMasturbation";
                case TrainingActType.Fisting: return "Sex_Fisting";
                case TrainingActType.Rimming: return "Sex_Rimming";
                case TrainingActType.Sixtynine: return "Sex_Sixtynine";
                default: return "Sex_Vaginal";
            }
        }
    }
}
