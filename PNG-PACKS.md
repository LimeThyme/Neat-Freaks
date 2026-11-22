# Neat Freaks pack format - schema 3

Keep this README beside pack.json. JSON has no comments, so instructions live here
instead of making a working manifest invalid. Field names must match exactly.

```json
{
  "schemaVersion": 3,
  "archives": [
    {
      "folder": "233-(Female Weapons)",
      "auto-indexing enabled": true,
      "palette-processing type": "equipment"
    }
  ]
}
```

The folder is relative to this pack and must match its on-disk name. Numeric names
such as 233 also work. Put static record-0.png files directly inside each archive
folder. The number selects images; the label is for people. Processing is selected
by the JSON property, not inferred from that number. Existing player race/gender
filtering and vanilla-item eligibility still apply; this is not custom-item support.

- auto-indexing enabled: true converts RGB colours to DFU palette slots once per
  loaded icon. Exact matches come first, nearest colours otherwise, lowest-slot
  ties. Transparent pixels become slot 0; exact opaque mask-black becomes 255.
- false preserves existing indexes only if the PNG has all 256 ART_PAL.COL colours
  in order and only slot 0 is transparent. Compatible mask slot 255 is supported.
  RGB/RGBA and incompatible indexed palettes retain their original colours/alpha
  and bypass native palette processing. No implicit conversion is performed.
- palette-processing type: clothing selects native mask removal then Clothing
  dye slots 96-111. equipment selects mask removal then WeaponsAndArmor slots
  112-127. Actual artifact items using equipment get mask removal without metal
  dyeing, matching DFU. Choosing clothing explicitly selects clothing processing.
  These choices affect indexed sources; unchanged RGB images have no dye slots.

Prefer real JSON booleans true/false. Quoted "TRUE", "False" and other casing also
work. Processing values are case-insensitive. Bare TRUE/FALSE are not valid JSON.
Numbers, null, yes/no, missing fields and unknown targets are rejected. Trailing
commas/comments should not be used. Do not include the old mode property.

Schema 2 is no longer accepted. Migration requires schemaVersion 3 and replacing
each mode: auto -> true; indexed -> false. Old fixed RGB/RGBA art -> false. An old
fixed PNG with a compatible DFU palette will now process; export that PNG as RGBA
if its unchanged appearance is intended. Choose clothing/equipment explicitly.

Reload from HUD with neatfreaks reload after editing. Conversion and render caches
clear on reload; disk PNGs are not rewritten. Enable both logging checkboxes for
source format, selected target and processing/bypass details. Unreadable icons
fall back to DFU; a rejected manifest has no registered replacements.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colours so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See DEBUG-LOGGING.md for examples and SOURCE-CHANGES.md for the engineering rationale.
