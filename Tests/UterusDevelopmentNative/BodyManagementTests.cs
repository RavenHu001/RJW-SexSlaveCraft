// AI 辅助生成：第 5 批以实际生产程序集验证只读准入、器官、身份、克隆和凝胶化健康清理。
using HarmonyLib;
using System.Reflection;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>沿用前四批原生身体与妊娠夹具；不加载游戏界面、地图或完整人格系统。</summary>
    private static void BodyManagementLifecycleTests(Harmony harmony)
    {
        Check("batch5 body definition filter accepts only this project", () =>
        {
            Require(SSCUterusDevelopmentUtility.IsBodyStateDef(development));
            Require(!SSCUterusDevelopmentUtility.IsBodyStateDef(null));
            Require(!SSCUterusDevelopmentUtility.IsBodyStateDef(new HediffDef { defName = "SSC_Exp_Genitals" }));
        });
        foreach (string state in new[] { "none", "incomplete", "closed", "open", "openPregnant", "closedPregnant", "loading" })
            Check("batch5 native readonly admission and actual submission agree: " + state, () =>
            {
                var pawn = NewPawn(); Hediff_SSCUterusDevelopment record = null;
                if (state != "none") record = Start(pawn, state == "incomplete" ? .7f : 1f);
                if (state.StartsWith("open")) Toggle(pawn, true);
                if (state.EndsWith("Pregnant")) pawn.health.hediffSet.hediffs.Add(new Hediff { def = HediffDefOf.Pregnant, pawn = pawn });
                bool before = record?.FertilityEnabled ?? false; float severity = record?.Severity ?? 0;
                int count = pawn.health.hediffSet.hediffs.Count, notices = notifications;
                try
                {
                    if (state == "loading") Scribe.mode = LoadSaveMode.LoadingVars;
                    bool allowed = SSCUterusDevelopmentUtility.CanSetFertilityEnabled(pawn, !before, out string reason);
                    Equal(count, pawn.health.hediffSet.hediffs.Count); Equal(notices, notifications);
                    Equal(before, record?.FertilityEnabled ?? false); Near(severity, record?.Severity ?? 0);
                    bool applied = SSCUterusDevelopmentUtility.TrySetFertilityEnabled(pawn, !before, out string actualReason);
                    Equal(allowed, applied); Equal(reason, actualReason);
                    Equal(state is "closed" or "open" or "closedPregnant", allowed);
                    if (!allowed) Require(!string.IsNullOrEmpty(reason));
                }
                finally { Scribe.mode = LoadSaveMode.Inactive; }
            });
        foreach (PawnIdentity identity in new[] { PawnIdentity.Slave, PawnIdentity.Master, PawnIdentity.Unset })
            Check("batch5 existing full body remains operable without binding in identity: " + identity, () =>
            {
                var pawn = SurgeryPawn(identity); var record = Start(pawn, 1f);
                Require(SSCUterusDevelopmentUtility.CanSetFertilityEnabled(pawn, true, out _)); Toggle(pawn, true);
                Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Near(1, record.Severity);
                Require(record.FertilityEnabled); Toggle(pawn, false);
            });
        Check("batch5 actual exit and return to slave identity preserves body selection", () =>
        {
            var pawn = SurgeryPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            Require(record.FertilityEnabled && SSCUterusDevelopmentUtility.Find(pawn) == record);
            Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave));
            Require(record.FertilityEnabled && SSCUterusDevelopmentUtility.Find(pawn) == record);
        });
        Check("batch5 genital removal, restoration and sex change follow torso record only", () =>
        {
            var pawn = SurgeryPawn(); var record = Start(pawn, .7f);
            pawn.health.RemoveHediff(pawn.health.hediffSet.GetFirstHediffOfDef(vagina)); pawn.gender = Gender.Male;
            Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Near(.7f, record.Severity);
            pawn.gender = Gender.Female; pawn.health.AddHediff(vagina, pawn.RaceProps.body.corePart);
            Equal(record, SSCUterusDevelopmentUtility.Find(pawn));
            pawn.health.RemoveHediff(record); pawn.gender = Gender.Male; pawn.gender = Gender.Female;
            Require(SSCUterusDevelopmentUtility.Find(pawn) == null); Near(1f, Factor(pawn));
        });
        // 后续敏感度／推进夹具也使用这些定义；Special独立运行不能依赖先执行本条Core用例。
        SSCDefOf.ChainOfSexSlave = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave), initialSeverity = .1f };
        SSCDefOf.BridleOfSexSlave = new HediffDef { defName = "Hediff_BridleOfSexSlave", hediffClass = typeof(Hediff_BridleOfSexSlave), initialSeverity = 1 };
        Check("batch5 actual binding, rebinding and unbinding preserve body development", () =>
        {
            // 提供真实但为空的游戏组件容器；不运行完整地图限制系统，也不替换绑定实现。
            typeof(Game).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(List<GameComponent>)).SetValue(Current.Game, new List<GameComponent>());
            var pawn = SurgeryPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            var first = SurgeryPawn(PawnIdentity.Master); var second = SurgeryPawn(PawnIdentity.Master);
            Require(SSCBondUtility.Bind(first, pawn));
            Require(SSCBondUtility.Bind(second, pawn, true)); Equal(second, SSCBondUtility.GetBoundMaster(pawn));
            Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Require(record.FertilityEnabled);
            Require(SSCBondUtility.Unbind(pawn)); Require(SSCBondUtility.GetBoundMaster(pawn) == null);
            Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Near(1, record.Severity); Require(record.FertilityEnabled);
        });
        Check("batch5 existing clone health copy inherits progress without custom toggle copy", () =>
        {
            var source = NewPawn(); var original = Start(source, 1); Toggle(source, true); var clone = NewPawn();
            Hidden(typeof(RabbitCloneUtility), null, "CopySscHediffsFromSource", source, clone);
            var copy = SSCUterusDevelopmentUtility.Find(clone);
            Require(copy != null && copy != original && copy.pawn == clone); Near(1, copy.Severity);
            Require(!copy.FertilityEnabled && original.FertilityEnabled); Equal(clone.RaceProps.body.corePart, copy.Part);
            Toggle(clone, true); Toggle(source, false); Require(copy.FertilityEnabled);
        });
        Check("batch5 full gel native health cleanup preserves torso development and toggle", () =>
        {
            var pack = Bare<ModContentPack>(); AccessTools.Field(typeof(ModContentPack), "packageIdInt").SetValue(pack, "rjw.sexslavecraft.tiejinver");
            var previousPack = development.modContentPack; development.modContentPack = pack;
            var pawn = NewPawn(); var record = Start(pawn, 1); Toggle(pawn, true);
            var temporary = new HediffDef { defName = "ForeignTemporary", hediffClass = typeof(Hediff), initialSeverity = 1 };
            pawn.health.AddHediff(temporary);
            SSCDefOf.SSC_FullGelatinizedBody = new HediffDef { defName = "SSC_FullGelatinizedBody", hediffClass = typeof(Hediff), initialSeverity = 1 };
            // 仅跳过图形/肖像刷新；原始清理、保留过滤与完全胶化 Hediff 添加继续实际执行。
            harmony.Patch(AccessTools.Method(typeof(FullGelatinizationUtility), "RefreshPawnVisuals"),
                prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
            harmony.Patch(AccessTools.Method(typeof(TranslatorFormattedStringExtensions), "Translate", new[] { typeof(string), typeof(NamedArgument) }),
                prefix: new HarmonyMethod(typeof(Program), nameof(FormattedBoundary)));
            try
            {
                FullGelatinizationUtility.CompleteSuccess(pawn);
                Require(pawn.health.hediffSet.GetFirstHediffOfDef(temporary) == null);
                Require(pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_FullGelatinizedBody) != null);
                Equal(record, SSCUterusDevelopmentUtility.Find(pawn)); Near(1, record.Severity); Require(record.FertilityEnabled);
            }
            finally { development.modContentPack = previousPack; }
        });
    }
}
