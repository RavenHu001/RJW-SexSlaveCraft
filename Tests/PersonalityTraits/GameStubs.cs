// 为直接链接的生产代码提供无界面测试环境。
// TraitSet 模拟的引擎行为及回归测试的验证边界见本目录 README.md。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;

namespace Verse
{
    public class Def
    {
        public string defName;
        public string LabelCap => defName;
    }

    public class ThingDef : Def
    {
    }

    public class HediffDef : Def
    {
    }

    public enum DestroyMode
    {
        Vanish
    }

    public interface IExposable
    {
        /// <summary>声明测试数据的保存读取入口，供链接的生产数据类型实现。</summary>
        void ExposeData();
    }

    public enum LookMode
    {
        Deep,
        Def,
        Value
    }

    public enum LoadSaveMode
    {
        Inactive,
        Saving,
        LoadingVars,
        PostLoadInit
    }

    public static class Scribe
    {
        public static LoadSaveMode mode;
        public static Dictionary<string, object> Values = new Dictionary<string, object>();
    }

    public static class Scribe_Values
    {
        /// <summary>在内存字典中记录或回放标量字段，缺少字段时使用默认值；不执行真实存档序列化。</summary>
        public static void Look<T>(ref T x, string s, T value = default(T))
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                Scribe.Values[s] = x;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                x = Scribe.Values.TryGetValue(s, out var saved) ? (T)saved : value;
        }
    }

    public static class Scribe_References
    {
        /// <summary>提供对象引用读写接口的空实现；本测试不验证跨存档引用解析。</summary>
        public static void Look<T>(ref T x, string s, bool saveDestroyedThings = false)
        {
        }
    }

    public static class Scribe_Defs
    {
        /// <summary>提供定义引用读写接口的空实现；本测试不加载游戏定义数据库。</summary>
        public static void Look<T>(ref T x, string s)
        {
        }
    }

    public static class Scribe_Deep
    {
        /// <summary>提供深层对象读写接口的空实现；本测试不验证真实对象序列化。</summary>
        public static void Look<T>(ref T x, string s)
        {
        }
    }

    public static class Scribe_Collections
    {
        /// <summary>提供列表读写接口的空实现；列表内容由用例直接构造。</summary>
        public static void Look<T>(ref List<T> x, string s, LookMode m)
        {
        }

        /// <summary>提供字典读写接口的空实现；不模拟真实存档中的键值序列化。</summary>
        public static void Look<T, U>(ref Dictionary<T, U> x, string s, LookMode m, LookMode n)
        {
        }
    }

    public static class DefDatabase<T>
        where T : Def
    {
        public static List<T> AllDefs = new List<T>();
        /// <summary>按名称查找测试注册的定义，不存在时返回空引用。</summary>
        public static T GetNamedSilentFail(string name) => AllDefs.FirstOrDefault(x => x.defName == name);
    }

    public class CompProperties
    {
        public Type compClass;
    }

    public class ThingComp
    {
        public Thing parent;
        /// <summary>提供组件存档生命周期的空基类入口，允许生产组件调用基类方法。</summary>
        public virtual void PostExposeData()
        {
        }

        /// <summary>返回空检查信息，作为生产组件界面说明的默认基类实现。</summary>
        public virtual string CompInspectStringExtra() => "";
    }

    public class Thing
    {
        public bool Destroyed;
        public Dictionary<Type, object> Comps = new Dictionary<Type, object>();
        /// <summary>按组件类型查找测试物品上注册的实例，没有匹配项时返回空引用。</summary>
        public T TryGetComp<T>()
            where T : class => Comps.TryGetValue(typeof(T), out var c) ? c as T : null;
        /// <summary>记录物品已被销毁，以便验证凝胶是否在成功植入后被消耗。</summary>
        public virtual void Destroy(DestroyMode mode)
        {
            Destroyed = true;
        }
    }

    public class Name
    {
    }

    public class NameTriple : Name
    {
        public string First, Nick, Last;
        /// <summary>保存测试角色的名、昵称和姓，供人格身份恢复用例比较。</summary>
        public NameTriple(string a, string b, string c)
        {
            First = a;
            Nick = b;
            Last = c;
        }
    }

    public class Map
    {
        public AttackTargetsCache attackTargetsCache = new AttackTargetsCache();
    }

    public class AttackTargetsCache
    {
        /// <summary>提供攻击目标缓存刷新接口的空实现；本测试不验证地图目标缓存。</summary>
        public void UpdateTarget(Pawn pawn)
        {
        }
    }

    public class PawnDrawer
    {
        public PawnRenderer renderer = new PawnRenderer();
    }

    public class PawnRenderer
    {
        /// <summary>提供图像失效通知的空实现；本测试不启动游戏渲染器。</summary>
        public void SetAllGraphicsDirty()
        {
        }
    }

    public class AgeTracker
    {
        public int AgeBiologicalYears = 25;
    }

    public class Pawn : Thing
    {
        public bool Dead;
        public Name Name = new NameTriple("", "pawn", "");
        public string LabelShort => (Name as NameTriple)?.Nick ?? "pawn";

        public Map Map = new Map();
        public object Position;
        public StoryTracker story;
        public HealthTracker health = new HealthTracker();
        public NeedsTracker needs;
        public SkillsTracker skills;
        public RelationsTracker relations;
        public AgeTracker ageTracker = new AgeTracker();
        public Pawn_GeneTracker genes;
        public Pawn_AbilityTracker abilities = new Pawn_AbilityTracker();
        public PawnDrawer Drawer = new PawnDrawer();
        /// <summary>为测试角色创建背景、特质及基因追踪器，使生产入口可在无界面环境执行。</summary>
        public Pawn()
        {
            story = new StoryTracker(this);
            genes = new Pawn_GeneTracker(this);
        }

        /// <summary>提供工作限制变化通知的空实现；本测试不验证真实工作列表。</summary>
        public void Notify_DisabledWorkTypesChanged()
        {
        }
    }

    public class Hediff
    {
        public HediffDef def;
        public float Severity;
    }

    public class HediffWithTarget : Hediff
    {
        public Thing target;
    }

    public class HediffSet
    {
        public List<Hediff> hediffs = new List<Hediff>();
        /// <summary>从测试健康状态列表中查找首个指定定义的条目。</summary>
        public Hediff GetFirstHediffOfDef(HediffDef def) => hediffs.FirstOrDefault(x => x.def == def);
    }

    public class HealthTracker
    {
        public HediffSet hediffSet = new HediffSet();
        /// <summary>从测试角色的健康状态列表中移除指定实例。</summary>
        public void RemoveHediff(Hediff h)
        {
            hediffSet.hediffs.Remove(h);
        }

        /// <summary>创建指定定义的健康状态并加入测试列表，返回新实例供用例检查。</summary>
        public Hediff AddHediff(HediffDef def)
        {
            var h = new Hediff
            {
                def = def
            };
            hediffSet.hediffs.Add(h);
            return h;
        }
    }

    public static class ThingMaker
    {
        /// <summary>创建带有人格存储组件的测试物品，供生产提取流程生成凝胶。</summary>
        public static Thing MakeThing(ThingDef def)
        {
            var t = new Thing();
            var c = new SexSlaveCraft.CompPersonalityStore
            {
                parent = t
            };
            t.Comps[typeof(SexSlaveCraft.CompPersonalityStore)] = c;
            return t;
        }
    }

    public static class GenSpawn
    {
        public static Thing LastSpawned;
        /// <summary>记录最近生成的测试物品，供用例读取凝胶；不模拟地图放置行为。</summary>
        public static void Spawn(Thing t, object p, Map m)
        {
            LastSpawned = t;
        }
    }

    public static class Messages
    {
        /// <summary>提供游戏提示接口的空实现，避免测试依赖界面系统。</summary>
        public static void Message(string s, Thing t, object d)
        {
        }
    }

    public static class Log
    {
        public static List<string> Errors = new List<string>();
        /// <summary>忽略警告输出，避免与本次断言无关的调试信息干扰测试结果。</summary>
        public static void Warning(string s)
        {
        }

        /// <summary>记录并输出生产代码报告的错误，使被捕获的异常也能导致用例失败。</summary>
        public static void Error(string s)
        {
            Errors.Add(s);
            Console.WriteLine(s);
        }
    }

    public static class TextExtensions
    {
        /// <summary>提供数字显示接口的最小实现；本测试不验证百分比格式与本地化。</summary>
        public static string ToStringPercent(this float x) => x.ToString();
    }
}

namespace RimWorld
{
    using Verse;

    public class TraitDef : Def
    {
        public List<TraitDef> conflictingTraits = new List<TraitDef>();
        public TraitDegreeData degreeData = new TraitDegreeData();
        public bool canBeSuppressed = true;
        /// <summary>按相同定义或用例配置的冲突列表判断特质冲突，不读取游戏配置。</summary>
        public bool CanSuppress(Trait trait) => trait.def == this || conflictingTraits.Contains(trait.def);
    }

    public class BackstoryDef : Def
    {
    }

    public class SkillDef : Def
    {
    }

    public class ThoughtDef : Def
    {
    }

    public class PawnRelationDef : Def
    {
    }

    public class RecipeDef : Def
    {
    }

    public enum Passion
    {
        None,
        Minor,
        Major
    }

    public class AbilityDef : Def
    {
    }

    public class TraitDegreeData
    {
        public List<AbilityDef> abilities = new List<AbilityDef>();
    }

    public class GeneticTraitData
    {
        public TraitDef def;
        public int degree;
    }

    public class GeneDef : Def
    {
        public List<GeneticTraitData> suppressedTraits = new List<GeneticTraitData>();
        public List<AbilityDef> abilities = new List<AbilityDef>();
    }

    public class Gene
    {
        public GeneDef def;
        public bool Active = true;
        public bool Overridden => !Active;

        public Gene overridingGene;
    }

    public class Pawn_GeneTracker
    {
        private readonly Pawn pawn;
        /// <summary>记录所属测试角色，以便添加基因时授予该角色对应的能力。</summary>
        public Pawn_GeneTracker(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public List<Gene> GenesListForReading = new List<Gene>();
        /// <summary>创建基因实例并授予其直接能力；不模拟完整的基因添加生命周期。</summary>
        public Gene AddGene(GeneDef def)
        {
            var gene = new Gene
            {
                def = def
            };
            GenesListForReading.Add(gene);
            foreach (AbilityDef ability in def.abilities)
                pawn.abilities.GainAbility(ability);
            return gene;
        }

        /// <summary>移除指定基因实例，用于检测人格替换是否误删宿主基因；不模拟完整的基因移除生命周期。</summary>
        public void RemoveGene(Gene gene)
        {
            GenesListForReading.Remove(gene);
        }
    }

    public class Pawn_AbilityTracker
    {
        public HashSet<AbilityDef> GrantedAbilities = new HashSet<AbilityDef>();
        /// <summary>以集合记录已授予能力，使同一能力被多个来源重复授予时保持幂等。</summary>
        public void GainAbility(AbilityDef def)
        {
            GrantedAbilities.Add(def);
        }

        /// <summary>直接移除指定能力，模拟原版移除特质时不会为共享能力进行来源计数的行为。</summary>
        public void RemoveAbility(AbilityDef def)
        {
            GrantedAbilities.Remove(def);
        }
    }

    public class Trait
    {
        public TraitDef def;
        public int Degree;
        public bool ScenForced;
        public Gene sourceGene, suppressedByGene;
        public Pawn pawn;
        public bool suppressedByTrait;
        public bool Suppressed => suppressedByGene != null || suppressedByTrait || sourceGene?.Overridden == true;
        public TraitDegreeData CurrentData => def.degreeData;

        /// <summary>保存特质定义、等级及剧本强制标记，来源基因和抑制状态由用例另行设置。</summary>
        public Trait(TraitDef d, int degree = 0, bool forced = false)
        {
            def = d;
            Degree = degree;
            ScenForced = forced;
        }
    }

    public class TraitSet
    {
        public List<Trait> allTraits = new List<Trait>();
        private readonly Pawn pawn;
        public int RecalculateCount, RecacheCount;
        /// <summary>关联所属角色，供特质变化时访问其基因与能力追踪器。</summary>
        public TraitSet(Pawn pawn)
        {
            this.pawn = pawn;
        }

        /// <summary>判断是否存在指定定义的活跃特质，忽略受抑制条目。</summary>
        public bool HasTrait(TraitDef d) => allTraits.Any(t => t.def == d && !t.Suppressed);
        /// <summary>判断是否存在定义和等级均匹配的活跃特质，模拟原版同等级去重检查。</summary>
        public bool HasTrait(TraitDef d, int degree) => allTraits.Any(t => t.def == d && t.Degree == degree && !t.Suppressed);
        /// <summary>取得首个指定定义的活跃特质，没有匹配项时返回空引用。</summary>
        public Trait GetTrait(TraitDef d) => allTraits.FirstOrDefault(t => t.def == d && !t.Suppressed);
        /// <summary>模拟原版的重复检查、冲突抑制、基因抑制重算和能力授予，供生产恢复逻辑接受回归验证。</summary>
        public void GainTrait(Trait t, bool suppressConflicts = false)
        {
            // 已核对原版的去重检查：受抑制特质不会被视为活跃的重复条目。
            if (!suppressConflicts && HasTrait(t.def))
                return;
            if (HasTrait(t.def, t.Degree))
                return;
            allTraits.Add(t);
            t.pawn = pawn;
            if (suppressConflicts)
            {
                if (allTraits.Any(current => current != t && t.def.CanSuppress(current) && !current.def.canBeSuppressed))
                    t.suppressedByTrait = true;
                else
                    foreach (Trait current in allTraits)
                        if (current != t && t.def.CanSuppress(current))
                            current.suppressedByTrait = true;
            }

            RecalculateSuppression();
            foreach (AbilityDef ability in t.CurrentData.abilities)
                pawn.abilities.GainAbility(ability);
        }

        /// <summary>模拟活跃特质检查、来源基因与能力移除，以及可选的冲突抑制解除。</summary>
        public void RemoveTrait(Trait t, bool unsuppressConflicts = false)
        {
            // 原版先检查是否存在同定义的活跃特质，而不是检查待删实例是否在列表中。
            if (!HasTrait(t.def))
                return;
            allTraits.Remove(t);
            if (t.sourceGene != null)
                pawn.genes.RemoveGene(t.sourceGene);
            foreach (AbilityDef ability in t.CurrentData.abilities)
                pawn.abilities.RemoveAbility(ability);
            if (unsuppressConflicts)
                foreach (Trait remaining in allTraits.Where(x => x.Suppressed))
                    if (!allTraits.Any(other => other != remaining && (other.sourceGene == null || !other.sourceGene.Overridden) && remaining.def.CanSuppress(other)))
                        remaining.suppressedByTrait = false;
        }

        /// <summary>按测试基因的覆盖和抑制规则更新基因抑制标记，同时保留特质之间的抑制标记。</summary>
        public void RecalculateSuppression()
        {
            // 原版重算基因抑制关系，不会清空由其他特质造成的抑制标记。
            RecalculateCount++;
            foreach (Trait trait in allTraits)
            {
                trait.suppressedByGene = trait.sourceGene?.Overridden == true ? trait.sourceGene.overridingGene : pawn.genes.GenesListForReading.FirstOrDefault(gene => gene.Active && gene.def.suppressedTraits.Any(suppressed => suppressed.def == trait.def && suppressed.degree == trait.Degree));
            }
        }

        /// <summary>记录私有缓存刷新入口的调用次数，使生产代码的反射路径能够在测试中执行。</summary>
        private void RecacheTraits()
        {
            RecacheCount++;
        }
    }

    public class StoryTracker
    {
        public TraitSet traits;
        public BackstoryDef Childhood, Adulthood;
        /// <summary>为所属测试角色建立特质集合。</summary>
        public StoryTracker(Pawn pawn)
        {
            traits = new TraitSet(pawn);
        }
    }

    public class NeedsTracker
    {
        public Mood mood;
        /// <summary>始终返回空需求，使人格特质用例无需运行真实恶堕需求系统。</summary>
        public T TryGetNeed<T>()
            where T : class => null;
        /// <summary>提供需求重建接口的空实现；本测试不验证游戏需求实例的增删。</summary>
        public void AddOrRemoveNeedsAsAppropriate()
        {
        }
    }

    public class Mood
    {
        public Thoughts thoughts;
    }

    public class Thoughts
    {
        public MemoryHandler memories;
        public SituationalThoughts situational;
    }

    public class SituationalThoughts
    {
        /// <summary>提供情境想法缓存失效通知的空实现。</summary>
        public void Notify_SituationalThoughtsDirty()
        {
        }
    }

    public class MemoryHandler
    {
        public List<Thought_Memory> Memories = new List<Thought_Memory>();
        /// <summary>将生产代码恢复的记忆加入测试列表，不模拟真实记忆合并规则。</summary>
        public void TryGainMemory(Thought_Memory thought)
        {
            Memories.Add(thought);
        }
    }

    public class Thought_Memory
    {
        public ThoughtDef def;
        public int age;
        public float moodPowerFactor;
        public Pawn otherPawn;
    }

    public static class ThoughtUtility
    {
        /// <summary>允许测试角色获得任意测试想法，使记忆入口无需依赖游戏想法判定系统。</summary>
        public static bool CanGetThought(Pawn p, ThoughtDef d) => true;
    }

    public static class ThoughtMaker
    {
        /// <summary>根据定义创建最小记忆实例，供生产恢复代码填写保存的记忆数据。</summary>
        public static object MakeThought(ThoughtDef d) => new Thought_Memory
        {
            def = d
        };
    }

    public class SkillRecord
    {
        public SkillDef def;
        public int Level;
        public float xpSinceLastLevel;
        public Passion passion;
    }

    public class SkillsTracker
    {
        public List<SkillRecord> skills = new List<SkillRecord>();
        /// <summary>从测试技能列表中取得指定定义的记录。</summary>
        public SkillRecord GetSkill(SkillDef d) => skills.FirstOrDefault(s => s.def == d);
        /// <summary>提供技能禁用状态通知的空实现；本测试不验证真实技能限制缓存。</summary>
        public void Notify_SkillDisablesChanged()
        {
        }

        /// <summary>提供技能资质缓存失效接口的空实现。</summary>
        public void DirtyAptitudes()
        {
        }
    }

    public class DirectRelation
    {
        public PawnRelationDef def;
        public Pawn otherPawn;
    }

    public class RelationsTracker
    {
        public List<DirectRelation> DirectRelations = new List<DirectRelation>();
        /// <summary>清空测试角色的直接关系，供人格提取及植入入口使用。</summary>
        public void ClearAllRelations()
        {
            DirectRelations.Clear();
        }

        /// <summary>在测试列表中添加指定关系和关联角色，不模拟完整社交关系系统。</summary>
        public void AddDirectRelation(PawnRelationDef d, Pawn p)
        {
            DirectRelations.Add(new DirectRelation { def = d, otherPawn = p });
        }
    }

    public static class SkillDefOf
    {
        public static SkillDef Social = new SkillDef();
    }

    public static class MessageTypeDefOf
    {
        public static object NeutralEvent = new object ();
    }

    public static class MeditationFocusTypeAvailabilityCache
    {
        /// <summary>提供冥想焦点缓存清理接口的空实现。</summary>
        public static void ClearFor(Pawn pawn)
        {
        }
    }
}

namespace HarmonyLib
{
    public static class AccessTools
    {
        /// <summary>按名称反射查找公开或非公开方法，模拟本次生产代码使用的反射辅助接口。</summary>
        public static System.Reflection.MethodInfo Method(Type type, string name) => type.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
    }
}

namespace SexSlaveCraft
{
    using Verse;

    public enum PawnIdentity
    {
        Unset,
        Slave
    }

    public enum SexSlaveSpecializationType
    {
        None
    }

    public class Need_Corruption
    {
        public float CurLevelPercentage, HighestCorruptionLevel;
        /// <summary>提供恶堕设置接口的空实现；人格特质测试不验证恶堕数值变化。</summary>
        public void SetCorruption(float x, bool b)
        {
        }

        /// <summary>提供当前及历史最高恶堕恢复接口的空实现。</summary>
        public void RestoreCorruption(float x, float y)
        {
        }
    }

    public class Hediff_ChainOfSexSlave : Hediff
    {
    }

    public class RecipeDef_PSTag : RecipeDef
    {
        public HediffDef hediffToAdd;
    }

    public class CompSexSlaveTraining
    {
        public PawnIdentity pawnIdentity;
        public SexSlaveSpecializationType specializationType;
        public float specializationProgress;
        public bool milkProductionEnabled;
        /// <summary>记录测试专精类型，供生产人格恢复流程写入组件状态。</summary>
        public void SetSpecialization(SexSlaveSpecializationType t)
        {
            specializationType = t;
        }

        /// <summary>提供专精状态同步接口的空实现；本测试不验证专精系统。</summary>
        public static void ReconcileSpecialization(Pawn p)
        {
        }
    }

    public static class RabbitCloneUtility
    {
        /// <summary>将测试角色视为普通角色，避免提取入口因克隆体规则提前返回。</summary>
        public static bool IsRabbitClone(Pawn p) => false;
    }

    public static class PersonalityGelUtility
    {
        /// <summary>返回最小凝胶物品定义，使提取流程不依赖真实物品配置。</summary>
        public static ThingDef GetPersonalityGelDefForPawn(Pawn p) => new ThingDef();
    }

    public static class SSCDefOf
    {
        public static TraitDef SSC_Trait_PE = new TraitDef
        {
            defName = "SSC_Trait_PE"
        };
        public static HediffDef SSC_PersonalityExcreting = new HediffDef
        {
            defName = "SSC_PersonalityExcreting"
        };
        public static HediffDef SSC_PersonalityExcreted_Done = new HediffDef
        {
            defName = "SSC_PersonalityExcreted_Done"
        };
        public static HediffDef SSC_Hediff_Bus, SSC_Hediff_Bus_Final, SSC_Hediff_Cow, SSC_Hediff_Cow_Final;
    }

    public static class SSCBondUtility
    {
        /// <summary>提供解除绑定接口的空实现；人格特质测试不运行锁链系统。</summary>
        public static void Unbind(Pawn p)
        {
        }

        /// <summary>返回绑定失败以跳过锁链恢复分支，明确该系统不在本测试替身的验证范围内。</summary>
        public static bool Bind(Pawn m, Pawn p, bool b) => false;
        /// <summary>返回空锁链实例，与测试环境不建立真实绑定的设定一致。</summary>
        public static Hediff_ChainOfSexSlave GetChain(Pawn p) => null;
    }

    public static class SSCIdentityUtility
    {
        /// <summary>提供身份设置接口的空实现；本测试不验证身份系统的副作用。</summary>
        public static void TrySetIdentity(Pawn p, PawnIdentity i)
        {
        }

        /// <summary>提供性奴派生特质同步接口的空实现；本测试聚焦普通人格特质。</summary>
        public static void SyncSexSlaveTraitFromHighestCorruption(Pawn p)
        {
        }
    }

    public static class PetSpecializationUtility
    {
        /// <summary>提供宠物专精状态清理接口的空实现。</summary>
        public static void RemoveAllPetStates(Pawn p)
        {
        }

        /// <summary>将测试标签视为非宠物专精标签，不模拟真实专精标签分类。</summary>
        public static bool IsPetTag(HediffDef d) => false;
        /// <summary>提供宠物专精标签应用接口的空实现。</summary>
        public static void ApplyExclusivePetTags(Pawn p, CompPersonalityStore c)
        {
        }

        /// <summary>提供宠物专精标签存储接口的空实现。</summary>
        public static void StoreExclusivePetTags(CompPersonalityStore c, Pawn p)
        {
        }
    }

    public static class BusSpecializationUtility
    {
        /// <summary>提供公交车专精标签应用接口的空实现。</summary>
        public static void ApplyExclusiveBusTags(Pawn p, CompPersonalityStore c)
        {
        }

        /// <summary>提供奶牛专精标签应用接口的空实现。</summary>
        public static void ApplyExclusiveCowTags(Pawn p, CompPersonalityStore c)
        {
        }

        /// <summary>提供公交车专精标签存储接口的空实现。</summary>
        public static void StoreExclusiveBusTags(CompPersonalityStore c, Pawn p)
        {
        }

        /// <summary>提供奶牛专精标签存储接口的空实现。</summary>
        public static void StoreExclusiveCowTags(CompPersonalityStore c, Pawn p)
        {
        }
    }

    public static class Strings
    {
        /// <summary>直接返回角色文字，替代提取完成提示的本地化生成。</summary>
        public static string Message_PersonalityExcretedComplete(string s) => s;
        /// <summary>返回来源文字，提供植入完成提示的最小测试实现。</summary>
        public static string Message_PersonalityFusionComplete(string s, string t) => s;
        /// <summary>直接返回人格名称，供凝胶检查信息生成流程使用。</summary>
        public static string Inspect_Personality(string s) => s;
        public static string Inspect_PersonalityNone = "none", Inspect_SexSlaveTag = "ss", Inspect_ExtraStatus = "tags";
        /// <summary>将关系数量转为文字，不验证真实界面措辞。</summary>
        public static string Inspect_RelationsCount(int n) => n.ToString();
        /// <summary>直接返回主人文字，替代所属关系提示的本地化生成。</summary>
        public static string Inspect_BelongsTo(string s) => s;
    }
}
