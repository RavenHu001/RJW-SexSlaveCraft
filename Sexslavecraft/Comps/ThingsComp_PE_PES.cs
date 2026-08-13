using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    // =========================================================
    // 1. 数据存储辅助类 (保持不变)
    // =========================================================

    public class StoredChainHediffData : IExposable
    {
        public float severity;
        public Pawn masterPawn;
        public void ExposeData()
        {
            Scribe_Values.Look(ref severity, "severity", 0f);
            Scribe_References.Look(ref masterPawn, "masterPawn", saveDestroyedThings: true);
        }
    }

    public class StoredMemoryData : IExposable
    {
        public ThoughtDef def;
        public int age;
        public float moodPowerFactor = 1f;
        public Pawn otherPawn;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref age, "age", 0);
            Scribe_Values.Look(ref moodPowerFactor, "moodPowerFactor", 1f);
            Scribe_References.Look(ref otherPawn, "otherPawn", saveDestroyedThings: true);
        }
    }

    public class StoredSkillData : IExposable
    {
        public SkillDef def;
        public int level;
        public float xp;
        public Passion passion;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref level, "level", 0);
            Scribe_Values.Look(ref xp, "xp", 0f);
            Scribe_Values.Look(ref passion, "passion", Passion.None);
        }
    }

    public class StoredRelationData : IExposable
    {
        public PawnRelationDef def;
        public Pawn otherPawn;
        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref otherPawn, "otherPawn", saveDestroyedThings: true);
        }
    }

    // =========================================================
    // 2. 组件定义
    // =========================================================
    public class CompProperties_PersonalityStore : CompProperties
    {
        public CompProperties_PersonalityStore()
        {
            this.compClass = typeof(CompPersonalityStore);
        }
    }

    // =========================================================
    // 3. 主组件逻辑 (Main Component)
    // =========================================================
    public class CompPersonalityStore : ThingComp
    {
        // --- 基础信息 ---
        public string firstName;
        public string nickName;
        public string lastName;

        // --- 特殊状态 ---
        public bool hasSexSlaveTrait = false;
        public int sexSlaveDegree = 0;
        public PawnIdentity sscIdentity = PawnIdentity.Slave;
        public float corruptionLevel = -1f;
        public float highestCorruptionLevel = -1f;
        public SexSlaveSpecializationType specializationType = SexSlaveSpecializationType.None;
        public float specializationProgress = 0f;
        public bool personalityExcretionCompleted = false;
        public bool milkProductionEnabled = true;

        // --- 复杂数据对象 ---
        public StoredChainHediffData chainHediffData = null;

        // --- 列表数据 ---
        public List<Trait> storedTraits = new List<Trait>();
        public List<StoredMemoryData> storedMemories = new List<StoredMemoryData>();
        public List<StoredSkillData> storedSkills = new List<StoredSkillData>();
        public List<StoredRelationData> storedRelations = new List<StoredRelationData>();

        // 背景故事
        public BackstoryDef childhood;
        public BackstoryDef adulthood;

        // 🔥【新增】Tag 存储字典 (Key=Hediff, Value=严重度)
        // ---------------------------------------------------------
        public Dictionary<HediffDef, float> hediffTags = new Dictionary<HediffDef, float>();

        // ---------------------------------------------------------
        // 数据保存/读取 (Scribe)
        // ---------------------------------------------------------
        public override void PostExposeData()
        {
            base.PostExposeData();

            // 基础值
            Scribe_Values.Look(ref firstName, "firstName");
            Scribe_Values.Look(ref nickName, "nickName");
            Scribe_Values.Look(ref lastName, "lastName");
            Scribe_Values.Look(ref hasSexSlaveTrait, "hasSexSlaveTrait", false);
            Scribe_Values.Look(ref sexSlaveDegree, "sexSlaveDegree", 0);
            Scribe_Values.Look(ref sscIdentity, "sscIdentity", PawnIdentity.Slave);
            Scribe_Values.Look(ref corruptionLevel, "corruptionLevel", -1f);
            Scribe_Values.Look(ref highestCorruptionLevel, "highestCorruptionLevel", -1f);
            Scribe_Values.Look(ref specializationType, "specializationType", SexSlaveSpecializationType.None);
            Scribe_Values.Look(ref specializationProgress, "specializationProgress", 0f);
            Scribe_Values.Look(ref personalityExcretionCompleted, "personalityExcretionCompleted", false);
            Scribe_Values.Look(ref milkProductionEnabled, "milkProductionEnabled", true);

            // 复杂对象
            Scribe_Deep.Look(ref chainHediffData, "chainHediffData");

            // 列表
            Scribe_Collections.Look(ref storedTraits, "storedTraits", LookMode.Deep);
            Scribe_Collections.Look(ref storedMemories, "storedMemories", LookMode.Deep);
            Scribe_Collections.Look(ref storedSkills, "storedSkills", LookMode.Deep);
            Scribe_Collections.Look(ref storedRelations, "storedRelations", LookMode.Deep);

            // Defs
            Scribe_Defs.Look(ref childhood, "childhood");
            Scribe_Defs.Look(ref adulthood, "adulthood");

            // 🔥【新增】保存 Tags 字典
            // LookMode.Def 保存Key, LookMode.Value 保存Value
            Scribe_Collections.Look(ref hediffTags, "hediffTags", LookMode.Def, LookMode.Value);

            // 防止读档后字典为null导致报错
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hediffTags == null)
            {
                hediffTags = new Dictionary<HediffDef, float>();
            }
        }

        // ---------------------------------------------------------
        // UI 显示信息
        // ---------------------------------------------------------
        public override string CompInspectStringExtra()
        {
            string info = Strings.Inspect_Personality(nickName ?? Strings.Inspect_PersonalityNone);
            if (hasSexSlaveTrait) info += Strings.Inspect_SexSlaveTag;

            if (storedRelations != null && storedRelations.Count > 0)
            {
                info += Strings.Inspect_RelationsCount(storedRelations.Count);
            }

            if (chainHediffData != null && chainHediffData.masterPawn != null)
            {
                info += Strings.Inspect_BelongsTo(chainHediffData.masterPawn.LabelShort);
            }

            // 🔥【新增】显示 Tag 信息
            if (hediffTags != null && hediffTags.Count > 0)
            {
                info += Strings.Inspect_ExtraStatus;
                foreach (var pair in hediffTags)
                {
                    info += $"\n - {pair.Key.LabelCap}: {pair.Value.ToStringPercent()}";
                }
            }

            return info;
        }

        // ---------------------------------------------------------
        // 核心功能：数据深拷贝 (CopyFrom)
        // ---------------------------------------------------------
        public void CopyFrom(CompPersonalityStore other)
        {
            if (other == null) return;

            // 1. 基础值复制
            this.firstName = other.firstName;
            this.nickName = other.nickName;
            this.lastName = other.lastName;
            this.hasSexSlaveTrait = other.hasSexSlaveTrait;
            this.sexSlaveDegree = other.sexSlaveDegree;
            this.sscIdentity = other.sscIdentity;
            this.corruptionLevel = other.corruptionLevel;
            this.highestCorruptionLevel = other.highestCorruptionLevel;
            this.specializationType = other.specializationType;
            this.specializationProgress = other.specializationProgress;
            this.personalityExcretionCompleted = other.personalityExcretionCompleted;
            this.milkProductionEnabled = other.milkProductionEnabled;
            this.childhood = other.childhood;
            this.adulthood = other.adulthood;

            // 2. 复杂对象复制
            if (other.chainHediffData != null)
            {
                this.chainHediffData = new StoredChainHediffData
                {
                    severity = other.chainHediffData.severity,
                    masterPawn = other.chainHediffData.masterPawn
                };
            }
            else
            {
                this.chainHediffData = null;
            }

            // 3. 列表复制 (先清空，再添加新实例)
            this.storedTraits.Clear();
            if (other.storedTraits != null)
            {
                foreach (var t in other.storedTraits)
                    this.storedTraits.Add(new Trait(t.def, t.Degree, t.ScenForced));
            }

            this.storedSkills.Clear();
            if (other.storedSkills != null)
            {
                foreach (var s in other.storedSkills)
                    this.storedSkills.Add(new StoredSkillData { def = s.def, level = s.level, xp = s.xp, passion = s.passion });
            }

            this.storedMemories.Clear();
            if (other.storedMemories != null)
            {
                foreach (var m in other.storedMemories)
                    this.storedMemories.Add(new StoredMemoryData { def = m.def, age = m.age, moodPowerFactor = m.moodPowerFactor, otherPawn = m.otherPawn });
            }

            this.storedRelations.Clear();
            if (other.storedRelations != null)
            {
                foreach (var r in other.storedRelations)
                    this.storedRelations.Add(new StoredRelationData { def = r.def, otherPawn = r.otherPawn });
            }

            // 🔥 4. Tags 字典复制
            this.hediffTags.Clear();
            if (other.hediffTags != null)
            {
                foreach (var pair in other.hediffTags)
                {
                    this.hediffTags.Add(pair.Key, pair.Value);
                }
            }
        }

        // ---------------------------------------------------------
        // Tag 操作接口
        // ---------------------------------------------------------
        public void SetTag(HediffDef def, float severity)
        {
            if (hediffTags == null) hediffTags = new Dictionary<HediffDef, float>();
            if (hediffTags.ContainsKey(def))
                hediffTags[def] = severity;
            else
                hediffTags.Add(def, severity);
        }

        public float GetTagSeverity(HediffDef def)
        {
            if (hediffTags != null && hediffTags.TryGetValue(def, out float val)) return val;
            return 0f;
        }

        public void RemoveTag(HediffDef def)
        {
            if (hediffTags != null && hediffTags.ContainsKey(def)) hediffTags.Remove(def);
        }

        public bool HasTag(HediffDef def)
        {
            return hediffTags != null && hediffTags.ContainsKey(def);
        }

        // ---------------------------------------------------------
        // 核心功能：存储 Pawn 数据
        // ---------------------------------------------------------
        public void StorePawnData(Pawn p)
        {
            if (p == null) return;

            // 1. 存名字
            if (p.Name is NameTriple triple)
            {
                this.firstName = triple.First;
                this.nickName = triple.Nick;
                this.lastName = triple.Last;
            }
            else
            {
                this.nickName = p.Name.ToString();
            }

            // 2. 存特殊特质
            var ssTrait = p.story?.traits?.allTraits.FirstOrDefault(x => x.def.defName == "SexSlaveCraft_SexSlave");
            if (ssTrait != null)
            {
                this.hasSexSlaveTrait = true;
                this.sexSlaveDegree = ssTrait.Degree;
            }

            // 3. 存 Corruption
            if (p.needs != null)
            {
                var corNeed = p.needs.TryGetNeed<Need_Corruption>();
                if (corNeed != null)
                {
                    this.corruptionLevel = corNeed.CurLevelPercentage;
                    this.highestCorruptionLevel = corNeed.HighestCorruptionLevel;
                }
            }

            // 4. 存 Hediff 链接
            var chainHediff = p.health.hediffSet.hediffs.FirstOrDefault(x => x.def.defName == "Hediff_ChainOfSexSlave");
            if (chainHediff is HediffWithTarget hTarget)
            {
                this.chainHediffData = new StoredChainHediffData
                {
                    severity = chainHediff.Severity,
                    masterPawn = hTarget.target as Pawn
                };
            }

            // 5. 存技能
            storedSkills.Clear();
            if (p.skills != null)
            {
                foreach (var s in p.skills.skills)
                {
                    storedSkills.Add(new StoredSkillData
                    {
                        def = s.def,
                        level = s.Level,
                        xp = s.xpSinceLastLevel,
                        passion = s.passion
                    });
                }
            }

            // 6. 存记忆
            storedMemories.Clear();
            if (p.needs?.mood?.thoughts?.memories != null)
            {
                foreach (var mem in p.needs.mood.thoughts.memories.Memories)
                {
                    if (mem == null || mem.def == null) continue;

                    storedMemories.Add(new StoredMemoryData
                    {
                        def = mem.def,
                        age = mem.age,
                        moodPowerFactor = mem.moodPowerFactor,
                        otherPawn = (mem.otherPawn != null && !mem.otherPawn.Destroyed) ? mem.otherPawn : null
                    });
                }
            }

            CompSexSlaveTraining trainingComp = p.TryGetComp<CompSexSlaveTraining>();
            if (trainingComp != null)
            {
                sscIdentity = trainingComp.pawnIdentity;
                specializationType = trainingComp.specializationType;
                specializationProgress = trainingComp.specializationProgress;
                milkProductionEnabled = trainingComp.milkProductionEnabled;
            }
            personalityExcretionCompleted = false;

            // 7. 存普通特质
            storedTraits.Clear();
            if (p.story?.traits != null)
            {
                foreach (var t in p.story.traits.allTraits)
                {
                    if (t.def.defName != "SexSlaveCraft_SexSlave" && t.def != SSCDefOf.SSC_Trait_PE)
                    {
                        storedTraits.Add(new Trait(t.def, t.Degree, t.ScenForced));
                    }
                }
            }

            // 8. 存社会关系
            storedRelations.Clear();
            if (p.relations != null && p.relations.DirectRelations != null)
            {
                foreach (var rel in p.relations.DirectRelations)
                {
                    if (rel.otherPawn != null)
                    {
                        storedRelations.Add(new StoredRelationData
                        {
                            def = rel.def,
                            otherPawn = rel.otherPawn
                        });
                    }
                }
            }

            // 9. 背景故事
            if (p.story != null)
            {
                this.childhood = p.story.Childhood;
                this.adulthood = p.story.Adulthood;
            }

            // ---------------------------------------------------------
            // 🔥 10. 【新增核心闭环】智能搜刮特化 Tag，存入新凝胶
            // ---------------------------------------------------------
            if (this.hediffTags == null) this.hediffTags = new Dictionary<HediffDef, float>();
            this.hediffTags.Clear();

            // 遍历所有 XML 中定义的特化配方
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.OfType<RecipeDef_PSTag>())
            {
                if (recipe.hediffToAdd != null)
                {
                    // 在小人身上寻找这些特定的 Hediff
                    Hediff existingTag = p.health.hediffSet.GetFirstHediffOfDef(recipe.hediffToAdd);

                    if (existingTag != null)
                    {
                        // 找到后，保存它和它的严重度
                        this.hediffTags[recipe.hediffToAdd] = existingTag.Severity;
                    }
                }
            }

            BusSpecializationUtility.StoreExclusiveBusTags(this, p);
            BusSpecializationUtility.StoreExclusiveCowTags(this, p);
            PetSpecializationUtility.StoreExclusivePetTags(this, p);
        }
    }
}
