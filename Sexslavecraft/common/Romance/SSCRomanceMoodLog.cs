// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>仍是原版同一条互动日志；只保存受控者各自背景，文字待定，不改社交好感。</summary>
    public sealed class PlayLogEntry_SSCRomance : PlayLogEntry_Interaction
    {
        private SSCRomanceMoodData actorEvent, targetEvent;
        /// <summary>Scribe 创建入口，不在加载时计算、补发或刷新心情。</summary>
        public PlayLogEntry_SSCRomance() { }
        /// <summary>保留原版参与者、语法包和日志身份，仅深复制已确认背景。</summary>
        public PlayLogEntry_SSCRomance(InteractionDef interaction, Pawn actor, Pawn target, List<RulePackDef> extras,
            SSCRomanceMoodData actorData, SSCRomanceMoodData targetData) : base(interaction, actor, target, extras)
        { actorEvent = actorData?.Copy(); targetEvent = targetData?.Copy(); }
        /// <summary>只向本人视角提供背景副本；不根据当前开关改写历史，不推断第三人知情。</summary>
        public SSCRomanceMoodData BackgroundFromPOV(Thing pov) =>
            pov == initiator ? actorEvent?.Copy() : pov == recipient ? targetEvent?.Copy() : null;
        /// <summary>开发占位仅在受控本人视角出现；正式文案待维护者定稿，不公开内部许可。</summary>
        public string BackgroundTextFromPOV(Thing pov) => BackgroundFromPOV(pov) == null ? string.Empty : " [SSC 浪漫反馈：文案待定]";
        /// <summary>补充原条目，不生成第二次互动；重复查看只格式化已保存背景。</summary>
        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
            => base.ToGameStringFromPOV_Worker(pov, forceLog) + BackgroundTextFromPOV(pov);
        /// <summary>背景随本条日志保存；不随人格复制命令或结算请求，读取不重放事件。</summary>
        public override void ExposeData()
        { base.ExposeData(); Scribe_Deep.Look(ref actorEvent, "sscActorEvent"); Scribe_Deep.Look(ref targetEvent, "sscTargetEvent"); }
    }

    public static class SSCRomanceMoodLog
    {
        /// <summary>替换原版构造调用，不新增第二条日志；无确认事件时精确使用原版条目。</summary>
        public static PlayLogEntry_Interaction Create(InteractionDef interaction, Pawn actor, Pawn target, List<RulePackDef> extras)
        {
            var f = SSCRomanceExecutionScope.MoodFrame;
            return f?.Settled == true && f.Actor == actor && f.Target == target && (f.ActorEvent != null || f.TargetEvent != null) ?
                new PlayLogEntry_SSCRomance(interaction, actor, target, extras, f.ActorEvent, f.TargetEvent) :
                new PlayLogEntry_Interaction(interaction, actor, target, extras);
        }
    }
}
