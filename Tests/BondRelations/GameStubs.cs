// AI 辅助生成
// 仅模拟已反编译的原生健康、双向社交存储及显示入口；主从规则/清理/命名/补丁均编译生产源码。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld;
using SexSlaveCraft;

namespace Verse
{
    public class Thing { public bool Destroyed; }
    public enum Gender { None, Male, Female }
    public class Pawn : Thing
    {
        public bool Dead, Discarded;
        public Gender gender;
        public string LabelShort = "Pawn";
        public string LabelShortCap => LabelShort;
        public readonly Health health;
        public readonly Pawn_RelationsTracker relations;
        public readonly CompSexSlaveTraining Training = new CompSexSlaveTraining();
        public Pawn() { health = new Health(this); relations = new Pawn_RelationsTracker(this); }
        public T TryGetComp<T>() where T : class => Training as T;
    }
    public class HediffDef { public float maxSeverity = 1f; public Type hediffClass; }
    public struct DamageInfo { }
    public class Hediff
    {
        public Pawn pawn; public HediffDef def; public float Severity;
        public virtual string LabelBase => "Chain";
        public virtual bool ShouldRemove => false;
        public virtual void PostAdd(DamageInfo? info) { }
        public virtual void PostRemoved() { }
        public virtual void Tick() { }
        public virtual void ExposeData() { }
        public virtual string Description => "";
        public virtual string TipStringExtra => "";
        public virtual IEnumerable<Gizmo> GetGizmos() => Array.Empty<Gizmo>();
    }
    public class HediffWithComps : Hediff { }
    public class HediffWithTarget : HediffWithComps { public Thing target; }
    public class Gizmo { }
    public enum LoadSaveMode { Inactive, PostLoadInit }
    public enum LookMode { Reference }
    public static class Scribe { public static LoadSaveMode mode; }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> list, string key, LookMode mode) { } }
    public static class Scribe_Values { public static void Look(ref bool value, string key, bool defaultValue) { } }
    public class HediffSet
    {
        public readonly List<Hediff> hediffs = new List<Hediff>();
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(h => h.def == def);
    }
    public class Health
    {
        // 原版先把 Hediff 从集合移除，再调用 PostRemoved；生产清理器因此能看到已解除的锁链。
        readonly Pawn owner;
        public readonly HediffSet hediffSet = new HediffSet();
        public Health(Pawn owner) { this.owner = owner; }
        public void AddHediff(Hediff hediff) { hediff.pawn = owner; hediffSet.hediffs.Add(hediff); hediff.PostAdd(null); }
        public void RemoveHediff(Hediff hediff) { if (hediffSet.hediffs.Remove(hediff)) hediff.PostRemoved(); }
    }
    public static class HediffMaker
    {
        public static Hediff MakeHediff(HediffDef def, Pawn pawn)
        { var h = (Hediff)Activator.CreateInstance(def.hediffClass); h.def = def; h.pawn = pawn; return h; }
    }
    public static class Extensions
    {
        public static bool IsHashIntervalTick(this Pawn pawn, int interval) => false;
        public static bool DestroyedOrNull(this Thing thing) => thing == null || thing.Destroyed;
        public static string CapitalizeFirst(this string value) => string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0]) + value.Substring(1);
        public static string Translate(this string key) => Translations[key];
        public static readonly Dictionary<string, string> Translations = new Dictionary<string, string>();
    }
    public class Game { }
    public abstract class GameComponent { public virtual void FinalizeInit() { } }
}
namespace RimWorld
{
    using Verse;
    public class PawnRelationDef
    {
        public string label = "Master", labelFemale = "完美性奴";
        public bool reflexive = true;
        public int opinionOffset = 100;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetGenderSpecificLabel(Pawn pawn) => pawn.gender == Gender.Female ? labelFemale : label;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetGenderSpecificLabelCap(Pawn pawn) => GetGenderSpecificLabel(pawn).CapitalizeFirst();
    }
    public class DirectPawnRelation { public PawnRelationDef def; public Pawn otherPawn; }
    public class Pawn_RelationsTracker
    {
        // 名字和可见性与实际程序集一致；好感说明补丁从原版实例读取此字段。
        readonly Pawn pawn;
        public readonly List<DirectPawnRelation> DirectRelations = new List<DirectPawnRelation>();
        public int BaseOpinion, CacheNotifications;
        public Pawn_RelationsTracker(Pawn pawn) { this.pawn = pawn; }
        public bool DirectRelationExists(PawnRelationDef def, Pawn other) => DirectRelations.Any(r => r.def == def && r.otherPawn == other);
        public int OpinionOf(Pawn other) => Math.Clamp(BaseOpinion + DirectRelations.Where(r => r.otherPawn == other).Sum(r => r.def.opinionOffset), -100, 100);
        public void AddDirectRelation(PawnRelationDef def, Pawn other)
        {
            // 模拟已核验的原生双向存储和缓存通知，不把 SSC 锁链规则写进引擎边界。
            if (DirectRelationExists(def, other)) return;
            DirectRelations.Add(new DirectPawnRelation { def = def, otherPawn = other });
            if (def.reflexive) other.relations.DirectRelations.Add(new DirectPawnRelation { def = def, otherPawn = pawn });
            CacheNotifications++; other.relations.CacheNotifications++;
        }
        public bool TryRemoveDirectRelation(PawnRelationDef def, Pawn other)
        {
            // 原生删除接口同时移除对方记录；模型要求反向记录存在，避免掩盖双向清理错误。
            var relation = DirectRelations.FirstOrDefault(r => r.def == def && r.otherPawn == other);
            if (relation == null) return false;
            if (def.reflexive)
            {
                var reverse = other.relations.DirectRelations.First(r => r.def == def && r.otherPawn == pawn);
                other.relations.DirectRelations.Remove(reverse);
            }
            DirectRelations.Remove(relation);
            CacheNotifications++; other.relations.CacheNotifications++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public string OpinionExplanation(Pawn other)
        {
            // 称谓调用保留在本体 IL 中，供生产 transpiler 从当前 tracker 装载查看者。
            var labels = new List<string>();
            foreach (var relation in DirectRelations)
                if (relation.otherPawn == other) labels.Add(relation.def.GetGenderSpecificLabelCap(other));
            return string.Join(", ", labels);
        }
    }
    public static class SocialCardUtility
    {
        sealed class CachedSocialTabEntry { public Pawn otherPawn; public List<PawnRelationDef> relations; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static string GetRelationsString(CachedSocialTabEntry entry, Pawn selPawnForSocialInfo)
        {
            // 只保留实际原版方法的两个称谓调用点和查看者参数，其他界面工作不在本宿主模拟。
            string text = "";
            foreach (var def in entry.relations)
                text = text.Length == 0 ? def.GetGenderSpecificLabelCap(entry.otherPawn) : text + ", " + def.GetGenderSpecificLabel(entry.otherPawn);
            return text;
        }
        public static string Render(Pawn observer, Pawn other, params PawnRelationDef[] defs)
            => GetRelationsString(new CachedSocialTabEntry { otherPawn = other, relations = defs.ToList() }, observer);
    }
}
namespace UnityEngine { public static class Mathf { public static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max); } }
namespace SexSlaveCraft
{
    using Verse;
    public enum PawnIdentity { Unset, Slave, Master }
    public class CompSexSlaveTraining { public PawnIdentity pawnIdentity; public Pawn selectedTrainer; }
    public static class SSCDefOf
    {
        public static HediffDef ChainOfSexSlave = new HediffDef { hediffClass = typeof(Hediff_ChainOfSexSlave) };
        public static HediffDef BridleOfSexSlave = new HediffDef { hediffClass = typeof(Hediff_BridleOfSexSlave) };
        public static HediffDef SSC_MasterBondEmpowerment = new HediffDef();
        public static PawnRelationDef SSC_FlawedLovers = new PawnRelationDef();
    }
    // 共振场数值不在本组重算；实际锁链和缰绳生命周期均编译生产源码。
    public static class SSCMasterBondUtility
    {
        public static void RefreshMasterEmpowerment(Pawn pawn) { }
        public static string GetEmpowermentSummary(Pawn pawn) => "empowerment";
    }
    public static class SSCIdentityUtility
    {
        public static bool IsMaster(Pawn pawn) => pawn?.Training.pawnIdentity == PawnIdentity.Master;
        public static bool TrySetIdentity(Pawn pawn, PawnIdentity id) { pawn.Training.pawnIdentity = id; return true; }
        public static int GetSexSlaveStageFromChain(Hediff_ChainOfSexSlave chain) => chain.Severity >= .5f ? 3 : 1;
    }
    public static class SSCRestrictionGameComponent
    {
        // 快照来源是外部边界；全地图/世界/商队的枚举由原有限制系统测试负责。
        public static readonly List<Pawn> Pawns = new List<Pawn>();
        public static List<Pawn> AllPawns() => Pawns.ToList();
        public static void Notify(Pawn pawn) { }
    }
    public static class SSCRestrictionTrainerAssignment
    {
        public static bool TryAssign(Pawn slave, Pawn trainer) { slave.Training.selectedTrainer = trainer; return true; }
    }
    public static class TrainerAssignmentUtility
    {
        public static Pawn GetActiveAssignedTrainer(Pawn pawn) => pawn.Training.selectedTrainer;
        public static bool CanAssignTrainerTo(Pawn slave, Pawn trainer) => true;
    }
    public static class TrainerSpecializationLifecycle
    {
        // 本套件仅观察社交维护，不执行训导官特化；作用域保持生产 Bind 的调用形状。
        sealed class Scope : IDisposable { public void Dispose() { } }
        public static IDisposable BeginMutation(Pawn pawn) => new Scope();
        public static void Notify(Pawn pawn) { }
    }
    public static class SSCSelfTrainingFeedback { public static void NotifyStatusChanged(Pawn pawn) { } }
    public static class ModLog { public static void Message(string message) { } }
    public static class Strings
    {
        public static string Chain_LabelWithPawn(string label, string name) => label + name;
        public static string Bridle_LabelWithNames(string label, string names) => label + names;
        public static string Bridle_DescSlaves(string names) => names;
    }
}
