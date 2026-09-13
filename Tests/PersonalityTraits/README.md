# Personality trait regression tests

Run from the repository root with the installed .NET 9 SDK:

```powershell
dotnet run --project Tests/PersonalityTraits/PersonalityTraits.csproj --configuration Release
```

The project has no package references and requires no game installation. For an
explicitly offline first restore, use the project directory as the only source:

```powershell
dotnet restore Tests/PersonalityTraits/PersonalityTraits.csproj --ignore-failed-sources --source Tests/PersonalityTraits
dotnet run --project Tests/PersonalityTraits/PersonalityTraits.csproj --configuration Release --no-restore
```

The project links the complete production `ExcretionUtility.cs`,
`ThingsComp_PE_PES.cs`, and `PersonalityTraitUtility.cs`. Capture, copying,
excretion, insertion, validation, and item consumption run through those source
files. The host does not reimplement the personality-transfer algorithm. To test
another exported source tree, pass `-p:SscSourceRoot=<absolute source directory>`.

The 15 cases cover:

- Replacing ordinary traits across bodies and restoring an earlier same-body
  snapshot, including different degrees of the same TraitDef.
- Capturing suppressed ordinary traits while excluding gene-granted and
  SSC-derived traits, and keeping independent trait instances through storage,
  copying, and insertion.
- Preserving the receiving body's genes and granted traits, including same-Def
  and different-Def conflicts, and removing hidden old ordinary traits.
- Preserving abilities shared by a removed ordinary trait and a retained gene
  or its granted trait.
- Restoring legacy version-zero snapshots, distinguishing an empty list from a
  missing snapshot, and rejecting missing data before modifying the receiving
  body or consuming the item.
- Writing and reading the version field, including a missing-key legacy default.

## Host contracts and limits

`GameStubs.cs` models the relevant RimWorld 1.6 `TraitSet` contracts checked
against the local game assembly during the fix:

- Trait queries ignore suppressed traits. `GainTrait` rejects an existing active
  duplicate; `suppressConflicts: true` enables trait conflict handling.
- `RemoveTrait` first checks for an active trait of the given Def. Removing a
  gene-granted trait can remove its source gene. Its conflict reconciliation
  considers ordinary traits and non-overridden genetic traits.
- Genetic suppression recalculation does not reset trait-based suppression.
- Trait removal can remove an ability that another retained source also grants;
  ability gains are idempotent.

The host assumes the relevant DLC behavior is enabled. Defs, trait conflicts,
genes, hediffs, and abilities are small in-memory models; they do not load game
XML or run Harmony patches. The ability tracker uses a set of AbilityDefs rather
than real Ability instances. Gene addition/removal is sufficient to detect an
unexpected loss during insertion; it does not model the complete gene lifecycle.
Cache and renderer notifications are no-ops, while the private trait recache
entry point exists so the production reflection path is exercised.

The Scribe value host records and replays the scalar fields requested by
`PostExposeData`. This checks that the version field participates in the
production save/load method; it is **not** a real RimWorld save-file round trip.
Collection and Def serialization remain no-ops. Full game-side caches,
suppression after later gene changes, work restrictions, needs, health effects,
Unity rendering, and compatibility with other mods still require game testing.

The missing-snapshot case intentionally verifies one validation error; any
unexpected error logged by a production catch block fails the case. A successful
run exits with code zero and prints `RESULT: 15/15 cases passed.`
