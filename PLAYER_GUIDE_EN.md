# RJW-SexSlaveCraft Complete Player Guide

> For RimWorld 1.6 and SexSlaveCraft 2.2.13, based on the current workspace code and installed Defs.\
> Audited on 2026-06-30.  
> Based on upstream 2.2.8; version 2.2.9 includes the specialization and ritual progression fixes, and 2.2.10 fixes stale training locks after interrupted rituals. See `CHANGELOG.md`.\
> Version 2.2.13 includes four confirmed fixes: implantation completing at a distance, the memory after gender reassignment surgery, stale UAP locks stopping ritual animations, and ritual fallback animation lookup. Fixes from 2.2.12 and earlier versions are included.\
> This guide describes the behavior implemented by the current C# and XML. Where an old changelog or description disagrees with the code, the discrepancy is listed under “Current Limitations and Known Differences.”

## 1. Scope and Dependencies

SexSlaveCraft, abbreviated SSC below, is built around this progression loop:

1. Set a humanlike pawn’s `Pawn Identity` to `Master` or `Sex Slave`.
2. Use ordinary `Training` to raise Corruption, opinion, and body-part experience while reducing Will.
3. Use the `Binding Ritual` to establish the master–slave bond, deepen the `Sex Slave Chain` health stage, and attempt slave conversion.
4. Develop either `Public Use Specialization` or `Cow Specialization`.
5. Use `Personality Excretion` to store a personality in `Personality Gel`, implant it into a Hollow, or edit it into a final specialization.
6. Use `Semi-gelatinization Surgery` and `Full Gelatinization Surgery` for high-risk body transformation.

Hard dependencies:

- Harmony
- RimJobWorld (RJW)

The `Binding Ritual` uses RimWorld’s ideology ritual system, so normal use generally requires Ideology. Rimworld Animations, RJW Onahole, Human Cattle, Equal Milking, RJW-PE, Humanoid Alien Races, and Sized Apparel are soft integrations rather than hard dependencies.

At startup, SSC injects its Training component, Training tab, and related surgeries into all Humanlike races. `Ninetailfox` and `Ninetailfoxwt` are also explicitly whitelisted.

## 2. Recommended Progression

### 2.1 Minimum Playable Route

1. Research `Training` for 500 research points.
2. Enable the `Training` work type on at least one free colonist.
3. Select a target and open the `Training` tab.
4. Set `Pawn Identity` to `Sex Slave` and enable `Allow Training`.
5. Choose an `Assigned Pose`; set an `Assigned Trainer` if a specific pawn must perform it.
6. Let the trainer work automatically or right-click the target to force the job.
7. Research `Body-part Training` to begin gaining body-part experience.
8. Add the `Binding Ritual` to an ideoligion and use it to advance the Chain health stage.

### 2.2 Mid- and Late-game Routes

- General route: `Training` → `Body-part Training` → `Personality Excretion` → semi/full gelatinization.
- Social and trade route: `Personality Editing (currently Public Use only)` → `Public Use` → `Public Use Specialization` → extract at 100% → edit into `Final Public Use Specialization` gel → implant.
- Milk route: raise Breasts to 70% → gain `Permanent Lactation Phase` → research `Milking Specialization` → `Cow Specialization` → edit into `Final Cow Specialization` gel.

## 3. The Training Tab

The tab is available for colonists, colony prisoners, and slaves.

### 3.1 Pawn Identity

| Identity | Function |
|---|---|
| Unset | Hides normal Training configuration |
| Sex Slave | Enables Training, specialization, pose, and trainer controls |
| Master | Cannot be an ordinary Training target; can fill the master role in a Binding Ritual |

`Master` is mainly a ritual-role requirement. An ordinary trainer only needs to be a free, standing colonist with the `Training` work type enabled; the trainer does not have to be marked as a Master.

### 3.2 Allow Training

The Training WorkGiver scans a pawn only when `Allow Training` is enabled.

Enabling it immediately runs an RJW receiver-eligibility check. A failed check does not turn the option back off, but eligibility is checked again before the job starts.

### 3.3 Allow Others

`Allow others to train or have sex` removes the bonded master’s exclusive claim over trainers and consensual partners.

- Selecting `Public Use Specialization` enables it automatically.
- Selecting `Cow Specialization` disables it automatically.
- Selecting no specialization disables it and resets specialization progress.
- Manually disabling it on a Public Use pawn does not remove the specialization; it only shows a warning.

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

A candidate trainer must be:

- a free colonist;
- alive and not downed;
- assigned to the `Training` work type.

If the target has a `Sex Slave Chain` and does not allow others, its bonded master is the only valid trainer. Public Use or the manual allow-others option removes this restriction.

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
| 10%–29.99% | Imprint (Pain) | 30% | ×1.1 |
| 30%–49.99% | Sex Slave | 50% | ×1.3 |
| 50%–89.99% | Imprint (Adaptation) | 90% | ×1.6 |
| ≥ 90% | Meat Toiletization | 100% | ×2.0 |

The brake cap is the next Corruption threshold this Chain stage is allowed to reach. After reaching 30%/50%/90%, the pawn still needs a Binding Ritual resolution to raise Chain severity. Gameplay checks use the Chain health stage; the Sex Slave trait is synchronized only from highest-ever Corruption and is not an authoritative gameplay predicate.

Minimum-Corruption effects from apparel or health only activate when this pawn’s historical maximum has already reached that floor, and only if that floor is inside the current Chain-stage interval. They preserve milestones already reached in the current interval; they do not let a low Chain stage jump directly to a later one.

Chain stage stat effects:

| Stage | Capacities | Market value |
|---|---|---:|
| Imprint (Pain) | Consciousness, Talking, Manipulation each -0.3 | ×0.9 |
| Sex Slave | Consciousness -0.2, Talking -0.2, Manipulation -0.1 | ×0.8 |
| Imprint (Adaptation) | Consciousness +0.1, Talking +0.1 | ×0.7 |
| Meat Toiletization | Consciousness +0.3, Talking +0.3, Manipulation +0.2 | ×0.5 |

Every Chain stage grants immunity to PNA addiction.

Persistent mood by stage:

| Stage | Mood |
|---|---:|
| Imprint (Pain) | -10 |
| Sex Slave | -5 |
| Imprint (Adaptation) | +3 |
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
- The ritual master is a free colonist of the player faction.
- The ritual master’s `Pawn Identity` is `Master`.
- The target is a colonist, colony slave, or colony prisoner.
- The target is alive, not downed, and passes RJW receiver eligibility.
- The target is not marked as a Master.
- The target has an `Assigned Trainer`.
- The assigned trainer is the ritual master.
- If the target has a non-Public-Use Chain, its existing master is also the ritual master.
- Both pawns can reach the ritual location.

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
| Spectators | 1 → +5%; 5 → +15%; 10 → +20% |
| Room impressiveness | 0 → 0%; 50 → +5%; 120 → +10% |
| Genital compatibility | score 0/1/2/3/4/5 → -20%/-10%/-5%/+5%/+15%/+30% |
| Master Social skill | 0/5/10/15/20 → -20%/0%/+20%/+30%/+40% |
| Repeated within three days | -20% |

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
- fill time: 15,000 ticks, about six hours;
- nutrition cost: 0.3 per day;
- while hungry, gain is scaled to available nutrition;
- the state does not expire from lack of nursing;
- lactation can be disabled in the Training tab;
- fertility factor is 0.05, or 1 when Human Cattle controls the system.

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

## 11. PNA Items and Basic PNA Launcher

### 11.1 Ingestion

| Item | `PNA Infection` | Addiction chance |
|---|---:|---:|
| Purple Aphrodisiac Nanofluid | +0.3 | 25% |
| Purple Aphrodisiac Nanofluid Plus | +1.0 | 50% |

`PNA Infection`:

- at 0.1: Manipulation -0.2 and Moving -0.2;
- at 1.0: Consciousness is capped at 10% and the pawn enters continuous orgasm;
- naturally decays by 1.0 per day.

`PNA Dependence`:

- begins at severity 0.5;
- decays by 0.066 per day, disappearing naturally in about 7.6 days;
- its need falls by 0.333 per day;
- withdrawal mood can reach -25;
- any pawn with a Sex Slave Chain is immune.

### 11.2 PNA Concentration Technology

At a drug lab:

- 5 normal PNA → 1 PNA Plus; work 450;
- 20 normal PNA → 4 PNA Plus; work 1,350.

Both recipes require `PNA Concentration Technology`.

### 11.3 Basic PNA Launcher

The enabled low-tier launcher:

- costs 50 steel, 2 components, and 20 normal PNA;
- crafting skill 4;
- requires vanilla Machining, but does not check SSC’s `Basic PNA Launcher` research;
- range 22.9;
- warmup 0.5 seconds;
- cooldown 3 seconds;
- base projectile damage 2.

When it hits a pawn with a Sex Slave Chain:

- heals a random naturally healable injury by 15;
- applies `PNA Combat Enhancement (Concentrated)`: Moving +0.5 and Manipulation +0.5;
- does not add the PNA debuff or extra direct damage.

The enhancement is added at severity 1.0 and decays by 2.0 per day, lasting about half a day.

Against an unchained target:

```text
PNA debuff gain = min(0.125 / resistance divisor, 1.0)
```

The resistance divisor normally uses Consciousness with a floor of 0.2. Consciousness above 2.0 creates a resistance mote. The hit also deals 1 armor-ignoring damage.

The high-tier PNA gun and ammunition XML are commented out and do not normally appear.

## 12. Public Use Specialization

### 12.1 Unlock and Selection

Complete `Public Use`. Selecting it:

- enables `Allow others to train or have sex`;
- applies the ordinary Public Use Hediff;
- restores saved specialization progress, with a minimum displayed Hediff severity of 1%;
- advances only while the active specialization type remains Public Use.

The Training tab’s internal option is named `Bus`, but the official player-facing term is `Public Use Specialization`.

### 12.2 Progress Sources

- Completing any RJW act with someone other than the master: specialization progress `+5%`.
- Ordinary Training: ordinary Public Use Hediff severity gains half of that session’s Corruption gain.
- Trade with a pawn trader: ordinary Public Use Hediff gains roughly `10% + calculated Corruption gain`.
- Trade with a faction settlement: ordinary Public Use Hediff `+10%`.
- Orbital trade ship: no effect.

“Other than the master” first checks the Chain master. If Public Use has removed exclusive ownership, the system falls back to `Assigned Trainer`.

The tab’s saved progress and direct Hediff severity changes are separate paths and can desynchronize; see section 22.

### 12.3 Stats

| Hediff severity | Trade price | Negotiation | Social impact | Talking |
|---:|---:|---:|---:|---:|
| 20% | +0.10 | +0.13 | +0.17 | +0.03 |
| 50% | +0.20 | +0.26 | +0.33 | +0.07 |
| Final | +0.30 | +0.40 | +0.50 | +0.10 |

### 12.4 Post-trade Event

This event runs only when the Public Use pawn is the player’s negotiator.

After trading with a pawn trader:

```text
consensual-sex chance =
    100%                                  if Corruption ≥ 20%
    round(10 + Corruption / 20% × 90)     otherwise
```

The remaining probability is split approximately 8:1 between assault and trader forgiveness.

An actual scene also requires:

- both pawns on the same map and within 15 cells;
- the trader not fighting or in a mental state;
- both pawns passing the appropriate RJW checks;
- the matching RJW JobDef being available.

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

Older gels did not record gene sources. Their saved entries are restored as personality traits because traits granted by the original body's genes can no longer be identified; the receiving body's genes are still preserved. See section 19 and the [fix details (Chinese)](Docs/人格普通特质迁移修复.md).

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

`Sex Slave Bridle` gives the master a toggle, enabled by default:

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

At more than 0.1% Corruption, a chained Sex Slave with a resolvable master can bypass vanilla lover restrictions and use the master’s ordinary multi-person bed.

Master resolution order:

1. Chain master;
2. `Assigned Trainer` if there is no Chain.

Bed capacity, reachability, reservation, allowed area, ideology rules, non-medical status, non-prisoner status, and at least two sleeping slots still apply.

When sharing the bed, the current negative slept-in-bedroom/barracks memory is removed and replaced with:

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

A vanilla slave whose historical maximum Corruption has ever exceeded zero is permanently treated as an SSC Sex Slave, even if current Corruption later decays to zero:

- has the vanilla slave disabled-work list cleared;
- no longer receives the vanilla slave work-speed StatPart.

### 18.6 Sex-protection Rules

With the global protection setting enabled:

- a chained Sex Slave cannot initiate assault;
- consensual sex for an ordinary chained Sex Slave is master-only;
- `Allow others to train or have sex` removes the consensual exclusivity;
- a Public Use pawn is not protected when it is the victim;
- a Public Use pawn may have consensual sex with anyone, but by default cannot initiate assault;
- under strict defaults, only the master may assault an ordinary chained Sex Slave;
- if `Sex slaves can be raped` is enabled, anyone may do so, but `Evilfall Combat Suit` blocks it.

LifeForce `randomrape` and `seduced` jobs are directly whitelisted and bypass SSC protection.

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
- blocks assault when `Sex slaves can be raped` is enabled.

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
| Enable sex-slave protection rules | On | Global protection master switch |
| Sex slaves can be raped | Off | Off keeps master exclusivity; on permits anyone, except when blocked by Evilfall Combat Suit |
| Public Use cannot initiate rape | On | Restricts a Public Use pawn as aggressor |
| Chained sex slaves cannot initiate rape | On | Restricts an ordinary chained pawn as aggressor |
| Non-rape acts only allow the master | On | Restricts consensual acts |
| SSC logging | On | Custom normal logs |
| High-frequency debug logging | Off | Guard, animation, and Training-flow logs |
| Low-frequency key logging | On | Onahole, ritual conversion, and important fallbacks |
| Use RJW original eligibility for training age checks | On | Current code branch is reversed relative to the displayed explanation |
| Debug Gizmos | Off | Quick-test buttons in developer mode |
| Full-gelatinization body coloring | On | Purple translucent body while preserving original head color |
| Allow ritual conversion into slavery | On | Disables only ritual slave conversion, not other ritual rewards |
| Enable Corruption decay | On | Off stops natural loss; apparel floors and Chain caps remain active |
| Base Corruption decay per day | 2% | Adjustable from 0% to 20% per day in 0.5% steps |
| Use old scoring | Off | Switches to old score, memories, and no natural Corruption decay |

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
4. **Several research nodes are technology-tree placeholders.** `Basic PNA Application`, `Basic PNA Launcher`, `Femboy Conversion`, pet cat/dog/rabbit, and `Combatant` do not unlock a complete matching system. `Personality Editing (currently Public Use only)` mainly acts as the parent node for Public Use and Cow research.
5. **Basic PNA Launcher checks vanilla Machining only.** Its recipe does not reference SSC’s launcher research.
6. **Sex Reassignment Surgery is not research-locked.** Its operation exists without a `researchPrerequisite`. Its companion ThoughtDef XML also contains a duplicate `defName`, which may cause a load issue for the success memory.
7. **Personality trait transfer and the gel UI have been fixed.** The maintainer has confirmed this round of fixes is effective. Ordinary personality traits are now restored from the gel snapshot while preserving the receiving body's genes and their traits. Legacy entries are restored as personality traits because their source was not recorded; new gels save only traits without a gene source. The Sex Slave trait is still rebuilt separately from restored highest-ever Corruption. See the [fix record (Chinese)](Docs/人格普通特质迁移修复.md) for details.
8. **Public Use has two potentially desynchronized progress values.** The tab uses `specializationProgress`, while trade and some Training logic directly change Public Use Hediff severity. Later tab synchronization can overwrite Hediff-only gains. Final recipes check the tab’s saved progress.
9. **Switching directly between Public Use and Cow preserves progress and the other family’s Hediff.** Only selecting no specialization resets progress. This permits coexistence and may also carry progress across types.
10. **Final Cow does not cause full gelatinization.** Old notes mention this linkage, but current Defs and C# do not add the completed state.
11. **Apparel decay multipliers run in the opposite direction from their descriptions.** Values 1.1 and 1.5 are multiplied into decay and therefore accelerate it. Their 30%/100% floors activate only after that pawn has historically reached the matching value.
12. **The RJW-original age/eligibility setting is wired in reverse.** On uses `LegacyCanBeFucked` and additionally requires RJW `rape_enabled`; off directly calls `xxx.can_be_fucked`. Toggle it when an apparently valid target cannot be trained.
13. **Chain progression requires reaching the threshold, then resolving a ritual.** Chain starts at 20%, and the current stage brakes Corruption at 30%. After reaching 30%, another Binding Ritual resolution is needed to raise Chain severity. The Sex Slave trait is synchronized only from highest-ever Corruption. Minimum-Corruption effects such as Apparel Bulletproof Suit preserve only milestones already reached inside the current Chain interval; they do not skip stages.
14. **Some Public Use protection patches check only the ordinary Hediff.** A Final Public Use pawn usually retains Bus specialization data and allows others, but early branches for full victim exemption and inability to initiate assault search specifically for the ordinary Public Use Hediff.
15. **The high-tier PNA gun is disabled.** Its XML is commented out; only the low-tier launcher is normally craftable.

-->

## 23. Research Tree

| Research | Cost | Prerequisite | Current gameplay function |
|---|---:|---|---|
| Training | 500 | None | Training tab features and WorkGiver |
| Body-part Training | 800 | Training | Six body-part experience tracks |
| Basic PNA Application | 800 | Training | No direct current reference |
| Sex Reassignment Surgery (Male to Female) | 1,000 | Basic PNA Application | Node does not actually lock the operation |
| Femboy Conversion | 1,200 | Sex Reassignment Surgery | Not fully implemented |
| Basic PNA Launcher | 1,000 | Basic PNA Application, Precision Rifling | Launcher recipe does not reference it |
| PNA Concentration Technology | 1,200 | Basic PNA Application | Converts normal PNA to PNA Plus |
| Personality Excretion | 1,500 | PNA Concentration Technology | Preparation operation and gel extraction |
| Personality Editing (currently Public Use only) | 1,500 | Body-part Training, Personality Excretion | Parent node for Public Use and Cow |
| Milking Specialization | 1,200 | Personality Editing (currently Public Use only) | Cow Specialization and Final Cow recipe |
| Public Use | 1,200 | Personality Editing (currently Public Use only) | Public Use Specialization and final recipe |
| Pet: Cat | 1,200 | Personality Editing (currently Public Use only) | Not implemented |
| Combatant | 1,200 | Personality Editing (currently Public Use only) | Not implemented |
| Pet: Dog | 1,500 | Pet: Cat | Not implemented |
| Pet: Rabbit | 1,800 | Pet: Dog | Not implemented |
| Partial Gelatinization | 2,000 | Personality Excretion | Semi-gelatinization Surgery |
| Full-body Gelatinization | 3,000 | Partial Gelatinization | Full Gelatinization Surgery |

## 24. Troubleshooting

### No Training option appears

Check, in order:

1. `Training` research is complete.
2. The trainer has the `Training` work type enabled.
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
