# Changelog

## 0.1.6 - 2026-11-14

- Use schema 3 property names in archive, decode and render/cache diagnostics.
- Distinguish author settings from actual source format and native-processing bypass.
- Correct summaries that implied native equipment processing always runs.
- Review all source comments and documentation for concise rationale; retain accurate explanations.
- Add logging terminology regression checks; image-processing behavior is unchanged.


## 0.1.5 - 2026-11-13

- Require schema 3; remove schema 2 compatibility and old mode properties.
- Add boolean auto-indexing and explicit clothing/equipment processing selection.
- Accept mixed-case quoted booleans and processing values.
- Preserve compatible indexes when conversion is off; otherwise preserve PNG colours.
- Remove ArchiveSelection equipment-processing helper; use pack-selected policy.
- Add adjacent pack README, migrated examples, schema validation and regression coverage.


## 0.1.4 - 2026-11-12

- Honor auto/indexed/fixed modes for equipment again.
- Remove masks using native ChangeMask; dye weapons/armor with WeaponsAndArmor.
- Preserve artifact palette after mask removal; separate artifact/material caches.
- Report metal-slot counts while retaining existing auto-index matching rules.
- Preserve clothing processing, lazy loading and layout; update tests and rationale docs.


## 0.1.3 - 2026-11-12

- Render vanilla weapons, artifacts and armor from raw PNGs on demand.
- Preserve PNG colours/alpha and reuse transparent-border crop and slot fitting.
- Override equipment archive modes to fixed for this test; bypass native processing.
- Match DFU inventory katana records and share raw renders across dye/material values.
- Preserve clothing behavior, selection, logging and fallback; update tests and audits.


## 0.1.2 - 2026-11-12

- Add Verbose Logging checkbox, default off and dependent on Enable Mod Logging.
- Separate event summaries from per-archive and per-image details.
- Name cache cleanup explicitly and show character transitions and registration outcomes.
- Keep warnings in both modes, with additional icon exception detail in verbose mode.
- Update rationale documentation, settings validation and mode-switch regression tests.


## 0.1.1 - 2026-11-12

- Reuse successful parent-path validation within each manifest scan; always check leaves and fully revalidate real reads.
- Batch routine diagnostics, retaining prompt warning/error and completion flushes.
- Add monotonic timings for scans, archive validation, reads, decoding, indexing and rendering.
- Review all nine source files for concise rationale comments; update descriptive or stale comments.
- Extend regression checks for buffering, validation reuse and lazy timing stages.
- Keep synchronous discovery, schema 2, current selection rules, caches and clothing processing.

## 0.1.1 — 2026-11-12

- Trigger pending archive registration before the standard inventory window opens,
  including empty inventories and inventories without eligible clothing.
- Keep first clothing-image fallback, manual reload and lifecycle invalidation.
- Preserve selection and texture caches across repeated openings. PNGs remain lazy.
- Log which trigger caused discovery. Verify/clean up both owned Harmony hooks.
- Update rationale comments, regression tests, documentation and build metadata.

## 0.0.9 — 2026-11-10

- Select one clothing, weapon, artifact and armor archive for the active player.
- Use gender for weapons/artifacts; use race body type and gender for armor.
- Add per-category registration diagnostics and preserve deferred, lazy discovery.
- Keep new categories discovery-only; preserve clothing rendering and native DFU
  rendering for weapons/artifacts/armor. No new image processing in this release.
- Add numeric/labelled 20-archive examples and tests for all vanilla profiles.
- Keep schema 2, `neatfreaks reload`, existing folder names and the 512 active cap.

## 0.0.8

Descriptive archive folders, player-specific clothing selection, lifecycle reload
and a full source-comment review.

## 0.0.7 and earlier

Native mask-black cleanup, auto indexing, inventory sizing, packs and diagnostics.
