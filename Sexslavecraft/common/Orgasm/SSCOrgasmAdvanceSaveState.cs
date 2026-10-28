// AI 辅助生成：每个 RJW 驱动自己的余数节点，不保存身体选择或可重放的推进命令。
using Verse;

namespace SexSlaveCraft
{
    /// <summary>只序列化数据，由标准 Scribe 调度引用恢复；本节点不回调或修改所属驱动。</summary>
    public sealed class SSCOrgasmAdvanceSaveState : IExposable
    {
        // 这些字段只校验旧余数的格式／归属／周期，不构成另一份身体档位或XML配置保存源。
        internal int SchemaVersion, JobLoadId, Level, MultiplierBits, Counter, Threshold, StartCounter, Duration, Orgasms, ToilIndex;
        internal string DriverType;
        internal double Carry;
        internal Hediff_SSCOrgasmSensitivity Body;
        internal Hediff_ChainOfSexSlave Chain;
        internal Pawn Actor, Master;

        /// <summary>缺版本按零读取，未知格式稍后拒绝；引用字段只在本节点自身的加载回调中解析。</summary>
        public void ExposeData()
        {
            // 缺节点为零余数；存在节点但缺版本须拒绝，不能自动冒充本格式。
            Scribe_Values.Look(ref SchemaVersion, "schemaVersion", 0, forceSave: true);
            Scribe_Values.Look(ref Carry, "carry", 0d);
            Scribe_Values.Look(ref JobLoadId, "jobLoadId", 0);
            Scribe_Values.Look(ref DriverType, "driverType");
            Scribe_Values.Look(ref Level, "level", 0);
            Scribe_Values.Look(ref MultiplierBits, "multiplierBits", 0);
            Scribe_Values.Look(ref Counter, "counter", 0);
            Scribe_Values.Look(ref Threshold, "threshold", 0);
            Scribe_Values.Look(ref StartCounter, "startCounter", 0);
            Scribe_Values.Look(ref Duration, "duration", 0);
            Scribe_Values.Look(ref Orgasms, "orgasms", 0);
            Scribe_Values.Look(ref ToilIndex, "toilIndex", 0);
            // 每个节点自行参加交叉引用恢复；本人／主人死亡不等于锁链指令失效，因此允许死亡对象引用。
            Scribe_References.Look(ref Body, "body");
            Scribe_References.Look(ref Chain, "chain");
            Scribe_References.Look(ref Actor, "actor", saveDestroyedThings: true);
            Scribe_References.Look(ref Master, "master", saveDestroyedThings: true);
        }
    }
}
