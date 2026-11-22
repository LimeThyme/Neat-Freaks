# Engineering rationale - 0.1.5 to 0.1.6

The latest game log confirmed schema 3 worked, but mode=auto exposed an old internal
label. Readers could reasonably think schema 2 remained active. This release changes
the explanation, not the processing rules: logs use the same field names as pack.json.

Archive and image diagnostics now show auto-indexing enabled=true/false and
palette-processing type=clothing/equipment. They separately explain the source:
auto-indexed DFU palette, existing DFU palette indexes preserved, or PNG colours
unchanged with native processing bypassed. Settings describe intent; the source
and native-stage lines describe what actually happened. This distinction matters
because false can preserve indexed data or leave RGB unchanged.

Normal startup/registration messages now say processing follows each archive's
schema 3 settings. They no longer claim that every equipment image receives native
material processing. Artifact behavior is reported when its actual render occurs;
registration alone cannot prove an artifact was requested or rendered.

Shared diagnostic helpers prevent archive, decode and cache messages from using
different terminology. Existing internal mode strings remain implementation details,
not manifest options or log fields. Parser rejection of the removed mode property
is intentional and still explains how to migrate.

All nine source files were reviewed. Comments retain concise reasons for palette
identity, source immutability, native target selection, path checks, lifecycle and
Harmony ownership. Stale clothing-only wording and indexed-mode error terminology
were corrected. Detailed release history belongs here and in CHANGELOG.md, not in
long inline narratives. Existing rationale that remains accurate was retained.

Native processing, cache keys, conversion, folder selection, schema validation and
UI sizing are unchanged. The regression suite checks the new wording alongside
pixel/native-processing tests. No installed files were modified.
