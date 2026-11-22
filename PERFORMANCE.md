# Performance behavior

Registration enumerates only the selected archives and reuses validated parents
within a scan. Actual file reads recheck paths. PNGs decode on first use; conversion
happens once. Indexed renders cache per dye, equipment artifacts share a mask-only
render, and unchanged RGB images share one render across dyes. Reload clears data.

Normal completion timings cover registration, not the whole game frame. Verbose
mode adds scan/validation/read/decode/index/render timings. Compare the same save,
pack and logging settings across repeated runs; distinguish cold disk reads from
warm cache hits. Disk activity, other mods and logging can affect the result.
Schema 3 adds no background worker or eager PNG load. See [DEBUG-LOGGING.md](DEBUG-LOGGING.md).

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colors so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See [DEBUG-LOGGING.md](DEBUG-LOGGING.md) for examples and [SOURCE-CHANGES.md](SOURCE-CHANGES.md) for the engineering rationale.
