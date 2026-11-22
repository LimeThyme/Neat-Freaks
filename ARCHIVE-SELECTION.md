# Selection versus processing

Race/gender still select relevant vanilla archives before file enumeration:
`clothing female 235-238/male 239-242`; `armor female 245-248/male 249-252`;
`weapons female 233/male 234`; `artifacts female 433/male 432`. DFU body morphology
chooses offsets: `Argonian 0`, `elves 1`, `humans 2`, `Khajiit 3`. Unknown profiles fail
closed. Folder labels are descriptive and never determine the character profile.

These numbers determine what to register, not how to process its pixels. Schema 3
requires an explicit clothing/equipment choice per folder. There is no processing
classification helper in `ArchiveSelection`. Vanilla template eligibility remains;
this release does not implement support for custom items or races.

First inventory opening triggers selection even with no eligible items. Item-image
requests are a fallback. Save/new-game invalidation clears the old selection and
icon data; manual HUD reload rereads the player. Reopening keeps caches. The shared
512 mapping limit and deterministic first-wins duplicate handling remain.

## Diagnostic wording in 0.1.6

Logs now name auto-indexing enabled and palette-processing type exactly as the
manifest does. They distinguish converted indexes, preserved indexes and unchanged
PNG colors so an expected bypass is not mistaken for a failed conversion. Native
target/stage lines describe actual work; archive registration only records intent.
This is a wording update: schema 3, processing and caching behavior are unchanged.
See [DEBUG-LOGGING.md](DEBUG-LOGGING.md) for examples and [SOURCE-CHANGES.md](SOURCE-CHANGES.md) for the engineering rationale.
