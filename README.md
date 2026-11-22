# Neat Freaks 0.1.6

Schema 3 gives pack authors two explicit controls: auto-indexing enabled and
palette-processing type (clothing/equipment). Old mode fields and schema 2 support
are removed. See PNG-PACKS.md or the README beside the supplied pack.json.

With auto-indexing off, compatible DFU indexed PNGs retain their slots; other PNGs
keep their colours and alpha and skip native palette processing. With it on,
colours are matched to ART_PAL.COL once. The selected processing target determines
native dye behavior independently of the folder number. Equipment artifacts remain
mask-only. No archive-based processing helper remains in ArchiveSelection.cs.

Race/gender archive selection, lazy loading, native processing, crop/slot fitting,
normal/verbose logging and manual reload remain. Only vanilla templates currently
qualify. Source images and paperdoll rendering are untouched.

Build with Precompiled (experimental) checked; see BUILD.md. Migrate your installed
pack.json to schema 3 before testing. Examples include full numeric/labelled packs;
folder strings must match your own folders. Keep your existing PNGs, not just the
five sample clothing images. This source kit does not include an exported dfmod.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See DEBUG-LOGGING.md for examples and SOURCE-CHANGES.md for the engineering rationale.
