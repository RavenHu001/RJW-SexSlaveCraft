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
        /// <summary>保存或读取锁链的严重度与主人引用，供人格植入时重建绑定关系。</summary>
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
        /// <summary>保存或读取记忆定义、已存在时间、心情倍率及关联角色。</summary>
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
        /// <summary>保存或读取技能定义、等级、经验和热情。</summary>
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
        /// <summary>保存或读取直接关系的定义与关联角色引用。</summary>
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
        /// <summary>将组件配置关联到人格存储组件的实现类型。</summary>
        public CompProperties_PersonalityStore()
        {
            this.compClass = typeof(CompPersonalityStore);
        }
    }

    // =========================================================
    // 3. 主组件逻辑
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
        // 版本 0：旧快照可能混有原身体基因授予的特质。
        // 版本 1：仅保存普通人格特质，包括暂时受到抑制的条目。
        public int traitSnapshotVersion;
        public List<StoredMemoryData> storedMemories = new List<StoredMemoryData>();
        public List<StoredSkillData> storedSkills = new List<StoredSkillData>();
        public List<StoredRelationData> storedRelations = new List<StoredRelationData>();

        // 背景故事
        public BackstoryDef childhood;
        public BackstoryDef adulthood;

        // 状态标签字典：键为健康状态定义，值为严重度。
        // ---------------------------------------------------------
        public Dictionary<HediffDef, float> hediffTags = new Dictionary<HediffDef, float>();

        // ---------------------------------------------------------
        // 数据保存/读取 (Scribe)
        // ---------------------------------------------------------
        /// <summary>保存或读取完整人格快照及特质格式版本，并在读档后补齐缺失的状态标签字典。</summary>
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
            Scribe_Values.Look(ref traitSnapshotVersion, "traitSnapshotVersion", 0);
            Scribe_Collections.Look(ref storedMemories, "storedMemories", LookMode.Deep);
            Scribe_Collections.Look(ref storedSkills, "storedSkills", LookMode.Deep);
            Scribe_Collections.Look(ref storedRelations, "storedRelations", LookMode.Deep);

            // 背景故事定义
            Scribe_Defs.Look(ref childhood, "childhood");
            Scribe_Defs.Look(ref adulthood, "adulthood");

            // 保存状态标签字典。
            // 使用定义引用模式保存键，使用数值模式保存严重度。
            Scribe_Collections.Look(ref hediffTags, "hediffTags", LookMode.Def, LookMode.Value);

            // 防止读档后字典为null导致报错
            if (Scribe.mode == LoadSaveMode.PostLoadInit && hediffTags == null)
            {
                hediffTags = new Dictionary<HediffDef, float>();
            }
        }

        // ---------------------------------------------------------
        // 界面显示信息
        // ---------------------------------------------------------
        /// <summary>生成人格凝胶的检查面板文字，显示姓名、身份、关系和附加状态。</summary>
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

            // 显示状态标签信息。
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
        /// <summary>复制另一凝胶的人格数据并为可变条目建立独立实例，保留特质快照的版本及缺失状态。</summary>
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

            // 3. 复制列表，为保存的数据建立独立实例。
            // 缺失快照必须保留为 null，不能转成合法空列表，
            // 否则植入时会误清空接收者的普通特质。
            this.storedTraits = other.storedTraits?
                .Where(t => t?.def != null)
                .Select(t => new Trait(t.def, t.Degree, t.ScenForced))
                .ToList();
            this.traitSnapshotVersion = other.traitSnapshotVersion;

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

            // 4. 复制状态标签字典。
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
        // 状态标签操作接口
        // ---------------------------------------------------------
        /// <summary>写入或更新指定健康状态标签的严重度。</summary>
        public void SetTag(HediffDef def, float severity)
        {
            if (hediffTags == null) hediffTags = new Dictionary<HediffDef, float>();
            if (hediffTags.ContainsKey(def))
                hediffTags[def] = severity;
            else
                hediffTags.Add(def, severity);
        }

        /// <summary>读取指定标签的严重度，不存在时返回零。</summary>
        public float GetTagSeverity(HediffDef def)
        {
            if (hediffTags != null && hediffTags.TryGetValue(def, out float val)) return val;
            return 0f;
        }

        /// <summary>从人格快照中移除指定健康状态标签。</summary>
        public void RemoveTag(HediffDef def)
        {
            if (hediffTags != null && hediffTags.ContainsKey(def)) hediffTags.Remove(def);
        }

        /// <summary>判断人格快照是否包含指定健康状态标签。</summary>
        public bool HasTag(HediffDef def)
        {
            return hediffTags != null && hediffTags.ContainsKey(def);
        }

        // ---------------------------------------------------------
        // 核心功能：存储角色数据
        // ---------------------------------------------------------
        /// <summary>采集角色的身份、技能、记忆、普通特质、关系及专精状态，生成独立的人格快照。</summary>
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

            // 3. 保存恶堕数据。
            if (p.needs != null)
            {
                var corNeed = p.needs.TryGetNeed<Need_Corruption>();
                if (corNeed != null)
                {
                    this.corruptionLevel = corNeed.CurLevelPercentage;
                    this.highestCorruptionLevel = corNeed.HighestCorruptionLevel;
                }
            }

            // 4. 保存锁链健康状态数据。
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

            // 基因及其授予特质属于身体。被抑制的普通特质仍属于人格，
            // 保存定义/等级，在新身体上重新决定是否受抑制。
            storedTraits = PersonalityTraitUtility.CaptureTraits(p);
            traitSnapshotVersion = 1;

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
            // 10. 收集角色已有的专精状态标签，存入新凝胶。
            // ---------------------------------------------------------
            if (this.hediffTags == null) this.hediffTags = new Dictionary<HediffDef, float>();
            this.hediffTags.Clear();

            // 遍历所有 XML 中定义的特化配方
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs.OfType<RecipeDef_PSTag>())
            {
                if (recipe.hediffToAdd != null)
                {
                    // 在角色身上查找配方对应的健康状态。
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
