using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using rjw;
using Verse;

// EN: This file is SSC's central registry for Def lookups and translated strings.
// EN: SSCDefOf loads the Jobs, Hediffs, Recipes, Thoughts, and ritual defs that the rest of the mod expects to exist.
// CN: 这个文件是 SSC 的核心 Def 和翻译字符串注册表。
// CN: SSCDefOf 会把模组里依赖的 Job、Hediff、Recipe、Thought 和仪式 Def 全部集中加载进来。
namespace SexSlaveCraft
{
    [StaticConstructorOnStartup]
    public static class SSCDefOf
    {
        // EN: SSC-owned Jobs are mandatory and must exist for daily training, personality excretion, insertion, and the Binding Ritual.
        // CN: 这些是 SSC 自己的核心 Job，日常调教、人格排泄、人格植入和绑定仪式都依赖它们。
        public static JobDef TrainingSexSlave;
        public static JobDef Training_Ritual;
        public static JobDef SSC_TrainingReceiver;
        public static JobDef SSC_Job_PE; // 人格排泄作业
        public static JobDef SSC_Job_InsertPersonality; // 塞入人格凝胶作业
        public static JobDef SSC_Job_PetAffection; // 宠物亲昵作业

        // EN: These RJW jobs stay optional because SSC only uses them for compatibility-side rape / receiver checks.
        // CN: 这些 RJW Job 保持可选，因为 SSC 只在兼容强暴 / receiver 判定时会用到它们。
        public static JobDef GettinRaped;         // 被强暴 (Receiver)
        public static JobDef RapeComfortPawn;     // 强暴RBQ (Initiator)
        public static JobDef RandomRape;          // 随机强暴 (Initiator)
        public static JobDef RapeEnemy;           // 强暴敌人 (Initiator)
        public static JobDef RapeEnemyByInsect;   // 虫子强暴 (Initiator)
        public static JobDef RapeEnemyByAnimal;   // 动物强暴 (Initiator)
        public static JobDef RapeEnemyByMech;     // 机械体强暴 (Initiator)
        public static JobDef RapeEnemyToParasite; // 寄生强暴 (Initiator)

        // EN: The rest of the file groups SSC traits, hediffs, thoughts, recipes, and ritual defs in one place.
        // CN: 后面的字段则把 SSC 的特质、Hediff、想法、手术配方和仪式 Def 统一收在一个地方。
        // Traits
        public static TraitDef SexSlaveTrait;

        // Things
        public static ThingDef EFOutfit;
        public static ThingDef SSC_PersonalitySlime;

        // Mood Thoughts
        public static ThoughtDef SSC_Training_Mood_Lvl1;
        public static ThoughtDef SSC_Training_Mood_Lvl2;
        public static ThoughtDef SSC_Training_Mood_Lvl3;
        public static ThoughtDef SSC_Training_MoodDynamic;

        // Social Thoughts
        public static ThoughtDef SSC_Training_Social_Lvl1;
        public static ThoughtDef SSC_Training_Social_Lvl2;
        public static ThoughtDef SSC_Training_Social_Lvl3;
        public static ThoughtDef SSC_Training_OpinionDynamic;

        // Corruption Rape Thoughts
        public static ThoughtDef SSC_CorruptionRape_Stage1;
        public static ThoughtDef SSC_CorruptionRape_Stage2;
        public static ThoughtDef SSC_CorruptionRape_Stage3;
        public static ThoughtDef SSC_CorruptionRape_Stage4;
        public static ThoughtDef SSC_CorruptionRape_VictimToBystanderPositive;

        // 仪式相关
        public static ThoughtDef SSC_Ritual_Euphoria_1;
        public static ThoughtDef SSC_Ritual_Euphoria_2;
        public static ThoughtDef SSC_Ritual_Euphoria_3;
        public static ThoughtDef SSC_Ritual_Euphoria_4;
        public static ThoughtDef SSC_Ritual_Best;
        public static ThoughtDef SSC_Ritual_Good;
        public static ThoughtDef SSC_Ritual_Boring;
        public static ThoughtDef SSC_Ritual_Terrible;
        public static ThoughtDef SSC_SharedBedWithMaster;
        public static ThoughtDef SSC_SharedBedWithTrainer;
        public static PawnRelationDef SSC_FlawedLovers;

        // Hediffs
        public static HediffDef BridleOfSexSlave;
        public static HediffDef ChainOfSexSlave;
        public static HediffDef SSC_MasterBondEmpowerment;
        public static HediffDef SSC_FullGelatinizationTemporary;
        public static HediffDef SSC_FullGelatinizedBody;
        public static HediffDef SSC_PersonalityExcreting;
        public static HediffDef SSC_PersonalityExcreted_Done;
        public static HediffDef SSC_Hediff_Bus;
        public static HediffDef SSC_Hediff_Bus_Final;
        public static HediffDef SSC_Hediff_Cow;
        public static HediffDef SSC_Hediff_Cow_Final;
        public static HediffDef SSC_HumanCattleLactationBridge;
        public static HediffDef SSC_Hediff_RabbitCloneLink;
        public static HediffDef SSC_Hediff_RabbitCloneLowPNA;

        // 🔥【新增】人格排泄后的深度宕机状态
        public static HediffDef SSC_PostExcretionComa;
        // 🔥【新增】人格植入后的深度宕机状态
        public static HediffDef SSC_PostInsertionComa;
        // 🔥【新增】永久泌乳状态 (用于Equal Milking兼容)
        public static HediffDef SSC_Lactating_SubState;

        // Research
        public static ResearchProjectDef SSC_BasicTraining;
        public static ResearchProjectDef SSC_BodyPartTraining;
        public static ResearchProjectDef SSC_RES_CowTraining;

        // Recipes
        public static RecipeDef SSC_InducePersonalityExcretion;
        public static RecipeDef SSC_Surgery_GenderChange_MtF;
        public static RecipeDef SSC_ApplyGelatinization;
        public static RecipeDef SSC_ApplyFullGelatinization;

        public static TraitDef SSC_Trait_PE;
        public static ThingDef SSC_PS_P;             // PLUS款
        public static ThingDef SSC_PS_U;             // ULTRA款
        public static ThingDef SSC_PS_P_U;           // PLUS_ULTRA款

        // ==========================================
        // 雕像系列 (产物)
        // ==========================================
        public static ThingDef SSC_PersonalitySlime_Edited; // 初始款雕像
        public static ThingDef SSC_PS_P_Edited;             // PLUS款雕像
        public static ThingDef SSC_PS_U_Edited;             // ULTRA款雕像
        public static ThingDef SSC_PS_P_U_Edited;           // PLUS_ULTRA款雕像
        public static RitualBehaviorDef SSC_BindingRitualBehavior;
        public static PreceptDef SSC_BindingRitualPrecept;

        // EN: Use strict lookup for SSC-owned defs so missing XML breaks loudly during startup instead of failing later inside gameplay.
        // CN: 对 SSC 自有 Def 使用严格获取，这样 XML 缺失会在启动阶段直接报错，而不是拖到玩法中途才炸出来。
        /// <summary>按名称读取 SSC 自有定义，缺失时在启动阶段记录错误并返回 null。</summary>
        /// <remarks>包括主人/调教员同床记忆定义；调用方仍需处理 DLL 与 XML 不配套时的空引用。</remarks>
        private static T GetDef<T>(string defName) where T : Def
        {
            T def = DefDatabase<T>.GetNamedSilentFail(defName);
            if (def == null)
            {
                Log.Error($"[SexSlaveCraft] ⛔ 严重错误: 找不到 {typeof(T).Name} 定义 '{defName}'。请检查 XML 文件！");
            }
            return def;
        }

        /// <summary>为指定互动任务统一关闭持械显示；缺失的可选任务定义会被跳过。</summary>
        private static void HideWeaponsDuringSexJobs(params JobDef[] jobDefs)
        {
            foreach (JobDef jobDef in jobDefs)
            {
                if (jobDef == null) continue;

                // EN: RimWorld checks neverShowWeapon before drafted, duty, lord, and comp weapon-display rules.
                // CN: RimWorld 会先检查 neverShowWeapon，再处理征召、Duty、Lord 和组件要求的持械显示。
                jobDef.alwaysShowWeapon = false;
                jobDef.neverShowWeapon = true;
            }
        }

        /// <summary>缓存 SSC 自有及可选兼容定义，并初始化任务显示设置，供后续玩法和界面统一引用。</summary>
        /// <remarks>两种同床记忆均从 XML 读取，不在运行时构造替代定义；缺失信息由 GetDef 报告。</remarks>
        static SSCDefOf()
        {
            // EN: Step 1: load every SSC-owned Def first so downstream systems can safely assume they already exist.
            // CN: 步骤 1：先加载全部 SSC 自有 Def，方便下游系统安全地假定它们已经存在。
            TrainingSexSlave = GetDef<JobDef>("SSC_Training_SexSlave");
            Training_Ritual = GetDef<JobDef>("SSC_Training_Ritual");
            SSC_TrainingReceiver = GetDef<JobDef>("SSC_TrainingReceiver");
            SSC_Job_PE = GetDef<JobDef>("SSC_Job_PE");
            SSC_Job_InsertPersonality = GetDef<JobDef>("SSC_Job_InsertPersonality");
            SSC_Job_PetAffection = GetDef<JobDef>("SSC_Job_PetAffection");

            // EN: Sex jobs must suppress equipped weapons even when their XML or ritual state asks RimWorld to show them.
            // CN: 性交 Job 必须压过 XML 或仪式状态里的持械要求，避免角色带着武器播放动画。
            HideWeaponsDuringSexJobs(TrainingSexSlave, Training_Ritual, SSC_TrainingReceiver, SSC_Job_PE);

            // EN: Step 2: then try the optional RJW compatibility defs without turning missing entries into startup errors.
            // CN: 步骤 2：再去读取可选的 RJW 兼容 Def，并且不要把缺失项升级成启动错误。
            GettinRaped = DefDatabase<JobDef>.GetNamedSilentFail("GettinRaped");
            RapeComfortPawn = DefDatabase<JobDef>.GetNamedSilentFail("RapeComfortPawn");
            RandomRape = DefDatabase<JobDef>.GetNamedSilentFail("RandomRape");
            RapeEnemy = DefDatabase<JobDef>.GetNamedSilentFail("RapeEnemy");
            RapeEnemyByInsect = DefDatabase<JobDef>.GetNamedSilentFail("RapeEnemyByInsect");
            RapeEnemyByAnimal = DefDatabase<JobDef>.GetNamedSilentFail("RapeEnemyByAnimal");
            RapeEnemyByMech = DefDatabase<JobDef>.GetNamedSilentFail("RapeEnemyByMech");
            RapeEnemyToParasite = DefDatabase<JobDef>.GetNamedSilentFail("RapeEnemyToParasite");

            // --- Traits ---
            SexSlaveTrait = GetDef<TraitDef>("SexSlaveCraft_SexSlave");

            // --- Things ---
            EFOutfit = GetDef<ThingDef>("Apparel_EFOutfit");
            SSC_PersonalitySlime = GetDef<ThingDef>("SSC_PersonalitySlime");

            // --- Mood Thoughts ---
            SSC_Training_Mood_Lvl1 = GetDef<ThoughtDef>("SSC_Training_Mood_Lvl1");
            SSC_Training_Mood_Lvl2 = GetDef<ThoughtDef>("SSC_Training_Mood_Lvl2");
            SSC_Training_Mood_Lvl3 = GetDef<ThoughtDef>("SSC_Training_Mood_Lvl3");
            SSC_Training_MoodDynamic = GetDef<ThoughtDef>("SSC_Training_MoodDynamic");

            // --- Social Thoughts ---
            SSC_Training_Social_Lvl1 = GetDef<ThoughtDef>("SSC_Training_Social_Lvl1");
            SSC_Training_Social_Lvl2 = GetDef<ThoughtDef>("SSC_Training_Social_Lvl2");
            SSC_Training_Social_Lvl3 = GetDef<ThoughtDef>("SSC_Training_Social_Lvl3");
            SSC_Training_OpinionDynamic = GetDef<ThoughtDef>("SSC_Training_OpinionDynamic");

            // --- Corruption Rape Thoughts ---
            SSC_CorruptionRape_Stage1 = GetDef<ThoughtDef>("SSC_CorruptionRape_Stage1");
            SSC_CorruptionRape_Stage2 = GetDef<ThoughtDef>("SSC_CorruptionRape_Stage2");
            SSC_CorruptionRape_Stage3 = GetDef<ThoughtDef>("SSC_CorruptionRape_Stage3");
            SSC_CorruptionRape_Stage4 = GetDef<ThoughtDef>("SSC_CorruptionRape_Stage4");
            SSC_CorruptionRape_VictimToBystanderPositive = GetDef<ThoughtDef>("SSC_CorruptionRape_VictimToBystanderPositive");

            // --- Ritual / Hediff / Relation / Research / Recipe ---
            SSC_Ritual_Euphoria_1 = GetDef<ThoughtDef>("SSC_Ritual_Euphoria_1");
            SSC_Ritual_Euphoria_2 = GetDef<ThoughtDef>("SSC_Ritual_Euphoria_2");
            SSC_Ritual_Euphoria_3 = GetDef<ThoughtDef>("SSC_Ritual_Euphoria_3");
            SSC_Ritual_Euphoria_4 = GetDef<ThoughtDef>("SSC_Ritual_Euphoria_4");
            SSC_Ritual_Best = GetDef<ThoughtDef>("SSC_Ritual_Best");
            SSC_Ritual_Good = GetDef<ThoughtDef>("SSC_Ritual_Good");
            SSC_Ritual_Boring = GetDef<ThoughtDef>("SSC_Ritual_Boring");
            SSC_Ritual_Terrible = GetDef<ThoughtDef>("SSC_Ritual_Terrible");
            SSC_SharedBedWithMaster = GetDef<ThoughtDef>("SSC_SharedBedWithMaster");
            SSC_SharedBedWithTrainer = GetDef<ThoughtDef>("SSC_SharedBedWithTrainer");

            BridleOfSexSlave = GetDef<HediffDef>("Hediff_BridleOfSexSlave");
            ChainOfSexSlave = GetDef<HediffDef>("Hediff_ChainOfSexSlave");
            SSC_MasterBondEmpowerment = GetDef<HediffDef>("SSC_MasterBondEmpowerment");
            SSC_FullGelatinizationTemporary = GetDef<HediffDef>("SSC_FullGelatinizationTemporary");
            SSC_FullGelatinizedBody = GetDef<HediffDef>("SSC_FullGelatinizedBody");
            SSC_PersonalityExcreting = GetDef<HediffDef>("Hediff_SSC_PersonalityExcreting");
            SSC_PersonalityExcreted_Done = GetDef<HediffDef>("Hediff_SSC_PersonalityExcreted_Done");
            SSC_Hediff_Bus = GetDef<HediffDef>("SSC_Hediff_Bus");
            SSC_Hediff_Bus_Final = GetDef<HediffDef>("SSC_Hediff_Bus_Final");
            SSC_Hediff_Cow = GetDef<HediffDef>("SSC_Hediff_Cow");
            SSC_Hediff_Cow_Final = GetDef<HediffDef>("SSC_Hediff_Cow_Final");
            SSC_HumanCattleLactationBridge = DefDatabase<HediffDef>.GetNamedSilentFail("SSC_HumanCattleLactationBridge");
            SSC_Hediff_RabbitCloneLink = GetDef<HediffDef>("SSC_Hediff_RabbitCloneLink");
            SSC_Hediff_RabbitCloneLowPNA = GetDef<HediffDef>("SSC_Hediff_RabbitCloneLowPNA");

            // 🔥【新增】获取排泄后的宕机状态
            SSC_PostExcretionComa = GetDef<HediffDef>("SSC_PostExcretionComa");
            // 🔥【新增】获取植入后的宕机状态
            SSC_PostInsertionComa = GetDef<HediffDef>("SSC_PostInsertionComa");
            // 🔥【新增】获取永久泌乳状态 (用于Equal Milking兼容)
            SSC_Lactating_SubState = GetDef<HediffDef>("SSC_Lactating_SubState");

            SSC_FlawedLovers = GetDef<PawnRelationDef>("SSC_FlawedLovers");

            SSC_BasicTraining = GetDef<ResearchProjectDef>("SSC_RES_BasicTraining");
            SSC_BodyPartTraining = GetDef<ResearchProjectDef>("SSC_RES_BodyPartTraining");
            SSC_RES_CowTraining = GetDef<ResearchProjectDef>("SSC_RES_CowTraining");

            SSC_InducePersonalityExcretion = GetDef<RecipeDef>("SSC_InducePersonalityExcretion");
            SSC_Surgery_GenderChange_MtF = GetDef<RecipeDef>("SSC_Surgery_GenderChange_MtF");
            SSC_ApplyGelatinization = GetDef<RecipeDef>("ApplyGelatinization");
            SSC_ApplyFullGelatinization = GetDef<RecipeDef>("ApplyFullGelatinization");
            SSC_Trait_PE = GetDef<TraitDef>("SSC_Trait_PE");

            // (这里删除了重复的 SSC_PersonalitySlime 获取，保留上面的即可)
            SSC_PS_P = GetDef<ThingDef>("SSC_PersonalitySlime_PLUS");
            SSC_PS_U = GetDef<ThingDef>("SSC_PersonalitySlime_ULTRA");
            SSC_PS_P_U = GetDef<ThingDef>("SSC_PersonalitySlime_PLUS_ULTRA");

            // --- 产物初始化 ---
            SSC_PersonalitySlime_Edited = GetDef<ThingDef>("SSC_PersonalitySlime_Edited");
            SSC_PS_P_Edited = GetDef<ThingDef>("SSC_PersonalitySlime_PLUS_Edited");
            SSC_PS_U_Edited = GetDef<ThingDef>("SSC_PersonalitySlime_ULTRA_Edited");
            SSC_PS_P_U_Edited = GetDef<ThingDef>("SSC_PersonalitySlime_PLUS_ULTRA_Edited");
            SSC_BindingRitualBehavior = GetDef<RitualBehaviorDef>("SSC_BindingRitualBehavior");
            SSC_BindingRitualPrecept = GetDef<PreceptDef>("SSC_BindingRitualPrecept");
            if (SSC_BindingRitualPrecept != null)
            {
                Log.Message($"[SSC DefOf] SSC_BindingRitualPrecept 加载成功: {SSC_BindingRitualPrecept.defName}");
            }
            else
            {
                Log.Error("[SSC DefOf] SSC_BindingRitualPrecept 加载失败！");
            }
        }
    }
    public static class Strings
    {
        // ... (保持你原有的 Strings 类代码不变) ...
        // 日常训练
        // {0}: score (string), {1}: level (int), {2}: corruptionGain (string)
        /// <summary>生成日常训练的评分和恶堕增长提示，不再显示旧评分等级。</summary>
        public static string DailyTrainingOutcome(string score, string corruption)
            => "SSC_DailyTrainingOutcome2".Translate(score, corruption);

        /// <summary>格式化旧版训练结果，保留分数、等级和恶堕变化三个显示参数。</summary>
        public static string DailyTrainingOutcome_Legacy(string score, int level, string corruption)
            => "SSC_DailyTrainingOutcome".Translate(score, level, corruption);

        // 仪式相关
        /// <summary>生成“主人 {0} | 性奴: {1}”的本地化文本，并填入调用参数。</summary>
        public static string Ritual_MasterSlaveHeader(string masterName, string slaveName)
            => "SSC_Ritual_MasterSlaveHeader".Translate(masterName, slaveName);

        /// <summary>生成仪式评分与等级的本地化结果文本。</summary>
        public static string Ritual_ScoreLevel(string score, int level)
            => "SSC_Ritual_ScoreLevel".Translate(score, level);

        /// <summary>将调用方提供的恶堕增量文本填入仪式结果说明。</summary>
        public static string Ritual_CorruptionGain(string corruption)
            => "SSC_Ritual_CorruptionGain".Translate(corruption);

        public static string Ritual_OutcomeHeader => "SSC_Ritual_OutcomeHeader".Translate();

        public static string Ritual_NoNewStage => "SSC_Ritual_NoNewStage".Translate();
        // 仪式结果
        /// <summary>生成“；由于理智脱出，{0} 彻底成为了 {1} 的性奴。”的本地化文本，并填入调用参数。</summary>
        public static string Ritual_FlawedLoversCreated(string slaveName, string masterName)
            => "SSC_Ritual_FlawedLoversCreated".Translate(slaveName, masterName);
        // 仪式角色检查
        public static string RitualRole_MustBeColonistSlavePrisoner
            => "SSC_RitualRole_MustBeColonistSlavePrisoner".Translate();

        /// <summary>生成角色已绑定其他主人的仪式拒绝说明，并显示当前主人姓名。</summary>
        public static string RitualRole_SlaveBoundToOther(string ownerName)
            => "SSC_RitualRole_SlaveBoundToOther".Translate(ownerName);

        public static string RitualRole_SlaveBoundToOther_Unknown
            => "SSC_RitualRole_SlaveBoundToOther_Unknown".Translate();

        // 绑定 / 转奴 / 特质变化文本
        /// <summary>生成“{0} 在炒饭智能的帮助下用无形的链子拴住了 {1}”的本地化文本，并填入调用参数。</summary>
        public static string Bond_BridleAdded(string masterName, string slaveName)
            => "SSC_Bond_BridleAdded".Translate(masterName, slaveName);

        /// <summary>生成锁链绑定通知，按性奴、主人的顺序填入姓名。</summary>
        public static string Bond_ChainAdded(string slaveName, string masterName)
            => "SSC_Bond_ChainAdded".Translate(slaveName, masterName);

        /// <summary>生成指定角色被奴役的仪式通知。</summary>
        public static string Ritual_Enslaved(string slaveName)
            => "SSC_Ritual_Enslaved".Translate(slaveName);

        /// <summary>生成指定角色奴役失败的仪式通知。</summary>
        public static string Ritual_EnslaveFailed(string slaveName)
            => "SSC_Ritual_EnslaveFailed".Translate(slaveName);

        /// <summary>格式化性奴特性从原等级提升到新等级的提示。</summary>
        public static string Trait_SexSlaveLevelUp(int oldDegree, int newDegree)
            => "SSC_Trait_SexSlaveLevelUp".Translate(oldDegree, newDegree);
        // 堕落阶段
        public static string Stage_Extreme => "SSC_Stage_Extreme".Translate();
        public static string Stage_Severe => "SSC_Stage_Severe".Translate();
        public static string Stage_Moderate => "SSC_Stage_Moderate".Translate();
        public static string Stage_Minor => "SSC_Stage_Minor".Translate();
        public static string Stage_Slight => "SSC_Stage_Slight".Translate();
        public static string Stage_Stable => "SSC_Stage_Stable".Translate();

        // 需求提示格式
        /// <summary>生成“{0}: {1} ({2})；{3}”的本地化文本，并填入调用参数。</summary>
        public static string Need_TipFormat(string label, string percent, string stage, string desc)
            => "SSC_Need_TipFormat".Translate(label, percent, stage, desc);
        //健康状态部分
        /// <summary>生成“{0} ({1})”的本地化文本，并填入调用参数。</summary>
        public static string Bridle_LabelWithNames(string label, string names)
                => "SSC_Bridle_LabelWithNames".Translate(label, names);

        /// <summary>将已整理的性奴姓名列表填入缰绳描述。</summary>
        public static string Bridle_DescSlaves(string names)
            => "SSC_Bridle_DescSlaves".Translate(names);
        /// <summary>将关联角色姓名附加到锁链标签的本地化模板中。</summary>
        public static string Chain_LabelWithPawn(string label, string pawnName)
            => "SSC_Chain_LabelWithPawn".Translate(label, pawnName);
        // 调教模式 Gizmo
        public static string TrainingMode_GizmoDesc => "SSC_TrainingMode_GizmoDesc".Translate();

        // 辅助方法：获取枚举对应的翻译
        // 逻辑：如果传入 Vaginal，它会自动去寻找 "SSC_Mode_Vaginal" 这个 Key
        /// <summary>根据调教姿势枚举读取对应的本地化名称。</summary>
        public static string GetModeLabel(TrainingActType type)
        {
            return $"SSC_Mode_{type}".Translate();
        }

        // Gizmo 标题：调用上面的方法获取中文名，然后填入标题
        /// <summary>把当前姿势的本地化名称填入调教模式按钮标题。</summary>
        public static string TrainingMode_GizmoLabel(TrainingActType type)
        {
            return "SSC_TrainingMode_GizmoLabel".Translate(GetModeLabel(type));
        }
        public static string Toggle_Label_Disabled => "SSC_Toggle_Label_Disabled".Translate();
        public static string Toggle_Label_Enabled => "SSC_Toggle_Label_Enabled".Translate();
        public static string Toggle_Label_Unknown => "SSC_Toggle_Label_Unknown".Translate();
        // 装备限制
        /// <summary>生成“{0} 明显不愿意穿这件衣服（试着提高恶堕值）。”的本地化文本，并填入调用参数。</summary>
        public static string EquipRestriction_NotCorruptedEnough(string pawnName)
            => "SSC_EquipRestriction_NotCorruptedEnough".Translate(pawnName);
        // Mod 设置
        public static string Setting_Category => "SSC_Setting_Category".Translate();
        public static string Setting_EnableSexSlaveProtectionRules => "SSC_Setting_EnableSexSlaveProtectionRules".Translate();
        public static string Setting_EnableSexSlaveProtectionRules_Desc => "SSC_Setting_EnableSexSlaveProtectionRules_Desc".Translate();
        public static string Setting_UseRJWOriginalEligibility => "SSC_Setting_UseRJWOriginalEligibility".Translate();
        public static string Setting_UseRJWOriginalEligibility_Desc => "SSC_Setting_UseRJWOriginalEligibility_Desc".Translate();
        public static string Setting_EnableDebugGizmos => "SSC_Setting_EnableDebugGizmos".Translate();
        public static string Setting_EnableDebugGizmos_Desc => "SSC_Setting_EnableDebugGizmos_Desc".Translate();
        public static string Setting_UseOldScoring => "SSC_Setting_UseOldScoring".Translate();
        public static string Setting_UseOldScoring_Desc => "SSC_Setting_UseOldScoring_Desc".Translate();

        // 简单开关状态描述
        public static string Toggle_Desc_Disabled => "SSC_Toggle_Desc_Disabled".Translate();
        public static string Toggle_Desc_Enabled => "SSC_Toggle_Desc_Enabled".Translate();
        // 精神状态压制
        /// <summary>生成“{0} 的狂暴被锁链压制了”的本地化文本，并填入调用参数。</summary>
        public static string Message_BerserkSuppressed(string pawnName)
            => "SSC_Message_BerserkSuppressed".Translate(pawnName);
        // 调教员选择 Gizmo
        // 1. 对应 XML: <SSC_Trainer_None>
        public static string Trainer_GizmoLabel_None => "SSC_Trainer_None".Translate();

        // 2. 对应 XML: <SSC_Trainer_Selected>，带参数 {0}
        /// <summary>将指定调教员姓名填入按钮标题。</summary>
        public static string Trainer_GizmoLabel_Selected(string name) => "SSC_Trainer_Selected".Translate(name);

        // 3. 对应 XML: <SSC_Trainer_Desc>
        public static string Trainer_GizmoDesc => "SSC_Trainer_Desc".Translate();

        // 4. 对应 XML: <SSC_Trainer_NoCandidates>
        public static string Trainer_NoCandidates => "SSC_Trainer_NoCandidates".Translate();

        // 5. 后缀不需要单独的方法，直接定义字符串方便拼接，或者也写成Translate
        public static string Trainer_MasterForced => "SSC_Trainer_MasterForced".Translate();
        public static string Trainer_Locked => "SSC_Trainer_Locked".Translate();
        public static string Trainer_Clear => "SSC_Trainer_Clear".Translate();

        // ==========================================
        // 新增本地化字符串 (扫描项目文件得到)
        // ==========================================
        
        // 消息类字符串
        /// <summary>生成“{0} 的人格已排泄完毕。”的本地化文本，并填入调用参数。</summary>
        public static string Message_PersonalityExcretedComplete(string pawnName) => "SSC_Message_PersonalityExcretedComplete".Translate(pawnName);
        /// <summary>生成“{0} 已完全融合了 {1} 的人格，并吸收了人格凝胶塑像的特质”的本地化文本，并填入调用参数。</summary>
        public static string Message_PersonalityFusionComplete(string consumerName, string nickName) => "SSC_Message_PersonalityFusionComplete".Translate(consumerName, nickName);
        public static string Message_SameIdeologyNoEffect => "SSC_Message_SameIdeologyNoEffect".Translate();
        /// <summary>生成指定角色转化完成的通知文本。</summary>
        public static string Message_EroticConversionComplete(string pawnName) => "SSC_Message_EroticConversionComplete".Translate(pawnName);
        /// <summary>生成指定角色人格排出完成的通知文本。</summary>
        public static string Message_PersonalityExcretionComplete(string pawnName) => "SSC_Message_PersonalityExcretionComplete".Translate(pawnName);
        /// <summary>格式化交易拒绝后被原谅的通知，保留商人、角色及代词参数。</summary>
        public static string Message_TradeRejectedForgiven(string traderName, string busName, string pronoun) => "SSC_Message_TradeRejectedForgiven".Translate(traderName, busName, pronoun);
        /// <summary>格式化交易双方自愿互动的通知文本。</summary>
        public static string Message_TradeConsensualSex(string busName, string traderName) => "SSC_Message_TradeConsensualSex".Translate(busName, traderName);
        /// <summary>格式化交易中发生强迫互动的通知，按受害者、施害者顺序填入姓名。</summary>
        public static string Message_TradeRapeOccurred(string victimName, string rapistName) => "SSC_Message_TradeRapeOccurred".Translate(victimName, rapistName);
        public static string Message_SlaveAlreadyLinked => "SSC_Message_SlaveAlreadyLinked".Translate();
        public static string Message_CannotUseNotHollow => "SSC_Message_CannotUseNotHollow".Translate();

        // UI标签类字符串
        public static string Label_SlaveTrainingIdentityMaster => "SSC_Label_SlaveTrainingIdentityMaster".Translate();
        /// <summary>生成“训练冷却中 ({0})”的本地化文本，并填入调用参数。</summary>
        public static string Label_TrainingCooldown(string time) => "SSC_Label_TrainingCooldown".Translate(time);
        public static string Label_WaitingForTraining => "SSC_Label_WaitingForTraining".Translate();
        /// <summary>将训练状态文本填入角色信息标签。</summary>
        public static string Label_SlaveTrainingStatus(string status) => "SSC_Label_SlaveTrainingStatus".Translate(status);
        /// <summary>将已格式化的百分比填入意识形态确定度下降标签。</summary>
        public static string Label_IdeologyCertaintyReduction(string percent) => "SSC_Label_IdeologyCertaintyReduction".Translate(percent);
        public static string Label_SexSlavePrefix => "SSC_Label_SexSlavePrefix".Translate();
        /// <summary>将调用方提供的关系数量文本填入信息标签。</summary>
        public static string Label_RelationsCount(string count) => "SSC_Label_RelationsCount".Translate(count);
        public static string Label_SkillsHeader => "SSC_Label_SkillsHeader".Translate();
        public static string Label_TraitsHeader => "SSC_Label_TraitsHeader".Translate();
        /// <summary>将姓名参数填入角色全名标签的本地化模板。</summary>
        public static string Label_FullName(string lastName) => "SSC_Label_FullName".Translate(lastName);
        /// <summary>生成包含技能名称与等级文本的信息标签。</summary>
        public static string Label_SkillLevel(string skillName, string level) => "SSC_Label_SkillLevel".Translate(skillName, level);
        /// <summary>生成技能热情标签，热情名称由调用方提供。</summary>
        public static string Label_SkillPassion(string passion) => "SSC_Label_SkillPassion".Translate(passion);
        /// <summary>生成技能经验标签，经验显示格式由调用方处理。</summary>
        public static string Label_SkillXP(string xp) => "SSC_Label_SkillXP".Translate(xp);
        public static string Label_Bloodlust => "SSC_Label_Bloodlust".Translate();

        // ==========================================
        // ITab_SexSlaveTraining 调教面板
        // ==========================================
        public static string ITab_IdentityHeader => "SSC_ITab_IdentityHeader".Translate();
        public static string ITab_IdentityUnset => "SSC_ITab_IdentityUnset".Translate();
        public static string ITab_IdentityMaster => "SSC_ITab_IdentityMaster".Translate();
        public static string ITab_IdentitySlave => "SSC_ITab_IdentitySlave".Translate();
        public static string ITab_SetIdentityUnset => "SSC_ITab_SetIdentityUnset".Translate();
        public static string ITab_SetAsSlave => "SSC_ITab_SetAsSlave".Translate();
        public static string ITab_SetAsMaster => "SSC_ITab_SetAsMaster".Translate();
        public static string ITab_AlreadyMaster => "SSC_ITab_AlreadyMaster".Translate();
        public static string ITab_TrainingLocked => "SSC_ITab_TrainingLocked".Translate();
        public static string ITab_TrainingLockedDesc => "SSC_ITab_TrainingLockedDesc".Translate();
        public static string ITab_TrainingSettingsHeader => "SSC_ITab_TrainingSettingsHeader".Translate();
        public static string ITab_AllowTraining => "SSC_ITab_AllowTraining".Translate();
        /// <summary>将剩余时间文本填入训练面板的冷却状态。</summary>
        public static string ITab_CooldownStatus(string time) => "SSC_ITab_CooldownStatus".Translate(time);
        public static string ITab_StatusReady => "SSC_ITab_StatusReady".Translate();
        public static string ITab_StatusDisabled => "SSC_ITab_StatusDisabled".Translate();
        public static string ITab_PoseHeader => "SSC_ITab_PoseHeader".Translate();
        public static string ITab_TrainerHeader => "SSC_ITab_TrainerHeader".Translate();
        public static string ITab_TrainerNone => "SSC_ITab_TrainerNone".Translate();
        public static string ITab_NoTrainerAvailable => "SSC_ITab_NoTrainerAvailable".Translate();
        public static string ITab_TrainerMasterSuffix => "SSC_ITab_TrainerMasterSuffix".Translate();
        public static string ITab_TrainerLockedSuffix => "SSC_ITab_TrainerLockedSuffix".Translate();
        public static string ITab_ClearTrainer => "SSC_ITab_ClearTrainer".Translate();
        public static string ITab_SpecializationHeader => "SSC_ITab_SpecializationHeader".Translate();
        public static string ITab_SpecializationNone => "SSC_ITab_SpecializationNone".Translate();
        public static string ITab_SpecializationBus => "SSC_ITab_SpecializationBus".Translate();
        public static string ITab_SpecializationCow => "SSC_ITab_SpecializationCow".Translate();
        public static string ITab_SpecializationPetCat => "SSC_ITab_SpecializationPetCat".Translate();
        public static string ITab_SpecializationPetDog => "SSC_ITab_SpecializationPetDog".Translate();
        public static string ITab_SpecializationPetRabbit => "SSC_ITab_SpecializationPetRabbit".Translate();
        /// <summary>将进度文本填入训练面板的特化进度标签。</summary>
        public static string ITab_SpecializationProgress(string progress) => "SSC_ITab_SpecializationProgress".Translate(progress);
        public static string ITab_SelectSpecializationNone => "SSC_ITab_SelectSpecializationNone".Translate();
        public static string ITab_SelectSpecializationBus => "SSC_ITab_SelectSpecializationBus".Translate();
        public static string ITab_SelectSpecializationCow => "SSC_ITab_SelectSpecializationCow".Translate();
        public static string ITab_SelectSpecializationPetCat => "SSC_ITab_SelectSpecializationPetCat".Translate();
        public static string ITab_SelectSpecializationPetDog => "SSC_ITab_SelectSpecializationPetDog".Translate();
        public static string ITab_SelectSpecializationPetRabbit => "SSC_ITab_SelectSpecializationPetRabbit".Translate();
        public static string ITab_SpecializationBusDisabledResearch => "SSC_ITab_SpecializationBusDisabledResearch".Translate();
        public static string ITab_SpecializationCowDisabledResearch => "SSC_ITab_SpecializationCowDisabledResearch".Translate();
        public static string ITab_SpecializationCowDisabledDegree => "SSC_ITab_SpecializationCowDisabledDegree".Translate();
        public static string ITab_SpecializationCowDisabledLactation => "SSC_ITab_SpecializationCowDisabledLactation".Translate();
        public static string ITab_SpecializationCowDisabledMissingRequirements => "SSC_ITab_SpecializationCowDisabledMissingRequirements".Translate();
        public static string ITab_SpecializationPetDisabledResearch => "SSC_ITab_SpecializationPetDisabledResearch".Translate();
        public static string ITab_SpecializationPetDisabledMissingRequirements => "SSC_ITab_SpecializationPetDisabledMissingRequirements".Translate();
        public static string ITab_SpecializationFinalizedSuffix => "SSC_ITab_SpecializationFinalizedSuffix".Translate();
        public static string ITab_SpecializationUnfinishedSuffix => "SSC_ITab_SpecializationUnfinishedSuffix".Translate();
        public static string ITab_SpecializationComplete => "SSC_ITab_SpecializationComplete".Translate();
        public static string ITab_MilkProductionToggle => "SSC_ITab_MilkProductionToggle".Translate();
        /// <summary>将模式名称填入兔特化繁殖模式标签。</summary>
        public static string ITab_RabbitReproductionMode(string mode) => "SSC_ITab_RabbitReproductionMode".Translate(mode);
        public static string ITab_RabbitReproductionOffspring => "SSC_ITab_RabbitReproductionOffspring".Translate();
        public static string ITab_RabbitReproductionClone => "SSC_ITab_RabbitReproductionClone".Translate();
        public static string ITab_SelectRabbitReproductionOffspring => "SSC_ITab_SelectRabbitReproductionOffspring".Translate();
        public static string ITab_SelectRabbitReproductionClone => "SSC_ITab_SelectRabbitReproductionClone".Translate();

        // ==========================================
        // ITab_PES 人格卡片面板
        // ==========================================
        public static string PES_Underage => "SSC_PES_Underage".Translate();
        /// <summary>生成包含完整姓名的人格悬停说明。</summary>
        public static string PES_FullName(string fullName) => "SSC_PES_FullName".Translate(fullName);
        /// <summary>生成童年背景的独立显示行。</summary>
        public static string PES_Childhood(string title) => "SSC_PES_Childhood".Translate(title);
        /// <summary>生成成年背景的独立显示行。</summary>
        public static string PES_Adulthood(string title) => "SSC_PES_Adulthood".Translate(title);
        public static string PES_NoBackground => "SSC_PES_NoBackground".Translate();
        /// <summary>生成人格特质区域的标题与有效条目数量。</summary>
        public static string PES_TraitsCount(int count) => "SSC_PES_TraitsCount".Translate(count);
        public static string PES_NoMaster => "SSC_PES_NoMaster".Translate();
        public static string PES_ChainSeverityLabel => "SSC_PES_ChainSeverityLabel".Translate();
        /// <summary>生成固定摘要中的社会关系数量。</summary>
        public static string PES_RelationshipSummary(int count) => "SSC_PES_RelationshipSummary".Translate(count);
        /// <summary>生成固定摘要中的记忆数量。</summary>
        public static string PES_MemorySummary(int count) => "SSC_PES_MemorySummary".Translate(count);
        public static string PES_TargetUnassigned => "SSC_PES_TargetUnassigned".Translate();
        public static string PES_ChangeTarget => "SSC_PES_ChangeTarget".Translate();
        public static string PES_SexSlaveTag => "SSC_PES_SexSlaveTag".Translate();
        /// <summary>生成人格编辑界面使用的关系数量标签。</summary>
        public static string PES_RelationsCount(int count) => "SSC_PES_RelationsCount".Translate(count);
        public static string PES_SkillsHeader => "SSC_PES_SkillsHeader".Translate();
        /// <summary>生成人格编辑界面的技能名称与等级文本。</summary>
        public static string PES_SkillLevel(string skillName, int level) => "SSC_PES_SkillLevel".Translate(skillName, level);
        /// <summary>生成人格编辑界面的技能热情文本。</summary>
        public static string PES_SkillPassion(string passion) => "SSC_PES_SkillPassion".Translate(passion);
        /// <summary>生成人格编辑界面的技能经验文本，保留传入的显示格式。</summary>
        public static string PES_SkillXP(string xp) => "SSC_PES_SkillXP".Translate(xp);
        public static string PES_TraitsHeader => "SSC_PES_TraitsHeader".Translate();
        public static string PES_NoTraits => "SSC_PES_NoTraits".Translate();
        public static string PES_BondHeader => "SSC_PES_BondHeader".Translate();
        /// <summary>生成人格编辑界面的主人姓名标签。</summary>
        public static string PES_MasterLabel(string masterName) => "SSC_PES_MasterLabel".Translate(masterName);
        /// <summary>生成人格编辑界面带有记忆数量的标题。</summary>
        public static string PES_MemoriesHeader(int count) => "SSC_PES_MemoriesHeader".Translate(count);
        public static string PES_MemoriesHint => "SSC_PES_MemoriesHint".Translate();

        // ==========================================
        // ITab_PES 人格凝胶分配相关
        // ==========================================
        public static string PES_AssignTarget => "SSC_PES_AssignTarget".Translate();
        /// <summary>生成“已指定: {0}”的本地化文本，并填入调用参数。</summary>
        public static string PES_AssignedTo(string name) => "SSC_PES_AssignedTo".Translate(name);
        public static string PES_Unassign => "SSC_PES_Unassign".Translate();
        public static string PES_NoHollowTargets => "SSC_PES_NoHollowTargets".Translate();
        /// <summary>生成人格编辑目标已经分配给其他角色的提示。</summary>
        public static string PES_AlreadyAssigned(string otherName) => "SSC_PES_AlreadyAssigned".Translate(otherName);
        public static string PES_WaitingForInsert => "SSC_PES_WaitingForInsert".Translate();
        /// <summary>生成指定角色人格插入完成的通知文本。</summary>
        public static string Message_PersonalityInserted(string name) => "SSC_Message_PersonalityInserted".Translate(name);

        // ==========================================
        // CompInspectStringExtra 检查面板
        // ==========================================
        /// <summary>生成“人格: {0}”的本地化文本，并填入调用参数。</summary>
        public static string Inspect_Personality(string name) => "SSC_Inspect_Personality".Translate(name);
        public static string Inspect_PersonalityNone => "SSC_Inspect_PersonalityNone".Translate();
        public static string Inspect_SexSlaveTag => "SSC_Inspect_SexSlaveTag".Translate();
        /// <summary>生成选中对象检查信息中的关系数量文本。</summary>
        public static string Inspect_RelationsCount(int count) => "SSC_Inspect_RelationsCount".Translate(count);
        /// <summary>生成选中对象检查信息中的主人归属文本。</summary>
        public static string Inspect_BelongsTo(string masterName) => "SSC_Inspect_BelongsTo".Translate(masterName);
        public static string Inspect_ExtraStatus => "SSC_Inspect_ExtraStatus".Translate();

        // ==========================================
        // HediffComp_ProducePAN 生产进度
        // ==========================================
        /// <summary>生成“进度: {0} (缺乏营养，停滞)”的本地化文本，并填入调用参数。</summary>
        public static string Produce_ProgressStalled(string progress) => "SSC_Produce_ProgressStalled".Translate(progress);
        /// <summary>生成“进度: {0}”的本地化文本，并填入调用参数。</summary>
        public static string Produce_Progress(string progress) => "SSC_Produce_Progress".Translate(progress);

        // ==========================================
        // Harmony_ShowPAN 检查栏生产进度
        // ==========================================
        /// <summary>生成“{0} 生产进度: {1}”的本地化文本，并填入调用参数。</summary>
        public static string Inspect_ProductionProgress(string productName, string progress) => "SSC_Inspect_ProductionProgress".Translate(productName, progress);

        // ==========================================
        // ConditioningUtility 仪式训练
        // ==========================================
        /// <summary>生成“{0} 的全身上下都在这场仪式中得到了深度的开发与训练。”的本地化文本，并填入调用参数。</summary>
        public static string Ritual_FullBodyTraining(string slaveName) => "SSC_Ritual_FullBodyTraining".Translate(slaveName);

        // ==========================================
        // RitualOutcomeEffectWorker 重复惩罚
        // ==========================================
        public static string Ritual_RepeatPenalty => "SSC_Ritual_RepeatPenalty".Translate();

        // ==========================================
        // CompAbilityEffect_EroticConversion 浮动文字
        // ==========================================
        /// <summary>生成“-{0} 认可度”的本地化文本，并填入调用参数。</summary>
        public static string Float_CertaintyReduction(string percent) => "SSC_Float_CertaintyReduction".Translate(percent);

        // ==========================================
        // Comp_Train 左下角状态显示
        // ==========================================
        public static string Train_IdentityMaster => "SSC_Train_IdentityMaster".Translate();
        public static string Train_IdentityUnset => "SSC_Train_IdentityUnset".Translate();
        /// <summary>生成“训练冷却中 ({0})”的本地化文本，并填入调用参数。</summary>
        public static string Train_Cooldown(string time) => "SSC_Train_Cooldown".Translate(time);
        public static string Train_Waiting => "SSC_Train_Waiting".Translate();
        /// <summary>将当前训练状态填入训练信息模板。</summary>
        public static string Train_Status(string status) => "SSC_Train_Status".Translate(status);
        /// <summary>生成指定角色解锁公交车特化的通知文本。</summary>
        public static string Message_BusSpecializationUnlocked(string name) => "SSC_Message_BusSpecializationUnlocked".Translate(name);
        /// <summary>生成指定角色解锁奶牛特化的通知文本。</summary>
        public static string Message_CowSpecializationUnlocked(string name) => "SSC_Message_CowSpecializationUnlocked".Translate(name);
        /// <summary>生成宠物特化解锁通知，填入角色姓名与特化名称。</summary>
        public static string Message_PetSpecializationUnlocked(string name, string specialization) => "SSC_Message_PetSpecializationUnlocked".Translate(name, specialization);
        public static string PetAffectionGizmoLabel => "SSC_PetAffectionGizmoLabel".Translate();
        /// <summary>生成宠物亲近指令的说明，并显示关联主人姓名。</summary>
        public static string PetAffectionGizmoDesc(string masterName) => "SSC_PetAffectionGizmoDesc".Translate(masterName);
        public static string PetAffectionNoMaster => "SSC_PetAffectionNoMaster".Translate();
        public static string PetAffectionNotPet => "SSC_PetAffectionNotPet".Translate();
        /// <summary>将剩余时间文本填入宠物亲近冷却说明。</summary>
        public static string PetAffectionCooldown(string time) => "SSC_PetAffectionCooldown".Translate(time);
        /// <summary>生成宠物与主人亲近的通知，按宠物、主人顺序填入姓名。</summary>
        public static string Message_PetAffection(string petName, string masterName) => "SSC_Message_PetAffection".Translate(petName, masterName);
        /// <summary>生成兔特化克隆出生通知，区分来源角色与新克隆体。</summary>
        public static string Message_RabbitCloneBirth(string sourceName, string cloneName) => "SSC_Message_RabbitCloneBirth".Translate(sourceName, cloneName);
        /// <summary>生成犬特化与动物互动触发通知，填入双方姓名。</summary>
        public static string Message_DogAnimalInteractionTriggered(string dogName, string animalName) => "SSC_Message_DogAnimalInteractionTriggered".Translate(dogName, animalName);

        // ==========================================
        // HediffComp_PermanentLactating 泌乳进度
        // ==========================================
        /// <summary>生成“充盈度: {0} (缺乏营养，停滞)”的本地化文本，并填入调用参数。</summary>
        public static string Lactating_Stalled(string progress) => "SSC_Lactating_Stalled".Translate(progress);
        /// <summary>生成“充盈度: {0} (已关闭泌乳)”的本地化文本，并填入调用参数。</summary>
        public static string Lactating_Disabled(string progress) => "SSC_Lactating_Disabled".Translate(progress);

        // ==========================================
        // Ritual 角色检查
        // ==========================================
        public static string Ritual_Unassigned => "SSC_Ritual_Unassigned".Translate();
        /// <summary>生成“{0} (未分配): {1}”的本地化文本，并填入调用参数。</summary>
        public static string Ritual_UnassignedDesc(string label, string offset) => "SSC_Ritual_UnassignedDesc".Translate(label, offset);
        /// <summary>生成“{0} (UI错误)”的本地化文本，并填入调用参数。</summary>
        public static string Ritual_UIError(string label) => "SSC_Ritual_UIError".Translate(label);
        public static string Ritual_MustBeColonist => "SSC_Ritual_MustBeColonist".Translate();
        public static string Ritual_NoCompData => "SSC_Ritual_NoCompData".Translate();
        public static string Ritual_NotDesignatedMaster => "SSC_Ritual_NotDesignatedMaster".Translate();
        public static string Ritual_MustBeColonistOrSlave => "SSC_Ritual_MustBeColonistOrSlave".Translate();
        public static string Ritual_MissingTrainingComp => "SSC_Ritual_MissingTrainingComp".Translate();
        public static string Ritual_CannotBeSlaveAsMaster => "SSC_Ritual_CannotBeSlaveAsMaster".Translate();
        public static string Ritual_NoMasterAssigned => "SSC_Ritual_NoMasterAssigned".Translate();
        /// <summary>生成锁链归属冲突的仪式提示，并显示已有主人姓名。</summary>
        public static string Ritual_ChainConflict(string masterName) => "SSC_Ritual_ChainConflict".Translate(masterName);
        public static string RJW_Short_TargetNull => "SSC_RJW_Short_TargetNull".Translate();
        public static string RJW_Short_Pass => "SSC_RJW_Short_Pass".Translate();
        public static string RJW_Short_Fail_Mechanoid => "SSC_RJW_Short_Fail_Mechanoid".Translate();
        public static string RJW_Short_Fail_Age => "SSC_RJW_Short_Fail_Age".Translate();
        public static string RJW_Short_Fail_Warcasket => "SSC_RJW_Short_Fail_Warcasket".Translate();
        public static string RJW_Short_Fail_CanDoLoving => "SSC_RJW_Short_Fail_CanDoLoving".Translate();
        public static string RJW_Short_Fail_NoUsableOrifice => "SSC_RJW_Short_Fail_NoUsableOrifice".Translate();
        public static string RJW_Short_Fail_CanBeFucked => "SSC_RJW_Short_Fail_CanBeFucked".Translate();
        public static string Legacy_Short_RequireRapeEnabled => "SSC_Legacy_Short_RequireRapeEnabled".Translate();
        public static string Legacy_Short_FailCanBeFucked => "SSC_Legacy_Short_FailCanBeFucked".Translate();
        public static string Legacy_Short_MissingAgeTracker => "SSC_Legacy_Short_MissingAgeTracker".Translate();
        public static string Legacy_Short_AgeGateFail => "SSC_Legacy_Short_AgeGateFail".Translate();
        public static string Legacy_Short_Pass => "SSC_Legacy_Short_Pass".Translate();
        public static string Train_Reason_NotHumanlike => "目标不是类人生物。";
        public static string Train_Reason_DeadOrSelf => "目标当前无法接受调教。";
        public static string Train_Reason_InvalidFaction => "目标必须是己方殖民者、奴隶或囚犯。";
        public static string Train_Reason_NotEnabled => "该目标未启用调教。";
        public static string Train_Reason_RitualBusy => "该目标正在进行绑定仪式。";
        public static string Train_Reason_ValidationCooldown => "该目标刚刚验证失败，请稍后再试。";
        public static string Train_Reason_AlreadyBeingTrained => "该目标正在接受调教。";
        /// <summary>生成包含剩余时间的调教冷却拒绝说明；沿用现有直接插值文本。</summary>
        public static string Train_Reason_Cooldown(string time) => $"该目标仍在调教冷却中：{time}";
        public static string Train_Reason_TrainerLocked => "该目标只能由指定的主人或调教师进行调教。";
        public static string Train_Reason_NotReservable => "当前无法预定该目标。";
    }
}
