# TBC 2.4.3 integration evidence

The TBC mode remains separate from the Vanilla 1.12.1 and WotLK 3.3.5 modes. It uses the files in
`Bindings_243_tbc` and applies CMaNGOS-specific attribute labels only when both TBC and the
`CMangosTbc` integration are selected.

## Spell attributes

`AttributeCatalog/cmangos-tbc-spell-attributes.json` contains all 32 bits for the seven client
attribute words (`Attributes` through `AttributesEx6`) and the 32-bit server-side override word.
Named bits come from the current MasterWoW `SpellDefines.h`; unnamed bits remain editable and are
labelled unknown. `Attributes` bit 12 is **Daytime Only** and bit 13 is **Night Only**. No clock
boundaries are asserted by the editor.

The server-side override word is catalogued for audit purposes but is not serialized into the
client `Spell.dbc`. The TBC DBC has seven client attribute words and the editor preserves all raw
bits, including unknown `AttributesEx6` bits 18-31.

`AttributesEx7` is not part of the build-8606 layout. The column remains visible for cross-version
orientation but is disabled in TBC mode and is never read or written. The current MasterWoW
`SpellAttributesEx6` enum defines bits 0-17 only; bits 18-31 therefore remain explicitly unknown
rather than receiving unrelated WotLK names.

## Spell family masks

The family-mask editor displays the two 32-bit TBC base words independently (the three-word effect
masks are WotLK-only and disabled in TBC mode). It supports named bits, unknown-bit editing, family
and spell searching, matching-spell lists, decimal and hexadecimal raw editing, and exact raw-value
preservation. Definitions remain scoped to the selected family. In TBC mode a pre-existing label is
only presented as identified when the imported TBC data contains matching spells; otherwise the bit
is explicitly shown as unknown while remaining editable.

The base **Edit Families** button is intentionally available in TBC because build 8606 stores a
64-bit base family mask as `SpellFamilyFlags1` and `SpellFamilyFlags2`. Effect-level family masks
and their editor buttons are WotLK-only and are disabled in TBC mode.

## Spell visuals

The TBC `SpellVisual.dbc` binding contains 25 fields. Its graph exposes 11 kit links:

- Precast, cast, impact, state, state-done and channel kits
- Caster-impact and target-impact kits
- Missile-targeting, instant-area and impact-area kits

The TBC `SpellVisualKit.dbc` binding contains 12 effect slots. TBC does not serialize the WotLK-only
`PersistentAreaKit`, missile cast/impact offsets, `SpellMissile.dbc`, `SpellMissileMotion.dbc`, or
model-attachment fields. Visual creation and cloning use the active binding's explicit columns.

The supplied active client directory did not contain `SpellVisualKit.dbc`,
`SpellVisualEffectName.dbc`, `AnimationData.dbc`, or the other supporting visual DBCs. The available
`SpellVisual.dbc` was validated independently. A second read-only TBC reference set was therefore
used to exercise the complete available visual graph: 4,746 SpellVisual rows, 4,162 SpellVisualKit
rows, 1,611 SpellVisualEffectName rows, and 3 SpellVisualPrecastTransitions rows all round-tripped
byte-for-byte.

## Supplied TBC client validation

Run:

```powershell
.\Tools\Test-DbcRoundTrip.ps1 `
  -BindingsDirectory .\Documentation\Bindings_243_tbc `
  -DbcDirectory '<2.4.3 DBFilesClient directory>' `
  -OutputDirectory '<temporary output directory>' `
  -ReportPath '<report.json>'
```

The test validates WDBC headers, binding field counts and record widths, file lengths, every string
offset, and then reconstructs each file without normalizing floats or unknown bit patterns. On the
owner-supplied build-8606 data, all eight files having both an input DBC and an editor binding were
byte-identical after reconstruction:

| File | Records | Fields | Record bytes |
| --- | ---: | ---: | ---: |
| AreaTable.dbc | 1,661 | 35 | 140 |
| SkillRaceClassInfo.dbc | 223 | 8 | 32 |
| Spell.dbc | 32,011 | 216 | 864 |
| SpellCastTimes.dbc | 61 | 4 | 16 |
| SpellCategory.dbc | 210 | 2 | 8 |
| SpellIcon.dbc | 1,731 | 2 | 8 |
| SpellVisual.dbc | 4,746 | 25 | 100 |
| TotemCategory.dbc | 19 | 20 | 80 |

This is serialization validation, not in-game certification.

## Configuration and database selection

The selected expansion, bindings, and DBC directory must describe the same client build. Selecting
`2.4.3 8606` now selects the packaged `Bindings_243_tbc` directory automatically. The application
validates every required DBC and binding before loading and reports the exact missing files and
configured directories; it no longer continues into a misleading null-reference failure.

SQLite database paths may be absolute. The configuration window displays the resolved path and has
a file picker for an existing `.db`, `.sqlite`, or `.sqlite3` file. A bare relative name still
resolves beside the running application for backward compatibility. This matters for self-contained
published builds: starting from a new directory with `SpellEditor` as the relative name creates a
new database there rather than opening a database from a different installation.

The supplied primary client directory is not a complete editor DBC set. It contains the eight
validated files listed above, but the editor also needs reference DBCs such as `SpellDuration`,
`SpellRange`, and `SpellRadius`. Point the editor at a complete build-8606 DBC directory and do not
mix it with the WotLK bindings.

The editor's primary `spell` table is the client-authoring projection produced from `Spell.dbc`.
CMaNGOS `spell_template`/server exports are separate evidence and must not replace that table: they
use different names and omit client-only localized strings and visual data. The CMaNGOS integration
reads its supported world tables separately. TBC also has no `CasterAuraSpell`,
`TargetAuraSpell`, exclusion-aura spell references, `PowerDisplayId`, or
`EffectBonusMultiplier1..3`; their controls are disabled and these columns are not read or written.
If a custom tooltip contains a newer-client `$bc1`/`$bc2`/`$bc3` token, the token is preserved
visibly instead of being replaced with zero or aborting spell selection.
The same regression was run against the available expansion reference sets: 19 Vanilla files and
245 WotLK files round-tripped byte-for-byte. The WotLK source contained one zero-byte
`CharVariations.dbc`; that invalid input was explicitly excluded and is not reported as passing.

## CMaNGOS world database mapping

The TBC integration was checked against the active MasterWoW loader queries and the supplied
`tbcmangos1110.sql` schema. It supports the spell-scoped rows in `spell_affect`, `spell_area`,
`spell_bonus_cpdata`, `spell_bonus_data`, `spell_chain`, `spell_cone`, `spell_elixir`,
`spell_learn_spell`, `spell_map`, `spell_map_restriction`, `spell_pet_auras`, `spell_proc_event`,
`spell_proc_item_enchant`, `spell_script_target`, `spell_scripts`, `spell_target_position`,
`spell_threat`, and the separately labelled server `spell_template` record. Tabs are shown only
when the connected database actually contains the table.

The TBC effect editor is version bounded by the inspected CMaNGOS TBC definitions: spell effects
are offered only for ids 0-153, aura types for ids 0-261, and implicit targets for ids 0-93.
WotLK-only values are therefore not selectable while authoring a TBC spell. If an imported record
already contains an out-of-version value, the editor labels it unsupported and preserves its exact
number instead of silently replacing it with zero.

Every tab can create a missing row for the selected spell without writing until **Save** is used.
The `spell_template` Add action initializes its TBC fields from the selected client Spell.dbc row,
but keeps the server record separate and exposes `IsServerSide` and `AttributesServerside` for an
explicit review. Named mask controls default to human-readable checkboxes; **Show raw number**
switches to the exact decimal/hexadecimal editor without changing the stored bits. Unknown bits are
retained by the raw value path.

`spell_facing` is present in the supplied dump but has no matching loader in the inspected current
MasterWoW core, so it is not presented as an active table. `spell_bonus_data_before` is a historical
backup table and is intentionally excluded. The integration does not expose Trinity/AzerothCore
tables. The non-existent CMaNGOS `spell_group_spell` mapping and the non-existent TBC
`spell_threat.InverseEffectMask` column remain excluded.
