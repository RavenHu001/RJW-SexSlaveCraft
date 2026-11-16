// AI 辅助生成：文化场次收益与凝胶分开认领，只消费对象本人的真实高潮事件。
using System;
using System.Collections.Generic;
using RimWorld;
using rjw;
using Verse;

namespace SexSlaveCraft
{
    internal static class SSCCulturePartnerCorruption
    {
        // 需求使用 0～1；0.0025 是 0.25 个百分点，与对象当前值无关。
        private const double Gain = .0025d;
        private static readonly string[] PreceptNames = {
            "SSC_Precept_PartnerCorruption_Off", "SSC_Precept_PartnerCorruption_MaleOnly",
            "SSC_Precept_PartnerCorruption_FemaleOnly", "SSC_Precept_PartnerCorruption_Unrestricted"
        };

        // 保持原人、原 Job 与原资格。调用原方法期间换来的参与者不能取得这一次旧事件的结算。
        internal sealed class Observation
        {
            internal SSCPartnerCorruptionScene Scene;
            internal SSCCulturePartnerCorruptionGameComponent Cooldowns;
            internal Need_Corruption Need;
            internal Gender Gender;
            internal readonly List<Source> Sources = new List<Source>(1);
        }

        internal readonly record struct Source(SSCPartnerCorruptionScene.Member Member, Hediff_ChainOfSexSlave Chain,
            Ideo Ideo, PreceptDef Precept, object Personality);

        /// <summary>只认施加者自己的现行戒律；缺失、关闭或同组冲突都不启用。</summary>
        private static PreceptDef MatchingPrecept(Ideo ideo, Gender gender)
        {
            if (ideo == null) return null;
            PreceptDef match = null;
            for (int i = 0; i < PreceptNames.Length; i++)
            {
                var def = DefDatabase<PreceptDef>.GetNamedSilentFail(PreceptNames[i]);
                if (def == null || !ideo.HasPrecept(def)) continue;
                // 关闭优先；限定档只用 Pawn.gender，性别不限包含 None。
                if (match != null || i == 0 || (i == 1 && gender != Gender.Male) || (i == 2 && gender != Gender.Female))
                    return null;
                match = def;
            }
            return match;
        }

        /// <summary>仅在既有观察器判断可能触发的外层 Orgasm 调用中拍摄场次。</summary>
        internal static Observation Capture(JobDriver_Sex driver, Need_Corruption need)
        {
            Pawn target = driver.pawn;
            // 先做廉价拒绝：冷却、主人及满值角色无需枚举同场人物或读取 UAP 会话。
            if (!ModsConfig.IdeologyActive || need == null || !(need.CurLevel < need.MaxLevel) ||
                !SSCCultureStateUtility.IsLiving(target) || SSCCultureStateUtility.IsMaster(target)) return null;
            var cooldowns = SSCCulturePartnerCorruptionGameComponent.Current;
            if (cooldowns == null || !cooldowns.CanClaim(target) ||
                !SSCPartnerCorruptionScene.TryCapture(driver, out var scene, out var people)) return null;

            var gender = target.gender;
            Observation observation = null;
            // 普通 RJW 的接收者不在自己的伴侣表中；UAP 的完整名单已经包含接收者。
            var receiver = scene.RjwReceiver;
            if (receiver != null) CaptureSource(receiver, people);
            foreach (Pawn other in people) CaptureSource(other, people);
            return observation;

            void CaptureSource(Pawn other, List<Pawn> participants)
            {
                // 对象不能给自己发放。文化只认锁链确定的实际性奴，不把用于途径预选的 ITab 身份当作资格。
                if (other == target || other?.Ideo == null || !scene.TryGetMember(other, participants, out var member)) return;
                var chain = SSCBondUtility.GetChain(other);
                if (chain == null || SSCPersonalityRuntimeUtility.IsMutating(other)) return;
                var ideo = other.Ideo;
                var precept = MatchingPrecept(ideo, gender);
                if (precept == null) return;
                // 只在第一名施加者满足条件时分配记录，并保留所有合格施加者供原调用完成后复核。
                observation ??= new Observation { Scene = scene, Cooldowns = cooldowns, Need = need, Gender = gender };
                observation.Sources.Add(new Source(member, chain, ideo, precept, SSCPersonalityRuntimeUtility.Capture(other)));
            }
        }

        /// <summary>外层观察已证实至少一次本人高潮；按对象只写一次，并先占冷却以抵御写入后的重入。</summary>
        internal static void Apply(Pawn target, Observation observation)
        {
            // 原生高潮与旧收益可能触发同步回调；重新确认对象仍持有原需求、当前不是主人、且原场没有失效。
            // 主人免疫只限制本来源；无需比对施加者的实际主人，也不会替对象占用冷却。
            if (observation == null || !ModsConfig.IdeologyActive || target.gender != observation.Gender ||
                SSCCultureStateUtility.IsMaster(target) || !ReferenceEquals(target.needs?.TryGetNeed<Need_Corruption>(), observation.Need) ||
                !observation.Cooldowns.CanClaim(target) || !observation.Scene.TryGetParticipants(out var people)) return;

            foreach (var source in observation.Sources)
            {
                // 仍须是同场、同一个已捕获的参与任务和施加资格；第一名离开不影响另一名原本有效的人。
                Pawn other = source.Member.Pawn;
                if (!source.Member.Current || !observation.Scene.TryGetMember(other, people, out var member) ||
                    member.Driver != source.Member.Driver || other.Ideo != source.Ideo || SSCBondUtility.GetChain(other) != source.Chain ||
                    !SSCPersonalityRuntimeUtility.IsCurrent(other, source.Personality) || SSCPersonalityRuntimeUtility.IsMutating(other) ||
                    MatchingPrecept(source.Ideo, observation.Gender) != source.Precept) continue;

                // 以此刻原需求的余量裁剪；凝胶先写入造成满值时，不为零收益占冷却。
                float before = observation.Need.CurLevel;
                float cap = observation.Need.MaxLevel;
                if (!float.IsFinite(before) || before < 0f || !float.IsFinite(cap)) return;
                float next = (float)Math.Min((double)before + Gain, cap);
                // SetCorruption 会同步既有成长效果，可能重入其他回调；所以先认领，只向原需求写一次。
                if (next > before && observation.Cooldowns.TryClaim(target)) observation.Need.SetCorruption(next);
                return;
            }
        }
    }
}
