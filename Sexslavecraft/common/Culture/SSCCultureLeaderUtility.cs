// AI 辅助生成：1.7 正式领袖只读查询，不授职、卸职或重算职位活动。
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // None 同时覆盖空缺和无有效任职者；多名有效任职者另由 Conflict 标记，不能任意选一位。
    public enum SSCCultureLeaderState { None, Master, NonMaster }

    // 只读结果保留有效任职者及分类；default 表示无领袖且无冲突，不触发职位修复。
    public readonly struct SSCCultureLeaderResult
    {
        public readonly Pawn Pawn;
        public readonly SSCCultureLeaderState State;
        public readonly bool Conflict;
        public SSCCultureLeaderResult(Pawn pawn, SSCCultureLeaderState state, bool conflict = false)
        { Pawn = pawn; State = state; Conflict = conflict; }
    }

    public static class SSCCultureLeaderUtility
    {
        public static SSCCultureLeaderResult Query(HashSet<Pawn> members)
        {
            // 玩家文化尚不存在时没有可查询的正式职位；不回退到阵营指针或其他世界文化。
            Faction player = Faction.OfPlayer;
            if (player?.ideos == null) return default;
            Pawn found = null;

            // 覆盖玩家主/次文化的实际职位。成员名册尚未筛主人，才能识别有效非主人领袖。
            // 不相信单独的 faction.leader 指针，不要求观察者同文化或任职者位于当前地图。
            foreach (Ideo ideo in player.ideos.AllIdeos)
            {
                if (ideo == null) continue;
                foreach (Precept_Role role in ideo.RolesListForReading)
                {
                    // 只检查实际领袖职位的单人任职槽；精神导师等角色不作为领袖候选。
                    if (role.def?.leaderRole != true) continue;
                    Pawn pawn = role.ChosenPawnSingle();
                    // RequirementsMet 只检查年龄及 roleRequirements；补上原版 ValidatePawn
                    // 对玩家任职者的自由非奴隶殖民者条件。不得调用 RecacheActivity 来修复数据。
                    if (!SSCCultureStateUtility.IsLiving(pawn) || pawn.Faction != player
                        || !members.Contains(pawn) || !pawn.IsFreeNonSlaveColonist
                        || !role.RequirementsMet(pawn)) continue;
                    // 同一 Pawn 被重复引用不构成冲突；不同有效任职者则返回显式冲突且保持原数据。
                    if (found != null && !ReferenceEquals(found, pawn))
                        return new SSCCultureLeaderResult(null, SSCCultureLeaderState.None, true);
                    found = pawn;
                }
            }
            // 通过任职资格后才读取 SSC 主人身份，避免把有效非主人误判为空缺。
            return found == null ? default : new SSCCultureLeaderResult(found,
                SSCIdentityUtility.IsMaster(found) ? SSCCultureLeaderState.Master : SSCCultureLeaderState.NonMaster);
        }
    }
}
