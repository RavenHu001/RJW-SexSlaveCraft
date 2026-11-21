using System.Xml;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void CorruptionMoodTests(string repo)
    {
        var xml = new XmlDocument();
        xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_CorruptionLonging.xml"));
        var thought = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement["ThoughtDef"], false);
        var dirty = AccessTools.Field(typeof(SituationalThoughtHandler), "thoughtsDirty");
        xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_OwnerlessSlave.xml"));
        foreach (string name in new[] { "Consciousness", "Talking", "Manipulation" })
            if (DefDatabase<PawnCapacityDef>.GetNamedSilentFail(name) == null)
                DefDatabase<PawnCapacityDef>.Add(new PawnCapacityDef { defName = name });
        if (DefDatabase<StatDef>.GetNamedSilentFail("MarketValue") == null)
            DefDatabase<StatDef>.Add(new StatDef { defName = "MarketValue", defaultBaseValue = 1f });
        var ownerlessDef = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement["HediffDef"], false);
        DefDatabase<HediffDef>.Add(ownerlessDef);
        var sensitivityDef = SSCOrgasmSensitivityUtility.Definition;
        if (sensitivityDef == null)
        {
            xml.Load(Path.Combine(repo, "1.7/Defs/HediffDefs/SSC_OrgasmSensitivity.xml"));
            sensitivityDef = DirectXmlToObject.ObjectFromXml<HediffDef>(xml.DocumentElement["HediffDef"], false);
            DefDatabase<HediffDef>.Add(sensitivityDef);
        }
        xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_OrgasmSensitivity.xml"));
        var sharedSensitivityMood = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement["ThoughtDef"], false);
        DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
        xml.Load(Path.Combine(repo, "1.7/Defs/ThoughtDefs/SSC_OwnerlessSlave.xml"));
        var ownerlessMood = DirectXmlToObject.ObjectFromXml<ThoughtDef>(xml.DocumentElement["ThoughtDef"], false);
        DefDatabase<ThoughtDef>.Add(ownerlessMood);

        (Pawn pawn, Need_Corruption need) Subject(PawnIdentity identity = PawnIdentity.Unset)
        {
            var pawn = ImprintPawn(identity);
            var need = new Need_Corruption(pawn)
            {
                def = new NeedDef { defName = "CorruptionMoodCorruption", label = "corruption" }
            };
            pawn.needs.AllNeeds.Add(need);
            pawn.needs.mood = new Need_Mood(pawn) { def = new NeedDef { defName = "CorruptionMoodFixture" } };
            pawn.needs.AllNeeds.Add(pawn.needs.mood);
            return (pawn, need);
        }

        Check("ownerless long-term mood appears through the native thought list and switches with the active carrier", () =>
        {
            var previous = ThoughtUtility.situationalNonSocialThoughtDefs;
            var previousSocial = ThoughtUtility.situationalSocialThoughtDefs;
            try
            {
                ThoughtUtility.Reset();
                var (pawn, _) = Subject(PawnIdentity.Slave);
                var record = pawn.health.AddHediff(ownerlessDef);
                var handler = pawn.needs.mood.thoughts.situational;

                Thought ReadMood()
                {
                    var thoughts = new List<Thought>();
                    handler.AppendMoodThoughts(thoughts);
                    return thoughts.SingleOrDefault(t => t.def == ownerlessMood);
                }

                foreach (var (severity, stage, mood) in new[] { (.1f, 0, -5f), (.3f, 1, -2f), (.5f, 2, 2f), (.9f, 3, 6f) })
                {
                    record.Severity = severity;
                    var thought = ReadMood();
                    Require(thought != null, $"missing ownerless mood at stage {stage + 1}");
                    Equal(stage, thought.CurStageIndex);
                    Equal(mood, thought.MoodOffset());
                }

                var master = ImprintPawn(PawnIdentity.Master);
                Require(SSCBondUtility.Bind(master, pawn));
                Require(ReadMood() == null);
                Require(SSCBondUtility.Unbind(pawn));
                Equal(6f, ReadMood().MoodOffset());
                record.Severity = .09f;
                Require(ReadMood() == null);
            }
            finally
            {
                ThoughtUtility.situationalNonSocialThoughtDefs = previous;
                ThoughtUtility.situationalSocialThoughtDefs = previousSocial;
            }
        });

        Thought AssertMood(Pawn pawn, float? expected, int? textStage = null)
        {
            var active = new List<Thought>();
            pawn.needs.mood.thoughts.situational.AppendMoodThoughts(thought, active);
            Require(active.Count == (expected.HasValue ? 1 : 0), $"expected mood {expected?.ToString() ?? "none"}, thought count {active.Count}");
            if (expected.HasValue)
            {
                Near(expected.Value, active[0].MoodOffset());
                if (textStage.HasValue)
                {
                    Equal(textStage.Value, active[0].CurStageIndex);
                    Require(ReferenceEquals(thought.stages[textStage.Value], active[0].CurStage), "must retain the stage's description");
                }
            }
            return active.Count == 0 ? null : active[0];
        }

        Check("ownerless and bound stage three survive native health Scribe and share stage effects", () =>
        {
            var master = ImprintPawn(PawnIdentity.Master);
            var (bound, _) = Subject(PawnIdentity.Slave);
            Require(SSCBondUtility.Bind(master, bound));
            SSCBondUtility.GetChain(bound).Severity = .8f;
            var (ownerless, _) = Subject(PawnIdentity.Slave);
            var record = ownerless.health.AddHediff(ownerlessDef);
            Equal(.2f, record.Severity);
            record.Severity = .8f;

            foreach (var (source, expectedMaster) in new[] { (bound, master), (ownerless, (Pawn)null) })
            {
                // 正式身份由夹具提供，原版 Scribe 恢复健康记录、严重度和锁链引用。
                var (body, need) = Subject(PawnIdentity.Slave);
                Load(Save(source), body, master);
                Equal(expectedMaster, SSCBondUtility.GetBoundMaster(body));
                Equal(.8f, TrainingOutcomeUtility.GetSexSlaveProgress(body).Severity);
                Equal(3, SSCIdentityUtility.GetSexSlaveStage(body));
                Equal(1.6f, TrainingOutcomeUtility.GetChainStageMult(body));
                Equal(.5f, TrainingOutcomeUtility.GetCorruptionStageFloor(body));
                need.SetCorruption(.95f);
                AssertMood(body, -3f);
            }
        });

        Check("ownerless progress locks the ordinary identity submission and refreshes the mood when it changes", () =>
        {
            var (pawn, need) = Subject(PawnIdentity.Slave);
            need.SetCorruption(.95f);
            Equal(0, SSCIdentityUtility.GetSexSlaveStage(pawn));
            Require(!SSCIdentityUtility.IsIdentityLocked(pawn));
            AssertMood(pawn, -10f);
            var record = pawn.health.AddHediff(ownerlessDef);
            Require(SSCIdentityUtility.IsIdentityLocked(pawn));
            Require(!SSCIdentityUtility.TrySetManualIdentity(pawn, PawnIdentity.Unset));
            Require(!SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Unset));
            AssertMood(pawn, -10f, textStage: 6);
            record.Severity = .5f;
            AssertMood(pawn, -3f);
            pawn.health.RemoveHediff(record);
            Require(!SSCIdentityUtility.IsIdentityLocked(pawn));
            AssertMood(pawn, -10f, textStage: 3);
        });

        Check("the RJW designation and outfit read ownerless stage boundaries", () =>
        {
            var (pawn, _) = Subject(PawnIdentity.Slave);
            var record = pawn.health.AddHediff(ownerlessDef);
            if (DefDatabase<TraitDef>.GetNamedSilentFail("Masochist") == null)
                DefDatabase<TraitDef>.Add(new TraitDef { defName = "Masochist" });
            Require(!SSCComfortDesignationCompatibility.IsMasochistOrSexSlave(pawn));
            record.Severity = .3f;
            Require(SSCComfortDesignationCompatibility.IsMasochistOrSexSlave(pawn));

            var previousDef = SSCDefOf.EFOutfit;
            try
            {
                var outfit = new Apparel { def = Bare<ThingDef>() };
                SSCDefOf.EFOutfit = outfit.def;
                string reason = null;
                bool result = true;
                record.Severity = MathF.BitDecrement(.9f);
                Require(!Patch_SexSlaveApparelRestriction.Prefix(outfit, pawn, ref reason, ref result) && !result);
                record.Severity = .9f;
                Require(Patch_SexSlaveApparelRestriction.Prefix(outfit, pawn, ref reason, ref result));
            }
            finally { SSCDefOf.EFOutfit = previousDef; }
        });

        Check("ownerless effects, mood and protection activate together at the first threshold", () =>
        {
            var (pawn, _) = Subject(PawnIdentity.Slave);
            var record = HediffMaker.MakeHediff(ownerlessDef, pawn);
            record.Severity = MathF.BitDecrement(.1f);
            pawn.health.AddHediff(record);
            bool CanRebel()
            {
                bool result = true;
                Patch_SlaveRebellionUtility_CanParticipateInSlaveRebellion.Postfix(pawn, ref result);
                return result;
            }
            Equal(0, record.CurStageIndex);
            Require(record.CurStage.statFactors.NullOrEmpty() && record.CurStage.capMods.NullOrEmpty());
            Require(!ownerlessMood.Worker.CurrentState(pawn).Active && CanRebel());
            Require(!SSCPnaExposureUtility.HasAddictionImmunity(pawn));
            Require(!pawn.health.hediffSet.HasHediff(sensitivityDef));

            record.Severity = .1f;
            Equal(1, record.CurStageIndex);
            Equal(1.25f, record.CurStage.statFactors.Single(x => x.stat.defName == "MarketValue").value);
            Equal(-.05f, record.CurStage.capMods.Single(x => x.capacity.defName == "Consciousness").offset);
            Equal(0, ownerlessMood.Worker.CurrentState(pawn).StageIndex);
            Equal(-5f, ownerlessMood.stages[0].baseMoodEffect);
            Require(!CanRebel() && SSCPnaExposureUtility.HasAddictionImmunity(pawn));
            Require(!SSCPnaExposureUtility.IsRooted(pawn) && !Utils.HasChainHediff(pawn));
            var automatic = pawn.health.hediffSet.GetFirstHediffOfDef(sensitivityDef);
            Require(automatic is Hediff_SSCOrgasmSensitivity && !automatic.ShouldRemove);
            Equal(sensitivityDef.Description, automatic.Description);
            var stageOne = SSCOrgasmSensitivityUtility.GetState(pawn);
            Equal(1, stageOne.EffectiveLevel);
            Equal(0, stageOne.SavedLevel);
            Equal(2f, stageOne.Multiplier);
            Near(.3f, stageOne.VulnerabilityOffset);
            record.Severity = .9f;
            var stageFour = SSCOrgasmSensitivityUtility.GetState(pawn);
            Equal(4, stageFour.EffectiveLevel);
            Equal(16f, pawn.GetStatValue(SSCOrgasmFrequencyUtility.Definition));
            Equal(3f, stageFour.VulnerabilityOffset);
            Require(sharedSensitivityMood.Worker.CurrentState(pawn).Active);
            Equal(automatic, pawn.health.hediffSet.GetFirstHediffOfDef(sensitivityDef));
            pawn.health.RemoveHediff(record);
            Require(!pawn.health.hediffSet.HasHediff(sensitivityDef));
            Require(!ownerlessMood.Worker.CurrentState(pawn).Active && CanRebel());

            // 原有手动档位在自动期间留在同一个实例中，重新绑定后立即恢复有主模式。
            var manual = HediffMaker.MakeHediff(sensitivityDef, pawn);
            manual.Severity = 2f;
            pawn.health.AddHediff(manual);
            pawn.health.AddHediff(ownerlessDef).Severity = .9f;
            Equal(manual, SSCOrgasmSensitivityUtility.Find(pawn));
            Equal(4, SSCOrgasmSensitivityUtility.GetState(pawn).EffectiveLevel);
            Equal(2f, manual.Severity);
            var chain = (Hediff_ChainOfSexSlave)HediffMaker.MakeHediff(SSCDefOf.ChainOfSexSlave, pawn);
            chain.target = ImprintPawn(PawnIdentity.Master);
            chain.Severity = .9f;
            pawn.health.AddHediff(chain);
            Equal(manual, SSCOrgasmSensitivityUtility.Find(pawn));
            Equal(2, SSCOrgasmSensitivityUtility.GetState(pawn).EffectiveLevel);
        });

        Check("ownerless stage changes drive read-only permissions and natural romance", () =>
        {
            var (pawn, _) = Subject(PawnIdentity.Slave);
            var other = ImprintPawn();
            var record = pawn.health.AddHediff(ownerlessDef);
            var comp = pawn.TryGetComp<CompSexSlaveTraining>();
            bool Allowed(SSCInteractionKind kind, Pawn actor, Pawn target)
                => SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(actor, target, kind, true)).Allowed;
            Require(comp.restrictionConfig == null && Allowed(SSCInteractionKind.SelfTraining, pawn, pawn));
            var saved = new SSCRestrictionConfig();
            comp.restrictionConfig = saved;
            Require(Allowed(SSCInteractionKind.Masturbation, pawn, pawn));

            record.Severity = .5f;
            Require(!Allowed(SSCInteractionKind.Masturbation, pawn, pawn));
            Require(Allowed(SSCInteractionKind.SelfTraining, pawn, pawn));
            Require(!Allowed(SSCInteractionKind.Consensual, pawn, other));
            Require(Allowed(SSCInteractionKind.Consensual, other, pawn));
            Equal(SSCRestrictionSource.OwnerlessStage,
                SSCRestrictionResolver.Resolve(pawn, null, SSCRestrictionRule.ReceiveTraining).Source);
            Require(!SSCRestrictionEditor.TrySet(pawn, SSCRestrictionRule.Masturbation, SSCRestrictionValue.Allow));
            Equal(1, SSCRestrictionLifecycle.ApplyDefaults(new[] { pawn }, new SSCRestrictionRules()).Skipped);
            Equal(saved, comp.restrictionConfig);

            var profile = new SSCRestrictionProfileDef
            {
                defName = "SSC_TestOwnerlessOverride", specialization = SexSlaveSpecializationType.Bus,
                forced = new SSCRestrictionOverrides { masturbation = SSCRestrictionValue.Allow }
            };
            DefDatabase<SSCRestrictionProfileDef>.Add(profile);
            try
            {
                comp.specializationType = SexSlaveSpecializationType.Bus;
                Require(Allowed(SSCInteractionKind.Masturbation, pawn, pawn));
                Equal(SSCRestrictionSource.Specialization,
                    SSCRestrictionResolver.Resolve(pawn, null, SSCRestrictionRule.Masturbation).Source);
                var equipment = Bare<ThingDef>();
                equipment.modExtensions = new List<DefModExtension>
                {
                    new SSCRestrictionEquipmentExtension
                    {
                        forced = new SSCRestrictionOverrides { masturbation = SSCRestrictionValue.Deny }
                    }
                };
                pawn.apparel = new Pawn_ApparelTracker(pawn);
                pawn.apparel.WornApparel.Add(new Apparel { def = equipment });
                Require(!Allowed(SSCInteractionKind.Masturbation, pawn, pawn));
                Equal(SSCRestrictionSource.Equipment,
                    SSCRestrictionResolver.Resolve(pawn, null, SSCRestrictionRule.Masturbation).Source);
            }
            finally
            {
                AccessTools.Method(typeof(DefDatabase<SSCRestrictionProfileDef>), "Remove")
                    .Invoke(null, new object[] { profile });
            }

            Require(SSCRomancePolicy.Evaluate(pawn, other, SSCRomanceAction.Romance).CanInitiate);
            Require(!SSCRomancePolicy.Evaluate(pawn, other, SSCRomanceAction.Proposal).CanInitiate);
            var view = SSCRomanceUI.GetRuleView(pawn, SSCRomanceSetting.InitiateRomance, SSCRomancePolicy.GetOwnerlessStage(pawn));
            Require(view.DisplayValue && !view.CanChange && view.Context == null);
            record.Severity = .9f;
            view = SSCRomanceUI.GetRuleView(pawn, SSCRomanceSetting.InitiateRomance, SSCRomancePolicy.GetOwnerlessStage(pawn));
            Require(!view.DisplayValue && !view.CanChange && view.Context == null);
            Require(!SSCRomancePolicy.Evaluate(pawn, other, SSCRomanceAction.Romance).CanInitiate);
            Require(SSCRomancePolicy.Evaluate(pawn, other, SSCRomanceAction.Breakup).CanInitiate);
            Equal(SSCRomanceReception.Vanilla,
                SSCRomancePolicy.Evaluate(other, pawn, SSCRomanceAction.Proposal).Reception);
            // 资格查询也会遇到尚无健康组件的角色。该状态沿原版判定。
            other.health = null;
            Require(SSCRomancePolicy.Evaluate(other, pawn, SSCRomanceAction.Romance).CanInitiate);
        });

        Check("corruption display and native thought use the same exact four boundaries", () =>
        {
            var (pawn, need) = Subject();
            float[] boundaries = { .12f, .3f, .5f, .9f };
            float?[] moods = { null, -1f, -3f, -6f, -10f };
            var markers = (List<float>)AccessTools.Field(typeof(Need), "threshPercents").GetValue(need);
            Require(markers.SequenceEqual(boundaries), "bar markers");

            void CheckLevel(float level, int index, int percentage)
            {
                need.CurLevel = level;
                var presentation = need.GetStagePresentation();
                Equal(index, presentation.Index);
                Equal(boundaries.Length, presentation.ThresholdCount);
                Equal((percentage / 100d).ToString("0%"), presentation.Percentage);
                Equal(percentage / 100f, presentation.Fraction);
                Equal(presentation.Fraction, Harmony_CorruptionNeedBar.GetFillFraction(need));
                Equal(level, need.CurLevelPercentage);
                string tip = need.GetTipString();
                Require(tip.Contains(presentation.Label) && tip.Contains(presentation.Percentage),
                    "need tooltip must use the overview's label and percentage");
                AssertMood(pawn, moods[index], index > 0 ? index - 1 : null);
            }

            for (int i = 0; i < boundaries.Length; i++)
            {
                int percentage = (int)MathF.Round(boundaries[i] * 100f);
                CheckLevel(MathF.BitDecrement(boundaries[i]), i, percentage - 1);
                CheckLevel(boundaries[i], i + 1, percentage);
            }
            CheckLevel(.119f, 0, 11);
            CheckLevel(MathF.BitDecrement(.91f), 4, 90);
            CheckLevel(.91f, 4, 91);
            CheckLevel(MathF.BitDecrement(1f), 4, 99);
            CheckLevel(1f, 4, 100);
        });

        Check("native need bar patch changes only the fill input and preserves other needs", () =>
        {
            var draw = AccessTools.Method(typeof(Need), nameof(Need.DrawOnGUI));
            var original = PatchProcessor.GetOriginalInstructions(draw).ToList();
            var rewritten = Harmony_CorruptionNeedBar.Transpiler(original.Select(code => new CodeInstruction(code))).ToList();
            var nativePercentage = AccessTools.PropertyGetter(typeof(Need), nameof(Need.CurLevelPercentage));
            var fill = AccessTools.Method(typeof(Widgets), nameof(Widgets.FillableBar), new[] { typeof(UnityEngine.Rect), typeof(float) });
            var display = AccessTools.Method(typeof(Harmony_CorruptionNeedBar), nameof(Harmony_CorruptionNeedBar.GetFillFraction));
            int changed = rewritten.FindIndex(code => code.Calls(display));
            Require(changed >= 0 && original[changed].Calls(nativePercentage) && rewritten[changed + 1].Calls(fill),
                "the native fill should read the display-only accessor");
            Equal(original.Count, rewritten.Count);
            Equal(1, rewritten.Where((code, index) => code.opcode != original[index].opcode ||
                !Equals(code.operand, original[index].operand)).Count());
            var (pawn, _) = Subject();
            pawn.needs.mood.CurLevel = .9099f;
            Equal(pawn.needs.mood.CurLevelPercentage, Harmony_CorruptionNeedBar.GetFillFraction(pawn.needs.mood));
        });

        Check("corruption thought follows formal identity changes at the same corruption", () =>
        {
            var (pawn, need) = Subject();
            pawn.TryGetComp<CompSexSlaveTraining>().ReplaceMasterQualification(true);
            need.SetCorruption(.95f);
            AssertMood(pawn, -10f);
            var handler = pawn.needs.mood.thoughts.situational;
            dirty.SetValue(handler, false);
            Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Master));
            Require((bool)dirty.GetValue(handler), "identity transition did not invalidate situational thoughts");
            AssertMood(pawn, null);
            Equal(.95f, need.CurLevel);
            Require(SSCIdentityUtility.TrySetIdentity(pawn, PawnIdentity.Slave));
            AssertMood(pawn, -10f, textStage: 3); // 正式身份已经生效，尚未建链。
        });

        Check("corruption thought refreshes at 31, 51 and 91 percent and after a ritual stage write", () =>
        {
            var (pawn, need) = Subject(PawnIdentity.Slave);
            var chain = (Hediff_ChainOfSexSlave)pawn.health.AddHediff(SSCDefOf.ChainOfSexSlave);
            var handler = pawn.needs.mood.thoughts.situational;
            foreach (var (severity, startPercent, boundTextStage) in new[] { (.2f, 31, 4), (.4f, 51, 5), (.8f, 91, 6) })
            {
                chain.Severity = severity;
                need.CurLevel = (startPercent - 1) / 100f;
                AssertMood(pawn, null);
                int index = need.GetStagePresentation().Index;
                dirty.SetValue(handler, false);
                float start = startPercent / 100f;
                float before = MathF.BitDecrement(start);
                need.CurLevel = before;
                Equal(before, need.CurLevel);
                Equal(((startPercent - 1) / 100d).ToString("0%"), need.GetStagePresentation().Percentage);
                Require(!(bool)dirty.GetValue(handler), "fractions inside the same displayed percent must not invalidate");
                AssertMood(pawn, null);
                need.CurLevel = start;
                Equal(index, need.GetStagePresentation().Index);
                Equal((startPercent / 100d).ToString("0%"), need.GetStagePresentation().Percentage);
                Require((bool)dirty.GetValue(handler), "crossing the whole-percent ceiling inside the same band must invalidate");
                AssertMood(pawn, -3f, boundTextStage);
            }
            dirty.SetValue(handler, false);
            need.CurLevel = .9099f;
            Require((bool)dirty.GetValue(handler), "dropping below 91% must invalidate");
            AssertMood(pawn, null);
            // 保持同一个极重度档，从一阶升至三阶只减轻扣分，升至四阶时心情消失。
            chain.SetProgressSeverity(pawn, .2f);
            need.CurLevel = .5f;
            AssertMood(pawn, -6f, textStage: 5);
            need.CurLevel = .91f;
            string extremeLabel = AssertMood(pawn, -10f, textStage: 6).LabelCap;
            foreach (var (severity, offset) in new[] { (.4f, -6f), (.8f, -3f) })
            {
                dirty.SetValue(handler, false);
                chain.SetProgressSeverity(pawn, severity);
                Require((bool)dirty.GetValue(handler), "stage change must refresh the offset even though the text stage stays the same");
                Equal(.91f, need.CurLevel);
                Equal(extremeLabel, AssertMood(pawn, offset, textStage: 6).LabelCap);
            }
            dirty.SetValue(handler, false);
            chain.SetProgressSeverity(pawn, .9f);
            Require((bool)dirty.GetValue(handler), "ritual stage write did not invalidate");
            AssertMood(pawn, null);
            need.CurLevel = 1f;
            AssertMood(pawn, null);
            pawn.health.RemoveHediff(chain);
            AssertMood(pawn, -10f, textStage: 3);
            dirty.SetValue(handler, false);
            Hidden(typeof(Pawn_NeedsTracker), pawn.needs, "RemoveNeed", need.def);
            Require((bool)dirty.GetValue(handler), "native removal of the corruption need did not invalidate");
            AssertMood(pawn, null);
        });
    }
}
