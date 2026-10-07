# RJW-SexSlaveCraft Quick Start Guide

[Project home](README.md) · [Documentation index / 文档索引](Docs/README.md) · [中文快速入门](读我，玩法介绍.md) · [Changelog](CHANGELOG.md)

> For RimWorld 1.6 and SexSlaveCraft 2.3.6.\
> This continuation is based on upstream 2.2.8. See `CHANGELOG.md` for release history.\
> Version 2.3.6 adds shared base specialization progress from completed daily Training and full Binding Rituals. The 2.3.5 self-training and Training handoff updates remain included. Download the installation ZIP and SHA-256 checksum from [v2.3.6 Release](https://github.com/RavenHu001/RJW-SexSlaveCraft/releases/tag/v2.3.6). Legacy RimTalk remains suspended.\
> Development update (2026-10-06): The current branch implements Cat/Dog training, pet exclusivity, gel finalization, final active abilities and visible affection. The 20% adult Cat/Dog consensual follow-up is implemented and awaits in-game acceptance. Ordinary-completion display and finalization tooltips are available. Rabbit selection remains disabled. These development changes are not included in the published 2.3.6 ZIP.\
> In-game names follow the mod's official English localization. For exact formulas, thresholds, and implementation notes, see `PLAYER_GUIDE_EN.md`.

> See the [2.3.6 notes](Docs/Releases/2.3.6/2.3.6发布说明.md) for this version and the [2.3.5 notes](Docs/Releases/2.3.5/2.3.5发布说明.md) for self-training.

## 1. What the Mod Does

SexSlaveCraft (SSC) is built around this progression:

1. Designate Masters and sex slaves.
2. Use ordinary Training to raise Corruption and develop body parts.
3. Perform the Binding Ritual to establish a bond and advance the `Sex Slave Chain` health stage.
4. Develop a specialization such as Public Use, Cow, Training Officer or Combatant.
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

Give at least one free colonist the `Master` identity and enable `Training` work. Masters always qualify as trainers. A Sex Slave can turn on `Is a trainer` after researching Training Officer Specialization, becoming a free colonist with a valid Master bond and Chain stage 3 or higher, selecting Training Officer, and reaching 20% progress. An active Final Training Officer also qualifies after switching specialization. Unset pawns never qualify.

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

### Self-training

An SSC Sex Slave with a `Sex Slave Chain` has a separate `Allow Self-training` permission, enabled by default for new and existing saves. Select that pawn and right-click the pawn to choose an available solo act. Automatic self-training can branch only from an RJW automatic masturbation candidate: when both permissions are available, the four Chain stages select self-training at 30% / 55% / 80% / 50%. When ordinary masturbation is denied but self-training is allowed, the candidate becomes self-training.

Completion grants Corruption and a dedicated one-day mood memory, subject to the current Chain cap. It does not start ordinary Training cooldown or create a bond. Denying ordinary masturbation produces different ongoing moods depending on whether self-training remains allowed. In this first version, genes that forbid masturbation also block self-training.

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
- ownership restrictions and rebellion suppression begin.

Bound pawns cannot switch SSC identity until their binding is explicitly removed. A Master with remaining bound slaves is also locked; the Sex Slave trainer toggle remains available. Shared-bed permission can also apply before binding when a valid trainer is assigned.

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

- applies its two reception defaults after binding and forces them when profile overrides are enabled; initiation and Training remain separate;
- gains progress from sex with someone other than the owner;
- triggers special events after trading as the negotiator;
- improves trade, negotiation, Social Impact, and Talking.

After a pawn-to-pawn trade, the negotiator may:

- initiate consensual sex;
- be raped by the trader;
- be released without sex.

Higher Corruption makes the consensual outcome more likely, reaching 100% at 20% Corruption. The Public Use pawn must personally negotiate a successful trade with an actual exchange; cancelled or empty trades and orbital trade ships do not trigger this mechanic. Both pawns must be available, within 15 cells on the same map, and pass the relevant RJW and reachability checks.

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

## Combatant Specialization

Combatant training uses purple nanofluid to reshape the body and nervous system for melee and ranged combat. Complete Combatant research and set the pawn's SSC identity to Sex Slave on the Training tab. No bound master or chain stage is required.

1. Gain progress through completed daily Training, full Binding Rituals and direct kills. Training by the bound master grants extra progress. Slaughter and indirect deaths do not count.
2. Basic bonuses begin at 20%; stronger bonuses and ongoing damage reduction begin at 50%. Switching specializations preserves progress.
3. Once training is complete, extract the personality gel, finalize it at a sculpting table, and implant it.
4. Final bonuses stay with the personality. While drafted, activate Combat Overdrive to stimulate the body's nanomachines for a brief combat boost.

Check health conditions for specific attributes. Overdrive lasts 1 in-game hour and has a 4-hour cooldown starting on activation. Undrafting does not end an active buff. Final achievements transfer with the personality; active Overdrive and the old ability's cooldown do not.

Combatant health labels use lavender, with a brighter final state. Training Officers use teal, or muted gray-green when the final state is disabled. Final Public Use and Cow names appear once each. Combat Overdrive has a dedicated female-silhouette icon with a ringed leather collar and violet neural accents.

See the [full Combatant reference](PLAYER_GUIDE_EN.md#how-does-combatant-progress) for formulas and attributes.

## Pet Cat and Dog (development branch)

To select Cat or Dog, set SSC identity to Sex Slave, complete Training and the matching pet research, and have no final Cat, Dog or Rabbit specialization. Selection requires neither a bound master nor a chain stage. Cat gains movement speed and melee dodge at 20%, with stronger bonuses at 50%; Dog improves animal taming and training stats at 20%, 50% and 80%. Ordinary direction changes retain separate progress; ordinary effects apply only to the current path.

Completed daily Training and full Binding Rituals give the current ordinary pet path shared progress. A current ordinary Cat gains 1 percentage point after successful affection toward its actual bound master; ordinary completion, any final pet specialization or another current path prevents this reward. Dog affection grants no progress. Actual animal training interactions and taming attempts give ordinary Dogs distinct progress; failed training rolls count as attempts, while cancellation before an interaction gives no training reward.

Cat and Dog progress uses one decimal place: 99.5%–99.8% remains incomplete. At the shared completion threshold, the progress line reads "Training complete; ready to finalize". Hover over it for the steps: extract personality gel, process it into a final Cat or Dog at a sculpting table, then implant it. Ordinary completion does not directly grant a final ability. Only one final Cat, Dog or Rabbit specialization may be obtained; final bonuses, abilities and affection remain after switching to a non-pet training path. Rabbit selection remains disabled.

| Final ability | Use and effect |
| --- | --- |
| Cat: Soothing Resonance | Select another conscious player-faction humanlike pawn anywhere on the same map. Walk onto its cell and soothe it for two seconds with hearts. At completion, recover its current mental state, or grant +8 mood and ×1.10 global work speed for half a day if it has no mental state and has a mood need. Reapplication refreshes encouragement. All current mental states can be recovered; catatonic breakdown is not treated. Both effects share a one-day cooldown. |
| Dog: Directed Taming | Select an eligible factionless or player-owned animal on the same map and instruct it on its cell for two seconds. Directly tame a wild animal, or fully complete one currently eligible, checked training item for a player-owned animal using vanilla priority, including retraining a decayed item. Preserve the animal's master. Both branches share a one-day cooldown and require no extra food or materials. Cancellation, an invalid target or no valid training item starts no new cooldown. The ability grants no extra animal-work progress or work follow-up event. |

Both abilities work while drafted or undrafted. Targets must be conscious; ordinary sleep can be interrupted and conscious downed targets are allowed. Directed Taming also excludes mental states and dryads. The caster must be able to reach the target's cell.

Ordinary affection starts automatically when both pawns are idle, awake and within about 2.9 cells. The pet approaches its actual bound master, pauses for two seconds and shows progress, facing and hearts. Success gives the master +5 mood for six hours, with one memory maximum, and starts a one-day affection cooldown. Work, combat, sleep and player commands prevent automatic interruption. Cancellation grants neither rewards nor a completion cooldown.

After successful affection, ordinary and effective final Cats/Dogs may initiate a consensual follow-up with their actual bound master. Both must be humanlike, biologically at least 18 and adult under RJW rules. When SSC permission and RJW consensual/quick-hookup settings, body eligibility, desire, pair willingness and cooldowns allow it, make one 20% roll for that completed affection. A miss, refusal or failure preserves affection rewards; it does not search for another partner, reroll or force an interaction. New commands during preparation cancel it and preserve the new jobs. There is no extra affection progress or new pair cooldown. In-game acceptance of this follow-up is still pending.

## 10. Final Specializations

Public Use, Cow and Combatant follow the same finalization workflow:

1. complete ordinary training in the chosen specialization;
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

Implantation takes 600 ticks. The Hollow waits while retaining its posture and sleep, and both pawns must remain within touch range. Losing contact or a valid target interrupts the procedure before personality transfer or gel consumption.

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
- gains increasingly positive rape memories at high Corruption.

Bound pawns use six individual initiation, reception and Training permissions. Default consensual initiation is owner-only; Public Use opens only its two reception rules. Actual owner initiation is immediately allowed by the unified policy. See the [2.3.1 notes](Docs/Releases/2.3.1/2.3.1发布说明.md).

At more than 0.1% Corruption, an SSC Sex Slave may share an ordinary multi-person bed with the bonded Master and active Assigned Trainer. For a vanilla slave, assign the Master or trainer first, then the slave. Vanilla lovers/spouses remain unaffected; medical rest and deathrest take priority.

After both pawns actually sleep together, the slave receives a one-day, non-stacking memory using the existing six mood values. Merely assigning a bed does not grant it. Vanilla and Mint lists show separate Master, Sex Slave and Trainer badges on the right of the name, with extra-permission status and threshold details on hover.

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
- forces denial of non-owner forced reception while bonded and restrictions are enabled;
- the floor only applies inside the current Chain-stage interval.

The current decay-multiplier direction does not match the item descriptions. Minimum-Corruption effects preserve only milestones the pawn has already reached inside the current Chain interval.

## 18. Important Settings

For a first game, keep defaults and review:

- `Enable behavior restrictions`;
- `Enable specialization overrides`;
- Default behavior rules (separate window; batch overwrite requires confirmation);
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

SSC's legacy RimTalk integration is suspended pending a complete redesign. SSC no longer sends scene dialogue, inserts Training interruption lines, clears RimTalk replies, or reserves dialogue generation. The settings page shows a suspension notice. RimTalk's own features remain controlled by RimTalk and its other extensions.

Scheduled Training remains part of SSC. Use each Sex Slave's Training tab to select a two-hour window and a frequency of once every 1–7 days. Automatic Training obeys both the timetable and the roughly nine-hour cooldown. Forced orders bypass the scheduled date/window, but not cooldown or safety checks.

<!-- Not-fully-implemented checklist temporarily hidden.
## 20. Content Not Fully Implemented

The following entries exist as research, defs, or placeholder code but do not currently form complete gameplay systems:

- Fine Training and sensitivity discovery;
- Pet Rabbit selection, clone and reproduction gameplay (selection remains disabled);
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
- active trainer identity and the trainer's Training work assignment;
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
