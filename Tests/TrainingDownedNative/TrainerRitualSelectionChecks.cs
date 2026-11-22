// AI 辅助生成。使用真实仪式许可与名单，只隔离殖民地和地图生成边界。
using RimWorld;
using HarmonyLib;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    private static void TrainerRitualSelectionChecks()
    {
        Check("binding ritual with an empty host slot skips the incapacitated first trainer", () =>
        {
            // Spawned 与 Map 已由本宿主提供；本例的三人均属于同一殖民地。
            var colonist = AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.IsColonist));
            environment.Patch(colonist, prefix: new HarmonyMethod(typeof(Program), nameof(True)));
            try
            {
                Pawn target = Pawn(PawnIdentity.Slave);
                Pawn first = Pawn(PawnIdentity.Master);
                Pawn second = Pawn(PawnIdentity.Master);
                var training = target.TryGetComp<CompSexSlaveTraining>();
                training.ReplaceAssignedTrainers(new[] { first, second });
                Set(first.health, "healthState", PawnHealthState.Down);
                var ritualTarget = new TargetInfo(IntVec3.Zero, map);

                Require(BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _));
                Require(!SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(first, target, true), false).Allowed);
                Require(SSCRestrictionTrainingUtility.Evaluate(
                    SSCRestrictionTrainingUtility.CreateRequest(second, target, true), false).Allowed);

                var roles = Bare<RitualRoleAssignments>();
                Set(roles, "forcedRoles", new Dictionary<string, Pawn> { ["master"] = first });
                var hostRole = new RitualRole_BindingMaster { id = "master" };
                Require(!BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _, roles, hostRole, true));

                training.RemoveAssignedTrainer(second);
                Require(!BindingRitualSelectionUtility.HasEligibleHost(target, ritualTarget, out _, skipReason: true));
            }
            finally { environment.Unpatch(colonist, HarmonyPatchType.Prefix, environment.Id); }
        });

        Check("formal ownerless selection and ritual start share the master and trainer officer routes", () =>
        {
            var boundary = new Harmony("ssc.tests.formal-ownerless-ritual-start");
            var priorChain = SSCDefOf.ChainOfSexSlave;
            var chainDef = new HediffDef { defName = "Hediff_ChainOfSexSlave", hediffClass = typeof(Hediff_ChainOfSexSlave) };
            var ownerlessDef = new HediffDef { defName = Hediff_SSCOwnerlessSlave.DefName, hediffClass = typeof(Hediff_SSCOwnerlessSlave) };
            DefDatabase<HediffDef>.Add(chainDef);
            DefDatabase<HediffDef>.Add(ownerlessDef);
            SSCDefOf.ChainOfSexSlave = chainDef;
            try
            {
                boundary.Patch(AccessTools.PropertyGetter(typeof(Pawn), nameof(Verse.Pawn.IsColonist)),
                    prefix: new HarmonyMethod(typeof(Program), nameof(True)));
                // 保留真正的 SSC 选角、开场复核和阶段认领；地图/原版角色通用门槛及 RJW 身体输入由夹具提供。
                foreach (var method in AccessTools.GetDeclaredMethods(typeof(RitualRoleAssignments))
                    .Where(m => m.Name == "PawnNotAssignableReason" && m.ReturnType == typeof(string)))
                    boundary.Patch(method, prefix: new HarmonyMethod(typeof(Program), nameof(AllowOwnerlessRitualRole)));
                boundary.Patch(AccessTools.Method(typeof(RitualRole), "AppliesIfChild"),
                    prefix: new HarmonyMethod(typeof(Program), nameof(True)));
                boundary.Patch(AccessTools.Method(typeof(Trainjudge), nameof(Trainjudge.TryCanBeFuckedWithReason),
                    new[] { typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(Program), nameof(True)));
                boundary.Patch(AccessTools.Method(typeof(TranslatorFormattedStringExtensions), "Translate",
                    new[] { typeof(string), typeof(NamedArgument) }),
                    prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessRitualReason)));
                boundary.Patch(AccessTools.Method(typeof(Translator), "Translate", new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(Program), nameof(OwnerlessRitualReason)));

                foreach (bool officerHosts in new[] { false, true })
                {
                    var master = Pawn(PawnIdentity.Master);
                    var host = officerHosts ? Pawn(PawnIdentity.Slave) : master;
                    if (officerHosts)
                    {
                        host.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = host, def = chainDef, target = master, Severity = .5f });
                        var officer = host.TryGetComp<CompSexSlaveTraining>();
                        officer.specializationType = SexSlaveSpecializationType.TrainerOfficer;
                        officer.specializationProgress = .25f;
                        Require(SSCIdentityUtility.SetTrainerEnabled(host, true));
                    }
                    var target = Pawn(PawnIdentity.Slave);
                    var training = target.TryGetComp<CompSexSlaveTraining>();
                    training.ReplaceAssignedTrainers(new[] { host });
                    var progress = new Hediff_SSCOwnerlessSlave { pawn = target, def = ownerlessDef, Severity = .8f };
                    target.health.hediffSet.hediffs.Add(progress);
                    var lord = Ritual(repository, host, target);
                    var ritual = (LordJob_Ritual)lord.LordJob;
                    var noTravel = TargetInfo.Invalid;
                    Require(BindingRitualSelectionUtility.HasEligibleHost(target, noTravel, out _));
                    Require(Harmony_BindingRitualStart.Prefix(noTravel, ritual.Ritual, ritual.assignments), "start guard rejected the valid assignment");
                    Require(BindingRitualStateUtility.TryBeginPhase(host, target, lord), "could not claim the first phase");

                    var driver = Bare<JobDriver_RitualTraining>();
                    driver.pawn = host;
                    driver.job = JobMaker.MakeJob(SSCDefOf.Training_Ritual, target);
                    Require(SSCRestrictionJobContext.TryCreate(driver, out var request));
                    Require(request.Kind == (officerHosts ? SSCInteractionKind.RitualTraining : SSCInteractionKind.BindingPreparation));
                    Require(SSCRestrictionTrainingUtility.Evaluate(request, false).Allowed);

                    if (officerHosts)
                    {
                        var equipment = Bare<ThingDef>();
                        equipment.modExtensions = new List<DefModExtension>
                        {
                            new SSCRestrictionEquipmentExtension { forced = new SSCRestrictionOverrides { receiveTraining = SSCRestrictionValue.Deny } }
                        };
                        target.apparel = new Pawn_ApparelTracker(target);
                        target.apparel.WornApparel.Add(new Apparel { def = equipment });
                        Require(!BindingRitualSelectionUtility.ValidateStart(ritual.assignments, noTravel, out _));
                        target.apparel = null;
                        target.health.hediffSet.hediffs.Remove(progress);
                        Require(!BindingRitualSelectionUtility.ValidateStart(ritual.assignments, noTravel, out _), "zero-stage target was treated as formal ownerless");
                    }
                    else
                    {
                        training.RemoveAssignedTrainer(host);
                        Require(!BindingRitualSelectionUtility.ValidateStart(ritual.assignments, noTravel, out _));
                    }
                }
            }
            finally
            {
                boundary.UnpatchAll(boundary.Id);
                SSCDefOf.ChainOfSexSlave = priorChain;
            }
        });
    }

    private static bool AllowOwnerlessRitualRole(ref string __result) { __result = null; return false; }
    private static bool OwnerlessRitualReason(string __0, ref TaggedString __result) { __result = __0; return false; }
}
