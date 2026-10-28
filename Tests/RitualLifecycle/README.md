# Binding Ritual lifecycle regression tests

2026-10-24 ability-toggle batch 3: **232/232** (228 + 4). The real PE job verifies all gift/milk combinations, clearing only the personality dictionary while preserving the old milk field, invalidating the old token and releasing the mutation scope. Snapshot serialization remains covered by PersonalityTraits; this host observes the StorePawnData boundary. See [batch 3](../../Docs/Development/Completed/通用能力开关框架第三批生命周期复核与验证.md).

The suite currently contains 228 cases, including ritual lifecycle, UAP compatibility,
daily training, Education interactions, receiver handoff/reservation regressions,
and exclusive ownership during personality excretion.

The ritual development toggle cases compile the production settings partial and whole outcome worker: defaults and missing keys, preference roundtrips, current setting at resolution, Def permission, culture eligibility, four outcome indices, actual capped letter values, cancellation, duplicate/reentrant callbacks and restored completion claims. Scribe and culture payout are observable model boundaries; the separate `RitualDevelopmentNative` suite exercises the real 1.7 curve/type gate and tracker. Neither suite is in-game acceptance.

The bond-relation regression runs the production relation admission and complete ritual outcome worker with observable social storage. Eight cases cover 79/80/100 opinion, one award per completed ritual, authorized third-party Master/officer hosts, incomplete rituals, and identity invalidation during opinion lookup or relation creation. Binding maintenance and actual Harmony label installation are covered by `BondRelations`.
The original lifecycle tests were committed with the lifecycle fix
in [481ac53](https://github.com/RavenHu001/RJW-SexSlaveCraft-TieJin-Modify/commit/481ac53135842064262d8a34a2beed173b0ace2b)
on the `Bug-fix` branch. The release script runs this suite alongside the 29-case
`RitualProgression` suite and stops packaging if either fails.

Run from the repository root with .NET SDK 9:

```powershell
dotnet run --project Tests/RitualLifecycle/RitualLifecycle.csproj
# Omit the UAP type entirely: shared cases plus an absence check.
dotnet run --project Tests/RitualLifecycle/RitualLifecycle.csproj -p:EnableUapTestStub=false
```

The project has no NuGet dependencies and does not require Unity or a game
installation. It links the production `BindingRitualStateUtility`, lifecycle
Harmony patches, `JobGiver_RitualBinding`, `JobDriver_RitualTraining`, the daily driver,
and the real `TrainingJobUtility`. Personality-excretion cases also compile the production
`JobDriver_PE`, `WorkGiver_PE`, `WorkGiverTargetUtility` and `PersonalityExcretionJobUtility`.

The 2026-10-11 qualification cases run the real extraction completion and qualification
clearer: the snapshot is captured before the original body is cleared; cancellation and
missing storage retain its qualification. Snapshot contents are an interface model here;
the real personality store and complete transfer are covered by `PersonalityTraits`.
Eleven cases cover reservation retention, both player-forced flags, stale candidates,
repeated handoff, cancellation/reassignment and one gel produced by the original
actor after a contender is rejected. Reservation storage and RJW partner registration
are small engine-boundary models; personality payload storage, rendering and Unity
are not executed. The original ownership cases failed eight times against the pre-fix production
files and all pass with the fix. The completed scene also records that its payload is stored
before cat and dog effects are detached; a missing storage component cannot invoke either
detach boundary. These checks run the real extraction task, while the actual pet-state and
cooldown algorithms are exercised by PersonalityTraits and PetCatAbility/PetDogAbility.

The 2026-10-24 PE approach regression adds 20 cases (12 also run without UAP).
It drives the production approach, receiver handoff, scene finish and extraction for
the same actor across two targets, with a retained external position lock between
scenes. Other cases cover Touch arrival and same-cell scene synchronization,
restored idle paths, retry frequency and limits, saved/missing retry fields,
resetting consecutive failures after movement, and stale callbacks after changing
the Job, driver, loadID, target or map, including RJW's cached PartnerPawn. Reentrant unlock and throwing external API
cases preserve replacement tasks or terminate a persistently blocked approach.
The path host records requests and supplies explicit arrival signals; the UAP stub
supplies locks and notification hooks. Neither reproduces SSC recovery logic, real
map pathfinding, actual UAP patch execution, or native Scribe cross-reference loading.
The initial 19-case batch failed 16 times before the fix; an additional cached-target
case failed until ownership also checked the live Job target. The final suite passed
228/228 cases with the UAP boundary and 212/212 without it. Related InteractionProtection (259/259) and
ForcedSexProps (28/28) passed, and an isolated production build against the local
RimWorld 1.7.4985 rev1269 assemblies succeeded without warnings or errors. Logs
and the test build are in `.builds/pe-approach/`. On 2026-10-24, the maintainer
confirmed in game that consecutive personality-excretion jobs no longer leave
the actor standing idle; this reported scenario is accepted. Other combinations
and native save/load recovery were not individually reported as tested in game.

`GameStubs.cs` supplies the minimal game host and stores observable state. The
tests do not duplicate the ritual ownership, progression, recovery or outcome
eligibility algorithms.

The trainer-officer feedback first version adds 18 cases and compiles the complete production
`RitualOutcomeEffectWorker_SSCBinding.Apply`. Daily cases exercise both forced flags, actual
trainer/receiver references, recursive callbacks, interrupted/missing scenes, formal outcome
exceptions, reconstruction with consumed/unconsumed job flags, and identity/task replacement
between experience and feedback. Whole-ritual cases run six real phase drivers, confirm no
per-phase feedback, then execute one production outcome for the actual officer; poor completed
outcomes still notify. Cancellation, recursive outcome calls, exceptions, restored consumed
claim fields and replacement rituals cannot repeat or borrow the notification. Callback changes
during feedback preserve replacement state and suppress later stale effects. The new
`RitualOutcomeInterfaces.cs` only supplies vanilla quality/text/letter API boundaries with one
outcome option, no spectator comp, no vanilla memory and no attachable outcome effect. It does
not reproduce ownership, phase progression or once-only outcome claims. Restored ritual fields
are supplied as they would appear after reference loading; this is not a complete Scribe
serialization test. Production officer qualification/social direction is tested by TrainerIdentity,
and one-day memory age across personality migration by PersonalityMemories.

The 13 receiver-handoff cases cover non-interrupting position synchronization,
replacement work and its reservations, reuse of the same Job object with a new
loadID, orphan receiver cleanup, repeated scene synchronization, mismatched pairs,
ritual/PE callers, and preservation of furniture receivers. The initial five cases
all failed against the original code. The host models the endCurrentJob teleport
flag and offers callbacks at teleport and receiver startup; pooled reuse is injected
explicitly, rather than emulating the entire engine scheduler. These tests do not
execute RimWorld's hunger/work selection or the installed furniture mod.

For in-game acceptance, restart with the rebuilt DLL and test adjacent-cell daily
training with (1) an empty food need and available food and (2) full food and an
available crafting bill. Position synchronization must preserve the initiator job,
start its matching receiver, and allow completion. Also check cancellation/drafting,
same-cell starts, consecutive targets, ritual phase changes and furniture receivers.

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

The unified training identity cases link the production admission adapter, daily driver, ritual driver and ritual state utility. They cover direct phase entry/completion/outcome claims with invalid SSC identities, the protection switch, first binding without a chain, identity changes during walking/Start/SexTick/ProcessSex, saved ritual recovery, continued claimed outcome eligibility, and delayed animation callbacks. Daily cases also exercise task replacement, pooled job IDs, changed targets, position setter callbacks and restored started jobs. Original job/target ownership must survive each callback boundary, with no later awards or cooldown after identity loss and no cleanup against a replacement target. The current suite passes **124/124** cases. The full ritual outcome worker's game APIs and actual cancellation/animation/save flows still require the production build and game verification; the host tests its shared continuation gate rather than simulating the entire outcome worker.

The Training Officer Stage 3 assertions require a completed daily scene to notify the trainer's provided-training reward once. A repeated payout callback cannot notify it again; TrainerIdentity covers that reward's eligibility, while CombatantSpecialization covers the shared receiver formula.

The eight daily specialization cases run the production daily driver to verify one
score shared by the formal outcome, body experience and common base award; repeated
or reentrant callbacks; partial-outcome exceptions; reconstruction of completed
and unfinished jobs through their actual ExposeData keys; interruptions; separate
jobs; and exclusion of ritual phases. Outcome calculations and the base award
are observable boundary stubs here; the formula and direction rules run in the
CombatantSpecialization suite. The Scribe dictionary model is not a real game save.

2026-10-15 第五步新增日常、仪式、人格排泄的强制动作失败及解析回调换任务六项边界回归。此处只控制解析返回，真实工具的旧部位清理及 Owner 校验由 [ForcedSexProps](../ForcedSexProps/README.md) 直接编译生产源码验证。

本批最终 RitualLifecycle 178/178，完整 30 套／2110 项通过；最终汇总 `.builds/pna-effects-step5/validation-final/summary.json`，仍待游戏验收。

仪式修正后 179/179：原生解析不到部位也能完成固定六阶段；真正定义初始化失败、外部回调替换及原经验保护保持。 日志位于 `.builds/pna-effects-step5-ritual-fix/`。

2026-10-15：维护者已确认第五步仪式及性交增加成瘾均正常，批准提交；本次只补实机验收记录，不重复运行回归。

## 欲望侵染第一批通用开关回归（2026-10-15）

本宿主已链接真实通用开关／人格凭据代码，新增本领域的状态闭环回归；范围、实际本批结果与实机边界见[第一批记录](../../Docs/Development/Completed/欲望侵染第一批通用开关框架实现与测试.md)。历史计数保留原批次语境。

## 人格排泄空解析回归（2026-10-22）

人格排泄沿用固定场景，不把交互部位匹配追加为工作准入。边界替身现区分空解析与定义／结果非法，新增用例直接执行生产接收、开始、收尾及提取；修复前 208／209，修复后 209／209，未增加失败冷却且能完成提取。既有非法动作拒绝、日常严格解析、解析回调换任务及占用清理继续执行。真实工具的空结果、旧部位清理及归属验证由 ForcedSexProps 28／28 补充；不宣称模型验证了真实女性角色的 RJW 解剖搜索，见[本批记录](../../Docs/Development/Completed/人格排泄固定动作空解析修复与测试.md)。

2026-10-28受方倒地修复：新增Core1项，实际 **233／233**（原232＋1）。生产阶段驱动在受方持续Downed期间完成六阶段，后继阶段重建接收器，末阶段只发送一次完成memo并清理占用。场景计时、结算和收尾执行生产代码；引擎任务安装及Lord事件为既有模型边界，原生健康／RJW条件／角色保留由 [TrainingDownedNative](../TrainingDownedNative/README.md) 验证。该结果不等于完整实机仪式验收。

2026-10-28：维护者确认倒地修复“实机测试没有问题”，整体验收关闭并按授权提交，见[验收记录](../../Docs/Development/Completed/受调教者倒地不中断调教修复与验证.md#实机验收关闭2026-10-28)。本轮未重跑自动回归；上述自动结果和隔离边界保留原时点，不补记逐项第三方组合通过。
