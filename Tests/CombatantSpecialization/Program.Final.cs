using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunFinalCases()
    {
        Run("终极优先且重复对账保留同一实例，不叠加普通收益", FinalMutualExclusion);
        Run("终极完成阻止重复选择和两类经验", FinalStopsTraining);
        Run("终极跨方向及明确留空保留，显示不被历史覆盖", FinalDirectionAndDisplay);
        Run("终极不依赖身份绑定，加载后对账保持实例", FinalPersistence);
        Run("终极定义与配方成本、八种凝胶及三语译文齐全", FinalDefinitions);
    }

    private static void FinalMutualExclusion()
    {
        var p = Pawn(); Train(p, 1f);
        var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        for (int i = 0; i < 5; i++) CompSexSlaveTraining.ReconcileSpecialization(p);
        Check(Ordinary(p) == null, "普通状态叠加");
        Check(p.health.hediffSet.hediffs.Count == 1 && p.health.hediffSet.hediffs[0] == final, "终极重建或重复");
    }

    private static void FinalStopsTraining()
    {
        var p = Pawn(); Train(p, 0.3f);
        p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        Check(!CombatantSpecializationUtility.CanSelect(p, out var reason) && reason == Strings.ITab_SpecializationFinalizedSuffix, "允许重复选择");
        Equal(0f, CombatantSpecializationProgressUtility.NotifyDailyTrainingCompleted(Pawn(), p, 80f));
        Equal(0f, CombatantSpecializationProgressUtility.TryGainProgress(p, CombatantSpecializationProgressUtility.KillProgress(2f)));
        Equal(0.3f, p.Training.specializationProgress);
    }

    private static void FinalDirectionAndDisplay()
    {
        var p = Pawn(); Train(p, 1f);
        var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
        CombatantSpecializationUtility.Sync(p);
        Check(ITab_SexSlaveTraining.Progress(p) == Strings.ITab_SpecializationComplete &&
            ITab_SexSlaveTraining.Label(p).Contains(Strings.ITab_SpecializationFinalizedSuffix), "终极仍显示普通完成");
        p.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
        p.Training.specializationProgress = 0.25f;
        CompSexSlaveTraining.ReconcileSpecialization(p);
        Check(ITab_SexSlaveTraining.Progress(p) != Strings.ITab_SpecializationComplete, "其他方向被终极覆盖");
        p.Training.SetSpecialization(SexSlaveSpecializationType.None);
        CompSexSlaveTraining.ReconcileSpecialization(p);
        Check(p.Training.specializationType == SexSlaveSpecializationType.None &&
            ITab_SexSlaveTraining.Label(p) == Strings.ITab_SpecializationNone &&
            p.health.hediffSet.hediffs.Contains(final), "留空时认领或丢失终极");
        Equal(1f, p.Training.ExportSpecializationProgress()["Combatant"]);
        Equal(0.25f, p.Training.ExportSpecializationProgress()["Cow"]);
    }

    private static void FinalPersistence()
    {
        foreach (PawnIdentity identity in Enum.GetValues<PawnIdentity>())
        {
            var p = Pawn(identity);
            // 模拟存档健康列表已恢复后的 PostLoadInit，不模拟真实 Scribe XML。
            var final = p.health.AddHediff(SSCDefOf.SSC_Hediff_Combatant_Final);
            Scribe.mode = LoadSaveMode.PostLoadInit;
            CompSexSlaveTraining.ReconcileSpecialization(p);
            Check(p.health.hediffSet.hediffs.Single() == final, "终极依赖身份或发生重建");
            Check(p.Training.specializationType == SexSlaveSpecializationType.None, "终极自动认领方向");
        }
    }

    private static void FinalDefinitions()
    {
        var def = XDocument.Load(Path.Combine(root, "Defs/HediffDefs/SSC_HediffDefs_CombatantSpecialization.xml"))
            .Root.Elements().Single(e => (string)e.Element("defName") == "SSC_Hediff_Combatant_Final");
        var stage = def.Element("stages").Elements("li").Single();
        var offsets = stage.Element("statOffsets"); var factors = stage.Element("statFactors");
        Equal(6f, Value(offsets, "ShootingAccuracyPawn")); Equal(6f, Value(offsets, "MeleeHitChance"));
        Equal(-0.06f, Value(offsets, "MentalBreakThreshold"));
        Equal(0.85f, Value(factors, "AimingDelayFactor")); Equal(0.85f, Value(factors, "MeleeCooldownFactor"));
        Equal(0.8f, Value(factors, "IncomingDamageFactor"));
        Check(def.Element("comps").Descendants("abilityDef").Single().Value == "SSC_CombatOverdrive", "终极能力未接入");
        var recipe = XDocument.Load(Path.Combine(root, "Defs/RecipeDefs/RecipeDef_PSEdit.xml"))
            .Root.Elements().Single(e => (string)e.Element("defName") == "SSC_PSEdit_Combatant_Final");
        Equal(3500f, Value(recipe, "workAmount"));
        Check((string)recipe.Element("workSpeedStat") == "GeneralLaborSpeed" &&
            recipe.Element("recipeUsers").Elements().Single().Value == "TableSculpting" &&
            (string)recipe.Element("researchPrerequisite") == "SSC_RES_Combatant", "设施或研究不符");
        Check((string)recipe.Element("requireSpecializationType") == "Combatant" &&
            (string)recipe.Element("requireSpecializationComplete") == "true", "缺少普通完成门槛");
        Check(recipe.Element("exclusiveTags").Elements().Single().Value == "SSC_Hediff_Combatant_Final", "普通标签错误放入筛料排除表");
        var ingredient = recipe.Element("ingredients").Elements().Single();
        Equal(1f, Value(ingredient, "count"));
        var gels = ingredient.Descendants("thingDefs").Single().Elements().Select(e => e.Value).ToArray();
        Check(gels.Length == 8 && gels.Distinct().Count() == 8 && gels.SequenceEqual(
            recipe.Element("fixedIngredientFilter").Element("thingDefs").Elements().Select(e => e.Value)), "八种凝胶白名单不一致");
        foreach (var lang in new[] { "English", "ChineseTraditional", "Russian" })
        {
            var text = XDocument.Load(Path.Combine(root, "Languages", lang, "DefInjected/RecipeDef/RecipeDef_PSEdit.xml"));
            foreach (var suffix in new[] { "label", "description", "jobString" })
                Check(!string.IsNullOrWhiteSpace((string)text.Root.Element("SSC_PSEdit_Combatant_Final." + suffix)), "缺少配方翻译");
        }
    }
}
