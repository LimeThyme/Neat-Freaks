# Validation - 0.1.6

Production compiles with warnings as errors against installed DFU 1.1.1, Unity
2019.4.41f2 and Harmony 2.4.2.0. All 43 offline scenarios and 8,264,675 assertions
passed. Many assertions compare individual pixels rather than distinct use cases.

Schema checks cover booleans and quoted mixed casing, invalid values, selected
processing independent of folder number, preserved DFU indexes including mask255,
foreign palette preservation, unchanged RGBA alpha, shared no-conversion caches,
and old schema rejection. Existing native dye, crop, lifecycle helper, Harmony,
logging and malformed PNG tests pass. Tests cover 108 indexed artwork files,
864 installed clothing PNGs and 406 installed equipment PNGs.

Installed images were exercised using schema 3 fixtures built from their folder
names; the installed schema 2 manifest was not accepted or edited. Runtime schema
migration is not provided. No installed PNGs or mod binaries were modified.

In game: export with Precompiled checked, migrate the pack, restart, load a save
and open inventory. Test true/false with indexed and RGBA images; select each target
on an example archive. Test equipment artifacts, both body profiles, reload and
verbose logging. Graphics, actual item selection and settings UI still require
live verification. Offline tests do not claim measured frame-time improvement.

## Logging terminology regression

Four combinations of conversion on/off and indexed/RGBA source verify that settings
use schema 3 names, source outcomes are explicit, and no mode= field appears in the
runtime diagnostic stream. Existing 108 indexed, 864 clothing and 406 equipment
image checks still pass. Live readability and graphics remain an in-game check.
