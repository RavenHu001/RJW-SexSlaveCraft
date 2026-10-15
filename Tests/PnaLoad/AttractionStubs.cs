using System;
using System.Collections.Generic;
using Verse;

// 仅提供实际 RJW 接口的最小边界；不复刻启动器、完整分项评价、资格或最终选取算法。
namespace rjw.Modules.Attraction
{
    public enum AttractionMode { Age, Physical, Romantic, Social }
    public enum FactorOperation { Add, Multiply, AddByFactor }
    public enum AttractionPurpose { General, ForFucking, ForRape }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class StandardPreferenceAttribute : Attribute { }

    public ref struct AttractionRequest
    {
        public readonly Pawn Pawn, Target;
        public readonly AttractionPurpose Purpose;
        private readonly Dictionary<string, AttractionPreference> preferences;
        public AttractionRequest(AttractionPurpose purpose, Pawn pawn, Pawn target)
        {
            Pawn = pawn; Target = target; Purpose = purpose;
            preferences = new Dictionary<string, AttractionPreference>();
        }
        public void SetPreference(AttractionPreference preference) => preferences[preference.Key] = preference;
        public AttractionPreference GetPreference(string key) => preferences.TryGetValue(key, out var value) ? value : null;
        public int PreferenceCount => preferences.Count;
    }

    public abstract class AttractionPreference
    {
        public AttractionMode Mode { get; }
        public string Key { get; }
        public FactorOperation Operation { get; }
        protected AttractionPreference(AttractionMode mode, string key, FactorOperation operation)
        { Mode = mode; Key = key; Operation = operation; }
        // 本测试只需要原生 Multiply 入口；其他运算应显式失败，不能被替身默默当乘法。
        public void Evaluate(ref AttractionRequest request, ref float factor)
        {
            if (Operation != FactorOperation.Multiply) throw new NotSupportedException();
            factor *= GetOperand(ref request, Math.Max(0f, factor));
        }
        protected abstract float GetOperand(ref AttractionRequest request, float factor);
        public virtual TaggedString Explain(ref AttractionRequest request, ref float factor) => throw new NotSupportedException();
    }
}

namespace Verse
{
    public readonly struct TaggedString
    {
        private readonly string value;
        private TaggedString(string text) { value = text; }
        public static implicit operator TaggedString(string text) => new TaggedString(text);
        public override string ToString() => value;
    }
}
