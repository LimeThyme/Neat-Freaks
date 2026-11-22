# Diagnostic logging

Enable Mod Logging for event summaries. Enable Verbose Logging as well for per-icon
stages. Both default off; changing settings does not clear images or rescan packs.
Logs live beside the loaded mod under Logs/NeatFreaks/NF.log and NF-prev.log. They
rotate once per session; toggles resume the same log. Routine lines batch up to 32,
with operation, warning/error and shutdown flushes. A crash may lose the last tail.

Normal lines explain save/new-game resets, registration triggers, character changes,
icon-cache cleanup, selected archives and warning counts. Verbose lines additionally
show schema, conversion choice, pack-selected processing, source format, timings,
native mask/dye stages and cache insertions. Cache hits stay quiet. Warnings/errors
remain visible regardless of verbosity and reach Unity even with dedicated logging off.

Schema 2 now produces an explicit migration warning. For schema 3 with conversion
off, indexed palette means native processing is available; PNG colours unchanged
means it was correctly bypassed. A registered mapping alone does not mean an image
has been requested or decoded. See the pack README before diagnosing missing dyes.

## Reading schema 3 decisions

The former mode=auto/preserve labels are removed from runtime diagnostics so log
readers see the same controls they edited in pack.json. Examples below omit paths
and timestamps for readability; actual lines keep both where applicable.

```text
Archive discovered: ...; schema=3; auto-indexing enabled=true; palette-processing type=equipment; added=28
PNG decoded: ...; 43x37; auto-indexing enabled=true; palette-processing type=equipment; source=auto-indexed DFU palette
Processing selected: 233:10; target=WeaponsAndArmor; source=auto-indexed DFU palette
```

With conversion off, source says either existing DFU palette indexes preserved or
PNG colours unchanged; native palette processing bypassed. The latter is expected
for RGB/RGBA and incompatible palettes. Selecting equipment in the manifest cannot
create dye slots in those unchanged pixels. An actual artifact can report target=
Artifact, followed by native mask removal and an explicit dye bypass.

Archive settings are intent, not evidence of processing. Render start/complete and
native-stage lines show work performed. Cache hits remain quiet; repeated materials
can reuse results without new messages. No mode field in the log is a pack option.
