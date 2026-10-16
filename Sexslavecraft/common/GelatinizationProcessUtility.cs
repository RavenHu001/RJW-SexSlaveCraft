using Verse;

// AI 辅助生成
namespace SexSlaveCraft
{
    /// <summary>移除临时状态即完成过程；同轮剩余组件和旧调试按钮不得再次结算。</summary>
    internal static class GelatinizationProcessUtility
    {
        /// <summary>确认这一具体过程仍附着于活着的角色；同角色的新过程不能让旧组件重新生效。</summary>
        public static bool IsActive(Hediff process)
        {
            // RemoveHediff 不会清空 process.pawn，故仅判断角色存在或存活不足以判定过程有效。
            // 按对象检查健康列表中的成员身份，防止同角色后来新增同名 Hediff 时误认旧过程仍在运行。
            return process?.pawn?.health?.hediffSet != null
                && !process.pawn.Dead
                && process.pawn.health.hediffSet.hediffs.Contains(process);
        }

        /// <summary>领取成功或失败结算资格；返回 false 时调用方必须跳过后续结果处理。</summary>
        public static bool TryComplete(Hediff process)
        {
            // 自然扫描、开发按钮和同轮剩余组件共用此入口；已移除或死亡的过程不能再次领取资格。
            if (!IsActive(process)) return false;
            // 游戏主线程同步执行移除，随后其他组件便能通过 IsActive 看到过程已经结束。
            // 调用方须在此之后才添加义肢或销毁身体；不引入额外完成字段及存档迁移。
            process.pawn.health.RemoveHediff(process);
            return true;
        }
    }
}
