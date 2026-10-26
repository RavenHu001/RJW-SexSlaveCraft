// AI 辅助生成：本人背景只消费已保存事实；沿原生语法解析，不进入互动或心情结算。
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Grammar;

namespace SexSlaveCraft
{
    /// <summary>三个日志包与十三个稳定根；文案和当前规则变化均不改变历史事件分类。</summary>
    internal static class SSCRomanceLogGrammar
    {
        private static readonly HashSet<string> warned = new HashSet<string>();

        /// <summary>只按有效事件选择展示入口；不读取当前阶段、开关、关系、主人或心情准入。</summary>
        private static bool Entry(SSCRomanceInteractionFact fact, out string pack, out string root)
        {
            pack = root = null;
            if (fact?.IsValid != true) return false;
            switch (fact.Kind)
            {
                case SSCRomanceCompositeEvent.ForcedRomanceAccepted: pack = "Accept"; root = "accept_romance"; break;
                case SSCRomanceCompositeEvent.ForcedProposalAccepted: pack = "Accept"; root = "accept_proposal"; break;
                case SSCRomanceCompositeEvent.ForcedRomanceRejected: pack = "Reject"; root = "reject_romance"; break;
                case SSCRomanceCompositeEvent.ForcedProposalRejected: pack = "Reject"; root = "reject_proposal"; break;
                case SSCRomanceCompositeEvent.ForcedProposalRejectedAndSeparated: pack = "Reject"; root = "reject_proposal_separated"; break;
                case SSCRomanceCompositeEvent.CommandRomanceAccepted: pack = "Command"; root = "command_romance_accepted"; break;
                case SSCRomanceCompositeEvent.CommandRomanceRejected: pack = "Command"; root = "command_romance_rejected"; break;
                case SSCRomanceCompositeEvent.CommandProposalAccepted: pack = "Command"; root = "command_proposal_accepted"; break;
                case SSCRomanceCompositeEvent.CommandProposalRejected: pack = "Command"; root = "command_proposal_rejected"; break;
                case SSCRomanceCompositeEvent.CommandProposalRejectedAndSeparated: pack = "Command"; root = "command_proposal_rejected_separated"; break;
                case SSCRomanceCompositeEvent.CommandEndLover: pack = "Command"; root = "command_end_lover"; break;
                case SSCRomanceCompositeEvent.CommandEndFiance: pack = "Command"; root = "command_end_fiance"; break;
                case SSCRomanceCompositeEvent.CommandDivorce: pack = "Command"; root = "command_divorce"; break;
                default: return false;
            }
            pack = "SSC_RomanceLog_" + pack;
            root = "ssc_romance_log_" + root;
            return true;
        }

        /// <summary>为当前本人人物解析专属补句；独立随机域确保直接查询、强制重绘和信件不影响游戏随机。</summary>
        internal static string Format(SSCRomanceInteractionFact fact, Pawn actor, Pawn target, int logID, bool forceLog)
        {
            if (actor == null || target == null || !Entry(fact, out var name, out var root)) return string.Empty;
            Rand.PushState();
            try
            {
                Rand.Seed = logID;
                var pack = DefDatabase<RulePackDef>.GetNamedSilentFail(name);
                if (pack == null) return Missing(name, root, "MissingPack");
                // 不把本人包加入共用 extras；原版双方共用的正文／结果语句始终由基类处理。
                var request = new GrammarRequest();
                request.Includes.Add(pack);
                request.Rules.AddRange(GrammarUtility.RulesForPawn("INITIATOR", actor, request.Constants));
                request.Rules.AddRange(GrammarUtility.RulesForPawn("RECIPIENT", target, request.Constants));
                string result;
                if (Resolve(pack, root, request, false, forceLog, out result)) return result;
                // 与原版一致保留未翻译规则回退；用真实成功回执，绝不把 ERR 当作历史正文。
                if (LanguageDatabase.activeLanguage != LanguageDatabase.defaultLanguage &&
                    Resolve(pack, root, request, true, forceLog, out result)) return result;
                return Missing(name, root, "Unresolvable");
            }
            catch (Exception error) { return Missing(name, root, "Exception:" + error.GetType().Name); }
            finally { Rand.PopState(); }
        }

        /// <summary>单次规则尝试隔离异常；本地化故障仍可尝试英文，英文失败才退回原正文。</summary>
        private static bool Resolve(RulePackDef pack, string root, GrammarRequest request, bool untranslated, bool forceLog, out string text)
        {
            text = null;
            try
            {
                // 显式检查根且保留原生 success；排列与 ERR 字符串都不能作为结果证据。
                var rules = untranslated ? pack.UntranslatedRulesPlusIncludes : pack.RulesPlusIncludes;
                if (rules == null || !rules.Any(rule => rule.keyword == root)) return false;
                text = GrammarResolver.ResolveUnsafe(root, request, out var success, "SSC romance log", forceLog, useUntranslatedRules: untranslated);
                return success && !string.IsNullOrWhiteSpace(text);
            }
            catch (Exception error)
            {
                Missing(pack.defName, root, (untranslated ? "UntranslatedException:" : "TranslatedException:") + error.GetType().Name);
                return false;
            }
        }

        /// <summary>同类资源故障只诊断一次；无负缓存，修复／加载资源后仍可重新解析，原版正文照常保留。</summary>
        private static string Missing(string pack, string root, string reason)
        {
            try
            {
                // 关闭输出不消耗诊断名额；恢复日志闸门后仍能看见首次实际故障。
                if (SSCLog.AnyEnabled && (SSCMod.settings == null || SSCMod.settings.enableSSCImportantLogs) &&
                    warned.Add(pack + "/" + root + "/" + reason))
                    SSCLog.WarningImportant("[SSC Romance] 日志背景回退：" + pack + "/" + root + " " + reason);
            }
            // 外部诊断接收器异常不能中断原版日志／信件或命令结果，沿心情诊断的既有隔离契约。
            catch (Exception) { }
            return string.Empty;
        }
    }
}
