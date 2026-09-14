# RJW-SexSlaveCraft Quick Start Guide

> For RimWorld 1.6 and SexSlaveCraft 2.2.11.\
> This continuation is based on upstream 2.2.8. See `CHANGELOG.md` for release history.\
> Version 2.2.11 includes three groups of fixes: ordinary personality trait restoration and the gel UI/localization; specialization history and actual memory values during personality transfer; specialization completion display and permanent lactation at full progress.\
> In-game names follow the mod's official English localization. For exact formulas, thresholds, and implementation notes, see `PLAYER_GUIDE_EN.md`.

## 1. What the Mod Does

SexSlaveCraft (SSC) is built around this progression:

1. Designate Masters and sex slaves.
2. Use ordinary Training to raise Corruption and develop body parts.
3. Perform the Binding Ritual to establish a bond and advance the `Sex Slave Chain` health stage.
4. Develop either Public Use Specialization or Cow Specialization.
5. extract a pawn's personality into Personality Gel, edit it, or implant it into a Hollow.
6. Attempt Semi-gelatinization or Full Gelatinization.

Required mods:

- Harmony
- RimJobWorld (RJW)

The Binding Ritual also requires the ideology ritual system to be available.

## 2. Getting Started

### Step 1: Research Training

`Training` is the entry point for the mod. Complete it before trying to configure pawns.

### Step 2: Assign a Trainer

Enable the `Training` work type for at least one free colonist.

A good trainer generally has:

- high Social skill;
- good physical condition;
- a positive relationship with the target;
- usable RJW sex parts.

### Step 3: Configure the Target

Select the intended target and open the `Training` tab:

1. set `Pawn Identity` to `sex slave`;
2. enable `Allow Training`;
3. select an `Assigned Pose`;
4. optionally choose an `Assigned Trainer`.

The target may be a colonist, prisoner, or slave, but must pass RJW's sex-target eligibility checks.

### Step 4: Start Training

Trainers may take the job automatically. You can also right-click the target and order it manually.

After successful ordinary Training, the target enters a cooldown of roughly nine in-game hours.

## 3. What Training Provides

Ordinary Training can:

- increase Corruption;
- change the target's opinion of the trainer;
- create mood memories;
- reduce a prisoner or colonist's will;
- develop the body part associated with the selected act;
- contribute to Public Use or Cow systems.

Training quality is mainly affected by:

- the trainer's Social skill;
- the target's opinion of the trainer;
- genital size compatibility;
- vanilla slave status;
- the target's Corruption and chain stage.

A high-Social Master is also the best choice for the Binding Ritual.

## 4. Corruption and the Bond

Corruption represents how deeply the target has been trained.

Once Corruption reaches the first bond threshold:

- the Master receives `Sex Slave Bridle`;
- the target receives `Sex Slave Chain`;
- the chain records the target's owner;
- the Master gains `Libidinal Resonance Field`;
- ownership restrictions, rebellion suppression, and shared-bed rules begin.

Corruption uses a default base decay of 2% per day. The mod settings can disable it or adjust it from 0% to 20% per day; recent Training quality, opinion of the Master, body-part development, and deeper Chain stages continue to modify the actual loss.

The current Chain stage brakes Corruption at the next threshold: the first stage can reach 30%, the second can reach 50%, and the third can reach 90%. After reaching a threshold, a Binding Ritual resolution is still required to advance the Chain stage.

## 5. Binding Ritual

The Binding Ritual is the main way to advance the Chain health stage. The Sex Slave trait is synchronized only from highest-ever Corruption and is not used as a gameplay predicate.

### Setup

1. Add `Binding Ritual` to the colony ideology.
2. Set the ritual leader's `Pawn Identity` to `Master`.
3. Set the target's identity to `sex slave`.
4. Assign the ritual leader as the target's trainer.
5. Choose a reachable ritual location.

### Ritual Sequence

A complete ritual performs six acts:

1. Handjob
2. Footjob
3. 69
4. Boobjob
5. Anal
6. Vaginal

All six phases must finish. An interrupted ritual does not receive the final SSC outcome.

Starting with 2.2.10, cancellation or departure of the Master or target releases the ritual lock.
Daily training can resume subject to its usual eligibility, schedule, and cooldown rules.
A new ritual starts at phase one; phase changes and save/load within the same active ritual preserve progress.

### Improving Ritual Quality

- use a Master with high Social skill;
- invite more spectators;
- improve room Impressiveness;
- avoid repeating the ritual within three days.

A completed Binding Ritual can:

- grant a large amount of Corruption and full-body development;
- reduce will;
- advance the Sex Slave Chain;
- synchronize the Sex Slave trait milestone mapped from highest-ever Corruption;
- attempt ritual enslavement;
- create a special Master/Perfect Sex Slave relationship at high opinion.

## 6. Body-part Training

Research `Body-part Training` before ordinary Training can grant body-part experience.

| Act | Developed part |
|---|---|
| Handjob, Mutual Masturbation | Hands |
| Footjob | Feet |
| Oral | Mouth |
| Boobjob | Breasts |
| Vaginal, Fingering, Fisting, 69 | Genitals |
| Anal, Rimming | Anus |

The six development tracks broadly provide:

- `Corruption Mark (Hands)`: work, shooting, and weapon handling;
- `Corruption Mark (Feet)`: movement, dodge, and carrying capacity;
- `Corruption Mark (Mouth)`: social impact, trade, and conversion;
- `Corruption Mark (Breasts)`: beauty, animal work, and Permanent Lactation;
- `Corruption Mark (Vagina)`: rest recovery, pain resistance, and Purple Aphrodisiac Nanofluid production;
- `Corruption Mark (Anus)`: healing, immunity, and pain resistance.

The Binding Ritual develops all six tracks at once.

## 7. PNA and Lactation

### Purple Aphrodisiac Nanofluid

At maximum genital development, a pawn begins producing `Purple Aphrodisiac Nanofluid`.

It is used for:

- direct ingestion;
- crafting the `Basic PNA Launcher`;
- producing `Purple Aphrodisiac Nanofluid Plus`;
- Personality Excretion;
- Full Gelatinization Surgery.

Ingestion causes `PNA Infection` and may cause `PNA Dependence`. Pawns with Sex Slave Chain are immune to PNA Dependence.

### Permanent Lactation

High breast development grants `Permanent Lactation Phase`.

- milk production consumes nutrition;
- production stalls during starvation;
- lactation can be disabled from the Training tab;
- Cow Specialization requires Permanent Lactation Phase.

When Cow Specialization is active, completed sex scenes accelerate milk production and specialization progress.

## 8. Public Use Specialization

Complete `Public Use` research, then select `Bus` in the Training tab.

Public Use Specialization:

- automatically enables sex and Training with other pawns;
- gains progress from sex with someone other than the owner;
- triggers special events after trading as the negotiator;
- improves trade, negotiation, Social Impact, and Talking.

After a pawn-to-pawn trade, the negotiator may:

- initiate consensual sex;
- be raped by the trader;
- be released without sex.

Higher Corruption makes the consensual outcome more likely. Orbital trade ships do not trigger this mechanic.

Reaching 100% does not directly create `Final Public Use Specialization`. Use the Personality Gel workflow described below.

## 9. Cow Specialization

Cow Specialization requires:

- `Milking Specialization` research;
- `Sex Slave Chain` health stage 2 or higher (severity at least 30%);
- `Permanent Lactation Phase`.

Cow progress comes from produced or consumed milk and adds an extra milk reservoir.

Tradeoffs:

- reduced movement;
- reduced Manipulation;
- increased Vulnerability.

Benefits:

- faster milk production;
- Beauty at higher stages;
- rapid milk acceleration after sex.

At 100%, use Personality Excretion and Personality Gel editing to obtain `Final Cow Specialization`.

## 10. Final Specializations

Both Final specializations use the same workflow:

1. raise Public Use or Cow Specialization to 100%;
2. perform Personality Excretion on that pawn;
3. obtain Personality Gel containing the specialization data;
4. process the gel at a sculpting table;
5. implant the edited gel into a Hollow.

Final specialization does not upgrade the original body directly.

## 11. Personality Excretion

### Preparation

After researching `Personality Excretion`, schedule `Induce Personality Excretion` from the Health tab.

The target receives `Preparing Personality Excretion`. It progresses naturally, while Anal sex accelerates it.

### Extraction

At 100% preparation:

1. use a colonist with Training work enabled;
2. right-click the target;
3. order Personality Excretion;
4. complete the fixed Anal scene.

After completion:

- Personality Gel spawns on the ground;
- the original body becomes a Hollow;
- the Hollow loses normal identity and social function;
- the Hollow enters a short shutdown period.

Deeper Sex Slave Chain stages produce higher-grade gel:

- `Personality Gel`
- `Basic Personality Gel`
- `Advanced Personality Gel`
- `Perfect Personality Gel`

### Stored Data

Personality Gel stores the source pawn's:

- name;
- skills and passions;
- memories and direct relations;
- backstories;
- ordinary personality traits and their degrees, including ordinary traits temporarily suppressed by genes;
- current Corruption, historical maximum Corruption, and bound Master;
- highest-ever Corruption and its derived Sex Slave trait milestone;
- Public Use or Cow data.

It stores personality rather than flesh. It does not copy the receiving body's appearance, age, genes, or body parts.

## 12. Personality Implantation

1. Select Personality Gel.
2. Open its personality-card tab.
3. Choose `Assign Target Hollow`.
4. Wait for a colonist with Training work enabled, or manually order insertion.

Successful implantation:

- consumes the gel;
- restores the stored identity data;
- restores Corruption, bond, skills, memories, and specialization;
- applies `Personality Implantation Adaptation Syndrome` for about one day.

Implantation replaces ordinary personality traits with the gel's snapshot, including when returning to the original body. The receiving body's genes and their traits remain intact; the Sex Slave trait is rebuilt separately from highest-ever Corruption. New gels exclude gene-granted traits, while older gels restore saved entries without knowing their original sources. A missing trait snapshot rejects implantation and preserves the gel. See the full guide for details.

## 13. Semi-gelatinization

After researching `Partial Gelatinization`, schedule `Semi-gelatinization Surgery` on an arm or leg.

The process creates a race:

- Adaptation reaches 100% first: the modification succeeds;
- Severity reaches 100% first: the limb is destroyed.

Higher Corruption strongly improves Adaptation.

Success produces:

- `Gelatinized Arm`: better Manipulation and armor, plus healing for injuries on that arm;
- `Gelatinized Leg`: better Moving and armor.

Parts with `Semi-gelatinization in Progress` are also preferred targets for redirected Master damage.

## 14. Full Gelatinization

Full Gelatinization is a high-risk endgame conversion.

Requirements:

- `Full-body Gelatinization` research;
- a Hollow with `Personality Excretion (Complete)`;
- Industrial medicine and Purple Aphrodisiac Nanofluid Plus;
- a skilled doctor.

Success depends on:

- Corruption;
- Hands, Feet, Mouth, Breasts, Genitals, and Anus development.

Only 100% Corruption and 100% in all six tracks guarantee success.

On success, `Full Gelatinization Complete`:

- removes most previous non-SSC, non-RJW health conditions;
- greatly improves Manipulation, Moving, Consciousness, armor, and regeneration;
- can tint the body translucent purple.

If Severity wins, the pawn is erased completely and leaves no recoverable corpse.

## 15. Benefits for the Master

### Libidinal Resonance Field

The field becomes stronger as the Master binds more sex slaves and their average Corruption rises.

It improves:

- Manipulation;
- melee hit and dodge;
- Move Speed;
- pain resistance;
- negotiation and Social Impact at high stages;
- incoming damage at the highest stage.

### Damage Sharing

`Sex Slave Bridle` provides a `Damage Sharing` toggle.

When enabled, eligible bound sex slaves on the same map absorb most incoming damage for the Master. Higher-Corruption slaves carry more weight.

Execution, surgery, EMP, and most toxic damage are not redirected.

## 16. Other Effects on Bound Sex Slaves

A pawn with Sex Slave Chain:

- cannot join prison breaks;
- cannot join slave rebellions;
- has Berserk suppressed;
- permanently loses vanilla disabled-work and slave work-speed penalties once historical maximum Corruption exceeds zero;
- may share a normal double bed with the Master;
- receives shared-bed mood based on Corruption and opinion;
- gains increasingly positive rape memories at high Corruption.

By default, a normal chained sex slave may only have consensual sex with the owner. Public Use or `Allow others to train or have sex` can lift that restriction.

## 17. Special Apparel

### Apparel Bulletproof Suit

- inexpensive entry-level outfit;
- basic protection;
- holds Corruption at a minimum of 30%;
- unlocks that floor only after the pawn’s historical maximum Corruption has reached 30%;
- useful for maintaining a target that has already reached 30%;
- the floor only applies inside the current Chain-stage interval.

### Evilfall Combat Suit

- requires `Perfect Sex Slave`;
- strong armor and combat bonuses;
- holds Corruption at 100%;
- unlocks that floor only after the pawn’s historical maximum Corruption has reached 100%;
- protects against rape when the setting allowing outside rape is enabled;
- the floor only applies inside the current Chain-stage interval.

The current decay-multiplier direction does not match the item descriptions. Minimum-Corruption effects preserve only milestones the pawn has already reached inside the current Chain interval.

## 18. Important Settings

For a first game, keep defaults and review:

- `Enable sex-slave protection rules`;
- `Allow sex slaves to be raped`;
- `Only the owner may initiate non-rape sex`;
- `Allow ritual enslavement`;
- `Enable Corruption decay`;
- `Base Corruption decay per day` (default 2%);
- `Enable Full Gelatinization Body Tint`;
- `Use Old Scoring`.

If a valid-looking target cannot be trained, try toggling `Use RJW original eligibility for training age checks`. The code branches behind this option are reversed relative to the displayed explanation.

## 19. Compatibility

- `RJW Onahole`: preserves the Onahole receiver job during SSC scenes.
- `Rimworld Animations`: supports ritual animations and hides weapons during animation.
- `Human Cattle`: becomes the authoritative milk-reservoir system.
- `Equal Milking`: recognizes Permanent Lactation Phase when Human Cattle is absent.
- `RJW-PE`: reads age configuration for eligibility diagnostics.
- `Humanoid Alien Races`: Sex Reassignment Surgery uses allowed body types.
- `LifeForce`: conflicting SSC body states remove the LifeForce gene and drop it in a genepack.

### RimTalk and Scheduled Training

RimTalk is optional. Enable `Trigger RimTalk dialogue for SSC sex scenes` in SSC settings, then use each Sex Slave's Training tab to choose a two-hour Training window and a frequency of once every 1–7 days.

- Automatic Training obeys both the timetable and the roughly nine-hour cooldown.
- A forced order bypasses only the scheduled date/window, not cooldown or safety checks.
- When the job is claimed, SSC displays a customizable local interruption using `{MASTER}`, `{SLAVE}`, `{TARGET}`, and `{ACT}`. This costs no tokens.
- When the sex scene actually starts, SSC waits for RimTalk to become idle and submits one multi-turn Event request.
- Binding Ritual phases, Personality Excretion, and Public Use trade sex also trigger direct RimTalk Event requests.
- Without RimTalk, or with the SSC integration toggle disabled, scheduled Training still works and no SSC dialogue request is made.

An active RimTalk stream cannot be redirected after its prompt has been sent. SSC therefore displays the local interruption first, blocks new unrelated dialogue from taking the next generation slot, and waits for the current stream to finish.

<!-- Not-fully-implemented checklist temporarily hidden.
## 20. Content Not Fully Implemented

The following entries exist as research, defs, or placeholder code but do not currently form complete gameplay systems:

- Fine Training and sensitivity discovery;
- Pet: Cat, Pet: Dog, and Pet: Rabbit;
- Combat Unit;
- Femboy Conversion;
- high-tier PNA weapon;
- automatic granting of `Erotic Word`;
- Genital Size Compatibility in the Binding Ritual.

Also note:

- `Basic PNA Launcher` research does not currently gate the weapon;
- Sex Reassignment Surgery is not gated by its SSC research;
- Final Cow Specialization does not automatically grant Full Gelatinization Complete;
- trade-based Public Use growth and tab specialization progress can become desynchronized.

-->

## 21. Quick Troubleshooting

### No Training option

Check:

- `Training` research;
- the trainer's Training work assignment;
- target identity and `Allow Training`;
- the nine-hour cooldown;
- Assigned Trainer;
- Sex Slave Chain ownership;
- RJW target eligibility.

### Corruption reached a threshold but Chain did not advance

Push Corruption to the current Chain stage’s brake cap, then complete a Binding Ritual. The first Chain stage can reach 30%, but Chain severity advances only during ritual resolution. If Corruption is still being forced back to 10%, confirm that the latest DLL is loaded.

### Body-part experience does not increase

Complete `Body-part Training` and select an act mapped to the intended part.

### Cow cannot be selected

You need Milking Specialization research, `Sex Slave Chain` health stage 2 or higher (severity at least 30%), and Permanent Lactation Phase.

### Specialization reached 100% but did not upgrade

Perform Personality Excretion, edit the gel at a sculpting table, then implant it into a Hollow.

### Personality Excretion is ready but nobody performs it

The extraction job requires a manual right-click order.

### Full Gelatinization keeps failing

It remains risky until Corruption and all six body-part tracks reach 100%. Wait for the game to report guaranteed success.
