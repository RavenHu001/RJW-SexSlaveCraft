// AI 辅助生成：第 3 批流程与存档专项。
using HarmonyLib;
using System.Reflection;
using RimWorld;
using rjw;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using Verse.AI.Group;

internal static partial class Program
{
    private static Pawn infestationSource, infestationTarget;
    private static bool infestationAllowed;
    private static float growthScore = 50f;
    private static int growthScoreCalls, dailyOutcomes, oldBodyCalls, sexProcessCalls, ritualOutcomes;
    private static Action growthOriginal, growthAfterDaily, growthAfterRitual;
    private static readonly List<string> growthOrder = new();
    private static readonly Dictionary<LordJob_Ritual, GrowthRitual> growthRituals = new();

    /// <summary>在同一个真实生产程序集宿主中执行场景、成长及存档测试。</summary>
    private static void GrowthFlowTests(string repo, Harmony harmony)
    {
        void Patch(Type type, string method, string prefix, Type[] signature = null)
            => harmony.Patch(AccessTools.Method(type, method, signature), prefix: new HarmonyMethod(typeof(Program), prefix));
        // Lord 的静态旗帜材质需要图形设备；仅隔离这一资源加载，不替换其任务/角色引用。
        Patch(typeof(MaterialPool), "MatFrom", nameof(Skip), new[] { typeof(string) });
        // 只把外部能力/地图资格和既有收益作为边界。新快照、成长、任务认领和仪式状态均执行生产代码。
        Patch(typeof(SSCDesireInfestationUtility), "CanAffect", nameof(InfestationEligibility));
        Patch(typeof(TrainingOutcomeUtility), "GetScore", nameof(GrowthScoreBoundary));
        Patch(typeof(SSCPnaSexSupplyUtility), "CaptureInfestationDose", nameof(ZeroDose));
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(SSCPnaSexSupplyUtility)).Where(m => m.Name == "ApplyInfestation" || m.Name == "ApplyFromProducer"))
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        Patch(typeof(SSCPnaSourceUtility), "IsPnaProducer", nameof(NotProducer));
        Patch(typeof(SSCDesireInfestationUtility), "ApplyCorruption", nameof(Skip));
        Patch(typeof(SSCDesireInfestationFeedbackUtility), "Capture", nameof(Skip));
        Patch(typeof(SexUtility), "ProcessSex", nameof(GrowthProcessBoundary));
        Patch(typeof(ConditioningUtility), "ExecuteOutcome", nameof(DailyOutcomeBoundary), new[] { typeof(Pawn), typeof(Pawn), typeof(float) });
        Patch(typeof(TrainingExpUtility), "ApplyExperienceFromScore", nameof(OldBodyBoundary));
        Patch(typeof(ConditioningUtility), "ExecuteRitualOutcome", nameof(RitualOutcomeBoundary));
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(SpecializationTrainingProgressUtility)).Where(m => m.Name == "NotifyTrainingCompleted"))
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        Patch(typeof(TrainerSpecializationProgressUtility), "NotifyProvidedTrainingCompleted", nameof(Skip));
        Patch(typeof(TrainerOfficerFeedback), "NotifyTrainingCompleted", nameof(Skip));
        Patch(typeof(SSCMasterProgressionUtility), "TryAddExperience", nameof(Skip));
        Patch(typeof(SSCBondRelationUtility), "TryCreateAfterRitual", nameof(Skip));
        harmony.Patch(AccessTools.PropertyGetter(typeof(CompSexSlaveTraining), "CurrentLocalDay"), prefix: new HarmonyMethod(typeof(Program), nameof(LocalDayBoundary)));
        Patch(typeof(JobDriver_Sex), "setup_ticks", nameof(Skip));
        Patch(typeof(JobDriver_SexBaseInitiator), "End", nameof(Skip));
        Patch(growthType.Assembly.GetType("SexSlaveCraft.UapRitualCompatibilityUtility"), "ReleasePositionLocks", nameof(Skip));
        Patch(typeof(OnaholeCompatibilityUtility), "TryUnregisterOnaholePartner", nameof(Skip));
        Patch(typeof(BindingRitualStateUtility), "IsBindingRitual", nameof(BindingRitualBoundary));
        Patch(typeof(BindingRitualStateUtility), "IsActiveRitualFor", nameof(ActiveRitualBoundary));
        Patch(typeof(LordJob_Ritual), "PawnWithRole", nameof(RitualRoleBoundary));
        Patch(typeof(Lord), "ReceiveMemo", nameof(RitualMemoBoundary));
        Patch(typeof(RitualOutcomeEffectWorker_SSCBinding), "GetQuality", nameof(RitualQualityBoundary));
        Patch(typeof(RitualOutcomeEffectWorker_SSCBinding), "GetOutcome", nameof(RitualPossibilityBoundary));
        Patch(typeof(RitualOutcomeEffectWorker_SSCBinding), "OutcomeQualityBreakdownDesc", nameof(EmptyBreakdown));
        // 只隔离信件文本格式化与本地化，不截断完整 Apply，便于验证重复认领。
        Patch(typeof(GrammarResolverSimpleStringExtensions), "Formatted", nameof(FormattedBoundary), new[] { typeof(string), typeof(NamedArgument) });
        Patch(typeof(TranslatorFormattedStringExtensions), "Translate", nameof(FormattedBoundary),
            new[] { typeof(string), typeof(NamedArgument), typeof(NamedArgument) });
        foreach (var m in AccessTools.GetDeclaredMethods(typeof(LetterStack)).Where(m => m.Name == "ReceiveLetter"))
            harmony.Patch(m, prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        AccessTools.Field(typeof(Game), "letterStack").SetValue(Current.Game, new LetterStack());
        // 旧部位定义和研究正常可用，确保新旧两份开发预算都真实执行。
        SSCDefOf.SSC_BodyPartTraining = DefDatabase<ResearchProjectDef>.GetNamed("SSC_RES_BodyPartTraining");
        researchProgress[SSCDefOf.SSC_BodyPartTraining] = float.MaxValue;
        foreach (var name in new[] { "Hand", "Foot", "Oral", "Breast", "Genitals", "Anus" })
            DefDatabase<HediffDef>.Add(new HediffDef { defName = "SSC_Exp_" + name, hediffClass = typeof(Hediff), maxSeverity = 1 });
        DailyGrowthTests();
        InfestationGrowthTests();
        RitualGrowthTests();
        GrowthSaveTests(harmony);
    }

    /// <summary>仅提供夹具方向的既有能力资格；不在此宿主重新实现主人能力和人格准入。</summary>
    private static bool InfestationEligibility(Pawn source, Pawn target, ref bool __result)
    { __result = infestationAllowed && source == infestationSource && target == infestationTarget; return false; }
    /// <summary>提供固定评分并真实取一次 Rand，验证新增计划不会重复取样或推进原结算随机序列。</summary>
    private static bool GrowthScoreBoundary(ref float __result)
    { growthScoreCalls++; _ = Rand.Value; __result = growthScore; growthOrder.Add("score"); return false; }
    /// <summary>本专项不运行 PNA 供给量计算。</summary>
    private static bool ZeroDose(ref float __result) { __result = 0; return false; }
    /// <summary>无普通产出来源，防止无关方向进入供给系统。</summary>
    private static bool NotProducer(ref bool __result) { __result = false; return false; }
    /// <summary>观察旧 ProcessSex 调用顺序并允许注入重入/替换/异常，不执行完整 RJW 怀孕与心情。</summary>
    private static bool GrowthProcessBoundary()
    { sexProcessCalls++; growthOrder.Add("ProcessSex"); growthOriginal?.Invoke(); return false; }
    /// <summary>既有日常后果是外部边界，新成长必须使用调用者传入的同一评分。</summary>
    private static bool DailyOutcomeBoundary()
    { dailyOutcomes++; growthOrder.Add("daily outcome"); growthAfterDaily?.Invoke(); return false; }
    /// <summary>记录旧部位日常入口，普通侵蚀的精确部位写入仍执行原生产代码。</summary>
    private static bool OldBodyBoundary()
    { oldBodyCalls++; growthOrder.Add("old body"); return false; }
    /// <summary>保留整场旧结果调用位置，隔离原有六部位、心情和意志后果。</summary>
    private static bool RitualOutcomeBoundary(ref string __result)
    { ritualOutcomes++; __result = ""; growthAfterRitual?.Invoke(); return false; }
    /// <summary>隔离地图所在地日期，日常训练完成及冷却写入继续使用生产实现。</summary>
    private static bool LocalDayBoundary(ref int __result) { __result = 0; return false; }

    /// <summary>为真实原生 Job 安装双向引用；任务与 loadID 的判断仍由生产守卫完成。</summary>
    private static T InstallDriver<T>(GrowthScene scene) where T : JobDriver_SexBaseInitiator, new()
    {
        Pawn actor = scene.Props.initiator, partner = scene.Props.recipient;
        actor.jobs ??= new Pawn_JobTracker(actor); partner.jobs ??= new Pawn_JobTracker(partner);
        var job = new Job { def = new JobDef { defName = "GrowthJob" + ++nextId, driverClass = typeof(T) },
            targetA = partner, loadID = ++nextId };
        var driver = new T { pawn = actor, job = job, Sexprops = scene.Props, ticks_left = 0 };
        actor.jobs.curJob = job; actor.jobs.curDriver = driver;
        return driver;
    }
    /// <summary>提供真实 Start 已成功返回的事实，直接执行同一生产 Postfix 所调用的守卫。</summary>
    private static void MarkStarted(JobDriver_SexBaseInitiator driver)
        => Hidden(guardType, null, "TryMarkStarted", driver, true);
    /// <summary>每例清空外部回调及计数，不清理被测生产任务状态。</summary>
    private static void ResetGrowthFlow()
    {
        growthScore = 50; growthScoreCalls = dailyOutcomes = oldBodyCalls = sexProcessCalls = ritualOutcomes = 0;
        growthOriginal = growthAfterDaily = growthAfterRitual = null; infestationAllowed = false; growthOrder.Clear();
    }

    /// <summary>枚举真实日常 Toil，并直接执行原有最终完成闭包验证整场认领。</summary>
    private static (GrowthScene scene, JobDriver_Training driver, Toil payout) DailyGrowth(bool contact = true, bool begun = true)
    {
        ResetGrowthFlow(); var s = new GrowthScene(contact: contact); var driver = InstallDriver<JobDriver_Training>(s);
        var toils = ((IEnumerable<Toil>)Hidden(typeof(JobDriver_Training), driver, "MakeNewToils")).ToArray();
        if (begun)
        {
            MarkStarted(driver);
            AccessTools.Field(typeof(JobDriver_Training), "sceneStarted").SetValue(driver, true);
        }
        return (s, driver, toils.Last());
    }
    /// <summary>验证实际日常闭包：原结算先执行、同场只领一次、无接触和中断不领。</summary>
    private static void DailyGrowthTests()
    {
        Check("batch3 missing driver never creates training evidence", () =>
        {
            var kind = growthType.Assembly.GetType("SexSlaveCraft.SSCInteractionKind");
            Require(Hidden(guardType, null, "TrainingParts", null, Enum.Parse(kind, "DailyTraining")) == null);
        });
        Check("batch3 actual daily completion grows once after ProcessSex and original body reward", () =>
        {
            var s = DailyGrowth(); s.payout.initAction(); s.payout.initAction();
            Near(.035f, Progress(s.scene.Target)); Equal(1, sexProcessCalls); Equal(1, dailyOutcomes); Equal(1, oldBodyCalls);
            Equal("ProcessSex,score,daily outcome,old body", string.Join(',', growthOrder));
        });
        foreach (string mode in new[] { "no contact", "not started", "time remaining", "old claimed", "no start snapshot" })
            Check("batch3 daily completion does not grant invalid scene: " + mode, () =>
            {
                var s = DailyGrowth(contact: mode != "no contact", begun: mode != "not started" && mode != "no start snapshot");
                if (mode == "time remaining") s.driver.ticks_left = 1;
                if (mode == "old claimed") AccessTools.Field(typeof(JobDriver_Training), "dailyOutcomeClaimed").SetValue(s.driver, true);
                if (mode == "no start snapshot") AccessTools.Field(typeof(JobDriver_Training), "sceneStarted").SetValue(s.driver, true);
                s.payout.initAction(); Near(.01f, Progress(s.scene.Target));
            });
        Check("batch3 daily original callback reentry cannot grant twice", () =>
        {
            var s = DailyGrowth(); growthOriginal = () => s.payout.initAction();
            s.payout.initAction(); Near(.035f, Progress(s.scene.Target)); Equal(1, sexProcessCalls);
        });
        Check("batch3 daily failure preserves existing claim and does not add compensation path", () =>
        {
            var s = DailyGrowth(); growthOriginal = () => throw new InvalidOperationException("original failure");
            try { s.payout.initAction(); } catch (InvalidOperationException) { }
            growthOriginal = null; s.payout.initAction(); Near(.01f, Progress(s.scene.Target)); Equal(1, sexProcessCalls);
        });
        Check("batch3 daily changed Job ID after original outcome invalidates old payout", () =>
        {
            var s = DailyGrowth(); growthAfterDaily = () => s.driver.job.loadID++;
            s.payout.initAction(); Near(.01f, Progress(s.scene.Target));
        });
    }

    /// <summary>使用实际守卫、方向计划和持久化认领，既有能力资格由本例显式提供。</summary>
    private static (GrowthScene scene, GrowthOrdinaryDriver driver) OrdinaryGrowth(bool reverse = false, bool contact = true)
    {
        ResetGrowthFlow(); var s = new GrowthScene(reverse, contact); var driver = InstallDriver<GrowthOrdinaryDriver>(s);
        infestationSource = s.Source; infestationTarget = s.Target; infestationAllowed = true;
        s.Source.relations = new Pawn_RelationsTracker(s.Source); s.Target.relations = new Pawn_RelationsTracker(s.Target);
        MarkStarted(driver); return (s, driver);
    }
    /// <summary>模拟原 ProcessSex 的外部业务边界，直接执行生产 Prefix/认领/Dispatch/释放。</summary>
    private static void FinishOrdinary(GrowthScene scene, bool runOriginal = true, Action original = null)
    {
        using var context = (SSCOrdinarySexOutcomeUtility.Context)Hidden(guardType, null, "PrepareOrdinaryOutcome", scene.Props);
        original?.Invoke();
        if (runOriginal && context?.TryClaim() == true) context.Dispatch();
    }
    /// <summary>普通完成仍采用原有 Context 与认领，检查分量独立、回调、方向及评分冻结。</summary>
    private static void InfestationGrowthTests()
    {
        foreach (bool reverse in new[] { false, true })
            Check("batch3 ordinary qualified infestation uses single frozen score and one award: " + reverse, () =>
            {
                var s = OrdinaryGrowth(reverse); FinishOrdinary(s.scene); FinishOrdinary(s.scene);
                Near(.035f, Progress(s.scene.Target)); Equal(1, growthScoreCalls);
                Near(.01f, s.scene.Target.health.hediffSet.hediffs.Single(h => h.def.defName == "SSC_Exp_Genitals").Severity);
            });
        Check("batch3 ordinary growth does not advance the original random stream", () =>
        {
            var s = OrdinaryGrowth(); float expected;
            Rand.PushState(741); try { expected = Rand.Value; } finally { Rand.PopState(); }
            Rand.PushState(741);
            try
            {
                // 仅验证评分准备边界；后续旧部位 AddHediff 自身合法使用随机数，不纳入此断言。
                using var context = (SSCOrdinarySexOutcomeUtility.Context)Hidden(guardType, null, "PrepareOrdinaryOutcome", s.scene.Props);
                Require(context != null); Near(expected, Rand.Value);
                Require(context.TryClaim()); context.Dispatch();
            }
            finally { Rand.PopState(); }
        });
        Check("batch3 ordinary genital max does not suppress independent uterus growth", () =>
        {
            var s = OrdinaryGrowth(); s.scene.Target.health.hediffSet.hediffs.Add(new Hediff { pawn = s.scene.Target,
                Part = s.scene.Organ.Part, def = DefDatabase<HediffDef>.GetNamed("SSC_Exp_Genitals"), Severity = 1 });
            FinishOrdinary(s.scene); Near(.035f, Progress(s.scene.Target));
        });
        Check("batch3 ordinary score and multipliers stay frozen across original settlement", () =>
        {
            var s = OrdinaryGrowth(); var need = GrowthCorruption(s.scene.Target, 0f);
            FinishOrdinary(s.scene, original: () => { need.CurLevel = .8f; growthScore = 500; });
            Near(.035f, Progress(s.scene.Target)); Equal(1, growthScoreCalls);
        });
        foreach (string mode in new[] { "no contact", "ability off", "not finished", "removed organ", "new props", "changed direction", "personality changed" })
            Check("batch3 ordinary completed candidate rejects invalid new growth: " + mode, () =>
            {
                var s = OrdinaryGrowth(contact: mode != "no contact");
                if (mode == "not finished") s.driver.ticks_left = 1;
                if (mode == "ability off") infestationAllowed = false;
                FinishOrdinary(s.scene, original: () =>
                {
                    if (mode == "removed organ") s.scene.Target.health.hediffSet.hediffs.Remove(s.scene.Organ);
                    if (mode == "new props") s.driver.Sexprops = new SexProps(s.scene.Source, s.scene.Target);
                    if (mode == "changed direction") s.scene.Props._resolved.Penetrations[0] = Tuple.Create(s.scene.Vagina, s.scene.Penetrator);
                    if (mode == "personality changed") SSCPersonalityRuntimeUtility.TryBeginMutation(s.scene.Target, out _);
                });
                Near(.01f, Progress(s.scene.Target));
            });
        Check("batch3 original callback reentry uses same ordinary in-flight guard", () =>
        {
            var s = OrdinaryGrowth(); FinishOrdinary(s.scene, original: () => FinishOrdinary(s.scene)); Near(.035f, Progress(s.scene.Target));
        });
        Check("batch3 skipped ordinary original leaves claim available for actual completion", () =>
        {
            var s = OrdinaryGrowth(); FinishOrdinary(s.scene, false); Near(.01f, Progress(s.scene.Target));
            FinishOrdinary(s.scene); Near(.035f, Progress(s.scene.Target));
        });
        Check("batch3 training jobs remain excluded from ordinary completion payout", () =>
        {
            var s = DailyGrowth(); infestationSource = s.scene.Source; infestationTarget = s.scene.Target; infestationAllowed = true;
            FinishOrdinary(s.scene); Near(.01f, Progress(s.scene.Target)); s.payout.initAction(); Near(.035f, Progress(s.scene.Target));
        });
        Check("batch3 ordinary Lovin context without parts cannot grant uterus growth", () =>
        {
            var s = OrdinaryGrowth(); bool claimed = false;
            using var ctx = SSCOrdinarySexOutcomeUtility.Capture(s.scene.Source, s.scene.Target, false, () => true, () => !claimed && (claimed = true));
            Require(ctx.TryClaim()); ctx.Dispatch(); Near(.01f, Progress(s.scene.Target));
        });
    }

    /// <summary>最小已分配仪式角色，不模拟地图管理器或原版仪式质量生成。</summary>
    private sealed class GrowthRitual
    {
        internal readonly GrowthScene Scene;
        internal readonly Lord Lord = new();
        internal readonly LordJob_Ritual Job = Bare<LordJob_Ritual>();
        internal readonly RitualOutcomeEffectWorker_SSCBinding Worker;
        internal readonly CompSexSlaveTraining Comp;
        internal bool Active = true;
        internal int Memos;
        internal float Quality = .5f;
        internal readonly RitualOutcomePossibility Outcome = new() { description = "测试结果", label = "测试结果" };

        /// <summary>关联真实 Lord、组件和结果处理器，外部活动状态由夹具边界提供。</summary>
        internal GrowthRitual(GrowthScene scene = null)
        {
            Scene = scene ?? new GrowthScene();
            Job.lord = Lord; AccessTools.Field(typeof(Lord), "curJob").SetValue(Lord, Job);
            Lord.ownedPawns.Add(Scene.Source); Lord.ownedPawns.Add(Scene.Target);
            var precept = Bare<Precept_Ritual>(); precept.def = new PreceptDef { defName = "UterusRitual", label = "测试仪式" };
            AccessTools.Field(typeof(LordJob_Ritual), "ritual").SetValue(Job, precept);
            Comp = Scene.Target.TryGetComp<CompSexSlaveTraining>();
            Worker = new RitualOutcomeEffectWorker_SSCBinding(new RitualOutcomeEffectDef { comps = new(), outcomeChances = new() { Outcome } });
            growthRituals.Add(Job, this);
            Require(BindingRitualStateUtility.TryBeginPhase(Scene.Source, Scene.Target, Lord));
        }
        /// <summary>执行生产阶段完成回调；开始与计时完成事实由夹具显式设置，原始快照仍由守卫捕获。</summary>
        internal void Phase(bool contact = true, bool completed = true)
        {
            Scene.Props._resolved.Penetrations.Clear();
            if (contact) Scene.Props._resolved.Penetrations.Add(Tuple.Create(Scene.Penetrator, Scene.Vagina));
            var driver = InstallDriver<JobDriver_RitualTraining>(Scene);
            AccessTools.Field(typeof(JobDriver_RitualTraining), "ritualLord").SetValue(driver, Lord);
            AccessTools.Field(typeof(JobDriver_RitualTraining), "phaseSceneStarted").SetValue(driver, true);
            AccessTools.Field(typeof(JobDriver_RitualTraining), "phaseRanToCompletion").SetValue(driver, completed);
            MarkStarted(driver);
            var contextType = typeof(TrainingJobUtility).GetNestedType("JobContext", BindingFlags.NonPublic);
            object context = Activator.CreateInstance(contextType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { driver }, null);
            Hidden(typeof(JobDriver_RitualTraining), driver, "OnPhaseComplete", context);
        }
    }
    /// <summary>只提供当前 Lord 的仪式类型，状态推进和认领继续执行生产工具。</summary>
    private static bool BindingRitualBoundary(LordJob_Ritual ritual, ref bool __result)
    { __result = ritual != null && growthRituals.ContainsKey(ritual); return false; }
    /// <summary>替代地图管理器与角色分配前提，保留本场主从和活动状态的变化。</summary>
    private static bool ActiveRitualBoundary(Pawn master, Pawn slave, Lord ritualLord, ref bool __result)
    {
        __result = ritualLord?.LordJob is LordJob_Ritual job && growthRituals.TryGetValue(job, out var f) &&
            f.Active && f.Scene.Source == master && f.Scene.Target == slave;
        return false;
    }
    /// <summary>返回夹具已分配角色，不复制阶段或领取规则。</summary>
    private static bool RitualRoleBoundary(LordJob_Ritual __instance, string roleId, ref Pawn __result)
    {
        if (!growthRituals.TryGetValue(__instance, out var f)) return true;
        __result = roleId == "master" ? f.Scene.Source : roleId == "slave" ? f.Scene.Target : null; return false;
    }
    /// <summary>模拟 memo 同步触发结果的外部调度，生产结果处理器必须此时已能读取最后阶段的凭据。</summary>
    private static bool RitualMemoBoundary(Lord __instance, string memo)
    {
        if (__instance.LordJob is not LordJob_Ritual job || !growthRituals.TryGetValue(job, out var f)) return true;
        if (memo == "SSC_Training_Finished") { f.Memos++; f.Worker.Apply(1f, new(), f.Job); }
        return false;
    }
    /// <summary>固定原仪式质量以验证 75q 的新成长量纲。</summary>
    private static bool RitualQualityBoundary(LordJob_Ritual jobRitual, ref float __result)
    { __result = growthRituals[jobRitual].Quality; return false; }
    /// <summary>只提供一个无记忆结果，避免未纳入本批的仪式抽奖与观众 UI。</summary>
    private static bool RitualPossibilityBoundary(LordJob_Ritual ritual, ref RitualOutcomePossibility __result)
    { __result = growthRituals[ritual].Outcome; return false; }
    /// <summary>隔离界面说明生成，实际成长结果仍由真实 worker 写入。</summary>
    private static bool EmptyBreakdown(ref string __result) { __result = ""; return false; }
    /// <summary>信件占位文本无需语言设备；格式化后的原文不参与成长计算。</summary>
    private static bool FormattedBoundary(string __0, ref TaggedString __result) { __result = __0; return false; }
    /// <summary>整场完成只发一次，阶段中断、空接触、旧字段与清理均不补造增长。</summary>
    private static void RitualGrowthTests()
    {
        Check("batch3 ritual six vaginal phases pay once using 75q", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual();
            for (int i = 0; i < 6; i++) f.Phase();
            Near(.02875f, Progress(f.Scene.Target)); Equal(1, f.Memos); Equal(1, ritualOutcomes);
            f.Worker.Apply(1, new(), f.Job); Near(.02875f, Progress(f.Scene.Target)); Equal(1, ritualOutcomes);
            BindingRitualStateUtility.EndRitual(f.Job);
            Require(AccessTools.Field(typeof(CompSexSlaveTraining), "uterusRitualEvidence").GetValue(f.Comp) == null);
        });
        Check("batch3 ritual final-phase-only evidence is saved before synchronous outcome", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual();
            for (int i = 0; i < 6; i++) f.Phase(contact: i == 5);
            Near(.02875f, Progress(f.Scene.Target)); Equal(1, f.Memos);
        });
        Check("batch3 ritual earlier contact still qualifies when final phase is not vaginal", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual();
            for (int i = 0; i < 6; i++) f.Phase(contact: i == 0);
            Near(.02875f, Progress(f.Scene.Target));
        });
        Check("batch3 ritual all empty phases preserve old outcome but no new development", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual();
            for (int i = 0; i < 6; i++) f.Phase(contact: false);
            Near(.01f, Progress(f.Scene.Target)); Equal(1, ritualOutcomes);
        });
        Check("batch3 interrupted ritual phase neither records contact nor advances", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual(); f.Phase(completed: false);
            Equal(0, f.Comp.ritualPhase); Near(.01f, Progress(f.Scene.Target)); Equal(0, f.Memos);
            Require(AccessTools.Field(typeof(CompSexSlaveTraining), "uterusRitualEvidence").GetValue(f.Comp) == null);
        });
        Check("batch3 old completed ritual without evidence does not retroactively gain", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual(); f.Comp.ritualPhase = 6;
            f.Worker.Apply(1, new(), f.Job); Near(.01f, Progress(f.Scene.Target)); Equal(1, ritualOutcomes);
        });
        Check("batch3 ritual removed original organ cannot support end reward", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual(); f.Phase(); f.Scene.Target.health.hediffSet.hediffs.Remove(f.Scene.Organ);
            for (int i = 1; i < 6; i++) f.Phase(contact: false);
            Near(.01f, Progress(f.Scene.Target));
        });
        Check("batch3 ritual end or new Lord clears previous completed contacts", () =>
        {
            ResetGrowthFlow(); var previous = new GrowthRitual(); previous.Phase();
            var evidence = AccessTools.Field(typeof(CompSexSlaveTraining), "uterusRitualEvidence");
            Require(evidence.GetValue(previous.Comp) != null);
            previous.Active = false;
            var next = new GrowthRitual(previous.Scene);
            Require(evidence.GetValue(next.Comp) == null); Equal(0, next.Comp.ritualPhase);
            for (int i = 0; i < 6; i++) next.Phase(contact: false);
            Near(.01f, Progress(next.Scene.Target));
            // 旧场迟到的清理不能删除后来场次的归属。
            BindingRitualStateUtility.EndRitual(previous.Job); Equal(next.Lord, next.Comp.bindingRitualLord);
            BindingRitualStateUtility.EndRitual(next.Job); Require(next.Comp.bindingRitualLord == null);
        });
        Check("batch3 ritual original outcome reentry cannot grant a second uterus award", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual();
            growthAfterRitual = () => f.Worker.Apply(1f, new(), f.Job);
            for (int i = 0; i < 6; i++) f.Phase();
            Equal(1, ritualOutcomes); Near(.02875f, Progress(f.Scene.Target));
        });
        Check("batch3 ritual losing active ownership inside original outcome stops new award", () =>
        {
            ResetGrowthFlow(); var f = new GrowthRitual(); growthAfterRitual = () => f.Active = false;
            for (int i = 0; i < 6; i++) f.Phase();
            Near(.01f, Progress(f.Scene.Target)); Equal(1, ritualOutcomes);
            Require(AccessTools.Field(typeof(CompSexSlaveTraining), "uterusRitualEvidence").GetValue(f.Comp) == null);
        });
    }
}

/// <summary>无需地图调度的原生 RJW 普通发起者；流程校验和完成认领仍由生产守卫负责。</summary>
public sealed class GrowthOrdinaryDriver : JobDriver_SexBaseInitiator
{
    /// <summary>测试只使用已开始/已完成事实，不创建移动和动画步骤。</summary>
    protected override IEnumerable<Toil> MakeNewToils() { yield break; }
}
