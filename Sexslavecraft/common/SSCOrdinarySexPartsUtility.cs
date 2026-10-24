// AI 辅助生成
using System;
using System.Collections.Generic;
using rjw;
using rjw.Modules.Interactions;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>RJW 实际部位到 SSC 身体开发的适配边界；不调用 resolved getter 或任何部位选择器。</summary>
    public static class SSCOrdinarySexPartsUtility
    {
        /// <summary>首次真实开始时只读取已有解析；可选凭据失败不改变 RJW 场景执行。</summary>
        internal static SSCOrdinarySexPartsSnapshot Capture(SexProps props)
        {
            if (Scribe.mode != LoadSaveMode.Inactive || props?.isCoreLovin != false) return null;
            try
            {
                var snapshot = ReadCached(props);
                snapshot?.Bind(props);
                return snapshot;
            }
            // 第三方部位实现的 getter 可能失败；可选开发凭据失效不阻断 Start、原结算或其他侵染分量。
            catch (Exception ex) { Log.Warning("[SSC InfestationParts] Cannot capture optional body evidence: " + ex.GetType().Name); return null; }
        }

        /// <summary>保留旧六部位的参与器官匹配语义；新项目的插入方向另行复查。</summary>
        internal static bool SameCachedParts(SSCOrdinarySexPartsSnapshot snapshot, SexProps props)
        {
            try
            {
                var current = ReadCached(props);
                if (current == null || current.Parts.Count != snapshot.Parts.Count) return false;
                foreach (var part in snapshot.Parts)
                    if (part == null || !current.Parts.Exists(part.Same)) return false;
                return true;
            }
            catch { return false; } // 仅把可选证据视为不可靠；不接管 RJW 原方法或原生异常。
        }

        /// <summary>核对真实缓存中的双方、交互、器官及接触端点，不访问会重新解析的 getter。</summary>
        private static SSCOrdinarySexPartsSnapshot ReadCached(SexProps props)
        {
            // 原缓存必须同时证明交互和双方方向；不能只凭 sexType 或某一个对象列表推测参与部位。
            var resolved = props?._resolved;
            if (props == null || props.initiator == null || props.recipient == null || props.initiator == props.recipient ||
                props.dictionaryKey == null || resolved?.Interaction?.Def != props.dictionaryKey ||
                resolved.Initiator != props.initiator || resolved.Recipient != props.recipient ||
                resolved.InitiatorParts == null || resolved.RecipientParts == null || resolved.Penetrations == null) return null;
            var snapshot = new SSCOrdinarySexPartsSnapshot
                { Initiator = props.initiator, Recipient = props.recipient, Interaction = props.dictionaryKey };
            foreach (var part in resolved.InitiatorParts)
                if (!Add(snapshot, part, snapshot.Initiator)) return null;
            foreach (var part in resolved.RecipientParts)
                if (!Add(snapshot, part, snapshot.Recipient)) return null;
            // 非插入交互可以没有接触边；有接触边时两端必须分别属于本场双方，顺序不代表主人方向。
            foreach (var contact in resolved.Penetrations)
            {
                if (contact?.Item1 == null || contact.Item2 == null) return null;
                Pawn a = contact.Item1.Owner, b = contact.Item2.Owner;
                if (!((a == snapshot.Initiator && b == snapshot.Recipient) ||
                      (a == snapshot.Recipient && b == snapshot.Initiator)) ||
                    !Add(snapshot, contact.Item1, a) || !Add(snapshot, contact.Item2, b)) return null;
#if SSC_RW17
                // Item1 为插入端，Item2 为被插入端；主人/发起者方向不能替代此凭据。
                if (contact.Item2.Family == LewdablePartFamily.Vagina)
                    MarkReceivingVagina(snapshot, contact.Item2, b);
                // 严格口交只看本场实际端点；普通、反向和六九式均按口腔归属判断。
                if (contact.Item1.Family == LewdablePartFamily.Penis && contact.Item2.Family == LewdablePartFamily.Mouth)
                    MarkReceivingPenileOral(snapshot, contact.Item2, b);
#endif
            }
            return snapshot;
        }

        /// <summary>按原器官与身体引用合并参与条目，方向标记在接触端点遍历中单独写入。</summary>
        private static bool Add(SSCOrdinarySexPartsSnapshot snapshot, ILewdablePart part, Pawn owner)
        {
            if (part == null || part.Owner != owner) return false;
            var rjwPart = part as RJWLewdablePart;
            Hediff organ = rjwPart?.SexPart?.AsHediff;
            var record = new SSCOrdinarySexPartsSnapshot.Part { Owner = owner, BodyPart = part.BodyPart,
                PartDef = part.BodyPart?.def, Family = part.Family, Organ = organ, HadOrgan = rjwPart != null };
            if (record.BodyPart == null || record.PartDef == null || (record.HadOrgan && organ == null)) return false;
            // 原始列表与接触端点可能重复；不同原器官保留为独立支持凭据，预算阶段再按开发记录去重。
            if (!snapshot.Parts.Exists(record.Same)) snapshot.Parts.Add(record);
            return true;
        }

#if SSC_RW17
        /// <summary>在已合并的器官条目上置位；原参与列表与接触端点重复时不丢失 true。</summary>
        private static void MarkReceivingVagina(SSCOrdinarySexPartsSnapshot snapshot, ILewdablePart part, Pawn owner)
        {
            Hediff organ = (part as RJWLewdablePart)?.SexPart?.AsHediff;
            foreach (var evidence in snapshot.Parts)
                if (evidence.Owner == owner && evidence.BodyPart == part.BodyPart &&
                    evidence.Family == LewdablePartFamily.Vagina && evidence.Organ == organ &&
                    evidence.HadOrgan == (part is RJWLewdablePart))
                    evidence.ReceivedVaginalPenetration = true;
        }

        /// <summary>验证保存的被插入条目；已有缓存时额外核对方向，读档无缓存时不重新解析。</summary>
        internal static bool IsCurrentReceivingVagina(SSCOrdinarySexPartsSnapshot snapshot, SexProps props,
            SSCOrdinarySexPartsSnapshot.Part evidence)
        {
            if (snapshot == null || evidence == null || !evidence.ReceivedVaginalPenetration ||
                evidence.Family != LewdablePartFamily.Vagina || !evidence.IsAttached() ||
                snapshot.Parts?.Contains(evidence) != true || !snapshot.Matches(props)) return false;
            if (props._resolved == null) return true;
            try
            {
                var current = ReadCached(props);
                return current?.Parts.Exists(p => p.ReceivedVaginalPenetration && evidence.Same(p)) == true;
            }
            catch { return false; } // 可选成长证据失效，不改变原有部位匹配与结算。
        }

        /// <summary>在已合并的器官条目上置位；原参与列表与接触端点重复时不丢失 true。</summary>
        private static void MarkReceivingPenileOral(SSCOrdinarySexPartsSnapshot snapshot, ILewdablePart part, Pawn owner)
        {
            Hediff organ = (part as RJWLewdablePart)?.SexPart?.AsHediff;
            foreach (var evidence in snapshot.Parts)
                if (evidence.Owner == owner && evidence.BodyPart == part.BodyPart &&
                    evidence.Family == LewdablePartFamily.Mouth && evidence.Organ == organ &&
                    evidence.HadOrgan == (part is RJWLewdablePart))
                    evidence.ReceivedPenileOralPenetration = true;
        }

        /// <summary>验证保存的严格口交口腔条目；已有缓存时额外核对方向，读档无缓存时不重新解析。</summary>
        internal static bool IsCurrentReceivingPenileOral(SSCOrdinarySexPartsSnapshot snapshot, SexProps props,
            SSCOrdinarySexPartsSnapshot.Part evidence)
        {
            if (snapshot == null || evidence == null || !evidence.ReceivedPenileOralPenetration ||
                evidence.Family != LewdablePartFamily.Mouth || !evidence.IsAttached() ||
                snapshot.Parts?.Contains(evidence) != true || !snapshot.Matches(props)) return false;
            if (props._resolved == null) return true;
            try
            {
                var current = ReadCached(props);
                return current?.Parts.Exists(p => p.ReceivedPenileOralPenetration && evidence.Same(p)) == true;
            }
            catch { return false; } // 可选成长证据失效，不改变原有部位匹配与结算。
        }
#endif

        internal static HediffDef GetExperienceDef(LewdablePartFamily family)
        {
            string name;
            switch (family)
            {
                case LewdablePartFamily.Hand: name = "SSC_Exp_Hand"; break;
                case LewdablePartFamily.Foot: name = "SSC_Exp_Foot"; break;
                case LewdablePartFamily.Mouth:
                case LewdablePartFamily.Tongue:
                case LewdablePartFamily.Beak: name = "SSC_Exp_Oral"; break;
                case LewdablePartFamily.Breasts: name = "SSC_Exp_Breast"; break;
                case LewdablePartFamily.Penis:
                case LewdablePartFamily.Vagina: name = "SSC_Exp_Genitals"; break;
                case LewdablePartFamily.Anus: name = "SSC_Exp_Anus"; break;
                default: return null;
            }
            return DefDatabase<HediffDef>.GetNamedSilentFail(name);
        }
    }
}
