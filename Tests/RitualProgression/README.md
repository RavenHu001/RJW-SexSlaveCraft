# Ritual progression regression tests

Run from the repository root with .NET SDK 9:

```powershell
dotnet run --project Tests/RitualProgression/RitualProgression.csproj -- Defs/HediffDefs/HediffOfSexSlave.xml
```

No NuGet packages or game installation are needed. The project links production
source files and reads the shipped chain stage definitions. `GameStubs.cs` only
provides a headless game host; progression, decay and chain mutations run through
the production implementations.

The host also reads initial/max severity from the XML. Missing values use the
verified RimWorld defaults (`initialSeverity = 0.5`, `maxSeverity = float.MaxValue`);
the current Chain XML overrides initial severity to 0.2 and does not impose a
100% maximum. This matters when reproducing the old ritual's overshoot.

The tests cover ritual resolution followed by demand refresh, repeated rituals,
stage thresholds, natural regression, earned equipment floors, disabled/zero/legacy
decay, first binding and daily progression. They do not exercise Unity rendering,
full ritual jobs, serialization, real trait synchronization or prisoner conversion.

To compare with a separately exported baseline, pass
`-p:SscSourceRoot=<absolute path to baseline Sexslavecraft sources>` before `--`.
