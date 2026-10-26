// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仍是原版同一条互动日志；只保存已确认的受控者各自事实，文字待定，不改社交好感。</summary>
    public sealed class PlayLogEntry_SSCRomance : PlayLogEntry_Interaction
    {
        private SSCRomanceInteractionFact actorEvent, targetEvent;
        /// <summary>Scribe 创建入口，不在加载时计算、补发或刷新心情。</summary>
        public PlayLogEntry_SSCRomance() { }
        /// <summary>保留原版参与者、语法包和日志身份，仅深复制已确认事实，不依赖心情准入。</summary>
        public PlayLogEntry_SSCRomance(InteractionDef interaction, Pawn actor, Pawn target, List<RulePackDef> extras,
            SSCRomanceInteractionFact actorData, SSCRomanceInteractionFact targetData) : base(interaction, actor, target, extras)
        { actorEvent = actorData?.Copy(); targetEvent = targetData?.Copy(); }
        /// <summary>只向本人视角提供背景副本；不根据当前开关改写历史，不推断第三人知情。</summary>
        public SSCRomanceInteractionFact BackgroundFromPOV(Thing pov)
        {
            var fact = pov == initiator ? actorEvent : pov == recipient ? targetEvent : null;
            // 深存事实同样校验主动／被动归属及原互动类型，损坏历史不得向错误一方显示控制背景。
            return fact?.IsValid == true && fact.IsCommand == (pov == initiator) &&
                SSCRomanceInteractionUtility.TryGetAction(intDef, out var action) && fact.Action == action ? fact.Copy() : null;
        }
        /// <summary>按本人冻结事实解析三类专属语法；正式文案仍占位，不读取当前许可或推断另一方知情。</summary>
        public string BackgroundTextFromPOV(Thing pov) => BackgroundTextFromPOV(pov, false);
        /// <summary>原版强制语法诊断沿本次查询传入；普通背景查询不绕过本人角色和行为校验。</summary>
        private string BackgroundTextFromPOV(Thing pov, bool forceLog)
            => SSCRomanceLogGrammar.Format(BackgroundFromPOV(pov), initiator, recipient, logID, forceLog);
        /// <summary>保留原正文及所有结果 extras，只补本人句子；原 tracker 信件自然使用发起者视角。</summary>
        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            var original = base.ToGameStringFromPOV_Worker(pov, forceLog);
            var background = BackgroundTextFromPOV(pov, forceLog);
            return string.IsNullOrEmpty(background) ? original : original + " " + background;
        }
        /// <summary>背景随本条日志保存；不随人格复制命令或结算请求，读取不重放事件。</summary>
        public override void ExposeData()
        { base.ExposeData(); Scribe_Deep.Look(ref actorEvent, "sscActorFact"); Scribe_Deep.Look(ref targetEvent, "sscTargetFact"); }
    }

    public static class SSCRomanceMoodLog
    {
        /// <summary>替换原版构造调用，不新增第二条日志；无确认事件时精确使用原版条目。</summary>
        public static PlayLogEntry_Interaction Create(InteractionDef interaction, Pawn actor, Pawn target, List<RulePackDef> extras)
        {
            var f = SSCRomanceExecutionScope.MoodFrame;
            return f?.FactsConfirmed == true && f.Actor == actor && f.Target == target &&
                SSCRomanceInteractionUtility.TryGetAction(interaction, out var action) && action == f.Action &&
                (f.ActorFact != null || f.TargetFact != null) ?
                new PlayLogEntry_SSCRomance(interaction, actor, target, extras, f.ActorFact, f.TargetFact) :
                new PlayLogEntry_Interaction(interaction, actor, target, extras);
        }
    }
}
