# Palette conversion and why it is optional

Schema 3 uses auto-indexing enabled rather than mode names. See PNG-PACKS.md for
the exact syntax. True converts decoded colours using the original ART_PAL.COL:
exact match first, squared RGB distance otherwise, lowest slot on ties. Alpha below
128 becomes transparent; opaque exact mask-black becomes index 255. The source
bitmap is cached and each dye starts from that source, not from a previous render.

False preserves compatible DFU indexes; incompatible palettes and RGB/RGBA stay
unchanged. This protects author colours without requiring a separate indexed
option. Palette indexes cannot be reliably reconstructed from equal RGB values;
authors who need exact slot identity should supply a compatible indexed PNG.

The pack chooses clothing (slots 96-111) or equipment (112-127). Native ChangeMask
runs before dye processing. Artifact items using equipment skip metal dyeing.
Fixed-colour source data has no index slots and skips both native calls. Cropping
and slot fitting occur afterward. Verbose logs distinguish these outcomes.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See DEBUG-LOGGING.md for examples and SOURCE-CHANGES.md for the engineering rationale.
