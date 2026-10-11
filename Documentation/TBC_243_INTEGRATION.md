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

## Spell family masks

The family-mask editor displays the two 32-bit TBC base words independently (the three-word effect
masks are WotLK-only and disabled in TBC mode). It supports named bits, unknown-bit editing, family
and spell searching, matching-spell lists, decimal and hexadecimal raw editing, and exact raw-value
preservation. Definitions remain scoped to the selected family. In TBC mode a pre-existing label is
only presented as identified when the imported TBC data contains matching spells; otherwise the bit
is explicitly shown as unknown while remaining editable.

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

The same regression was run against the available expansion reference sets: 19 Vanilla files and
245 WotLK files round-tripped byte-for-byte. The WotLK source contained one zero-byte
`CharVariations.dbc`; that invalid input was explicitly excluded and is not reported as passing.

## CMaNGOS world database mapping

The TBC integration was checked against the active MasterWoW base schema and loader queries. It
supports `spell_bonus_data`, `spell_proc_event`, `spell_threat`, `spell_elixir`, `spell_learn_spell`,
`spell_pet_auras`, `spell_affect`, and `spell_chain`. It does not expose Trinity/AzerothCore tables.
The non-existent CMaNGOS `spell_group_spell` mapping and the non-existent TBC `spell_threat` column
`InverseEffectMask` were removed.
