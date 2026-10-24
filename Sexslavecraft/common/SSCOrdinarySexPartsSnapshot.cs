// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>普通双人任务开始时的部位凭据；只保存身体引用，不保存或重新选择 RJW 交互。</summary>
    public sealed class SSCOrdinarySexPartsSnapshot : IExposable
    {
        internal Pawn Initiator, Recipient;
        internal InteractionDef Interaction;
        internal List<Part> Parts = new List<Part>();
        private SexProps boundProps;

        /// <summary>同一身体记录可承载多个器官；保留原器官引用供去重前及写入前独立复查。</summary>
        public sealed class Part : IExposable
        {
            internal Pawn Owner;
            internal BodyPartRecord BodyPart;
            internal BodyPartDef PartDef;
            internal LewdablePartFamily Family;
            internal Hediff Organ;
            internal bool HadOrgan;
#if SSC_RW17
            // 只由真实接触的 Item2 写入；随本次 Job 保存，不是身体的永久状态。
            internal bool ReceivedVaginalPenetration;
            // 只在阴茎向 Mouth 的真实接触上置位，不把舔舐、喙部或其他插入当成严格口交。
            internal bool ReceivedPenileOralPenetration;
#endif

            /// <summary>保存原器官引用及本场接触事实；旧档没有新标记时不能推补插入证据。</summary>
            public void ExposeData()
            {
                Scribe_References.Look(ref Owner, "owner");
                Scribe_BodyParts.Look(ref BodyPart, "bodyPart");
                Scribe_Defs.Look(ref PartDef, "partDef");
                Scribe_Values.Look(ref Family, "family", LewdablePartFamily.Undefined);
                Scribe_Values.Look(ref HadOrgan, "hadOrgan", false);
                Scribe_References.Look(ref Organ, "organ");
#if SSC_RW17
                Scribe_Values.Look(ref ReceivedVaginalPenetration, "receivedVaginalPenetration", false);
                Scribe_Values.Look(ref ReceivedPenileOralPenetration, "receivedPenileOralPenetration", false);
#endif
            }

            internal bool Same(Part other) => other != null && Owner == other.Owner && BodyPart == other.BodyPart &&
                PartDef == other.PartDef && Family == other.Family && HadOrgan == other.HadOrgan && Organ == other.Organ;

            internal bool IsAttached() => TrainingExpUtility.IsPresentPart(Owner, BodyPart) && BodyPart.def == PartDef &&
                (!HadOrgan || (Organ != null && Organ.pawn == Owner && Organ.Part == BodyPart &&
                    Owner.health.hediffSet.hediffs.Contains(Organ)));
        }

        // 绑定只在真实开始或读档引用恢复后进行；运行中的 SexProps 被替换不能继承旧部位凭据。
        internal void Bind(SexProps props) => boundProps = props;

#if SSC_RW17
        /// <summary>将方向复查限制在原来绑定的 SexProps；调用方不能借新对象续用旧接触。</summary>
        internal bool IsCurrentReceivingVagina(Part evidence)
            => SSCOrdinarySexPartsUtility.IsCurrentReceivingVagina(this, boundProps, evidence);

        /// <summary>使用本场绑定的对象复查严格口交；旧档缺标记时不推补接触。</summary>
        internal bool IsCurrentReceivingPenileOral(Part evidence)
            => SSCOrdinarySexPartsUtility.IsCurrentReceivingPenileOral(this, boundProps, evidence);
#endif

        internal bool Matches(SexProps props)
        {
            if (props == null || props != boundProps || props.initiator != Initiator || props.recipient != Recipient ||
                props.dictionaryKey != Interaction || Interaction == null || Parts == null) return false;
            // 读档后尚无缓存时使用保存的原凭据；若原生已重解析，则只允许相同参与部位，绝不补进新候选。
            return props._resolved == null || SSCOrdinarySexPartsUtility.SameCachedParts(this, props);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Initiator, "initiator");
            Scribe_References.Look(ref Recipient, "recipient");
            Scribe_Defs.Look(ref Interaction, "interaction");
            Scribe_Collections.Look(ref Parts, "parts", LookMode.Deep);
        }
    }
}
