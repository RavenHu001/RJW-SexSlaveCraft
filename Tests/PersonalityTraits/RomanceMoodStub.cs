// AI 辅助生成：模型宿主仅提供记忆子类边界；实际原生合并由 RomanceControlNative 验证。
#if SSC_RW17
namespace SexSlaveCraft
{
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
