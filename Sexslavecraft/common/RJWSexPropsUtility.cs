using System.Reflection;
using RimWorld;
using rjw;
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

        private static readonly FieldInfo InteractionField = typeof(SexProps).GetField("_interaction", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void ApplyTrainingAct(SexProps sexProps, Pawn initiator, Pawn partner, TrainingActType actType)
        {
            // EN: Convert SSC's training-mode selection into the RJW sex type + interaction pair for this scene.
            // CN: 把 SSC 的调教姿势选择换算成这次场景要用的 RJW sex type 和 interaction 组合。
            ApplySexType(sexProps, initiator, partner, ConvertToRJWType(actType), ConvertToInteractionDefName(actType));
        }

        public static void ApplySexType(SexProps sexProps, Pawn initiator, Pawn partner, xxx.rjwSextype sexType, string interactionDefName)
        {
            if (sexProps == null) return;

            sexProps.pawn = initiator;
            sexProps.partner = partner;

            // EN: Compatibility path: RJW changed the sexType backing field across versions, so all fallback names live here.
            // CN: 兼容分支：RJW 不同版本会改 sexType 背后的字段名，所以所有回退字段名统一收在这里。
            if (SexTypeField != null)
            {
                SexTypeField.SetValue(sexProps, (xxx.rjwSextype?)sexType);
            }

            if (!string.IsNullOrEmpty(interactionDefName))
            {
                // EN: The dictionaryKey must follow the forced act, or RJW may rebuild the wrong interaction later.
                // CN: dictionaryKey 必须跟着强制姿势一起改，不然 RJW 后面可能会重建出错误的 interaction。
                InteractionDef interaction = DefDatabase<InteractionDef>.GetNamedSilentFail(interactionDefName);
                if (interaction != null)
                {
                    sexProps.dictionaryKey = interaction;
                }
            }

            InteractionField?.SetValue(sexProps, null);
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
