# Troubleshooting by expected behavior

No replacements after upgrading: schema 2 is rejected in 0.1.6. Migrate `pack.json`
using [PNG-PACKS.md](PNG-PACKS.md) and run `neatfreaks reload` from HUD. The schema 3 field names must
match exactly; remove mode, supply both controls and use valid JSON.

No dye changes: with auto-indexing false, RGB/RGBA and incompatible palette images
remain unchanged. Enable conversion or export a compatible DFU palette. With an
indexed source, verify clothing/equipment selects the intended slot range. Artifacts
using equipment intentionally retain their palette after mask removal.

Black regions vanish: exact opaque mask-black is treated as slot 255 during auto
conversion. To preserve authored RGB black, turn conversion off and use RGBA.
Miscoloured details: equal RGB colours cannot reveal original slot identity; export
an indexed DFU palette if exact slots matter.

Wrong body or new PNG not visible: close item windows, change the character if
needed, then run `neatfreaks reload`. It clears caches and scans selected folders.
Check folder strings and `record-0.png` names. Unsupported images fall back to DFU.
Enable verbose logging to separate registration, decode, policy and native stages.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See [DEBUG-LOGGING.md](DEBUG-LOGGING.md) for examples and [SOURCE-CHANGES.md](SOURCE-CHANGES.md) for the engineering rationale.
