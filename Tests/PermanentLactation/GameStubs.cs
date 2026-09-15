using System;
using System.Collections.Generic;
using System.Reflection;

// 仅模拟组件调用、奶量字段及基础存档读写，不启动 Unity 或第三方模组。
namespace RimWorld { public static class NamespaceMarker { } }

namespace UnityEngine
{
    public static class Mathf
    {
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), float.Epsilon * 8f);
    }
}

namespace Verse
{
    public class HediffCompProperties { public Type compClass; }
    public class HediffCompProperties_Chargeable : HediffCompProperties
    {
        public float fullChargeAmount = 1f, initialCharge;
        public int ticksToFullCharge = 60000;
        public string labelInBrackets = "{0}";
    }
    public class HediffCompProperties_Lactating : HediffCompProperties_Chargeable { }
    public class HediffDef { public List<HediffCompProperties> comps = new List<HediffCompProperties>(); }
    public class Hediff { public HediffDef def = new HediffDef(); public Pawn pawn; }
    public class Pawn
    {
        public int Tick, HashOffset;
        public Needs needs = new Needs();
        public SexSlaveCraft.CompSexSlaveTraining Training = new SexSlaveCraft.CompSexSlaveTraining();
        public T TryGetComp<T>() where T : class => Training as T;
        public bool IsHashIntervalTick(int interval) => (Tick + HashOffset) % interval == 0;
    }
    public class Needs { public Food food = new Food(); }
    public class Food { public float CurLevel = 1f; }

    public class HediffComp_Chargeable
    {
        private float charge;
        public HediffCompProperties props;
        public Hediff parent;
        public Pawn Pawn => parent.pawn;
        public float Charge { get => charge; protected set => charge = value; }
        public virtual void CompPostMake() { }
        public virtual void CompPostTick(ref float adjustment) { }
        public virtual void CompPostTickInterval(ref float adjustment, int delta) => throw new InvalidOperationException("永久泌乳不能恢复基类计时。");
        public virtual void CompPostPostRemoved() { }
        public virtual string CompLabelInBracketsExtra => "";
        public virtual void CompExposeData() => Scribe_Values.Look(ref charge, "charge", ((HediffCompProperties_Chargeable)props).initialCharge);

        /// <summary>模拟原版从私有奶量字段扣除资源，避免测试只覆盖自定义 setter。</summary>
        public float GreedyConsume(float amount)
        {
            float consumed = Math.Min(charge, amount);
            charge -= consumed;
            return consumed;
        }
    }
    public class HediffComp_Lactating : HediffComp_Chargeable { }

    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
    }
    public static class Scribe_Values
    {
        /// <summary>只模拟字段默认值和保存/读取阶段；真实 Scribe XML 加载器由游戏内复核。</summary>
        public static void Look<T>(ref T value, string key, T defaultValue = default(T), bool forceSave = false)
        {
            if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = value;
            if (Scribe.mode == LoadSaveMode.LoadingVars) value = Scribe.Data.TryGetValue(key, out object saved) ? (T)saved : defaultValue;
        }
    }
}

namespace HarmonyLib
{
    public sealed class Traverse
    {
        private object target;
        public static Traverse Create(object target) => new Traverse { target = target };
        public TraverseField<T> Field<T>(string name) => new TraverseField<T>(target, name);
    }
    public sealed class TraverseField<T>
    {
        private readonly object target;
        private readonly FieldInfo reflectedField;

        /// <summary>沿继承链查找私有字段；本机不存在的旧泌乳计时字段沿用 Harmony 的空操作。</summary>
        public TraverseField(object target, string name)
        {
            this.target = target;
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                reflectedField = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (reflectedField != null) break;
            }
        }
        public T Value
        {
            get => reflectedField == null ? default(T) : (T)reflectedField.GetValue(target);
            set { reflectedField?.SetValue(target, value); }
        }
    }
}

namespace SexSlaveCraft
{
    public class CompSexSlaveTraining { public bool milkProductionEnabled = true; }
    public static class HumanCattleBridgeUtility
    {
        public static bool IsLoaded;
        public static int Calls;
        /// <summary>只验证接管入口及 SSC 奶量清零，不模拟 HumanCattle 自身产奶。</summary>
        public static void ApplyDoopMode(HediffComp_PermanentLactating comp) { Calls++; comp.CurrentCharge = 0f; }
        public static void CleanupBridge(Verse.Pawn pawn) { }
    }
    public static class Strings
    {
        public static string Lactating_Disabled(string progress) => progress;
        public static string Lactating_Stalled(string progress) => progress;
    }
}
