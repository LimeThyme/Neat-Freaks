# Build and migrate to 0.1.6

Use DFU 1.1.1 and Unity 2019.4.41f2. This is a source kit, with exported mods under [Builds/](Builds/).
Choose the correct build for your operating system.
Offline compilation verifies code but cannot replace Mod Builder's platform export.

## Import

Back up the old Neat Freaks source folder outside the project's `Assets` tree.
Keeping both versions under Assets causes duplicate class definitions. Copy:

```text
.\Neat Freaks - 0.1.6 - Source
```

to:

```text
[Your Daggerfall Unity Source Directory]\Assets\Untracked\ModBuilder\Neat Freaks - 0.1.6 - Source
```

Import only that Unity subtree. Keep `Tools`, `DeveloperTests`, `ValidationRecords` and
documentation outside `Assets`. The standalone artwork is installed separately.
No helper script auto-launches/closes Unity or modifies installed game files.

## References and export

Keep one managed Harmony 2.4.2.0+ compiler reference, compatible with Editor and
the selected standalone platform, with Auto Reference enabled. A runtime Harmony
`.dfmod` alone is not a compiler reference. Reuse the compatible DLL already in
your project instead of adding duplicates. DFU supplies FullSerializer and its
settings/console APIs. Do not bundle the reference DLL in Neat Freaks.

1. Let Unity finish compiling; resolve Console errors first.
2. Open Daggerfall Tools â†’ Mod Builder.
3. Load `Assets/Untracked/ModBuilder/Neat Freaks - 0.1.6/neatfreaks.dfmod.json`.
4. Confirm **eleven inputs**: nine source files (`Bootstrap`, `InventoryPatch`,
   `Icons`, `IconLibrary`, `PngIcon`, `NeatFreaksLog`, `IconLayout`, `AutoIndexer`, `ArchiveSelection`), `modsettings.json`, and the
   mod manifest. No PNG or pack manifest is bundled.
5. **Check Precompiled (experimental)**. Select the platform/output directory and
   export `neatfreaks.dfmod`. The unchecked runtime compiler route is not this kit's
   documented workflow.

Mod Builder generates its assembly payload from these sources. Do not add an old
DLL.bytes payload yourself. Keep a pristine copy of the kit because the Unity
working copy may gain `.meta` or generated export files. The mod GUID is unchanged;
0.1.6 replaces the existing Neat Freaks mod rather than becoming a second mod.

## Install

Replace the old exported mod in the game's Mods directory and enable Harmony first.
Copy this kit's `StreamingAssets/Textures/NeatFreaks` into:

```text
[Your Daggerfall Installation Directory]\StreamingAssets\Textures\NeatFreaks
```

For Unity Play Mode, put the same texture subtree beneath the project's
`Assets/StreamingAssets/Textures`. The standalone and editor roots are separate.
Neither the source ModBuilder folder nor PortableAppdata/Mods/GameData is the
texture root. Application.streamingAssetsPath determines it at runtime.

Restart after installing the new code. Later artwork additions/edits require
only `neatfreaks reload`, not another build. Logging is off by default; enable it
through DFU Mod Options if needed. An enabled session creates Mods/Logs/NeatFreaks.

## Verify

Startup reports deferred selection and initially zero mappings. Open inventory or
run `neatfreaks reload` from HUD. Check Player selection in NF.log and the subsequent
scan count.

## Pack migration before testing

0.1.6 accepts only schema 3. Follow [PNG-PACKS.md](PNG-PACKS.md); Keep your PNG collection and use
the supplied labelled/numeric examples as a guide. The README beside pack.json
is excluded from Unity build inputs and is for pack makers.
Installed files have not been changed by this build task.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See [DEBUG-LOGGING.md](DEBUG-LOGGING.md) for examples and [SOURCE-CHANGES.md](SOURCE-CHANGES.md) for the engineering rationale.
