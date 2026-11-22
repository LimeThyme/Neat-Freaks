# Why these tests exist

Run Run-Tests.ps1 with UnityEditorDirectory, GameManagedDirectory, HarmonyAssembly,
PaletteFile and a writable ResultsDirectory. Optional ArtPackDirectory exercises
indexed artwork; AutoPackDirectory exercises the installed vanilla PNG folders.
See VALIDATION.md for the current result and in-game limitations.

Tests use production code and real DFU/Harmony assemblies. Rendering callbacks
replace GPU upload only, keeping parsing, indexing, native dyes and layout real.
Schema 3 fixtures are generated for installed folders without modifying installed
manifests. Test fixture labels describing indexed/fixed input formats are not
accepted manifest modes. The runtime accepts schema 3 only.

Important failures include wrong target inference, lost palette slot identity,
extra cache work, hidden errors and malformed PNGs. The audit tool independently
checks pack authoring rules; it is not a replacement for these native-code tests.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See DEBUG-LOGGING.md for examples and SOURCE-CHANGES.md for the engineering rationale.
