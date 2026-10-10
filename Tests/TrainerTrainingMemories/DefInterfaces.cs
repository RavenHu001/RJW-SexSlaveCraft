using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;

// Def 解析模型与真实 XML 加载：直接读取生产 Def 文件，避免复制数值表。
namespace Verse
{
    /// <summary>按 defName 建立类型化索引；缺失时返回 null，与生产 GetDef 语义一致。</summary>
    public static class DefDatabase<T> where T : Def
    {
        private static readonly Dictionary<string, T> entries = new Dictionary<string, T>();

        public static void Clear() => entries.Clear();

        public static void Register(T def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName)) return;
            entries[def.defName] = def;
        }

        public static T GetNamedSilentFail(string defName)
            => (defName != null && entries.TryGetValue(defName, out T def)) ? def : null;

        public static IEnumerable<T> AllDefs => entries.Values;
    }
}

// 工程 Def 常量入口所需的最小 Def 类型模型；具体语义由其余套件覆盖。
namespace RimWorld
{
    public class AbilityDef : Verse.Def { }
    public class HediffDef : Verse.Def { }
    public class JobDef : Verse.Def { }
    public class PawnRelationDef : Verse.Def { }
    public class PreceptDef : Verse.Def { }
    public class RecipeDef : Verse.Def { }
    public class ResearchProjectDef : Verse.Def { }
    public class RitualBehaviorDef : Verse.Def { }
    public class ThingDef : Verse.Def { }
    public class TraitDef : Verse.Def { }
}

// Def 名称常量入口：只保留本套件使用的训导官弱套与主人原套想法定义。
// 生产同名字段与加载语句由工程其它套件覆盖，这里不复制数值。
namespace SexSlaveCraft
{
    public static class SSCDefOf
    {
        public static RimWorld.ThoughtDef SSC_Training_Mood_Lvl1;
        public static RimWorld.ThoughtDef SSC_Training_Mood_Lvl2;
        public static RimWorld.ThoughtDef SSC_Training_Mood_Lvl3;
        public static RimWorld.ThoughtDef SSC_Training_MoodDynamic;
        public static RimWorld.ThoughtDef SSC_Training_Social_Lvl1;
        public static RimWorld.ThoughtDef SSC_Training_Social_Lvl2;
        public static RimWorld.ThoughtDef SSC_Training_Social_Lvl3;
        public static RimWorld.ThoughtDef SSC_Training_OpinionDynamic;

        public static RimWorld.ThoughtDef SSC_TrainerTraining_Mood_Lvl1;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_Mood_Lvl2;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_Mood_Lvl3;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_MoodDynamic;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_Social_Lvl1;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_Social_Lvl2;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_Social_Lvl3;
        public static RimWorld.ThoughtDef SSC_TrainerTraining_OpinionDynamic;

        public static RimWorld.ThoughtDef SSC_TrainerOfficer_DutyFulfilled;

        /// <summary>按生产入口的语义从类型化索引解析常量，缺失时保持 null。</summary>
        public static void Initialize()
        {
            SSC_Training_Mood_Lvl1 = GetDef("SSC_Training_Mood_Lvl1");
            SSC_Training_Mood_Lvl2 = GetDef("SSC_Training_Mood_Lvl2");
            SSC_Training_Mood_Lvl3 = GetDef("SSC_Training_Mood_Lvl3");
            SSC_Training_MoodDynamic = GetDef("SSC_Training_MoodDynamic");
            SSC_Training_Social_Lvl1 = GetDef("SSC_Training_Social_Lvl1");
            SSC_Training_Social_Lvl2 = GetDef("SSC_Training_Social_Lvl2");
            SSC_Training_Social_Lvl3 = GetDef("SSC_Training_Social_Lvl3");
            SSC_Training_OpinionDynamic = GetDef("SSC_Training_OpinionDynamic");

            SSC_TrainerTraining_Mood_Lvl1 = GetDef("SSC_TrainerTraining_Mood_Lvl1");
            SSC_TrainerTraining_Mood_Lvl2 = GetDef("SSC_TrainerTraining_Mood_Lvl2");
            SSC_TrainerTraining_Mood_Lvl3 = GetDef("SSC_TrainerTraining_Mood_Lvl3");
            SSC_TrainerTraining_MoodDynamic = GetDef("SSC_TrainerTraining_MoodDynamic");
            SSC_TrainerTraining_Social_Lvl1 = GetDef("SSC_TrainerTraining_Social_Lvl1");
            SSC_TrainerTraining_Social_Lvl2 = GetDef("SSC_TrainerTraining_Social_Lvl2");
            SSC_TrainerTraining_Social_Lvl3 = GetDef("SSC_TrainerTraining_Social_Lvl3");
            SSC_TrainerTraining_OpinionDynamic = GetDef("SSC_TrainerTraining_OpinionDynamic");

            SSC_TrainerOfficer_DutyFulfilled = GetDef("SSC_TrainerOfficer_DutyFulfilled");
        }

        private static RimWorld.ThoughtDef GetDef(string defName)
            => Verse.DefDatabase<RimWorld.ThoughtDef>.GetNamedSilentFail(defName);
    }
}

namespace TrainerTrainingMemories
{
    /// <summary>从真实 Def XML 解析 ThoughtDef，供生产常量入口与断言共用同一份数值来源。</summary>
    internal static class ThoughtDefLoader
    {
        /// <summary>弱套新文件的路径，相对仓库根目录。</summary>
        public const string TrainerDefsRelativePath = "Defs/ThoughtDefs/SSC_TrainerTrainingMemories.xml";

        /// <summary>主人原套所在的两个文件，原套数值只用于互相校核，不参与修改。</summary>
        public static readonly string[] MasterDefsRelativePaths =
        {
            "Defs/ThoughtDefs/SSC_TrainingOpinionMemory.xml",
            "Defs/ThoughtDefs/Thought_Training.xml"
        };

        /// <summary>解析一个 ThoughtDef 文件里的全部定义并登记到类型化索引。</summary>
        public static List<ThoughtDef> LoadFile(string repoRoot, string relativePath)
        {
            string fullPath = System.IO.Path.Combine(repoRoot, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            var document = XDocument.Load(fullPath);
            var loaded = new List<ThoughtDef>();
            foreach (XElement element in document.Root.Elements("ThoughtDef"))
            {
                ThoughtDef def = Parse(element);
                DefDatabase<ThoughtDef>.Register(def);
                loaded.Add(def);
            }

            return loaded;
        }

        /// <summary>按生产 XML 结构建立 ThoughtDef：类名、容量、时长与全部阶段数值。</summary>
        private static ThoughtDef Parse(XElement element)
        {
            var def = new ThoughtDef
            {
                defName = (string)element.Element("defName"),
                thoughtClass = ResolveType((string)element.Element("thoughtClass")),
                stackLimit = ParseInt(element.Element("stackLimit"), 3),
                stackLimitForSameOtherPawn = ParseInt(element.Element("stackLimitForSameOtherPawn"), 3),
                durationDays = ParseFloat(element.Element("durationDays"), 0f),
                showBubble = string.Equals((string)element.Element("showBubble"), "true", StringComparison.OrdinalIgnoreCase),
                stagesStack = string.Equals((string)element.Element("stagesStack"), "true", StringComparison.OrdinalIgnoreCase)
            };

            XElement stages = element.Element("stages");
            if (stages != null)
            {
                foreach (XElement stage in stages.Elements("li"))
                {
                    def.stages.Add(new ThoughtStage
                    {
                        label = (string)stage.Element("label"),
                        labelSocial = (string)stage.Element("labelSocial"),
                        description = (string)stage.Element("description"),
                        baseMoodEffect = ParseFloat(stage.Element("baseMoodEffect"), 0f),
                        baseOpinionOffset = ParseFloat(stage.Element("baseOpinionOffset"), 0f)
                    });
                }
            }

            return def;
        }

        private static int ParseInt(XElement element, int fallback)
            => element == null ? fallback : int.Parse(element.Value, CultureInfo.InvariantCulture);

        private static float ParseFloat(XElement element, float fallback)
            => element == null ? fallback : float.Parse(element.Value, CultureInfo.InvariantCulture);

        /// <summary>解析 thoughtClass：无命名空间的短名在 RimWorld 命名空间下查找。</summary>
        private static Type ResolveType(string className)
        {
            if (string.IsNullOrEmpty(className)) return typeof(Thought_Memory);

            Type type = Type.GetType(className, throwOnError: false);
            if (type != null) return type;

            type = typeof(Thought_Memory).Assembly.GetType("RimWorld." + className, throwOnError: false);
            if (type != null) return type;

            type = typeof(SexSlaveCraft.Thought_MemoryTrainerTraining).Assembly.GetType(className, throwOnError: false);
            if (type != null) return type;

            throw new InvalidOperationException("无法解析 thoughtClass：" + className);
        }
    }
}
