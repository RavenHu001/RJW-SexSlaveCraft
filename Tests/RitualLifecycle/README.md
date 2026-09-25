# Binding Ritual lifecycle regression tests

The suite currently contains 61 cases, including ritual lifecycle, UAP compatibility,
daily training, Education interactions, and receiver handoff/reservation regressions.
The original lifecycle tests were committed with the lifecycle fix
in [481ac53](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/commit/481ac53135842064262d8a34a2beed173b0ace2b)
on the `Bug-fix` branch. The release script runs this suite alongside the 29-case
`RitualProgression` suite and stops packaging if either fails.

Run from the repository root with .NET SDK 9:

```powershell
dotnet run --project Tests/RitualLifecycle/RitualLifecycle.csproj
# Omit the UAP type entirely: 51 shared cases plus one absence check (52 total).
dotnet run --project Tests/RitualLifecycle/RitualLifecycle.csproj -p:EnableUapTestStub=false
```

The project has no NuGet dependencies and does not require Unity or a game
installation. It links the production `BindingRitualStateUtility`, lifecycle
Harmony patches, `JobGiver_RitualBinding`, `JobDriver_RitualTraining`, the daily driver,
and the real `TrainingJobUtility`.
`GameStubs.cs` supplies the minimal game host and stores observable state. The
tests do not duplicate the ritual ownership, progression, recovery or outcome
eligibility algorithms.

The UAP cases also link the production `UapRitualCompatibilityUtility`. A test-only
type with UAP's real full name and unlock signature models the stale-job lock and
its confirmed animation-stop behavior. A positive control reproduces that failure;
the production driver must release old locks before RJW Start and on completion
or interruption. Further checks cover unrelated participants, preserving new locks,
late callbacks, load-time enumeration, preserving animations/jobs, and API exceptions.
This adapter does not execute the installed UAP DLL or Unity rendering. In-game
validation should restart RimWorld and start the ritual from a pre-ritual save,
including Footjob and Vaginal transitions; an already-cleared animation queue in
an old failure snapshot is not reconstructed by this phase-boundary fix.

These tests exercise cancellation while walking, cancellation during a scene,
interruption of a single job in a still active ritual, participant removal, phase
changes, independent rituals, late callbacks from a previous ritual, one-time
outcome eligibility and recovery of legacy state. The final-phase test synchronously
cleans up the ritual and reenters its phase finish from the completion memo, checking
that scene processing, scene cleanup, the memo and outcome claim each occur once.
Cleanup also preserves the assigned trainer, specialization progress and daily
cooldown fields present in the host comp.

The host runs the real driver's preparation, scene initialization, tick and finish
callbacks. Job cleanup invokes only the driver's global finish actions and the
current toil's finish actions; a future scene toil is never finished on behalf of
a walking job. Physical eligibility remains an interface model; receiver startup,
position synchronization, reservation checks and abort cleanup run production code.
The host job tracker can accept or reject a requested receiver. RJW calls are
counted without running their game effects.

The reservation model stores exact target/pawn/job triples and throws on an invalid
release. Four regression cases cover consecutive targets for one trainer, removal
during teleport notification, preservation of another job's reservation, and repeated
handoff. They require the actual target receiver job and initiator to be installed,
not just a training label on the initiating pawn. The path follower supplies an
explicit arrival callback; it does not calculate map paths or run the game scheduler.

The host invokes the production Harmony patch methods directly. It does not apply
Harmony detours or execute RimWorld's complete lord state graph, job scheduling,
Unity rendering, RJW animations, actual rewards or Scribe serialization. A restored
active ritual is represented by restoring the references and flags that Scribe
would load; this verifies recovery decisions and load-time toil enumeration, not
the serializer itself. Full in-game cancellation and save/load remain integration
checks. The separate `RitualProgression` suite exercises long-term chain and
corruption numerical behavior.

Stage 3B also links the production daily driver and restriction core/adapter. Its cases cover preparation failure, walking cleanup, unstarted scene settlement, stale callbacks, normal daily payout, owner precedence, phase cancellation and cancellation ownership. Cancellation is modeled by invoking the production cleanup patches after removing the lord; the real RimWorld signal graph is not executed. Exact restriction save markers and Harmony dispatch are covered by InteractionProtection; role selection and the full trainer identity utility are covered by TrainerIdentity.

The Training Officer Stage 3 assertions require a completed daily scene to notify specialization progress once. A repeated payout callback cannot notify it again; the actual progress calculation is covered by TrainerIdentity.
