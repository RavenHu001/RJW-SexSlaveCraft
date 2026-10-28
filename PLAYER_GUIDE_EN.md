# RJW-SexSlaveCraft Complete Player Guide

[2.4.7: new controls and compatibility](#ssc-247)

**2.4.7 — development registration, 2026-10-28; unreleased.** Sensitivity, personal PNA production controls, UAP compatibility and bond/Training fixes target 1.7; 1.6/Common stay frozen at 2.4.1. The latest public download remains [v2.4.6](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.4.6). About/binary metadata remain 2.4.6 pending a release rebuild. Three-language romance display retains its independent verification scope; PNA production guidance has been accepted.

[2.4.6: stages, orders, moods and personal resets](#27-246-personal-abilities-and-romance-controls-17-unreleased)

[New in 2.4.4: culture, ritual points and cosmetics](#25-244-culture-rituals-and-cosmetic-reshaping-17-development-only)

> This guide covers **SSC 2.4.7 (development registration, unreleased)** for RimWorld 1.7; 1.6 remains frozen at 2.4.1. See the [release scope](Docs/Releases/2.4.7/2.4.7发布说明.md#english). Earlier section versions identify introductions.

[Project home](README.md) · [Player reading order](Docs/Players/README.md#english-reading-order) · [Quick start](QUICK_START_EN.md) · [中文机制详解](机制详解.md)

**Upgrade note:** 1.7 removes the old Basic PNA Launcher, its projectile, recipe and dedicated research. Saves referencing those definitions have no migration path. Replace the old mod folder completely when upgrading.

## 1. Scope and Dependencies

SexSlaveCraft, abbreviated SSC below, is built around this progression loop:

1. Set a humanlike pawn’s `Pawn Identity` to `Master` or `Sex Slave`.
2. Use ordinary `Training` to raise Corruption, opinion, and body-part experience while reducing Will.
3. Use the `Binding Ritual` to establish the master–slave bond, deepen the `Sex Slave Chain` health stage, and attempt slave conversion.
4. Choose one of six specializations: `Public Use`, `Cow`, `Training Officer`, `Pet Cat`, `Pet Dog` or `Combatant`.
5. Use `Personality Excretion` to store a personality in `Personality Gel`, implant it into a Hollow, or edit it into a final specialization.
6. Use `Semi-gelatinization Surgery` and `Full Gelatinization Surgery` for high-risk body transformation.

Hard dependencies:

- Harmony
- RimJobWorld (RJW)

Binding Rituals and their stage-progression route require the Ideology ritual system. Royalty is optional; when installed, basic Training research requires a techprint supplied by a Seed of Desire or the separate vanilla Training techprint. Rimworld Animations, RJW Onahole, Human Cattle, Equal Milking, RJW-PE, Humanoid Alien Races, and Sized Apparel are soft integrations rather than hard dependencies.

At startup, SSC injects its Training component, Training tab, and related surgeries into all Humanlike races. `Ninetailfox` and `Ninetailfoxwt` are also explicitly whitelisted.

## 2. Recommended Progression

### 2.1 Minimum Playable Route

1. Research `Training` for 500 research points. With Royalty, supply any missing techprint first; direct Master selection does not bypass this research requirement.
2. Set at least one free colonist to Master and enable `Training` work. If direct Master selection is disabled, acquire permanent qualification first. A Sex Slave can later qualify through Training Officer specialization.
3. Select a target and open the `Training` tab.
4. Set `Pawn Identity` to `Sex Slave` and enable `Allow Training`.
5. Choose an `Assigned Pose`; set an `Assigned Trainer` if a specific pawn must perform it.
6. Let the trainer work automatically or right-click the target to force the job.
7. Research `Body-part Training` to begin gaining body-part experience.
8. Add the `Binding Ritual` to an ideoligion and use it to advance the Chain health stage.

### 2.2 Mid- and Late-game Routes

- Master route: acquire qualification when required → select Master → personally complete Training and Rituals → view Symbiosis of Desire growth and damage sharing in the Master panel.
- Pet route: complete Cat/Dog research → ordinary training → gel processing and implantation → Gentle Comfort or Animal Training.
- Combatant route: ordinary Training and direct kills → gel finalization → Combat Overdrive.
- General route: `Training` → `Body-part Training` → `Personality Excretion` → semi/full gelatinization.
- Social and trade route: `Personality Editing` → `Public Use` → `Public Use Specialization` → extract at 100% → edit into `Final Public Use Specialization` gel → implant.
- Milk route: raise Breasts to 70% → gain `Permanent Lactation Phase` → research `Milking Specialization` → `Cow Specialization` → edit into `Final Cow Specialization` gel.
- Training Officer route: research `Training Officer Specialization` → maintain a valid Master bond and Chain stage 3 as a free colonist Sex Slave → select Training Officer → reach 20% and turn on `Is a trainer` → complete training and shape final personality gel.

## 3. The Training Tab

The tab is available for colonists, colony prisoners, and slaves.

### 3.1 Pawn Identity

| Identity | Function |
|---|---|
| Unset | Hides normal Training configuration |
| Sex Slave | Enables Training, specialization, pose, and trainer controls; may turn on trainer duty after qualifying as a Training Officer |
| Master | Cannot be an ordinary Training target; can fill the master role in a Binding Ritual |

Masters always qualify as trainers, and Unset pawns never do. A Sex Slave must finish `Training Officer Specialization` research, remain a free colonist with a valid Master bond and current Chain stage 3 or higher, and reach 20% progress in the selected Training Officer direction before turning on `Is a trainer`. An active Final Training Officer also qualifies after switching directions. Losing a continuous condition suspends trainer duty while preserving the personal on/off choice. Training work priority and the target's `Allow Training` setting are separate. A qualifying Sex Slave can train others but does not gain the Master ritual role.

Pawns with a Chain, and Masters whose Bridle still has valid bound targets, cannot change SSC identity. The UI and shared setter both reject the change, preserving bond and growth progress. Explicit unbinding restores switching when no blocking bond remains; the Sex Slave trainer toggle remains usable. This does not reconstruct Chains already lost before the fix.

**Unified receiver identity in 2.3.9.** Automatic Training, forced Training, Binding Rituals and new trainer assignments require the receiver's SSC role to be `Sex Slave`. Vanilla colonist, prisoner or slave status, Corruption, the Sex Slave trait and historical enable/assignment settings cannot replace this role. Receiver identity, trainer qualification, ownership, permissions and physical eligibility remain separate checks. Disabling behavior restrictions, using the bound Master or forcing a command does not waive the receiver role. Set Sex Slave identity first, then assign an SSC Master for first bonding; an unbound Sex Slave without a Chain can still establish the first bond. Self-training shares the identity rule and retains its Chain and independent permission requirements.

Candidate hints, selection, startup, ongoing execution and outcomes recheck receiver identity. Changing a ritual protagonist's SSC identity during preparation cancels the matching ritual. Losing the receiver role stops the task and releases only its own preparation; unfinished Training rewards are not granted. Daily research, enable settings, cooldowns, timetables and class avoidance retain their existing scope. Rituals do not inherit the daily enable switch or cooldown.

**Historical saves in 2.3.9.** Loading does not infer or rewrite SSC identity from enable settings, assignments, Corruption or Chains, and does not erase growth. Historical settings on an Unset pawn do not grant receiver eligibility; ineligible old tasks stop even if the scene had started. An Unset pawn without a Chain can choose Sex Slave normally. If an Unset pawn retains a valid historical Chain, the Training tab offers `Restore historical sex slave role` with confirmation. This restores only the role, preserving the original Chain, owner, Training mode, assignment, Corruption and specialization progress. It does not rebind the pawn or grant trainer qualification. Dead pawns, missing or damaged relationships and conflicting valid Master-side bonds have no recovery option. Recovery does not resume an interrupted old task.

On upgrade, the old Sex Slave trainer toggle is turned off once, including for previously assigned trainers. Assignment records remain but are inactive until the pawn qualifies and the player turns the toggle on again. Unset trainers are not promoted to Master. Later loads preserve the new choice.

**Training Officer duty and trust (introduced in 2.3.9, retained in 2.4.0).**

| Effect | Recipient and value | Type and expiry |
| --- | --- | --- |
| Completed duty | Active Officer: +3 mood for one day | Timed memory; shared across receivers, refreshed by later completions, and expires naturally after appointment or eligibility ends |
| Master's appraisal | Actual Master's opinion of their bound, active Officer: +5 | Conditional social thought; disappears when the bond or appointment conditions fail |
| Reverse trust | Officer's opinion of the actual Master: +5 | Conditional social thought; disappears when the bond or appointment conditions fail |
| Steady trust mood | Officer: +3 mood | Conditional mood thought; disappears when the bond or appointment conditions fail |

Duty memory requires successful daily Training or a whole Binding Ritual. Ordinary completion and an active final Officer training another direction still qualify when experience gained is zero. Cancellation, failure, individual ritual stages, self-training and receiving Training grant no duty memory.

The last three effects are **conditional thoughts, not memories**: they have no memory duration, capacity or accumulating instances and are not stored in personality gel. Disabling appointment, losing continuing eligibility or unbinding removes them; restoring conditions restores them. Temporary work suspension, downing or travel does not remove them. Social display follows the vanilla query cache of about 100 ticks. Daily receiver memories use a separate weakened set; see section 5.5. Implementation and acceptance records: [duty feedback](Docs/Development/Completed/训导官职责反馈实现与测试记录.md) and [reverse trust](Docs/Development/Completed/训导官任职信任实现与测试记录.md).

### 3.1.1 Permanent Master qualification (2.4.0)

Master qualification and progression are included in 2.4.0. Historical 2.3.9 packages do not contain these additions.

| State | Ownership and purpose |
| --- | --- |
| Permanent Master qualification | A personal personality record that permits Master selection when direct manual selection is disabled. Leaving Master identity preserves it. |
| Current SSC identity | The current Unset / Sex Slave / Master role. Qualification does not automatically change it; manually choosing Master does not grant permanent qualification. |
| Training research and techprints | Global records belonging to the current save. They control research admission and unlocked content, do not transfer with a personality and do not replace personal qualification. |

The pawn's identity section always shows `Master qualification: acquired / not acquired` separately from the current role. Its tooltip also explains whether direct manual selection is enabled. The personality gel card shows the record stored in that gel; a Chain owner, Sex Slave trait or qualified host cannot replace it. Both displays are read-only and neither grants qualification nor triggers migration.

**Seed use.** An eligible Humanlike pawn who is not an SSC Sex Slave and has not acquired qualification can use a `Seed of Desire`. The normal use takes about ten seconds and success consumes one seed. Qualified pawns and SSC Sex Slaves are refused; cancellation or failing the final checks consumes nothing. Use grants permanent qualification without changing identity or removing bond restrictions. Vanilla slave status is separate from SSC Sex Slave identity.

**Research.** With Royalty, `Training` requires one vanilla techprint. The seed supplies only the missing techprint count, adding neither research progress nor Intellectual experience and never completing research directly. The separate vanilla Training techprint remains an independent research source, grants no personal qualification and retains its vanilla effects. Without Royalty there is no added techprint gate, while the seed still grants personal qualification. Vanilla loading compatibility supplies newly required techprints for old projects that are complete or have any research progress; projects at zero progress retain the gate. Existing progress is not reset.

New settings and missing configuration fields default to off. Explicitly saved on/off choices and existing Master identities are preserved.

**Manual selection.** Direct Master selection is disabled by default; enabling it bypasses only the personal qualification requirement. Turning it off prevents unqualified pawns from newly selecting Master, without removing an existing identity or qualification. It supplies no research techprints. Pawns holding a Chain and Masters with valid bound targets remain subject to existing identity locks.

**Personality transfer.** Extraction, saving, copying or processing gel and implantation preserve the record. Implantation replaces the host's previous qualification with the source personality's value, including replacing acquired with not acquired. Extracting a personality clears its qualification from the empty original body. Clones are independent personalities and do not inherit source qualification. On first upgrade, old Masters and old Master gels receive qualification once. Later loads do not infer qualification from Master identity, so new unqualified clones remain unqualified.

**Initial free quest.** After loading, the automatic check runs once: Training must be unfinished and the player must have no living, undestroyed, non-Hollow current SSC Master. Maps, world pawns, caravans and transport are covered. Permanent qualification does not substitute for the current role; partly researched Training is still unfinished. The save records successful quest creation, preventing another automatic offer after reloading or ending the quest. It offers one seed, has no acceptance deadline and delivers by drop pod after acceptance. Delivery waits if there is temporarily no suitable home map.

**Later sources and settings.** Seeds enter standard quest rewards and ancient ruin loot pools using artifact references; they remain obtainable after research is complete. With Ideology, enhanced rooms and ancient security crates are also included. Seeds are rare, and group weights are not per-item probabilities. Caravans, settlements and orbital traders do not stock them for purchase; players may sell existing seeds. No crafting recipe is provided. Settings can spawn one seed directly on the current map or trigger the initial free quest. The quest button bypasses research, existing-Master and past-grant conditions but cannot duplicate another unfinished claim quest. Neither button grants qualification directly; missing maps or other unavailable conditions disable buttons with an explanation.

### 3.1.2 Master progression and practice (2.4.0)

Becoming a Master creates a four-stage progression record. Personally completing daily training grants **1 experience**; a complete binding ritual grants **3**. Interruptions grant none, and Trainer Officer work gives no proxy experience to its owner. Fully trained targets still provide practice. View experience in the Master panel; the health tooltip shows descriptions and bonuses. The 2.4.0 thresholds are 0/12/40/100 total experience. Growth transfers with personality gel. Leaving the Master role preserves experience and suspends bonuses while hiding the entry from the normal health tab. Returning to the role displays the original stage.

The health entry is named “Symbiosis of Desire”, with Budding, Awakening, Command and Ascendance stages. Hover for the stage text and bonuses. The Master panel names the next stage. Crossing a threshold through practice or debug experience produces a message targeting the Master; loading, personality insertion and returning to the Master role do not report level-ups.

Bonuses apply only while the current SSC identity is Master. The five bonuses are Consciousness, Sight, Hearing, Social Impact and Injury Healing. At the highest stage they are +40/+30/+30 percentage points, ×1.20 and ×1.50. Injury Healing accelerates both natural and tended healing; progression no longer directly adds Manipulation or stacks a natural-healing factor. Seed use does not create or reset progression. Clones inherit neither the Master role nor growth; gel implantation replaces the host record exactly.

| Symbiosis of Desire stage | Total XP | Consciousness | Sight | Hearing | Social Impact | Injury Healing |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Budding | 0 | +10 pp | +5 pp | +5 pp | ×1.05 | ×1.10 |
| Awakening | 12 | +20 pp | +10 pp | +10 pp | ×1.10 | ×1.20 |
| Command | 40 | +30 pp | +20 pp | +20 pp | ×1.15 | ×1.35 |
| Ascendance | 100 | +40 pp | +30 pp | +30 pp | ×1.20 | ×1.50 |

These are each stage's total 2.4.0 bonuses, not additions stacked on earlier stages. Thresholds and values come from `SSC_Hediff_MasterProgression`. Capacity offsets use percentage points (pp); stat factors are multipliers. Resonance and other sources still contribute separately to the final pawn attributes.

Rewards are claimed once per completed session; previously settled jobs receive no historical experience. The current cap is 100. An authorized different Master earns its own practice, while a Trainer Officer in the ritual host role grants no proxy experience. The +10 debug command requires developer mode and SSC debug Gizmos and never creates a missing growth record. See the [batch 2 test guide (Chinese)](Docs/Development/Completed/主人阶段第二批测试说明.md).

The Master training tab shows stage, total experience and progress toward the next stage. Hover over the bonus summary for values. Damage sharing is configured here and remains saved in the bridle: without a bridle the toggle is disabled, but a temporary lack of eligible recipients does not prevent changing the preference. The former damage-sharing Gizmo is removed. Thresholds and the cap come from the Def; the highest stage shows a full bar. Viewing a missing record never creates one. The maintainer confirmed the batch 3 panel and toggle work as expected on 2026-10-13; see the [test guide (Chinese)](Docs/Development/Completed/主人阶段第三批测试说明.md).

### 3.2 Allow Training

The Training WorkGiver scans a pawn only when `Allow Training` is enabled. 2.3.9 also requires SSC Sex Slave identity; a saved enable setting cannot replace it.

Enabling it immediately runs an RJW receiver-eligibility check. A failed check does not turn the option back off, but eligibility is checked again before the job starts.

### 3.3 Behavior Restrictions

Seven individual permissions replace Allow Others: the original six behavior rules plus independent self-training permission, introduced in 2.3.5 and retained in 2.4.0; see section 18.6. Public Use affects two reception defaults/overrides. An effective Training Officer forces permission for initiating consensual or forced sex as soon as the direction is selected; the 20% threshold governs trainer duty only. An active final record continues to apply after switching directions, while an invalid record is suspended. Global controls and source priority still apply. Specialization changes do not rewrite saved personal choices.

### 3.4 Assigned Pose

Available choices:

- Automatic
- Vaginal
- Anal
- Oral
- Boobjob
- Handjob
- Footjob
- Fingering
- Mutual masturbation
- Fisting
- Rimming
- 69

`Automatic` lets RJW choose. A manual choice overwrites the current RJW `SexProps` and interaction.

### 3.5 Assigned Trainer

2.3.9 accepts new assignments only for SSC Sex Slaves. The candidate list, confirmation and direct assignment entry recheck the receiver role. An unbound pawn's historical assignment can still be cleared; existing owner-only rules continue to govern clearing a bound pawn's assignment.

A candidate trainer must be:

- a free colonist;
- an active trainer, and not the target itself;
- alive, not destroyed and not downed;
- assigned to the `Training` work type.

If non-owner Training is denied for a bound target, assignment is coordinated to the owner. Selecting an eligible non-owner trainer also grants Training permission unless a forced rule denies it. Ordinary interactions keep their own checks.

Menus and direct assignments share eligibility and authorization checks. Automatic Training goes only to the active assigned trainer; the owner retains manual and ritual access. Disabling trainer status preserves the assignment as inactive; other trainers do not automatically take over. Temporary work or health restrictions do not erase the shared-bed relationship. Started scenes finish; preparation is checked again.

### Bond Overview (2.4.3, 1.7 only)

Select a Master and open Training to see member counts and portraits; the adjacent command selects available, valid bound members on the current map. A slave's Master portrait locates and selects the Master. The magnifying glass opens the compact overview, the trainer pencil changes the assignment, and hover tips show names and details. Normal viewing reads existing relationships without automatic global scans or repairs. See the [completion record](Docs/Development/Completed/主从关系概览完成核对.md).

## 4. Ordinary Training

### 4.1 Target Conditions

Ordinary Training requires all of the following:

- `Training` research is complete;
- the target is Humanlike;
- trainer and target are different pawns;
- the target is alive;
- the target is a colony prisoner, colonist, or slave;
- the target passes the selected SSC/RJW receiver check;
- `Allow Training` is enabled;
- the target is not in a Binding Ritual, Personality Excretion scene, or another Training scene;
- the target is not on Training cooldown;
- trainer assignment and Chain ownership rules are satisfied;
- both pawns can reserve and reach the required targets.

After target validation fails, the WorkGiver waits 300 ticks before retrying that target.

### 4.2 Cooldown

A completed ordinary Training session applies a `22,500 tick` cooldown, or about nine in-game hours.

The Binding Ritual does not use this cooldown.

### 4.3 Resolution Order

After a successful scene:

1. RJW resolves the sex act normally.
2. SSC calculates the Training score.
3. Corruption increases.
4. If Public Use is active, its ordinary Hediff severity may also increase.
5. Will is reduced for eligible prisoners or colonists.
6. `Sex Slave Bridle` and `Sex Slave Chain` stage logic runs.
7. Mood and opinion memories are applied.
8. The actual act grants experience to its mapped body part.
9. The 22,500-tick cooldown begins.

### 4.4 Self-training

An SSC Sex Slave with a `Sex Slave Chain` may enable `Allow Self-training` independently of ordinary masturbation. It defaults to on for new pawns and missing fields in old saves; an explicit off choice persists. Select and right-click the pawn to choose an available solo interaction. Automatic self-training branches only from an RJW automatic masturbation candidate. With both permissions available, the four current Chain stages select self-training at 30% / 55% / 80% / 50%; with only self-training available, the candidate becomes self-training. Genes that forbid masturbation still block this first version.

At scene start, `C` is current Corruption clamped to 0–1, and `A` is opinion of the imagined pawn divided by 100 and clamped to 0–1. The actual bound Master takes priority over a valid Assigned Trainer. With no valid pawn or nonpositive opinion, `A = 0`.

```text
S = 10 + 20 × C + 10 × A
G = 0.5 × Clamp(S / 500 + 0.025, 0.01, 0.12)
```

The score ranges from 10 to 40. Before the current Chain cap, `G` is 2.25–5.25 percentage points. Old ordinary Training scoring, Social skill, Masochist and body-part sizes do not affect this formula. The completion message follows ordinary Training's `Score | Corruption` format and identifies the pawn.

Completion gives the pawn one dedicated mood memory lasting one day, with at most one copy. Its mood combines a stage base of −4 / −2 / 0 / +2, +2 for Masochist, and the imagined pawn's original opinion at scene start: −3 for ≤−30, +2 for ≥30, otherwise 0. Denying ordinary masturbation also produces one of two mutually exclusive ongoing moods depending on whether self-training is allowed; stage and Masochist affect them. Self-training neither creates a bond nor updates ordinary Training cooldown, scheduled completion date or last score.

## 5. Current Training Score

### 5.1 Base Score

```text
S =
    2.5 × trainer Social skill
  + target opinion of trainer / 40
  + vanilla-slave bonus
  + size score
```

The vanilla-slave bonus is `+3`; otherwise it is `0`.

### 5.2 Size Score

Girth difference:

| Difference | Score |
|---:|---:|
| ≥ 3 | +10 |
| ≥ 1 | +5 |
| ≥ -0.5 | +1 |
| ≥ -1.5 | 0 |
| ≥ -3 | -3 |
| < -3 | -5 |

Length difference:

| Difference | Score |
|---:|---:|
| ≥ 5 | +10 |
| ≥ 0 | +5 |
| ≥ -2 | 0 |
| < -2 | -5 |

The final size score is derived from these comparisons for the current interaction.

### 5.3 Corruption Gain

```text
raw gain = S / 500 + 0.025
session gain = clamp(raw gain, 0.01, 0.12)
```

The result is then limited by the current Chain stage’s Corruption brake cap. A good score can push Corruption up to the next Chain threshold, but it cannot skip the required Binding Ritual resolution.

### 5.4 Opinion and Mood

```text
opinion change =
    round(
        0.4 × trainer Social
      + 0.3 × size score
      + S / 8
    )
```

The opinion memory lasts 15 days and stacks up to 40 times for the same trainer. Its one-day mood echo stacks up to three times:

| Opinion change | Mood echo |
|---:|---:|
| < 0 | -10 |
| 0–5 | +2 |
| 6–10 | +8 |
| > 10 | +15 |

## 6. Corruption, Decay, and the Chain

### 6.1 Display Stages

| Corruption | Display stage |
|---:|---|
| < 5% | Stable |
| 5%–24.99% | Mild |
| 25%–49.99% | Moderate |
| 50%–74.99% | Severe |
| 75%–94.99% | Deep |
| ≥ 95% | Extreme |

### 6.2 Daily Decay

Under the current score system, base daily decay `B` is configurable and defaults to `2%`:

```text
D = B
  × sex-need factor
  × last-score factor
  × opinion factor
  × Chain factor
  × body-part factor
  × apparel/health modifiers
```

Disabling `Enable Corruption decay` stops natural loss while leaving apparel/health floors and the current Chain brake cap active. Setting the slider to `0%` has the same effect on natural loss.

```text
sex-need factor =
    clamp(1 - 1.5 × (Sex need - 0.8)², 0.2, 1.0)

last-score factor =
    clamp(1 - last score / 200, 0.3, 1.0)

opinion factor =
    clamp(1 - opinion of bonded master / 200, 0.3, 1.0)
```

Chain factor:

| Chain severity | Decay factor |
|---:|---:|
| No Chain or < 10% | 0.1 |
| 10%–29.99% | 0.1 |
| 30%–49.99% | 0.3 |
| 50%–89.99% | 0.5 |
| ≥ 90% | 0.8 |

Each body-part track reduces decay:

| One track’s experience | Reduction |
|---:|---:|
| < 30% | 0.5% |
| 30%–69.99% | 3% |
| 70%–99.99% | 4.5% |
| ≥ 100% | 7.5% |

```text
body-part factor = max(1 - total reduction across six tracks, 0.1)
```

### 6.3 Sex Slave Bridle and Sex Slave Chain

At 10% Corruption:

- the master receives `Sex Slave Bridle`, which tracks all bonded targets;
- the target receives `Sex Slave Chain`, which records one master;
- the master begins receiving `Libidinal Resonance Field`;
- ownership, rebellion prevention, bed sharing, and sex-protection rules become active.

The initial Chain severity is `20%`.

### 6.4 Chain Brake Caps and Experience Multipliers

| Chain severity | Stage | Corruption brake cap | Body-part XP |
|---:|---|---:|---:|
| No Chain | — | 12% | ×1.0 |
| 10%–29.99% | Novice Sex Slave | 30% | ×1.1 |
| 30%–49.99% | Sex Slave | 50% | ×1.3 |
| 50%–89.99% | Submissive Sex Slave | 90% | ×1.6 |
| ≥ 90% | Meat Toiletization | 100% | ×2.0 |

The brake cap is the next Corruption threshold this Chain stage is allowed to reach. After reaching 30%/50%/90%, the pawn still needs a Binding Ritual resolution to raise Chain severity. Gameplay checks use the Chain health stage; the Sex Slave trait is synchronized only from highest-ever Corruption and is not an authoritative gameplay predicate.

Minimum-Corruption effects from apparel or health only activate when this pawn’s historical maximum has already reached that floor, and only if that floor is inside the current Chain-stage interval. They preserve milestones already reached in the current interval; they do not let a low Chain stage jump directly to a later one.

Chain stage stat effects:

| Stage | Capacities | Market value |
|---|---|---:|
| Novice Sex Slave | Consciousness, Talking, Manipulation each -0.3 | ×0.9 |
| Sex Slave | Consciousness -0.2, Talking -0.2, Manipulation -0.1 | ×0.8 |
| Submissive Sex Slave | Consciousness +0.1, Talking +0.1 | ×0.7 |
| Meat Toiletization | Consciousness +0.3, Talking +0.3, Manipulation +0.2 | ×0.5 |

Every Chain stage grants immunity to PNA addiction.

Persistent mood by stage:

| Stage | Mood |
|---|---:|
| Novice Sex Slave | -10 |
| Sex Slave | -5 |
| Submissive Sex Slave | +3 |
| Meat Toiletization | +12 |

### 6.5 Chain Regression

When natural decay is enabled and positive, earned equipment/health-effect floors apply before checking regression. If Corruption is still below the current Chain stage's floor, the Chain regresses by one actual stage:

- 90% stage → 50%;
- 50% stage → 30%;
- 30% stage → 10%;
- 10% stage → 0.

Corruption is then clamped to the new stage’s cap.

### 6.6 Ordinary Orgasms

A pawn whose `Sex Slave Chain` health condition is in any active stage gains an additional `0.3%` Corruption whenever RJW triggers an orgasm.

## 7. Will Reduction and Slave Conversion

For a colony prisoner or colonist who is not already a vanilla slave, Training reduces Will according to that session’s Corruption gain.

### 7.1 Ordinary Will Reduction

Let current Will be `W`:

| Session Corruption gain | Will reduction |
|---:|---:|
| < 4% | `0.2 + 0.10W` |
| 4%–5.99% | `0.4 + 0.125W` |
| 6%–7.99% | `0.6 + 0.20W` |
| ≥ 8% | `0.8 + 0.25W` |

Binding Ritual stages multiply this result. A complete six-act ritual normally resolves with a `×1.25` stage multiplier.

### 7.2 Ritual Conversion Chance

Conversion is attempted only when:

- Corruption is at least 20%;
- the target is not already a vanilla slave;
- the target is a colony prisoner or colonist;
- `Allow ritual conversion into slavery` is enabled.

```text
score term = clamp(ritual score / 75, 0, 1)
corruption term = clamp((Corruption - 0.2) / 0.8, 0, 1)
Will used = clamp(current Will, 15, 80)
Will-break term = 1 - Will used / 80

conversion chance =
    0.40 × score term
  + 0.30 × Will-break term
  + 0.30 × corruption term
```

Races with a natural base Will of 15 or less have a ritual conversion cap of 90%.

## 8. Binding Ritual

### 8.1 Requirements

- The ideoligion contains `Binding Ritual`.
- The host is a free colonist of the player faction.
- The host has a valid trainer identity: an SSC Master or an eligible SSC Sex Slave trainer.
- The target is a colonist, colony slave, or colony prisoner.
- The target is alive, not downed, and passes RJW receiver eligibility.
- The target has SSC Sex Slave identity (the 2.3.9 unified requirement).
- For first bonding, the host must be an SSC Master and the target's active assigned trainer.
- For an existing bond, the actual Master may host without being excluded by another assigned trainer. A third-party host must be the active assigned trainer and pass the applicable Training permission checks.
- Both pawns can reach the ritual location.

The setup window uses gold whip icons for host candidates and purple collar icons for sex slave candidates. Collar icons additionally require SSC Sex Slave identity; vanilla colonist, prisoner or slave status alone does not qualify. Eligible Sex Slave trainers can show both icons. A whip indicates host/trainer duty and does not establish ownership. A slash indicates the role is currently unavailable; a check indicates assignment. Hover or drag to inspect the proposed pair and its full requirements.

These icons provide candidate information and do not replace complete admission checks. 2.3.8 tightened collar candidate hints without unifying backend admission. 2.3.9 requires SSC Sex Slave identity during actual selection, startup, execution and outcomes, while retaining role, pairing, physical, permission and reachability checks. Pawns without a candidate icon may still be spectators.

### 8.2 Fixed Six-act Sequence

The ritual runs:

1. Handjob
2. Footjob
3. 69
4. Boobjob
5. Anal
6. Vaginal

SSC gives the final ritual payout only after all six stages finish. An interrupted ritual does not receive the full SSC resolution.

Version 2.2.10 ties the training lock and outcome eligibility to the whole ritual. Cancellation or departure of the Master
or target clears its temporary state; an ordinary spectator leaving does not release that lock. An interrupted phase job
can retry while its ritual remains active. Phase changes and save/load preserve progress in that ritual, while a new ritual
starts at phase one. Each ritual can claim its final outcome only once.

Runtime recovery also clears stale flags left by ended rituals in older saves. This preserves the assigned trainer,
long-term progression, and existing daily cooldown. Daily training still requires its usual eligibility and schedule checks.

### 8.3 Quality

Quality is the sum of the following terms, clamped to 0–100%:

| Component | Curve |
|---|---|
| Spectators | 0 → +0%; 1 → +5%; 5 → +15%; 10 or more → +20% |
| Room impressiveness | 0 → 0%; 50 → +5%; 120 → +10% |
| Genital compatibility | score 0/1/2/3/4/5 → -20%/-10%/-5%/+5%/+15%/+30% |
| Master Social skill | 0/5/10/15/20 → -20%/0%/+20%/+30%/+40% |
| Repeated within three days | -20% |

The spectator bonus interpolates linearly between these points: three give +10%, and four give +12.5%. The master and ritual target are excluded. The setup preview estimates the assigned audience; the final outcome requires attendance for at least half the ritual's actual elapsed time. Brief visits do not count, while spectators who leave after meeting the threshold retain their contribution. Result letters also show the factor when no spectators qualify.

Version 2.3.8 includes the actual-time correction for full attendance being counted as zero, as well as protagonist exclusion in the preview and final outcome. The maintainer confirmed the spectator fix works in game. Historical 2.3.7 packages do not contain it; see the [development record (Chinese)](Docs/Development/Completed/绑定仪式观众助兴修复.md).

Ritual genital compatibility is not fully connected and stays at its lowest bracket. Ordinary Training size scoring is unaffected.

### 8.4 Outcome Weights

Base outcome weights are:

- Bad: 0.1
- Average: 0.5
- Successful: 0.3
- Perfect: 0.1

For ritual quality `Q`:

```text
actual weights = 0.1, 0.5Q, 0.3Q, 0.1Q
```

The terrible result therefore occupies a larger share of the outcome pool at low quality.

### 8.5 Rewards

```text
ritual score = 75Q
ritual Corruption gain = 12% × Q
```

The ritual also:

- reduces Will;
- attempts slave conversion;
- gives all participants the matching ritual memory;
- gives the target a 30-day mood/opinion memory toward the master;
- grants experience to all six body-part tracks;
- advances the Chain health stage based on Corruption;
- creates the special Master/Perfect Sex Slave relation when the target’s opinion of the master is at least 80, setting mutual opinion to `+100`.

Participant memories:

| Outcome | Mood | Duration |
|---|---:|---:|
| Bad | -3 | 2 days |
| Average | 0 | 1 day |
| Successful | +3 | 3 days |
| Perfect | +4 | 6 days |

Target’s additional 30-day memory:

| Outcome | Mood | Opinion of master |
|---|---:|---:|
| Bad | -15 | -20 |
| Average | -5 | -5 |
| Successful | +5 | +10 |
| Perfect | +8 | +15 |

### 8.6 Ritual Body-part Experience

Each of the six tracks gains:

```text
0.5 × Q × Corruption-stage multiplier × Chain-stage multiplier
```

A high-quality Binding Ritual is therefore the fastest broad-spectrum development method.

### 8.7 Ritual Chain Growth and Historical Trait Sync

At ritual resolution, Corruption of at least 30% deepens the Chain:

- 30%–49.99% Corruption: Chain gains up to `20%`;
- at least 50% Corruption: Chain gains up to `30%`.

New Chain progress cannot exceed current Corruption at resolution. A Chain already above current Corruption retains its existing progress during ritual resolution. For example, an 80% Chain with 65% Corruption stays at 80% instead of jumping to 100%. Natural decay can still regress the Chain separately, one stage at a time.

The Sex Slave trait is no longer advanced directly by the ritual. It is synchronized automatically from highest-ever Corruption:

| Highest-ever Corruption | Trait degree | Official label |
|---:|---:|---|
| 10%–29.99% | 1 | Recently Trained |
| 30%–49.99% | 2 | Moderately Trained |
| 50%–89.99% | 3 | Deeply Trained |
| ≥ 90% | 4 | Perfect Sex Slave |

## 9. Body-part Training

Research `Body-part Training` first.

```text
body-part XP gain =
    Training score / 1000
  × extra multiplier
  × Corruption-stage multiplier
  × Chain-stage multiplier
```

Corruption-stage multiplier:

| Corruption | Multiplier |
|---:|---:|
| < 5% | ×1.0 |
| 5%–24.99% | ×1.3 |
| 25%–49.99% | ×1.5 |
| 50%–74.99% | ×1.8 |
| ≥ 75% | ×2.0 |

### 9.1 Act Mapping

| Act | Experience track |
|---|---|
| Handjob, mutual masturbation | Hands |
| Footjob | Feet |
| Vaginal, scissoring, fingering, fisting, 69 | Genitals |
| Anal, rimming | Anus |
| Oral | Mouth |
| Boobjob | Breasts |

### 9.2 Shared Thresholds

- 0%: developing
- 30%: trained
- 70%: sensitive or thoroughly transformed
- 100%: final corrupted state

### 9.3 Hands

| XP | Effects |
|---:|---|
| 30% | Manipulation +0.05; shooting accuracy +0.03; global work speed ×1.10; melee cooldown ×0.95; aiming delay ×0.95 |
| 70% | Manipulation +0.10; shooting accuracy +0.06; work speed ×1.15; melee cooldown ×0.90; aiming delay ×0.90 |
| 100% | Manipulation +0.20; shooting accuracy +0.10; work speed ×1.25; melee cooldown ×0.80; aiming delay ×0.80 |

### 9.4 Mouth

| XP | Effects |
|---:|---|
| 30% | Talking +0.10; social impact ×1.15 |
| 70% | Talking +0.20; social impact ×1.30; trade price +0.05; negotiation +0.10 |
| 100% | Talking +0.35; social impact ×1.50; beauty +1; conversion power +0.25; trade price +0.15; negotiation +0.25 |

### 9.5 Feet

| XP | Effects |
|---:|---|
| 30% | Moving +0.05; melee dodge +5; move speed +0.1; carrying capacity +5 |
| 70% | Moving +0.15; melee dodge +12; move speed +0.25; carrying capacity +15 |
| 100% | Moving +0.25; melee dodge +20; move speed +0.4; carrying capacity +30 |

### 9.6 Genitals

| XP | Effects |
|---:|---|
| 30% | pain-shock threshold +0.05; rest recovery ×1.10 |
| 70% | pain-shock threshold +0.15; beauty +1; rest recovery ×1.25 |
| 100% | beauty +2; pain-shock threshold +0.30; rest recovery ×1.50; begins producing `Purple Aphrodisiac Nanofluid` |

### 9.7 Breasts

| XP | Effects |
|---:|---|
| 30% | beauty +1 |
| 70% | beauty +2; animal tame and train chance each +0.10; gains `Permanent Lactation Phase` |
| 100% | beauty +3; animal tame and train chance each +0.20 |

### 9.8 Anus

| XP | Effects |
|---:|---|
| 30% | injury healing ×1.25; immunity gain ×1.05 |
| 70% | injury healing ×1.50; immunity gain ×1.15; pain-shock threshold +0.10 |
| 100% | injury healing ×2.00; immunity gain ×1.30; pain-shock threshold +0.20 |

## 10. PNA, Lactation, and Erotic Word

### 10.1 Purple Aphrodisiac Nanofluid Production

At 100% Genitals, the pawn gains PNA production:

- progress increases by 20% per day;
- production consumes about 0.15 nutrition per day;
- production stops while starving;
- a full cycle creates 10 `Purple Aphrodisiac Nanofluid`;
- on a map, the items drop nearby; in a caravan, they enter inventory.

Starting from 1%, one natural batch takes about 4.95 days.

### 10.2 Permanent Lactation Phase

At 70% Breasts:

- milk capacity: 0.125;
- fill time from empty: 15,000 ticks, about six in-game hours, with sufficient nutrition and no extra production or milking;
- additional nutrition cost while producing: 0.3 per day, or about 0.075 from empty to full;
- while hungry, gain is scaled to available nutrition;
- the state does not expire from lack of nursing;
- lactation can be disabled in the Training tab;
- fertility factor is 0.05, or 1 when Human Cattle controls the system.

Version 2.2.14 fixes slow base production caused by counting only the current update interval during batch processing. Production now accumulates at least 60 ticks and uses the entire accumulated interval for both milk and nutrition costs. Pending time is cleared while full, disabled, or controlled by Human Cattle; costs near capacity cover only the milk actually stored. Progress below one batch is saved. Existing saves retain their milk without backfilling past losses. These base rates apply to SSC-controlled production; Human Cattle uses its own production rules when it takes over.

### 10.3 Sex-driven Maturation

After an RJW sex act, if the pawn currently has Cow Specialization and lactation is enabled:

```text
base maturation B =
    clamp(score / 100 × 0.15, 0.05, 0.30)

final maturation =
    B × Cow-stage multiplier
```

Human–animal sex uses a fixed score of 50, so base maturation is `7.5%`.

| Cow progress | Multiplier |
|---:|---:|
| < 20% | ×0.60 |
| 20%–49.99% | ×1.00 |
| 50%–99.89% | ×1.30 |
| ≥ 99.9% | ×1.60 |

Vaginal, oral, and anal acts count as direct supply and do not add a host nutrition cost. Other acts consume nutrition in proportion to maturation; detected internal ejaculation data can lower that cost.

Maturation can advance:

- Purple Aphrodisiac Nanofluid production;
- the Permanent Lactation reservoir;
- the extra Cow reservoir;
- Cow Specialization progress.

Overflow milk tries to spawn `EM_HumanMilk`; if unavailable, it falls back to vanilla `Milk`.

### 10.4 Erotic Word

The ability is designed with these rules:

- colony prisoners only;
- range 2.9;
- warmup 2 seconds;
- cooldown 180,000 ticks, or three days;
- no effect on a target of the same ideoligion.

It multiplies the target’s certainty:

| Mouth stage | Remaining certainty | Reduction |
|---|---:|---:|
| 0%–29.99% | ×7/8 | 12.5% |
| 30%–69.99% | ×3/4 | 25% |
| 70%–99.99% | ×3/5 | 40% |
| 100% | ×1/2 | 50% |

At 1% certainty or less, the target converts to the caster’s ideoligion and receives 50% starting certainty.

The ability and grant component exist, but the current Mouth Hediff XML does not attach that grant component. It is therefore not normally acquired through gameplay.

<a id="11-pna-items-and-basic-pna-launcher"></a>

<a id="11-pna-items-and-basic-pna-launcher"></a>

## 11. PNA Items and Weapons

### 11.1 Ingestion

This section describes the accepted PNA redesign in the 2.4.1 release. Load L is raw severity divided by 2, ranging from 0 to 100%. The former +0.3/+1.0 infection doses, 25%/50% random addiction chances and linear daily clearance no longer describe this branch.

| Source | Load added | Existing legal PNA need refill | Base increase to existing addiction |
| --- | ---: | ---: | ---: |
| One normal PNA | +20 percentage points | Base +100 points, capped | D+0.10 |
| One PNA Plus | +70 percentage points | Base +100 points, capped | D+0.20 |

Both drugs are taken one unit at a time and provide no nutrition. Actual quantities and existing body-size/tolerance modifiers use the ingestion mechanism once.

Ordinary bodies use five fixed stages. Capacity changes are percentage points; each row is the complete stage value rather than an addition to earlier rows.

| Load | Sex drive | Consciousness | Manipulation / Moving, each | Sight / Hearing, each | Pain factor | Mood |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| Present, below 15% | ×1.50 | -1 | -2 | 0 | ×0.90 | +1 |
| 15% to <40% | ×2.00 | -3 | -5 | 0 | ×0.75 | +2 |
| 40% to <70% | ×2.50 | -6 | -10 | -5 | ×0.50 | +4 |
| 70% to <97% | ×3.25 | -12 | -20 | -15 | ×0.25 | +6 |
| 97% to 100% | ×4.00 | -20, capped at 10% | -30 | -30 | ×0 | +8 |

Pawns with an actual effective Chain use positive activation stages instead, without ordinary penalties, terminal collapse or ordinary PNA addiction:

| Load | Sex drive | Consciousness | Manipulation / Moving, each | Pain factor | Mood |
| --- | ---: | ---: | ---: | ---: | ---: |
| Present, below 15% | ×1.50 | +2 | +3 | ×0.95 | +2 |
| 15% to <40% | ×2.00 | +4 | +6 | ×0.90 | +4 |
| 40% to <70% | ×2.50 | +6 | +10 | ×0.80 | +6 |
| 70% to <97% | ×3.25 | +8 | +15 | ×0.70 | +8 |
| 97% to 100% | ×4.00 | +10 | +20 | ×0.60 | +10 |

Both bodies receive SexSatisfaction ×1.10/×1.20/×1.35/×1.50/×1.75 across the five stages. This increases satisfaction awarded, not orgasm frequency. The ordinary terminal stage retains collapse through its Consciousness cap; no periodic orgasm events or animations were added.

For L≤0.97, clearance is `0.03+0.20L` per day; above that, it is 0.25 per day. The load entry is removed at 0.5%. From full load without new input, leaving the terminal stage takes about 2.88 game hours, and complete clearance about 10.01 days. Ordinary and chained bodies share this curve.

Addiction and its need are separate:

- A positive input leaving an ordinary body at 100% load creates one addiction with D=1. Reaching the 97% terminal stage alone does not create it. Loading an old full-load record does not create addiction.
- Vanilla recovery reduces D by 0.10 per effective health day, without load or withdrawal gates. D=1 takes about ten days without further exposure. The bracketed health percentage means recovery already completed; stronger addiction lowers that displayed percentage.
- Non-withdrawal sex drive is ×1.25. Withdrawal gives ×1.75, Manipulation ×0.85, Vulnerability +0.30 and mood -18. These multiply with load, producing ×5/×7 sex drive at terminal ordinary load.
- The need falls by 0.333 per day; ≤1% means withdrawal. A newly created legal need starts full. Existing needs are not reset and illegal needs are not forced onto pawns.
- Real inputs ending at terminal load add D+0.20 to existing addiction: normal/PNA Plus total +0.30/+0.40. Initial addiction and debug inputs get no extra increase; D is capped at 1.
- Binding clears addiction and converts load at the same severity; unbinding converts it back without restoring addiction. Current Masters are immune to PNA drug effects. Becoming a Master clears existing drug effects while retaining bodily production; leaving does not restore them. This does not grant immunity to every weapon injury.
- PNA load, addiction, need level and production belong to the current body. They are not carried by personality transfer or inherited from SSC clone sources. Automatic ingestion keeps existing drug policies, without a PNA safety threshold or normal-drug preference.

### 11.2 PNA Concentration Technology

2.4.3 on 1.7 adds **Rough Concentration of Purple Aphrodisiac Nanofluid** at campfires and stoves: 10 normal PNA → 1 of the same PNA Plus, work 600, using Cooking. It requires PNA Concentration Technology; fuel, power and worktable speed follow the building. Modded worktables supporting vanilla simple meals or bulk simple meals receive this recipe after loading. On 2026-10-18, the maintainer confirmed that low-tech research and rough concentration work in game, closing acceptance for this batch. It is not included in the existing 2.4.2 release package. See the [implementation record](Docs/Development/Completed/低科技适配首批实现与测试.md).

At a drug lab:

- 5 normal PNA → 1 PNA Plus; work 450;
- 20 normal PNA → 4 PNA Plus; work 1,350.

Both recipes require `PNA Concentration Technology`.

### 11.3 Basic PNA Launcher (removed in 1.7)

2.4.3 on 1.7 removes the old launcher, its projectile, crafting recipe and dedicated research. Its specialized code and definitions are archived. Saves referencing these old definitions are not supported; no replacement or research compensation is provided. The frozen 1.6 version is unchanged.

The nine current PNA weapons retain their coated-wound or spacer delivery paths, shared resistance, dosage and feedback. Basic PNA Applications, PNA Concentration and shared body states remain available. See the [removal record](Docs/Development/Completed/SSC旧简易PNA发射器移除与归档.md).

### 11.4 Producer Sex Supply and Source Preference

A pawn with the normal `SSC_Purple_HyperLactation` production Hediff supplies its partner after a genuinely completed ordinary RJW paired act: +2 load points and +10 points to an existing legal PNA need. Existing addiction gains base D+0.01; terminal input adds another +0.20, totaling +0.21, capped at 1. Initial addiction still requires full load and receives no extra increase.

Positions, genital selection and condoms do not filter this supply. Two producers supply each other; multi-participant scenes count independently completed pairs, not orgasm count or animation lists. Solo acts, SSC daily training, binding rituals, personality excretion and vanilla Lovin are excluded. Master recipients remain immune; effective-Chain recipients receive activation. Load, addiction or Master identity alone does not make a source. Rabbit clone production is excluded. Supply adds no production-progress or nutrition cost; existing sex-related production logic remains.

For an addicted observer evaluating a normal producer or an enabled Master who can legally infest that observer (without stacking dual-source multipliers), RJW's independent attraction multiplier is ×1.25 outside withdrawal and ×1.50 in withdrawal, otherwise ×1. It applies to General, ForFucking and ForRape. Master and effective-Chain observers receive no addiction preference; existing permissions and selection rules remain.

Bestow Bliss is implemented and accepted in all four batches: the original +8% load/full existing-need refill (2.4.3 on 1.7 scales load from 3% to 100% by fluid output; accepted in-game on 2026-10-20), base +0.02 existing addiction or +0.22 after terminal input, additional Corruption, actual-part development and independent feedback. See [the next section](#115-master-desire-infestation). Personal automatic activation maintenance remains existing future work.

<a id="115-master-desire-infestation"></a>

### 11.5 Bestow Bliss

Combine sexual skill with actively controlled aphrodisiac nanomachines to bestow unparalleled bliss during intimacy, gradually increasing Corruption and reshaping the recipient's body to become more sensitive to sexual stimulation.

The first implementation is complete and accepted across all four batches; it remains part of version 2.4.1. Select a current player-faction SSC Master and enable **Bestow Bliss** under personal abilities on the Training tab. It defaults off. Actual effects require no additional player-faction condition, permanent qualification, Master level, bond count or bridle.

Recipients must be living, valid flesh humanlikes with an actual Corruption need and SSC personality component, neither Hollow nor currently a Master. Missing needs are not created. A recipient may be unbound or bonded to another Master. Ordinary RJW paired encounters and native Lovin qualify with the Master in either role. Mutual masturbation with genuine paired participation can qualify; solo acts cannot. Training, ritual stages, self-training and personality excretion are excluded. Preparation, rejection, interruption and cancellation grant nothing. Each completion pays once, independent of orgasm count or animation participants.

| Component | Current rule |
| --- | --- |
| PNA input | 2.4.3 on 1.7: +3 to +100 load points from the Master’s genital fluid output; reference output 20 gives +8; fill an existing legal PNA need |
| Existing ordinary addiction | Base D+0.02; add 0.20 if input ends terminal, totaling +0.22, capped at 1 |
| Initial addiction | Still requires full load; receives no extra existing-addiction reinforcement |
| Corruption | Up to +1 additional percentage point; new scoring caps unchained recipients at 12% and chained recipients by their current stage; old scoring retains the need's original cap. Existing over-cap values are not reduced |
| Part development | A shared 20% budget under the same base conditions, divided across distinct actual recipient part/development-type pairs; Body-part Training research and individual caps apply |

Effective-Chain recipients receive positive PNA activation without ordinary addiction. Master recipients are immune to the entire infestation. A saturated component does not block other valid components. When the source is both a normal producer and an active infestation source in the same direction, only the stronger infestation input is applied.

Part development uses pre-infestation score, Corruption and Chain multipliers. Shares assigned to saturated or later-invalid parts are not redistributed. Actual hand, foot, mouth/tongue/beak, breast, genital and anal participation determines mapping. An ongoing old scene lacking reliable evidence skips development only. Native Lovin lacks whole-scene part evidence and grants no development; Lovin replaced by an ordinary RJW driver follows the RJW path.

Feedback uses only pre-infestation current Corruption C and the recipient's current opinion O of the actual source. O retains its sign and includes existing memories; C is not historical maximum Corruption. Coercion, participant roles and orgasm count do not change this table. Existing RJW feedback remains independent.

| Pre-infestation state | Initial mood | Initial opinion of actual source |
| --- | ---: | ---: |
| C < 30%, O >= 0 | +4 | +5 |
| C < 30%, O < 0 | -4 | -5 |
| C >= 30%, O >= 0 | +8 | +10 |
| C >= 30%, O < 0 | +4 | 0; remove this source's old infestation opinion without adding a zero record |

C=30% and O=0 enter the high-Corruption and nonnegative-opinion branches. Crossing a boundary during settlement affects the next encounter. Mood lasts one day, globally limited to one infestation memory. Opinion lasts three days, one per actual Master, at most three Masters. New reactions replace across stages and refresh duration; a fourth source displaces the oldest record. Opinion stays at its initial value for approximately 2.1 days and fades over the final approximately 0.9 day. Missing mood carriers skip memories without blocking other valid components.

Disabling the ability leaves existing memories to expire. The preference follows personality storage and implantation, pauses outside the Master role, and resumes on return. New clones do not inherit it. Feedback memories retain stage, actual source and remaining duration through the existing personality-memory path; PNA remains with the body. Infestation creates or transfers no bond, grants no Master experience and advances no Chain stage.

Addicted observers apply the existing source preference to an enabled Master who can legally infest them: ×1.25 outside withdrawal or ×1.50 in withdrawal, without multiplying twice for dual sources. This does not guarantee invitation acceptance or add preference for non-addicted observers. In 2.4.3 on 1.7, load scales with the current body’s fluid-producing organs: effective output is clamped to 7.5–250, giving 3–100 load points; output 20 gives 8 points. Missing output retains the 3-point minimum. This measures output capacity, not actual encounter transfer; condoms and participating organs add no new entry requirements. Further input rules remain future work under [item 22](Docs/Design/未来内容开发规划.md#22-主人身份的功能与数值拓展). Personal activation maintenance remains under existing [item 25](Docs/Design/未来内容开发规划.md#25-新版-pna-的性奴正面效果与主动补给). Dedicated RimTalk adaptation is deferred.

See the [completion audit (Chinese)](Docs/Development/Completed/主人欲望侵染完成核对.md) for acceptance and compatibility boundaries.

### 11.6 Medieval PNA Weapons and Visual Feedback

These features are included in the 2.4.3 package for 1.7; the historical 2.4.2 package lacks them. Primitive PNA Weapons is Medieval research costing 800 points, requiring Basic PNA Application rather than concentration research. The weapons use traditional weapon-poisoning techniques to infuse their metal heads with ordinary purple nanofluid.

| Base parameter | Short spear | Blowpipe | Light crossbow |
| --- | --- | --- | --- |
| Main damage / armor penetration | 5 / 12% | 3 / 5% | 7 / 18% |
| Range | Melee | 17.9 tiles | 30.9 tiles |
| Warmup / cooldown | 1.8-second cooldown | 0.5 / 1 second | 1.5 / 2.5 seconds |
| PNA load per qualifying hit | +15 percentage points | +6 points | +10 points |
| Hit profile | Melee hit rating +2 | Touch/short/medium/long accuracy 65/45/20/10% | 50/80/80/60% |
| Wood / steel / normal PNA cost | 30 / 15 / 10 | 20 / 5 / 6 | 45 / 25 / 10 |
| WorkToMake / Crafting skill | 4000 / 3 | 2400 / 2 | 8000 / 4 |

These are base values before applicable pawn/quality effects; listed accuracy is not the final hit chance. Fueled/electric smithies make all three, and crafting spots also make blowpipes. Materials are fixed. PNA is consumed during crafting, with no later refilling, per-shot fluid use or separate ammunition item. Secondary shaft/stock strikes deal 2 blunt damage, have zero penetration and a 2-second cooldown, and do not apply PNA.

One dose is applied only after that hit creates a qualifying fresh open wound in flesh. Full armor blocks, bruises, secondary blunt strikes and old wounds do not qualify. Multi-part injury propagation still gives one dose. Actual load equals baseline potency divided by pre-hit Consciousness or an existing high-Consciousness resistance record; without a record the divisor has a 20% floor, and a hit adds at most 50 load points. Masters reject the drug and effective-Chain recipients receive activation, but these weapons retain injuries and do not heal. The old launcher is removed from 1.7. Reduced trauma does not guarantee a nonfatal hit.

The info-card potency stat remains **15% / 6% / 10%**, representing a qualifying hit at 100% Consciousness without a resistance record. It is not accuracy, concentration, remaining fluid or a damage multiplier, and does not change with the selected target. Hover text and the detailed report explain the correction and identity rules.

The six current ranged projectile types leave purple meteor-like trails behind their tails. A successful dedicated spear thrust lights the spearhead with a **4.5×** soft halo for **0.8 seconds** at normal speed: 0.3 seconds held bright, then 0.5 seconds fading. It may remain after native retraction without extending the attack animation. A fully armor-blocked contact can glow; misses, dodges and blunt strikes do not. Accepted PNA input produces a rising inverted purple heart lasting 1.2 seconds, including valid full-load input; immune Masters produce no heart. Contact glow and absorption hearts signal different outcomes.

Weapon PNGs use 256×256 canvases; dart/bolt PNGs use 128×128 and drawSize 0.5/0.6. The blowpipe uses a short vanilla whoosh firing sound. See the [completion audit](Docs/Development/Completed/SSC武器拓展首批完成核对.md) for current assets and per-batch validation limits.

### 11.7 Industrial PNA Weapons

This is accepted 2.4.3 content for 1.7. Research **Industrial PNA Weapons** (1600 points) after Primitive PNA Weapons, PNA Concentration and Machining; craft all three at a machining table.

| Parameter | Short whip | Repeating needle gun | Precision needle rifle |
| --- | --- | --- | --- |
| Base damage / armor penetration | 3 / 20% | 2 / 30% per needle | 4 / 40% |
| Range | Adjacent melee | 22.9 | 36.9 |
| Warmup / cooldown | — / 1.4 s | 0.5 / 1.3 s | 1.5 / 2.5 s |
| Shots per attack | One | Three, 6 ticks apart | One |
| Base PNA load | 18 points | 8 per needle | 20 points |
| Steel / chemfuel / industrial components / PNA Plus | 20 / 20 / 1 / 6 | 45 / 10 / 3 / 8 | 65 / 15 / 4 / 10 |
| Work / Crafting skill | 8000 / 4 | 14000 / 5 | 18000 / 6 |

Crafting consumes PNA Plus and polymer materials; no separate needles, gas or refilling are required. The same fresh-organic-wound rule as medieval weapons applies, with one dose per hit and separate snapshots per needle. Damage and penetration use normal quality curves; PNA load does not. Both guns' ordinary defensive strikes deal base 9 blunt damage with a 2-second cooldown, using normal quality/automatic penetration and no PNA. The whip's ordinary handle strike is base 2 blunt, zero penetration and a 2-second cooldown, without PNA.

The short whip has +3 melee hit score and, with Ideology, +0.20 suppression power and +15 terror source. Working-end attacks against non-hostile valid Chain-bound targets bypass target dodge without adding a new melee-threat entry; attackers can still miss, and normal wounds remain. Handle strikes and hostile targets retain their normal defense rules.

Four 256px whip images cover item/idle/strike/recovery; its final draw size is 1.4 with aligned grip, a 45° idle angle and 6/12-tick strike/recovery timing. A purple upper-arc afterimage signals contact, even when armor blocks damage; misses do not show it. Accepted drug input alone produces the inverted heart. Dedicated gun/needle art, purple trails and pneumatic shot sounds are complete. See the [industrial completion audit](Docs/Development/Completed/SSC工业时代纳米催情液武器完成核对.md).

### 11.8 Spacer PNA Weapons

2.4.3 adds **PNA Shape Control** (3000 points, Spacer), requiring Industrial PNA Weapons and Fabrication plus a high-tech research bench connected to a multi-analyzer. All three weapons are crafted at a fabrication bench with fixed ingredients, consuming PNA Plus only during crafting.

| Parameter | Silk whip | Pulse gun | Projector gun |
| --- | --- | --- | --- |
| Ordinary-quality base damage / penetration | 1 / 60% | 1 / 70% per pulse | 1 / 90% |
| Range | Adjacent melee | 25.9 | 42.9 |
| Warmup / cooldown | — / 1.2 s | 0.4 / 1.0 s | 1.3 / 2.1 s |
| Shots per attack | One | Three, 5 ticks apart | One |
| Base PNA load | 20 points | 10 per pulse | 25 points |
| Repair budget per effective hit | 1 | 0.5 | 1.25 |
| Steel / plasteel / chemfuel / advanced components / PNA Plus | 20 / 20 / 10 / 1 / 10 | 30 / 40 / 10 / 2 / 12 | 35 / 50 / 10 / 3 / 15 |
| Work / Crafting skill | 24000 / 6 | 45000 / 7 | 60000 / 8 |

These are baseline values before quality, users and other modifiers. Damage and penetration follow native quality; PNA and repair budgets do not. Finished weapons need no separate ammunition or refill. Pulse/projector guns retain ordinary defensive blunt strikes at base 9 damage and 2-second cooldown, with no PNA, spacer protection or repair. The silk whip has only its nanomachine working end, prioritised over other available melee actions for eligible flesh targets.

**Targets and protection:** ordinary targets undergo one normal armor resolution; a blunt conversion retains physical damage but delivers no PNA or repair. Valid Chain-bound targets absorb effective main attacks without wounds or ordinary armor wear and receive activation/repair. Masters receive no PNA, new resistance record or main-attack wound. Shields, cover, misses and intercepted shots still prevent delivery. Non-organic artificial parts do not receive input or repair. Low damage does not guarantee survival for an ordinary injured target.

**Dose and repair:** request load is baseline divided by pre-hit consciousness resistance, with a minimum divisor of 20% when no prior high-consciousness record applies and a single-hit cap of 50 load points. Repair uses baseline ×5, independent of resistance, quality and final added load. Each accepted input can repair the most severe eligible non-permanent natural injury, up to its remaining severity; overflow is discarded. Artificial parts, missing parts, diseases and permanent scars are excluded. Full-load targets can still be repaired. There is no shared pool, recovery timer or cumulative rate limit; independent shots/shooters resolve independently.

**Sharing and whip cooperation:** PNA never travels with transferred Master damage. Medieval/industrial transfers retain physical wounds only; spacer main attacks on Masters create no transferred damage. As with the industrial working end, silk-whip attacks against non-hostile valid Chain-bound targets bypass dodge without registering a new melee threat, while attacker misses remain possible.

**Resources:** the silk whip has its own item image (drawSize 0.75) and three aligned hand poses (1.75), with 45° idle, 6/12-tick timing and a hit afterimage. The final pulse/projector sprites are 256px at 1.05/1.25; purple charge-cluster projectiles use existing trails and accepted-input hearts. Accepted vanilla charge-weapon sounds remain. Further whip sound work is deferred after the trial was reverted. Completion and validation limits: [spacer audit](Docs/Development/Completed/SSC太空时代纳米催情液武器完成核对.md).

## 12. Public Use Specialization

### 12.1 Unlock and Selection

Complete `Public Use`. Selecting it:

- applies its two reception defaults after binding, with forced allowances while specialization overrides are enabled;
- applies the ordinary Public Use Hediff;
- restores saved specialization progress, with a minimum displayed Hediff severity of 1%;
- advances only while the active specialization type remains Public Use.

The Training tab’s internal option is named `Bus`, but the official player-facing term is `Public Use Specialization`.

### 12.2 Progress Sources

- Completing any RJW act with someone other than the master: specialization progress `+5%`.
- Completed daily Training or a full Binding Ritual: shared score-based base specialization progress; the former half-Corruption daily increment no longer applies.
- Trade with a pawn trader: ordinary Public Use Hediff gains roughly `10% + calculated Corruption gain`.
- Trade with a faction settlement: ordinary Public Use Hediff `+10%`.
- Orbital trade ship: no effect.

Progress accounting is separate from interaction permission. Public Use does not remove the actual bond or grant owner permission to third parties.

Base Training progress and trade rewards now update the saved component progress and synchronize the ordinary Hediff. Final recipes check the saved progress.

### 12.3 Stats

| Hediff severity | Trade price | Negotiation | Social impact | Talking |
|---:|---:|---:|---:|---:|
| 20% | +0.10 | +0.13 | +0.17 | +0.03 |
| 50% | +0.20 | +0.26 | +0.33 | +0.07 |
| Final | +0.30 | +0.40 | +0.50 | +0.10 |

### 12.4 Post-trade Event

This event runs only when the Public Use pawn personally negotiates a successful trade with an actual exchange. Cancelled or empty trades do not trigger it.

After trading with a pawn trader:

```text
consensual-sex chance =
    100%                                  if Corruption ≥ 20%
    round(10 + Corruption / 20% × 90)     otherwise
```

The remaining probability is split approximately 8:1 between assault and trader forgiveness.

An actual scene also requires:

- both pawns spawned on the same map and within 15 cells, alive, standing, and undrafted;
- neither pawn fighting, in a mental state, already in an interaction, or performing an uninterruptible job;
- both pawns passing the appropriate RJW checks;
- the initiator being able to reserve and reach the partner, with the matching job available and actually started.

Version 2.2.15 uses RJW's `Quickie` / `RandomRape` definitions and allows either initiating or receiving anatomy in the consensual eligibility check. The partner is assigned a wait of up to 600 ticks while the initiator approaches. Success messages require the job to become current; failed starts clean up only the event's own waiting or queued jobs. Existing probabilities and trade growth calculations are retained.

If physical or RJW conditions fail, the branch may still increase specialization Hediff severity without starting a scene.

## 13. Cow Specialization

### 13.1 Requirements

- Complete `Milking Specialization`.
- The `Sex Slave Chain` health condition is at least stage 2 (severity at least 30%).
- The pawn has `Permanent Lactation Phase`.

In ordinary progression, this means Breasts must have reached at least 70%.

### 13.2 Progress

```text
specialization progress gain =
    calculated milk production × 0.35
```

Without Human Cattle, orgasm maturation counts expected milk in both Permanent Lactation and the extra Cow reservoir.

With Human Cattle, actual milk removed from the Doop reservoir by milking or nursing advances Cow progress instead.

### 13.3 Ordinary Cow Reservoir and Stats

Extra reservoir:

- capacity 0.28;
- initial amount 0.08;
- fills in 12,000 ticks, about 4.8 hours;
- consumes 0.40 nutrition per day;
- every 600 ticks, attempts to move stored amount into the Permanent Lactation reservoir.

| Progress | Effects |
|---:|---|
| < 20% | no specialization stats |
| 20%–49.99% | Vulnerability +3; Moving ×0.60; Manipulation -0.30 |
| ≥ 50% | the above, plus beauty +1 |

### 13.4 Final Cow Specialization

- Vulnerability +3;
- beauty +2;
- Moving ×0.60;
- Manipulation -0.30;
- reservoir capacity 0.40;
- initial amount 0.12;
- fill time 9,000 ticks, about 3.6 hours;
- nutrition cost 0.55 per day.

Becoming a Final Cow does not automatically apply `Full Gelatinization Complete`.

## 14. Final Specializations and Personality Gel

An ordinary Public Use or Cow specialization at 99.9% does not upgrade directly on the same pawn. Finalization uses Personality Gel:

1. Perform Personality Excretion on the specialized pawn. The gel stores specialization type, progress, and specialization Hediff tags.
2. At an art bench, use the corresponding personality-edit recipe for `Final Public Use Specialization` or `Final Cow Specialization`.
3. Each recipe has 3,500 work.
4. The gel must contain at least 99.9% saved progress and the matching ordinary specialization tag.
5. The recipe consumes the original gel and creates an edited gel of the same grade.
6. Assign that edited gel to a Hollow and implant it.

Ordinary and final Hediffs in the same specialization family are mutually exclusive. Public Use and Cow are separate families and their Hediffs can coexist, but the Training tab records only one active specialization type at a time.

## 15. Personality Excretion and Implantation

### 15.1 Preparation Surgery

After researching `Personality Excretion`, schedule the operation that injects PNA Plus to induce it:

- 5 Medicine-category items;
- 15 Purple Aphrodisiac Nanofluid Plus;
- work 1,000;
- applies `Preparing Personality Excretion`;
- starts at 1%;
- naturally gains 30% per day.

Natural preparation takes about 3.3 days. Each anal act adds another `10%`.

### 15.2 Performing Personality Excretion

At 100% preparation, right-click the target with a colonist assigned to the `Training` work type.

The target must be:

- Humanlike;
- someone other than the worker;
- a colonist or colony prisoner;
- eligible as an RJW receiver;
- fully prepared;
- reservable.

The Personality Excretion scene is fixed to Anal.

### 15.3 Gel Grade

Grade depends on current Chain severity:

| Chain severity | Product |
|---:|---|
| < 30% | `Personality Gel` |
| 30%–49.99% | `Basic Personality Gel` |
| 50%–79.99% | `Advanced Personality Gel` |
| ≥ 80% | `Perfect Personality Gel` |

Personality Gel is non-tradable, non-stackable, has market value 1,000, and mass 0.5.

Edited variants retain the corresponding grade.

### 15.4 Stored Data

The gel stores:

- name;
- serialized Sex Slave trait compatibility data, whose authoritative source is highest-ever Corruption;
- current Corruption and historical maximum Corruption;
- specialization type, progress, and lactation toggle;
- Chain severity and master reference;
- skill levels, experience, and passions;
- current memories;
- ordinary traits and their degrees, including traits temporarily suppressed by genes; new gels exclude gene-granted traits;
- direct social relations;
- childhood and adulthood backstories;
- ordinary and final Public Use/Cow tags.

It does not store the receiving body’s appearance, age, body parts, or genes.

### 15.5 The Hollow Body

After excretion, the original body:

- loses all direct relations;
- gains `Personality Excretion (Complete)` and the PE marker trait;
- has Social set to 0;
- has other skills set to 6;
- loses current skill XP;
- loses all ideoligion certainty;
- has mood fixed at 50%;
- cannot have mental breaks or inspirations;
- has Consciousness capped at 60% and Moving capped at 80%;
- receives learning factor -1.0 and social impact -0.5;
- gains `Personality Excretion Adaptation Syndrome`.

The adaptation syndrome is currently added at severity 0.5 and decays by 0.5 per day, so it lasts about one day.

### 15.6 Assignment and Automatic Implantation

Use the Personality tab on the gel to assign a Hollow. A Training worker can then perform implantation automatically if:

- the gel has an assigned pawn;
- that pawn still has `Personality Excretion (Complete)`;
- gel and pawn can be reached and reserved;
- neither is forbidden.

The procedure lasts 600 ticks. The Hollow waits while retaining posture and sleep, and contact must remain possible throughout. Before applying the personality, SSC rechecks contact, shared map, and that the target is still a living Hollow. A failed check interrupts implantation without transferring personality data or consuming the gel.

### 15.7 Restored Data

Implantation:

- removes the Hollow and PE markers;
- clears the receiving body’s previous Corruption, Sex Slave trait, Chain, and Public Use/Cow states;
- restores name, Corruption, backstories, highest-ever Corruption, and specialization data, then rebuilds the Sex Slave trait from that maximum;
- replaces the receiving body's ordinary personality traits with the gel's snapshot, preserves that body's genes and their traits, and reapplies suppression according to those genes;
- reconstructs the Chain master;
- restores skills, passions, XP, memories, and direct relations;
- restores saved specialization tags;
- adds `Personality Implantation Adaptation Syndrome`.

Returning a personality to its original body also restores the extraction-time trait snapshot. A valid empty list clears the body's ordinary traits; a missing snapshot rejects implantation and leaves both the receiving pawn and gel unchanged.

Older gels did not record gene sources. Their saved entries are restored as personality traits because traits granted by the original body's genes can no longer be identified; the receiving body's genes are still preserved. See section 19 and the [fix details (Chinese)](Docs/Development/Completed/人格普通特质迁移修复.md).

## 16. Semi-gelatinization Surgery

### 16.1 Operation

Requires `Partial Gelatinization`:

- target is a non-missing arm or leg core;
- Medical skill 5;
- 1 industrial medicine;
- work 2,500;
- surgery success factor 0.95;
- configured failure-death chance 5%;
- unavailable after full gelatinization.

### 16.2 Adaptation Race

Every 600 ticks:

```text
severity += random(0.3%, 0.6%)
```

```text
if Corruption ≥ 70%:
    adaptation always += random(0.5%, 1.0%)

otherwise:
    gain chance = 50% + 40% × Corruption
    on success, adaptation += random(0.5%, 1.0%)
```

Reaching 100% adaptation first succeeds. Reaching 100% severity first fails.

### 16.3 Results

Success:

- arm → `Gelatinized Arm`: Manipulation +0.2, blunt armor +0.3, sharp armor +0.2;
- leg → `Gelatinized Leg`: Moving +0.25, blunt armor +0.3, sharp armor +0.2;
- a Gelatinized Arm heals 0.4 damage from one random naturally healable injury every 600 ticks.

Failure:

- the selected limb becomes a stable missing part;
- no fresh-amputation bleeding state remains.

A limb currently undergoing semi-gelatinization is also a priority target for Damage Sharing.

## 17. Full Gelatinization Surgery

### 17.1 Requirements

- `Full-body Gelatinization` research;
- Humanlike target;
- target has completed Personality Excretion and remains a Hollow;
- not already in progress or complete;
- Medical skill 10;
- 4 industrial medicine;
- 10 Purple Aphrodisiac Nanofluid Plus;
- work 5,000;
- surgery success factor 0.85;
- configured failure-death chance 20%.

After the operation succeeds, `Full Gelatinization in Progress` begins a second adaptation-versus-severity race.

### 17.2 Readiness

Body-part weights:

| Track | Weight |
|---|---:|
| Hands | 15% |
| Feet | 15% |
| Mouth | 15% |
| Breasts | 15% |
| Genitals | 20% |
| Anus | 20% |

If the same Hediff appears on multiple parts, its mean severity is used.

```text
P = weighted sum of six body-part tracks
R = 0.45 × Corruption + 0.55 × P
W = R² × (3 - 2R)
```

### 17.3 Adaptation and Collapse

Every 600 ticks, before guaranteed success:

```text
adaptation gain A = lerp(1%, 4%, W)
adaptation chance = lerp(15%, 95%, W)
```

Adaptation increases by `A` only when the roll succeeds.

```text
minimum severity gain = lerp(8%, 2%, W)
maximum severity gain = lerp(10%, 4.8%, W)
```

Severity gains a random amount from that interval every check.

### 17.4 Guaranteed Success

All seven values must be at least 99.9%:

- Corruption;
- Hands;
- Feet;
- Mouth;
- Breasts;
- Genitals;
- Anus.

In guaranteed mode, every 600 ticks:

- adaptation gains 30–45%;
- the severity-gain interval is multiplied by 0.35.

Adaptation will then finish before severity.

### 17.5 Outcomes

Success:

- removes most non-SSC and non-RJW health states;
- preserves SSC states;
- preserves RJW genital Hediffs and body-part-bound RJW Hediffs;
- adds `Full Gelatinization Complete` and `Gel Shell`;
- Manipulation +0.8;
- Moving +0.5;
- Consciousness +0.5;
- blunt armor +1.2;
- sharp armor +0.8;
- pain-shock threshold +0.2;
- heals 1.2 damage from every naturally healable injury every 300 ticks;
- can apply optional purple translucent body coloring.

Failure removes the pawn completely from both the map and world-pawn list. No corpse or recoverable body remains.

### 17.6 Gel appearance (2.4.2 / 1.7 only)

After full gelatinization, use Gel Appearance on the Training tab to enable per-pawn rendering, view the swatch/opacity and open the color wheel, RGB/HSV pages, HEX and opacity controls. Defaults are dark purple, 90% HSV saturation and 85% opacity. Resetting appearance does not change the rendering switch; cancel discards edits. Disabling rendering retains saved colors and gameplay effects. Vanilla eyes and FA animation/occlusion remain preserved. Missing per-pawn fields inherit the legacy global preference once. The pawn-gizmo color button was removed. The frozen 1.6 runtime keeps its original global body-tint behavior.

## 18. Master–Slave Linkages

### 18.1 Libidinal Resonance Field

A master with at least one valid bonded Sex Slave gains `Libidinal Resonance Field`. Its main tier is determined by bond count; average Corruption interpolates within that tier:

```text
smoothed Corruption E =
    [C² × (3 - 2C)]^1.35

field severity =
    lerp(tier minimum, tier maximum, E)
```

| Bonds | Severity range | Manipulation | Melee hit/dodge | Move speed | Pain threshold |
|---:|---:|---:|---:|---:|---:|
| 1–2 | 1%–24% | +0.08 | each +0.04 | +0.08 | +0.08 |
| 3–4 | 25%–49% | +0.15 | each +0.08 | +0.15 | +0.15 |
| 5–7 | 50%–74% | +0.22 | each +0.12 | +0.22 | +0.25 |
| ≥ 8 | 75%–100% | +0.30 | each +0.16 | +0.30 | +0.35 |

The highest tier additionally grants:

- negotiation +0.10;
- incoming damage ×0.92;
- social impact ×1.15.

Under the old score system only, the four field tiers multiply Training Corruption gain by `1.1/1.3/1.5/1.8`.

### 18.2 Damage Sharing

Configure the toggle in the Master growth panel on the Training tab. It remains stored in `Sex Slave Bridle` and defaults to enabled. Without a bridle it is disabled; a temporary lack of eligible recipients does not prevent saving the preference. The old standalone Gizmo is removed:

- the master keeps 30% of incoming damage;
- 70% is distributed among valid bonded Sex Slaves;
- candidates must be on the same map, alive, and not downed;
- each candidate’s weight is `0.25 + Corruption`;
- if any candidate is undergoing semi-gelatinization, only those candidates are used;
- temporary gelatinizing parts are preferred, followed by stable Gelatinized limbs, then ordinary limbs.

Damage that is not shared:

- execution cuts;
- surgery cuts;
- fire-extinguishing damage;
- EMP;
- ToxicBurn;
- any damage whose DefName contains `Toxic`.

### 18.3 Bed Sharing

An SSC Sex Slave with current Corruption strictly above 0.1% gains extra permission to share an ordinary multi-person bed with both the bonded Master and active Assigned Trainer. Binding does not exclude the trainer. Ordinary vanilla slaves without SSC Sex Slave identity receive no such exception.

Bed sharing supplies an additional permission; existing lovers/spouses may still share beds. Separate 2.4.6 romance initiative/response rules are described in section 27. Medical rest and deathrest take priority; ordinary rest tries the pawn’s assigned bed, then the Master’s bed, then the trainer’s bed. Bed capacity, reachability, reservations and other environment checks remain native.

A Sex Slave with vanilla slave status cannot be assigned directly to an empty colonist bed: assign the Master or active trainer first, then the eligible slave. Colonist Sex Slaves follow native assignment rules. Existing bed ownership is not forcibly removed.

Vanilla and Dubs Mint Menus show independent Master (gold), Sex Slave (pink-purple) and Trainer (cyan-blue) badges to the right of the name. Empty beds show all actual roles. Occupied beds show roles only for assigned pawns and their direct SSC partners, using the union for multiple owners without recursively extending it. Rows without badges remain available under native rules.

A separate marker shows SSC permission: green for a qualifying assigned partner; yellow for waiting on an empty bed or retaining an existing assignment; gray for no extra permission. Hover text distinguishes these states and lists the Master, Assigned Trainer and relevant slaves’ current Corruption and threshold result. Native assignment rejection reasons and full names remain available on hover.

Only actual simultaneous sleep in the same bed is recorded. When laying down ends, the Sex Slave receives a one-day, non-stacking memory. Merely assigning a bed or lying awake is insufficient. Master and trainer memories have separate text; if both actually shared the sleep, the Master memory takes priority even if the Master wakes first. The existing negative bedroom/barracks memory exemption applies to the slave and actual partners; no extra positive shared-bed memory is granted to the Master/trainer.

| Condition | Mood |
|---|---:|
| Master/Perfect Sex Slave relation exists | +13 |
| Corruption ≥ 50% | +9 |
| Corruption ≥ 30% | +5 |
| Otherwise, opinion < 30 | -6 |
| Otherwise, opinion < 50 | -3 |
| Otherwise, opinion ≥ 50 | +2 |

### 18.4 Mood after Assault

Below 20% Corruption, RJW’s original logic applies. At 20% or above, SSC forces masochistic handling and replaces the victim memory:

| Corruption | Mood |
|---:|---:|
| 20%–49.99% | +1 |
| 50%–69.99% | +3 |
| 70%–89.99% | +5 |
| ≥ 90% | +8 |

The memory lasts 10 days, stacks to 100, and uses a 0.4 stack multiplier.

At 20%–69.99%, the normal observer penalty is cancelled. At 70% or more, visible same-faction observers within 15 cells receive a `+5` opinion memory.

### 18.5 Rebellion and Work

With a Chain:

- the pawn cannot join prison breaks;
- the pawn cannot join slave rebellions;
- berserk mental states are intercepted;
- interception refills Suppression;
- Suppression no longer changes naturally.

A vanilla slave whose historical maximum Corruption has exceeded zero permanently retains the following slave work adjustments, even if current Corruption later decays to zero. These effects do not grant SSC role identity or Training eligibility:

- has the vanilla slave disabled-work list cleared;
- no longer receives the vanilla slave work-speed StatPart.

### 18.6 Behavior Restrictions

With restrictions enabled, only pawns with an actual owner bond are affected. Identity, specialization or apparel alone does not activate rules. The actual owner initiating with their own bound pawn is immediately allowed; normal physical and job requirements still apply.

| Individual rule | Factory default for new pawns |
| --- | --- |
| Solo masturbation | Deny |
| Self-training | Allow; SSC Sex Slave identity and a Chain are still required |
| Consensual initiation | Enabled, owner only |
| Forced initiation | Deny |
| Consensual reception from non-owners | Deny |
| Forced reception from non-owners | Deny |
| Training from non-owners | Deny |

Self-training and ordinary solo activity use separate permissions; new pawns and saves without the self-training field default to allowing it. Ordinary pair jobs check both initiation and reception; Training and rituals use the separate Training rule. Equipment overrides take priority over enabled specialization overrides, then saved choices. Public Use defaults and forces its two reception permissions, without opening initiation or Training. Protective apparel denies forced reception from non-owners. Overrides never overwrite saved choices.

The Training ITab expands a right-side rule panel. Its entry is disabled when inactive; forced entries display effective values and are locked. Defaults have their own mod-settings window; editing them leaves existing pawns intact, while batch application requires confirmation.

Legacy saves migrate once. Unbinding/rebinding retains choices. Permission changes retain normal cleanup for started scenes; later jobs, phases and participants are checked again. 2.3.9 treats receiver identity as a continuing requirement: Training scenes whose receiver loses SSC Sex Slave identity stop without unfinished rewards. Supported LifeForce paths and native Lovin use unified permission checks; legacy whitelists are removed.

## 19. Apparel and Sex Reassignment

### 19.1 Apparel Bulletproof Suit

Official English localization uses the name `Apparel Bulletproof Suit`.

- female apparel;
- cost: 5 components, 200 cloth, 200 steel;
- crafting skill 4;
- sharp armor 0.1, blunt armor 0.2, cold insulation 10;
- move speed -0.2;
- slave suppression offset +0.1;
- Corruption floor 30%;
- the 30% floor unlocks only after this pawn’s historical maximum Corruption has reached 30%;
- the floor only applies inside the current Chain-stage interval;
- decay multiplier 1.1.

### 19.2 Evilfall Combat Suit

- wearable only at `Sex Slave Chain` health stage 4 (severity at least 90%);
- cost: 50 plasteel, 250 synthread, 2 advanced components, 25 devilstrand, 20 luciferium;
- crafting skill 8;
- sharp armor 1.0 and blunt armor 0.8;
- comfortable temperature expanded by 30°C in both directions;
- slave suppression offset +1;
- global work speed +0.1;
- melee dodge +0.3;
- move speed +0.3;
- Corruption floor 100%;
- the 100% floor unlocks only after this pawn’s historical maximum Corruption has reached 100%;
- the floor only applies inside the current Chain-stage interval;
- decay multiplier 1.5;
- forces denial of non-owner forced reception while bonded and restrictions are enabled.

`Need_Corruption` persistently records the highest Corruption that pawn has ever reached. Apparel floors preserve an unlocked milestone; they do not grant 30% or 100% by themselves. In the current formula, decay multipliers above 1 accelerate decay rather than slowing it.

### 19.3 Sex Reassignment Surgery (Male to Female)

Current operation conditions:

- target is male;
- 5 Medicine-category items;
- 10 Purple Aphrodisiac Nanofluid;
- Medical skill 5;
- work 4,000;
- surgery success factor 0.90.

On success:

- changes gender to female;
- removes the old penis, vagina, and breasts;
- rebuilds genitals, breasts, and anus according to RJW’s female configuration;
- attempts to replace a male body type with a female body type;
- chooses from valid race body types for HAR races;
- removes beard and refreshes graphics.

Pawns with mood needs also receive a surgery memory lasting 15 days, with a base mood effect of −10 and a one-stack limit. Version 2.2.13 fixes its duplicate definition name. If the definition is missing, SSC retains the diagnostic and skips memory addition. Memories missed in the past are not granted retroactively.

The operation currently does not reference `Sex Reassignment Surgery (Male to Female)` as a research prerequisite, so that research node does not actually lock it.

`Femboy Conversion` exists as a research node but is not implemented as a complete gameplay system.

## 20. Settings

| Setting | Default | Effect |
|---|---:|---|
| Allow direct Master selection | Off | Bypass personal qualification for new Master choices; grants no permanent qualification and does not bypass research |
| Enable behavior restrictions | On | Controls unified restrictions and overrides; off retains configurations |
| Enable specialization overrides | On | Controls profile overrides only; equipment remains active |
| Default behavior rules | See section 18.6 | Used for initialization; explicit batch overwrite requires confirmation |
| SSC logging | On | Custom normal logs |
| High-frequency debug logging | Off | Guard, animation, and Training-flow logs |
| Low-frequency key logging | On | Onahole, ritual conversion, and important fallbacks |
| Use RJW original eligibility for training age checks | On | Current code branch is reversed relative to the displayed explanation |
| Debug Gizmos | Off | Quick-test buttons in developer mode |
| Per-pawn gel rendering (2.4.2 / 1.7) | On for new bodies; legacy preference for old saves | On the Training tab; controls body/head/SA rendering while retaining saved colors when off. The frozen 1.6 runtime keeps its global body tint |
| Ritual development points (2.4.4 / 1.7) | Off | Uses vanilla development conditions, outcome curve and reform cap; no historical backfill |
| Allow ritual conversion into slavery | On | Disables only ritual slave conversion, not other ritual rewards |
| Enable Corruption decay | On | Off stops natural loss; apparel floors and Chain caps remain active |
| Base Corruption decay per day | 2% | Adjustable from 0% to 20% per day in 0.5% steps |
| Use old scoring | Off | Switches to old score, memories, and no natural Corruption decay |
| Spawn Seed of Desire / trigger claim quest | Action buttons | In the settings debug area; create an item or a free quest, without directly granting personal qualification |

### 20.1 Old Score System

```text
S_old =
    2 × Social
  + opinion / 5
  + vanilla-slave bonus 5
  + Chain health-stage bonus

final score =
    S_old × size multiplier + random(-5, +5)
```

Chain health-stage bonus:

- stage 1: +5
- stage 2: +10
- stage 3: +30
- stage 4: +60

Size multiplier is roughly `0.5–1.5`.

```text
G_old =
    clamp(
        S_old / 100 × outcome-level multiplier,
        0.5%,
        8%
    )
```

Outcome-level multipliers are level 1 `×1.0`, level 2 `×0.8`, and level 3 `×0.6`, followed by the master resonance multiplier.

Under old scoring, Corruption has no natural decay; only apparel floors and the 100% maximum clamp apply.

## 21. Compatibility

### 21.1 RJW Onahole

If the target is already running `BeOnahole`:

- SSC does not forcibly replace the receiver job;
- the current trainer is registered with Onahole;
- reversed `SexProps` are synchronized;
- the partner is removed when the scene ends;
- ordinary Training, Binding Ritual, and Personality Excretion all use this integration path.

### 21.2 Rimworld Animations

- Binding Ritual searches by reflection for compatible group animations.
- If normal animation startup fails, SSC reads the animation framework's own definition database and attempts a manual fallback.
- Playback requires an installed framework and compatible assets. Missing matches retain the warning and existing ritual timing.
- When UAP is installed, SSC releases participant position locks before new ritual stages and when started stages finish, preventing stale job checks from stopping new animations. This does not rebuild queues already cleared in old failure saves.
- Weapons are hidden during animation.
- The integration is optional and creates no hard dependency.

### 21.3 Human Cattle

When Human Cattle is detected, the Doop reservoir becomes the only real milk source:

- SSC Permanent Lactation and extra Cow reservoirs stop growing and are cleared;
- SSC orgasm maturation no longer adds directly to breasts or spawns overflow milk;
- PNA production is unaffected;
- milk actually consumed by milking or nursing still advances Cow progress.

SSC injects into Human Cattle’s `BaseLactationFactor`:

| SSC state | Multiplier |
|---|---:|
| Cow stage 0 | ×1.10 |
| Cow stage 1 | ×1.25 |
| Cow stage 2 | ×1.40 |
| Final Cow | ×1.50 |

### 21.4 Equal Milking

Without Human Cattle, Equal Milking recognizes `SSC_Lactating_SubState` as lactation. When Human Cattle is loaded, SSC stops that handoff to prevent duplicate reservoirs.

### 21.5 RJW-PE

SSC reads RJW-PE’s Humanlike and Animal age settings through reflection for detailed failure reports. Final target eligibility still follows the currently selected RJW/SSC `can_be_fucked` branch.

### 21.6 LifeForce Gene

With Biotech active, if a pawn has `rjw_genes_lifeforce` and gains one of these conflicting SSC states:

- master–slave Chain;
- Personality Excretion;
- Hollow or post-excretion coma;
- semi-gelatinization;
- full gelatinization;

SSC removes the LifeForce gene and spawns a genepack containing only that gene at the pawn’s feet.

### 21.7 HAR and Sized Apparel

- Sex Reassignment Surgery tries to respect body types permitted by a HAR race.
- Full-gelatinization coloring additionally recognizes apparel graphics whose path contains `SizedApparel`.

### 21.8 RimTalk and Scheduled Training

SSC's legacy RimTalk integration is suspended pending a complete redesign. SSC no longer sends scene dialogue, inserts Training interruption lines, clears RimTalk replies, or reserves dialogue generation. The settings page shows a suspension notice. RimTalk's own features remain controlled by RimTalk and its other extensions.

Each Sex Slave has a timetable in the Training tab:

- the start hour may be set from 00:00 through 23:00;
- every automatic Training window lasts two in-game hours;
- the interval may be set to once every 1–7 days;
- automatic Training must satisfy both the timetable and the normal 22,500-tick cooldown;
- a player-forced order bypasses the scheduled date and window, but not cooldown, reachability, ownership, or target-safety checks;
- disabling the timetable restores the old behavior: automatic Training may start whenever cooldown and normal checks permit.

The timetable belongs to SSC and works without RimTalk.

Legacy preferences are retained for future migration and cannot re-enable the archived integration.

<!-- Current limitations checklist temporarily hidden.
## 22. Current Limitations and Known Differences

This section records the audited code behavior and known limitations.

1. **Fine Training is not implemented.** The body-part detection, sensitivity, reward, and progress methods in `FineTrainingUtility` still return placeholders. The Training tab fields are reserved only.
2. **Binding Ritual genital compatibility is fixed at the worst bracket.** The ritual calls a size-difference method that currently returns `0`, producing `-20%`. Ordinary Training size scoring works separately.
3. **Erotic Word has no normal acquisition path.** The ability and grant component exist, but the Mouth Hediff XML does not attach the component.
4. **Several research nodes are technology-tree placeholders.** `Femboy Conversion` still lacks a complete matching system. Basic PNA Application is a prerequisite for later PNA research, and the obsolete launcher research has been removed from 1.7. Version 2.3.7 implements ordinary Cat/Dog training, pet exclusivity, gel finalization, final active abilities and visible affection; the adult consensual follow-up is implemented and awaits in-game acceptance. Rabbit selection remains disabled. `Combatant` supports ordinary training, experience, gel finalization, and the final Combat Overdrive ability. `Personality Editing` acts as the parent node for specialization research.
5. **The old Basic PNA Launcher is removed from 1.7.** Its dedicated recipe and research are also removed without save migration; frozen 1.6 content is unchanged.
6. **Sex Reassignment Surgery is not research-locked.** Its operation exists without a `researchPrerequisite`. The duplicate definition for its surgery memory was fixed in 2.2.13; the current independent memory definition is not a remaining duplicate-load issue.
7. **Personality trait transfer and the gel UI have been fixed.** The maintainer has confirmed this round of fixes is effective. Ordinary personality traits are now restored from the gel snapshot while preserving the receiving body's genes and their traits. Legacy entries are restored as personality traits because their source was not recorded; new gels save only traits without a gene source. The Sex Slave trait is still rebuilt separately from restored highest-ever Corruption. See the [fix record (Chinese)](Docs/Development/Completed/人格普通特质迁移修复.md) for details.
8. **Public Use progress now uses the saved component.** Version 2.3.6 routes daily base Training progress through the shared component entry; trade rewards also update component progress and synchronize the ordinary Hediff. Final recipes continue to check saved progress.
9. **Ordinary specialization progress is saved separately for each direction.** Switching directions or selecting None pauses the previous path and removes inactive ordinary states. Returning restores that direction's own progress rather than carrying another path's progress across. Final achievements remain subject to their own continuing eligibility rules.
10. **Final Cow does not cause full gelatinization.** Old notes mention this linkage, but current Defs and C# do not add the completed state.
11. **Apparel decay multipliers run in the opposite direction from their descriptions.** Values 1.1 and 1.5 are multiplied into decay and therefore accelerate it. Their 30%/100% floors activate only after that pawn has historically reached the matching value.
12. **The RJW-original age/eligibility setting is wired in reverse.** On uses `LegacyCanBeFucked` and additionally requires RJW `rape_enabled`; off directly calls `xxx.can_be_fucked`. Toggle it when an apparently valid target cannot be trained.
13. **Chain progression requires reaching the threshold, then resolving a ritual.** Chain starts at 20%, and the current stage brakes Corruption at 30%. After reaching 30%, another Binding Ritual resolution is needed to raise Chain severity. The Sex Slave trait is synchronized only from highest-ever Corruption. Minimum-Corruption effects such as Apparel Bulletproof Suit preserve only milestones already reached inside the current Chain interval; they do not skip stages.
15. **The old high-tier PNA gun is archived.** Its commented-out gun/projectile configuration and remaining damage definition were removed together with the old launcher; 1.7 now uses the nine current weapons.

-->

## 23. Research Tree

In version 2.3.7, Cat selection requires SSC Sex Slave identity, completed Training and Cat research, and no final Cat, Dog or Rabbit specialization. A bound master and a chain stage are not selection requirements. Completed daily Training or full Binding Rituals give the current ordinary pet path shared progress. A current ordinary Cat additionally gains 1 percentage point from successful affection toward its actual bound master while idle nearby, using the existing 60,000-tick (one-day) cooldown. Completed ordinary training, any final pet specialization, or a different current direction blocks this reward; Dog affection grants no progress. Cat's movement and melee dodge bonuses begin at 20% and improve at 50%. Cat/Dog progress uses one decimal place and reads "Training complete; ready to finalize" at the shared 0.999 threshold. Hover for personality-gel extraction, sculpting-table processing and implantation steps; ordinary completion does not directly grant a final ability. Final bonuses, abilities and affection remain after switching directions. Final Cat, Dog and Rabbit specializations are mutually exclusive; ordinary direction changes retain historical progress. Rabbit selection stays disabled.

Final Cats use Gentle Comfort on another conscious player-faction humanlike pawn on the same map, walking onto its cell for two seconds. At completion, recover any current MentalState, or grant +8 mood and ×1.10 global work speed for half a day; both branches share a one-day cooldown. Catatonic breakdown is not treated by removing a Hediff. Final Dogs use Animal Training on an eligible animal on its cell for two seconds, directly taming a wild animal or fully completing one currently eligible checked training item for a player-owned animal using vanilla priority and preserving its master. Both branches share a one-day cooldown; cancellation or an invalid target starts no new cooldown. The ability grants no extra work progress or work follow-up event.

Ordinary affection automatically discovers the actual bound master within about 2.9 cells while both are idle and awake. The pet approaches, pauses for two seconds and shows progress, facing and hearts. Success gives the master +5 mood for six hours, with one memory maximum, and starts a one-day cooldown; cancellation gives no reward. Eligible ordinary and effective final Cats/Dogs make one 20% roll to initiate a consensual follow-up with that master after affection. Both must be biologically at least 18 and adult under RJW rules, and SSC permission plus RJW consensual eligibility must allow it. A miss, refusal or failure preserves affection rewards without rerolling or forcing an interaction. New commands during preparation cancel the follow-up and preserve the new jobs. There is no extra pair cooldown or separate affection progress. In-game acceptance of this follow-up is pending. See the [Cat/Dog quick start](QUICK_START_EN.md#pet-cat-and-dog-237) for controls.

| Research | Cost | Prerequisite | Current gameplay function |
|---|---:|---|---|
| Training | 500 | No research prerequisite; requires a techprint with Royalty | Training tab features and WorkGiver |
| Body-part Training | 800 | Training | Six body-part experience tracks |
| Basic PNA Application | 800 | Training | Prerequisite for primitive weapons, concentration and later PNA research |
| Sex Reassignment Surgery (Male to Female) | 1,000 | Basic PNA Application | Node does not actually lock the operation |
| Femboy Conversion | 1,200 | Sex Reassignment Surgery | Not fully implemented |
| PNA Cosmetic Application (2.4.4, 1.7) | 1,500 | Basic PNA Application, Sterile Materials | Cosmetic surgery, Medical 8; not required for the cultural ritual |
| Primitive PNA Weapons (2.4.3, 1.7) | 800 | Basic PNA Application | Short spear, blowpipe and light crossbow; no concentration requirement |
| Industrial PNA Weapons (2.4.3, 1.7) | 1,600 | Primitive PNA Weapons, PNA Concentration, Machining | Short whip, repeating needle gun and precision needle rifle |
| PNA Shape Control (2.4.3, 1.7) | 3,000 | Industrial PNA Weapons, Fabrication; high-tech bench + multi-analyzer | Silk whip, pulse gun and projector gun; fabrication bench |
| PNA Concentration Technology | 1,200 | Basic PNA Application | Converts normal PNA to PNA Plus |
| Personality Excretion | 1,500 | PNA Concentration Technology | Preparation operation and gel extraction |
| Personality Editing | 1,500 | Body-part Training, Personality Excretion | Parent node for specialization research |
| Milking Specialization | 1,200 | Personality Editing | Cow Specialization and Final Cow recipe |
| Public Use | 1,200 | Personality Editing | Public Use Specialization and final recipe |
| Pet: Cat | 1,200 | Personality Editing | Ordinary training, shared experience, affection progress, gel finalization and Gentle Comfort; completion tooltip |
| Combatant | 1,200 | Personality Editing | Ordinary training, experience, gel finalization, and final Combat Overdrive implemented |
| Pet: Dog | 1,500 | Pet: Cat | Ordinary training, shared and animal-related experience, work events, gel finalization and both Animal Training branches |
| Pet: Rabbit | 1,800 | Pet: Dog | Selection disabled; marked Incomplete; saved birth mode read-only |
| Partial Gelatinization | 2,000 | Personality Excretion | Semi-gelatinization Surgery |
| Full-body Gelatinization | 3,000 | Partial Gelatinization | Full Gelatinization Surgery |

## 24. Troubleshooting

### How do qualification, Master identity and growth differ after upgrading to 2.4.0?

They are separate records: qualification is permanent personal eligibility, identity says whether the pawn is currently a Master, and Symbiosis of Desire stores total experience. Default direct selection can bypass qualification but does not grant it. Seeds do not change identity or reset growth. Eligible legacy Masters and legacy Master gels receive one-time qualification and zero-experience baseline growth migrations, without retrospective credit for old Training. Later loads do not continually infer these records.

### Why is there no seed quest, or why did a seed not complete research?

The initial automatic quest requires unfinished Training research, no valid current player SSC Master, and no previous successful initial-quest creation in the save. The manual quest button bypasses these conditions but cannot duplicate an unfinished claim quest. With Royalty, a seed supplies missing techprints without research progress or Intellectual experience; without Royalty it still grants personal qualification. Natural reward frequency and balance remain listed under [pending verification](Docs/Development/Verification/README.md).

### Why did the growth health entry disappear, and where is damage sharing?

Leaving Master identity or becoming a Hollow hides Symbiosis of Desire from the normal health tab and suspends its bonuses while retaining experience. Returning restores the saved stage. Experience and damage sharing are in the Master's Training tab; the old Gizmo is removed. A bridle is required to use the toggle, but a temporary lack of recipients does not prevent saving the preference.

### Does Personality Gel merge Master qualification and experience from both bodies?

No. Gel copying and processing preserve the source records; implantation replaces the host's qualification and growth exactly, rather than taking the higher values. A source without records also clears the host's old records. New clones inherit neither Master identity, qualification nor growth. See [qualification](#311-permanent-master-qualification-240) and [progression](#312-master-progression-and-practice-240).

### No Training option appears

Check, in order:

1. `Training` research is complete.
2. The trainer has active trainer identity and the `Training` work type enabled.
3. The target’s `Pawn Identity` is `Sex Slave`.
4. `Allow Training` is enabled.
5. The target is not inside the nine-hour cooldown.
6. `Assigned Trainer` matches the worker.
7. A Chain is not forcing a different master.
8. RJW allows the target as a receiver.
9. RJW `rape_enabled` and SSC’s eligibility setting are not forcing the legacy check to fail.

### Corruption reached a threshold but Chain did not advance

The normal flow is to push Corruption to the current Chain stage’s brake cap, then complete a Binding Ritual. The first Chain stage can reach 30%, but Chain severity advances only during ritual resolution. If Corruption is still being forced back to 10%, confirm that the latest DLL is loaded and no old build is overriding it.

### Body-part experience does not increase

- `Body-part Training` must be researched.
- The act must map to that track.
- Training score must be positive.
- A Binding Ritual must finish completely before its six-track reward is paid.

### Cow Specialization cannot be selected

All three are required:

- `Milking Specialization` research;
- `Sex Slave Chain` health stage 2 or higher (severity at least 30%);
- 70% Breasts and `Permanent Lactation Phase`.

### A 100% specialization did not become Final

Finalization is a gel loop: Personality Excretion → edit the gel at an art bench → implant it into a Hollow. It never upgrades automatically on the original pawn.

### Personality Excretion is fully prepared but does not start

Its WorkGiver does not scan the whole map automatically. Right-click the target with a colonist assigned to Training. The target must also pass RJW receiver eligibility.

### Nobody implants the Personality Gel

- Assign a Hollow in the gel’s Personality tab.
- The Hollow must still have `Personality Excretion (Complete)`.
- A worker must have the Training work type enabled.
- Gel and Hollow must be reachable, reservable, and allowed.

### Full Gelatinization keeps failing

This is a continuing race rather than one success roll. Before all six body-part tracks and Corruption reach 99.9%, severity usually grows faster than adaptation. True guaranteed success requires all seven values at 99.9% or higher.

### SSC milk reservoirs stop under Human Cattle

This is intentional integration behavior. Human Cattle becomes the sole real reservoir, SSC clears and disables its own milk reservoirs, and Cow multipliers are injected into `BaseLactationFactor`.

### How does Combatant progress?

Combatant training guides purple nanofluid to reshape the body and nervous system. Finalization stores this pattern with the personality. Combat Overdrive actively stimulates those nanomachines for a brief combat boost.

**Selection and progress.** Complete Combatant research and set the pawn's SSC identity to Sex Slave on the Training tab. No bound master or chain stage is required; vanilla slave status alone does not qualify. Switching specializations preserves each path's progress, and selecting None pauses ordinary training. Ordinary bonuses are removed when switching away or selecting None; returning restores the saved progress and its bonuses. Final bonuses survive specialization changes, unbinding and chain regression.

**Display.** Combatant health labels use lavender, with a brighter final state. Training Officers use teal, or muted gray-green when the final state is disabled. Final Public Use and Cow names appear once each. Combat Overdrive has a dedicated female-silhouette icon with a ringed leather collar and violet neural accents.

**Passive bonuses.** Each stage uses its complete set of bonuses below. Final bonuses replace ordinary bonuses.

| Attribute | Basic: 20% | Proficient: 50% through ordinary completion | Finalized and implanted |
| --- | ---: | ---: | ---: |
| Raw shooting accuracy rating | +2 | +4 | +6 |
| Raw melee hit rating | +2 | +4 | +6 |
| Aiming time | ×0.95 | ×0.90 | ×0.85 |
| Melee attack cooldown | ×0.95 | ×0.90 | ×0.85 |
| Mental break threshold | −2 percentage points | −4 percentage points | −6 percentage points |
| Incoming damage | ×1.00 | ×0.90 | ×0.80 |

These bonuses begin at 20%. Accuracy ratings are converted through game curves; they are not direct percentage-point increases to hit probability. The mental break threshold reduction also applies outside combat.

**Experience.** Only SSC sex slaves currently training Combatant gain progress. Ordinary completion or prior finalization stops further gains. Progress in these formulas uses a 0–1 scale and is limited by the remaining progress.

| Source | Progress gained | Per-event cap |
| --- | --- | --- |
| Completed daily Training | `min(score / 2000, 0.04)` | 4 percentage points |
| Successfully completed full Binding Ritual | Use `ritual quality × 75` as the score in the same formula | 4 percentage points |
| Training by the actual bound master | Calculate the capped base reward above, then multiply by 1.5 | 6 percentage points |
| Direct kill | `min(actual body size × 0.01, 0.03)` | 3 percentage points |

Zero or negative scores grant no progress. Other eligible trainers grant the base reward; assigned trainers do not receive the master multiplier merely by being assigned. Interrupted Training and incomplete Rituals grant none. Other currently trained specializations use the same base formula; ordinary completion or finalization stops further progress. Self-training grants none.

Kill rewards have no minimum: body size 0.25 grants 0.25 percentage points, and body size 1 grants 1 point. Species, faction, hostility and downed status do not affect rewards. Direct hunting kills, executions, finishing blows and direct weapon/projectile explosions count. Slaughter, blood loss, ongoing fire, traps, environmental chain explosions and deaths from redirected bond damage do not. Indirect deaths do not credit a previous attacker.

**Finalization.** When the tab shows training complete and ready to finalize, keep Combatant selected, extract the personality gel, process it at a sculpting table and implant it. The recipe consumes one eligible gel and requires 3500 work; all eight existing gel bases are supported. The personality must have completed ordinary Combatant training without prior Combatant finalization. Other specialization histories and final achievements are preserved. Ordinary completion retains proficient bonuses and grants no ability by itself.

**Combat Overdrive.** Final combatants can activate it on themselves while drafted. The ability description summarizes its function, while the health condition automatically lists its effects. The ability's own multipliers are:

| Effect | Multiplier |
| --- | ---: |
| Melee damage | ×1.30 |
| Melee attack cooldown | ×0.75 |
| Ranged attack cooldown | ×0.80 |
| Incoming damage | ×0.75 |
| Movement speed | ×1.25 |
| Pain | ×0.50 |

The buff lasts 1 in-game hour. Its 4-hour cooldown starts on successful activation. Undrafting hides the command and prevents further activation, but an active buff continues. Ranged attack cooldown affects recovery between firing cycles, not the interval between shots within a burst.

Combined with final passive bonuses, this specialization alone gives incoming damage `0.80 × 0.75 = 0.60` and melee cooldown `0.85 × 0.75 = 0.6375`. Body training, equipment and other effects still contribute to the final result.

**Saves and personality transfer.** Normal saves preserve cooldown and remaining buff time. Personality extraction or replacement removes the old body's Combatant states, ability and active buff. Implanting a final personality grants a new ability without carrying over the old cooldown or active buff. A personality without Combatant achievements does not inherit them from its receiving body.


<a id="25-244-culture-rituals-and-cosmetic-reshaping-17-development-only"></a>

## 25. 2.4.4 Culture, Rituals and Cosmetic Reshaping (1.7 Only)

This section describes the 2.4.4 release for RimWorld 1.7. 1.6/Common remain frozen. Start with the [quick start](QUICK_START_EN.md#22-244-culture-and-cosmetic-reshaping-17-development-only).

### 25.1 Culture and actual identities

Master-slave Order is an impact-3 meme with six groups and 28 precepts: sex-slave attitudes (6), Training Officers (5), Masters (5), Master counts (3), bound-pawn counts (3), and gender rules (6). Gender rules cover female only, male only, unrestricted, female duty, male duty and non-Master duty. Current Masters are exempt from the duty; actual bonds and adult membership still matter.

Cultural checks use actual valid Chains. Training-tab identity remains an activity selection. Training Officers use existing role eligibility, and leadership uses valid vanilla office holders. Titles or faction-leader pointers alone do not establish office. Attitudes affect mood/opinion; they do not automatically alter work admission, willingness, qualification or bonds.

Master counts use the valid member roster. Each Master's living bound pawns are counted across maps and deduplicated. Excess counts accumulate up to three: Master-count mood -6/-15/-27; bound-pawn-count self mood -3/-9/-18; an observer's count precept also applies -5/-15/-30 opinion toward the Master. Culture, observer/self rules and vanilla final modifiers remain distinct.

Seven cultural icons, four-language text, ideology names, leader/moral-guide titles, Binding Ritual name presets and 17 paragraphs across six background frameworks are included. Dynamic deity/founder names and mixed backgrounds support new generation. Existing saved names remain; separate chief roles, further meme interactions and global performance work are still future scope.

### 25.2 Ritual development points

The global setting defaults to off, including configurations missing the key, and is shared across saves. When enabled, SSC Binding Rituals use the vanilla anytime-ritual curve: 0/0/1/2 points for the four outcome tiers. Fluid-ideology development conditions and the reform cap still apply; letters show the actual increase.

The setting is read at completion. Settlement happens once per whole ritual, not once per each of the six stages. Off hides this reward and prevents new points while preserving existing points and other effects. Past rituals are not backfilled; no separate cooldown is added.

### 25.3 Cosmetic eligibility and costs

Success improves vanilla Beauty along `-2 → -1 → no trait → +1 → +2`. Existing +2 or higher levels remain unchanged and block further treatment. Conflicting traits, gene-sourced/suppressed Beauty, duplicate/unknown levels and ongoing recovery are rejected. Neither route requires SSC identities or a bond. Ordinary Beauty follows the existing personality-gel transfer rules; the recovery reaction stays with the body, so moving into a recovering body does not bypass it.

| Requirement | Surgery | Reshaping Ritual |
| --- | --- | --- |
| Prerequisite | PNA Cosmetic Application, 1,500 research; Basic PNA Application + Sterile Materials | Master-slave Order; no research |
| Practitioner | Adult free colonist with medical work enabled and Medical 8 | Medical 6; follows the ritual ideology; distinct from recipient |
| Recipient | Eligible adult player-managed flesh humanoid, including colonists, colony slaves and prisoners | Eligible adult colonist/colony slave of the practitioner's ideology; no prisoners |
| Materials | 10 normal PNA + 3 industrial/glitterworld medicine | 20 normal PNA + 2 herbal medicine |
| Duration | 4,000 work, modified by medical work rules | 5,000-tick main stage, plus preparation |
| Quality | Vanilla anesthesia, medical quality and surgery limits | Vanilla ritual quality and a single weighted outcome; Medical replaces Social |

The ritual uses vanilla sky-lantern material preparation. The practitioner carries supplies in inventory, and a required stage consumes them before the main stage; outcomes do not charge again. Spectators are optional and may follow other ideologies. The pair stand adjacent, practitioner facing recipient and recipient facing spectators. Purple mist runs during the main stage and fades after ending/cancellation. Four name presets apply to newly created instances; saved names are retained. At Medical 6, baseline success is about 61.5%, subject to quality modifiers such as surroundings and spectators.

### 25.4 Recovery and failure

Both routes share a reaction that decays by 0.10 severity per day, with staged pain/work effects and remaining hours shown in health details. Initial severity: surgery success/failure 0.05/0.25; ritual success/failure 0.15/0.60. Natural recovery is approximately 12/60/36/144 hours respectively. Treatment is blocked until recovery, including after success.

Failure does not improve Beauty and causes a stronger reaction. No extra refunds, material rollback or independent cooldown are provided. Dead/destroyed recipients are skipped at outcome; a living recipient who loses eligibility receives the route's failure result. Interruption and occupancy follow vanilla systems.

### 25.5 Personality Excretion fix and verification

When its fixed interaction has no anatomical resolution, Personality Excretion now uses the existing empty-contact path, clearing stale parts while retaining the correct participants. It adds no identity/anatomy gate and does not relax ordinary Training resolution. The maintainer confirmed the originally reported female pair can complete the work; other combinations and interruptions are not inferred as independently tested.

Culture and cosmetics have batch acceptance, with final targeted results of 458/458 and 56/56 at their actual dates. They are not combined into a new full release gate. See [2.4.4 verification](Docs/Releases/2.4.4/2.4.4版本验证.md) for evidence and release status.

## 26. 2.4.5 body development and fertility control (1.7, unreleased)

The 2000-point Medieval internal-modification research uses a simple research bench. Both surgeries require Medicine 10, 4000 work, five concentrated PNA and six medicines. Vanilla anesthesia and surgical outcomes are retained, with direct-death failures disabled. Uterine surgery requires an eligible adult female human-flesh SSC slave who is not pregnant. Stomach surgery requires a valid stomach and mouth; gender and pregnancy do not exclude it. Medicine selection still respects patient medical-care settings.

Successful surgery starts independent body progress at 1%. Eligible development uses its own body-part evidence and the existing corruption/bond multipliers, with coefficient 0.5 and a ten-percentage-point cap per event, without an extra daily cap. Personality transfer does not carry body records; cloning follows existing health copying.

Uterine development blocks new conception. At completion, fertility remains off until enabled in body management. Enabling it gives ×2.5 vanilla fertility and ×0.8 duration for eligible new pregnancies, without rewriting existing pregnancies. The toggle cannot be disabled during pregnancy. Only SSC's own permanent-lactation fertility suppression is neutralized; other infertility factors and milk production remain unchanged. Health tooltips show actual fertility factors.

| Stomach progress | Hunger rate | Eating speed | Food poisoning |
| --- | --- | --- | --- |
| Below 30% | ×1 | ×1 | No reduction |
| 30% to below 70% | ×0.90 | ×1.10 | No reduction |
| 70% to below 100% | ×0.75 | ×1.25 | No reduction |
| 100% | ×0.50 | ×1.50 | Vanilla normal-ingestion immunity |

Stages replace earlier stages rather than multiplying cumulatively. External effects stack under vanilla rules; permanent lactation's separate nutrition cost is retained. Removed/replaced organs follow actual health-record lifecycle; lost progress is not restored from a hidden backup.

Core features have maintainer acceptance, and finalized four-language resources are implemented. Later body-resource deployment and display acceptance is also closed. The fertility-specialization candidate ability is outside this delivered scope. See the [version scope](Docs/Releases/2.4.5/2.4.5发布说明.md#english) and [verification record](Docs/Releases/2.4.5/2.4.5版本验证.md).


<a id="27-246-personal-abilities-and-romance-controls-17-unreleased"></a>

## 27. 2.4.6 personal abilities and romance controls (1.7)

### 27.1 Abilities, preferences and stages

Five personal abilities share queries and submission checks while keeping their original save owners: Bestow Bliss belongs to personality, damage sharing to its bridle, milk production to the original Training Comp with existing transfer exceptions, fertility to uterine development and gel rendering to body appearance. Original defaults and production/damage/rendering rules remain. Behavior permissions, identity, Training switches and color/opacity are separate settings.

Romance saves eleven preferences: three initiative behaviors, three initiative partner categories, two ordinary response permissions and three acceptance guarantees. Permissions default on and guarantees off. Effective Chain stage 2 unlocks initiative, stage 3 responses and stage 4 commands; tooltips use localized stage names. The controlled pawn needs an actual SSC identity and valid current bond. Hollow, transformation and carrier changes retain their existing restrictions.

Courtship/proposals require both behavior and partner permission. Partner classification prioritizes the pawn's actual Master, then SSC sex slaves, then others; breakup/divorce ignores the partner filter. The matching acceptance guarantee overrides its owner's ordinary response prohibition but cannot release the initiator's restriction or necessary interaction conditions. Turning off the other guarantees does not reclassify a partner as “other”. Forced rejection affects the real result; it does not fabricate a zero preview chance or remove an otherwise valid initiative target.

### 27.2 One-time orders, personal resets and personality

Named courtship, proposal and relationship-ending commands select a currently eligible target and use the existing interaction/job. A valid one-time authorization overrides only the executor's initiative preferences; it changes no saved preference and does not exempt the recipient's response or necessary conditions. Interruption ends the request; loading and carrier changes recheck ownership rather than granting a new carrier permission.

Behavior restrictions and romance each have a personal reset. The former uses the current global template and specialization defaults, with equipment/specialization rules still effective; the latter restores the eleven defaults above. Reset does not unlock stages, change research/global switches, clear relationships/history or edit the template. Disabled/stale UI requests cannot update a replacement personality or carrier.

Ordinary personality extraction, gel processing and implantation carry romance preferences and settled history. New clones receive default control preferences, while history follows the existing copying boundary. Losing a Chain stage suspends effects without deleting saved choices. Orders and pending events do not travel as authority over a new body.

### 27.3 Mood, logs and confirmed events

The controlled pawn has fourteen continuous branches and 108 event branches, plus 26 long-duration variants sharing text. Stage, effective traits and relationship context select branches. Existing values and 3/25/30-day durations remain; balance was not adjusted for this registration.

Event memories group by saved partner and five meanings: received courtship, commanded courtship, received proposal, commanded proposal and relationship ending. Different partners/groups coexist. A new real event in the same group replaces the branch, value and full standard duration regardless of strength/sign. Duplicate callbacks do not refresh it. Valid zero outcomes settle the matching group/stage set without a zero-value thought; technical failure preserves valid history. Relationship formation, engagement, rejected proposals retaining the relationship and relationship endings trigger directed cleanup.

Settlement requires completed interaction facts, confirmed final relationships and native admission. Matching vanilla mood additions/renewals/capacity effects are handled precisely; unrelated history and social parent memories remain. Restoring saved history does not recalculate, renew or replay relationship cleanup.

Three categories/thirteen log entries, participant-perspective history, vanilla letters, six task reports and five results share those facts. Presentation does not reroll outcomes. This does not add other observers' opinions or the newly proposed romance events.

### 27.4 Copy and validation boundaries

Each language has 270 display texts: 246 thought names/descriptions, thirteen logs, six task reports and five results. Simplified Chinese v1 has local in-game acceptance. Traditional Chinese, English and Russian have review approval and 590 targeted text checks, but await deployment/in-game display. Technical carrier fallbacks are separate. Final four-language PNA item/research guidance was deployed and accepted on 2026-10-27; it describes existing production, not a new output boost/facility.

Mechanisms, logs, personal resets and stage hints have batch acceptance. The historical 2.4.6 release build had aligned metadata; current VERSION/docs are 2.4.7 while runtime metadata remains 2.4.6; full release-gate and package results are recorded separately. See [verification and open scope](Docs/Releases/2.4.6/2.4.6版本验证.md).

<a id="ssc-247"></a>
## 28. 2.4.7 body controls and compatibility rules (1.7 development)

**Registered 2026-10-28; unreleased.** [Development notes](Docs/Releases/2.4.7/2.4.7发布说明.md#english) · [Commit audit](Docs/Releases/2.4.7/2.4.7开发归并核对.md) · [Validation](Docs/Releases/2.4.7/2.4.7版本验证.md).

### Body sensitivity

Off by default; the body holds the single saved choice. Editing requires an editable player-faction body, SSC Sex Slave identity and a valid actual Chain; stale menus are rejected. The Master's death or absence does not revoke a saved instruction. Unbinding, invalid identity or a Hollow body removes the SSC effect. Lower stages cap the effective level while preserving the saved choice, which can become effective again after advancement. Switching off removes the body record. Personality transfer does not carry it; Rabbit clones copy it only at birth, not again at maturity.

| Level | Minimum Chain stage | This source's frequency factor | Vulnerability offset |
| --- | ---: | ---: | ---: |
| Off | No effect | ×1 | 0 |
| 1 | 1 | ×2 | +0.30 |
| 2 | 2 | ×4 | +0.75 |
| 3 | 3 | ×8 | +1.50 |
| 4 | 4 | ×16 | +3.00 |

Ongoing mood follows effective level and Chain stage: stage 1 level 1 gives−3; stage 2 levels 1/2 give−1/−2; stage 3 levels 1/2/3 give+1/+2/+3; stage 4 levels 1/2/3/4 give+2/+3/+4/+6. Disabled or ineligible bodies receive no effect from this source.

The final **Orgasm Frequency Multiplier** is a general stat, default/minimum ×1. Valid external sources can affect RJW pawns without SSC identity. SSC sensitivity contributes one factor through the native stat pipeline and is not multiplied twice. Current supported timing paths supplement actual original progress, retaining fractional carry with validated task/cycle/final-factor restoration. There is no multiplier-based event/reward replay loop or scene extension. High factors remain limited by native event checks; independent third-party timers require explicit integration. See the [current integration specification](Docs/Development/Reference/高潮推进与敏感度接入规范.md).

### PNA production and status

The sixth common personal ability is available to bodies with the original ordinary PNA producer and Training Comp. It adds no research or bond requirement; management follows the tab's existing identity/self-training admission, including eligible colony prisoners. New producers and old saves missing the field default to enabled. The producer stores the choice; personality transfer and personal-rule resets do not clear it.

Pausing retains the current batch's Severity and stops natural accumulation, the eligible Cow maturation A contribution, associated extra nutrition costs and full-batch settlement. Other interaction effects and baseline hunger remain. Resuming continues from the current value with no catch-up. The indented percentage is progress toward the next batch, not item inventory. Disabled takes precedence over insufficient nutrition. The latter requires enabled production, an incomplete batch, a food need and less than the current interval's cost. Exact sufficiency, zero cost, no food need and full batches keep their original behavior; resumed growth waits for the native 60-tick interval.

### Directed attraction and UAP

Attraction only applies from an eligible SSC slave toward their actual Master; both must be at least 18 biological years old and satisfy the original adult classifications. After native Preflight and before final need/reservation modifiers, full voluntary appraisal receives a base floor of 1, retaining naturally higher values. Native zero reservation may still produce zero. Local opinion/willingness exceptions do not rewrite opinion, orientation or global settings and do not raise Master-side willingness. Native explanations remain with an SSC result appended. This neither guarantees execution nor forces automatic partner choice: body, desire, path, cooldown, work, pairing and SSC permissions still apply. Bond/personality changes invalidate old authorization and caches; PNA source preference remains separate.

UAP is an optional soft dependency; its files are not modified. With restrictions enabled, the whole group is checked pair by pair, and startup waits until all actual jobs are installed. A separate global manual override defaults to off and only applies to an explicit UAP window request, not arbitrary playerForced jobs. It bypasses the relevant SSC behavior permissions while retaining native matching/body requirements. Pre-commit failure cancels this session's precise jobs; old callbacks cannot claim new work. Loading cancels identified unfinished group drivers without resuming or replaying settlement. Cross-mod side effects already performed during initialization are not transactionally rolled back.

### Downed receivers and resonance

Only an already-started actual SSC receiver pair and the Binding Ritual slave role receive downing continuity, using original timing, stages and handoffs. Initial ritual candidates must still not be downed. Trainer downing, death/invalidity, player cancellation, ordinary RJW, Personality Excretion, self-training and third-party receiver drivers retain their original rules. Old interrupted rituals or removed roles are not reconstructed.

Each resonance query reuses one statistics result, preserving bond filtering, zero contribution for missing Corruption needs, thresholds, interpolation, display and refresh rules. It adds no cache or saved state. This reduces repeated query work; overall TPS/FPS improvement has not been measured.

### Version and remaining scope

Scoped in-game acceptance exists for these features; unreported language/mod combinations are not independently declared passed. Traditional Chinese/English/Russian romance display and other separate checks remain in the [verification index](Docs/Development/Verification/README.md). Automatic warm-up/slowdown, a standalone framework, PNA boosting/facilities and new social events remain future work. This registration performs no rebuild or full release gate.
