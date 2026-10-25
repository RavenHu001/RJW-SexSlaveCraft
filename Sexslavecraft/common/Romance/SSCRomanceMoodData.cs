// AI 辅助生成
using Verse;

namespace SexSlaveCraft
{
    /// <summary>历史基准快照；普通保存、人格凝胶及原生克隆均复制，不携带授权或待结算请求。</summary>
    public sealed class SSCRomanceMoodData : IExposable
    {
        public SSCRomanceMoodEvent Kind;
        public SSCRomanceOldRelation Relation;
        public int Stage, Opinion, Value;
        public bool Masochist;
        public float? Corruption;
        public SSCRomanceSetting Rule;
        public bool HasRule;
        public Pawn Master;
        /// <summary>值字段独立复制，历史主人保留原引用；不带原人格运行凭据。</summary>
        public SSCRomanceMoodData Copy() => (SSCRomanceMoodData)MemberwiseClone();
        /// <summary>显式保存可空恶堕的存在性，避免缺需求被读成低恶堕并改变历史解释。</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref Kind, "kind"); Scribe_Values.Look(ref Relation, "relation");
            Scribe_Values.Look(ref Stage, "stage"); Scribe_Values.Look(ref Opinion, "opinion");
            Scribe_Values.Look(ref Value, "value"); Scribe_Values.Look(ref Masochist, "masochist");
            Scribe_Values.Look(ref Rule, "rule");
            Scribe_Values.Look(ref HasRule, "hasRule");
            Scribe_References.Look(ref Master, "master", saveDestroyedThings: true);
            // Scribe 不直接序列化 Nullable；独立存在位保留合法零值及缺需求区别。
            bool has = Corruption.HasValue; float value = Corruption ?? 0;
            Scribe_Values.Look(ref has, "hasCorruption"); Scribe_Values.Look(ref value, "corruption");
            if (Scribe.mode == LoadSaveMode.LoadingVars) Corruption = has ? value : (float?)null;
        }
    }

}
