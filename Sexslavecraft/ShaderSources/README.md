`SSC_FullGelBody.shader` is tracked here as source.

RimWorld does not load raw `.shader` text files from the C# source tree directly at runtime.
To make this shader actually load in-game, it must be built into a Unity asset bundle or another shader-loading path that RimWorld can resolve.

Current runtime hook expects the shader path:

- `Sexslavecraft/SSC_FullGelBody`

Current Def name:

- `SSC_FullGelBody`
