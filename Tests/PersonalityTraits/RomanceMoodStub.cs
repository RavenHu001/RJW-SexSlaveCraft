// AI 辅助生成：模型宿主仅提供记忆子类边界；实际原生合并由 RomanceControlNative 验证。
#if SSC_RW17
namespace SexSlaveCraft
{
    /// <summary>旧载荷模型隔离排泄准入，继续验证既有主人凝胶数据；当前主人拒绝及健康移除由原生专项验证。</summary>
    public static class SSCPersonalityExcretionAdmission
    {
        public static bool CanExtract(Verse.Pawn pawn) => pawn != null;
        // 末端准入仍属于原生孕期宿主；历史载荷模型只提供成功空间边界。
        internal static bool TryContinueExtraction(Verse.Pawn pawn, Verse.Thing preparedProduct) => pawn != null;
        public static void RemoveForbiddenPreparation(Verse.Pawn pawn) { }
    }
    /// <summary>本模型只构造无印记人格；印记实际获取、锁定及迁移由原生专项验证，不在此复制算法。</summary>
    public static class SSCImprintUtility
    {
        /// <summary>本宿主不构造初始印记，因而不提供该额外主人选择准入。</summary>
        public static bool HasBornMasterEligibility(Verse.Pawn pawn) => false;
        /// <summary>本宿主健康定义均属原有人格场景，新增印记过滤在原生专项另验。</summary>
        public static bool IsImprintDef(Verse.HediffDef def) => false;
        /// <summary>允许本模型的无印记载荷继续进入既有人格恢复流程。</summary>
        public static bool CanRestore(Verse.Pawn pawn, CompPersonalityStore data) => true;
        /// <summary>无印记来源不产生新增健康写入，恢复流程仍执行其他真实生产模块。</summary>
        public static bool Restore(Verse.Pawn pawn, CompPersonalityStore data) => true;
        /// <summary>模型未构造印记，不在此复制健康移除行为。</summary>
        public static void Clear(Verse.Pawn pawn) { }
        /// <summary>模型没有新增印记可供采集，真实Def＋严重度采集由原生专项验证。</summary>
        public static void Store(CompPersonalityStore data, Verse.Pawn pawn) { }
        /// <summary>无游戏任务模型不构造待用种子，不模拟UseItem取消流程。</summary>
        public static void CancelPendingSeedUse(Verse.Pawn pawn) { }
        /// <summary>仅满足本模型凝胶检查文字边界，不用固定文本证明生产翻译正确。</summary>
        public static string GelLabel(CompPersonalityStore data) => "No imprint";
    }
    /// <summary>无游戏的载荷模型不构建健康／情境思想缓存；真实刷新由 UterusDevelopmentNative 验证。</summary>
    public static class SSCOrgasmSensitivityUtility
    {
        /// <summary>仅提供人格作用域通知边界，不在模型宿主复制身体敏感度业务。</summary>
        public static void NotifyStateChanged(Verse.Pawn pawn) { }
    }
    public sealed class Thought_MemorySSCRomance : RimWorld.Thought_Memory
    {
        public SSCRomanceMoodData Data;
        public bool IsPendingNewEvent => false;
        public bool CopiedPendingEvent => false;
    }
    /// <summary>模型宿主只保存排序字段；游戏级水位及真实恢复由 RomanceControlNative 执行生产入口验证。</summary>
    public static class SSCRomanceMoodOrder
    {
        /// <summary>不在无游戏的特质模型宿主发放、刷新或重排记忆。</summary>
        public static void Observe(long order) { }
    }
}
#endif
