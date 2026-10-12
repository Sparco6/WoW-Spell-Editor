using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using SpellEditor.Sources.Constants;

namespace SpellEditor.Sources.Emulator.CMangos.Tbc
{
    internal abstract class TbcSpellTable : CMangosTable
    {
        protected static readonly FlagSet RaceMask = new FlagSet("TbcPlayableRaceMask", null,
            new uint[] { 1, 2, 4, 8, 16, 32, 64, 128, 512, 1024 },
            new[] { "Human", "Orc", "Dwarf", "Night Elf", "Undead", "Tauren", "Gnome", "Troll", "Blood Elf", "Draenei" });
        protected static readonly EnumSet Boolean = new EnumSet(null, new long[] { 0, 1 }, new[] { "No", "Yes" });
        protected static readonly EnumSet Gender = new EnumSet(null, new long[] { 0, 1, 2 }, new[] { "Male", "Female", "Either" });

        protected TbcSpellTable(CMangosEmulator emulator) : base(emulator) { }
        protected static WorldColumn Spell(string name = "entry") =>
            new WorldColumn { Name = name, IsKey = true, IsPrimarySpellKey = true };
    }

    internal sealed class SpellBonusCpDataTable : TbcSpellTable
    {
        public SpellBonusCpDataTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_bonus_cpdata";
        public override string OrderBy => "`entry`";
        public override bool IsSingleRow => true;
        public override bool InheritsRankChain => true;
        public override string SpellFilter(RankChain chain) => In("entry", chain.SpellId, chain.FirstSpellId);
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell();
            yield return new WorldColumn { Name = "direct_bonus_per_cp", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "dot_bonus_per_cp", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "ap_bonus_per_cp", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "sp_bonus_per_cp", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "max_cp", DefaultValue = "5" };
            yield return new WorldColumn { Name = "consume_cp" };
            yield return new WorldColumn { Name = "comment", Kind = ColumnKind.Text, AllowNull = true, IsMultiline = true };
        }
        public override string FallbackHeader => "Combo scaling";
        public override string FallbackDescription => "Server-side per-combo-point damage, healing, AP and spell-power scaling.";
        public override string FallbackEmpty => "No combo-point coefficient override. Use Add to create one for this spell.";
        public override string[] FallbackLabels => new[] { "Spell", "Direct per CP", "Periodic per CP", "AP per CP", "Spell power per CP", "Maximum CP", "Consume override", "Comment" };
        public override string[] FallbackTooltips => new[] { "First-rank spell id.", "Flat direct bonus for each combo point.", "Flat periodic bonus for each combo point.", "Attack-power coefficient per combo point.", "Spell-power coefficient per combo point.", "Maximum combo points used by scaling.", "Reserved by this core; zero preserves normal consumption.", "Database note." };
    }

    internal sealed class SpellAreaTable : TbcSpellTable
    {
        public SpellAreaTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_area";
        public override string OrderBy => "`area`, `quest_start`, `aura_spell`";
        public override string SpellFilter(RankChain chain) => $"`spell` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell("spell");
            yield return new WorldColumn { Name = "area", IsKey = true };
            yield return new WorldColumn { Name = "quest_start", IsKey = true };
            yield return new WorldColumn { Name = "quest_start_active", IsKey = true, Enum = Boolean };
            yield return new WorldColumn { Name = "quest_end" };
            yield return new WorldColumn { Name = "condition_id" };
            yield return new WorldColumn { Name = "aura_spell", Kind = ColumnKind.Int, IsKey = true, ShowSpellPicker = true, FallbackNegativeOption = "Forbid this aura" };
            yield return new WorldColumn { Name = "racemask", IsKey = true, Flags = RaceMask };
            yield return new WorldColumn { Name = "gender", IsKey = true, DefaultValue = "2", Enum = Gender };
            yield return new WorldColumn { Name = "autocast", Enum = Boolean };
        }
        public override string FallbackHeader => "Area rules";
        public override string FallbackDescription => "Automatically apply or restrict this spell by area, quest, condition, aura, race and gender.";
        public override string FallbackEmpty => "No area rule. Use Add to create one for this spell.";
        public override string[] FallbackLabels => new[] { "Spell", "Area", "Start quest", "Quest active", "End quest", "Condition", "Required/forbidden aura", "Race mask", "Gender", "Autocast" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", "AreaTable id; zero means any area where other conditions match.", "Quest required to start the rule.", "1 requires the start quest to be active.", "Completed quest ending the rule.", "CMaNGOS condition_entry id.", "Positive requires the aura; negative forbids it.", "Playable-race bitmask; zero means every race.", "0 male, 1 female, 2 either.", "1 automatically applies the spell while the rule matches." };
    }

    internal sealed class SpellMapTable : SpellAreaTableBase
    {
        public SpellMapTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_map";
        protected override string ScopeColumn => "map";
        public override string FallbackHeader => "Map rules";
        public override string FallbackDescription => "Automatically apply this spell by map, quest, condition, aura, race and gender.";
    }

    internal abstract class SpellAreaTableBase : TbcSpellTable
    {
        protected SpellAreaTableBase(CMangosEmulator core) : base(core) { }
        protected abstract string ScopeColumn { get; }
        public override string OrderBy => $"`{ScopeColumn}`, `quest_start`, `aura_spell`";
        public override string SpellFilter(RankChain chain) => $"`spell` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell("spell");
            yield return new WorldColumn { Name = ScopeColumn, IsKey = true };
            yield return new WorldColumn { Name = "quest_start", IsKey = true };
            yield return new WorldColumn { Name = "quest_start_active", IsKey = true, Enum = Boolean };
            yield return new WorldColumn { Name = "quest_end" };
            yield return new WorldColumn { Name = "condition_id" };
            yield return new WorldColumn { Name = "aura_spell", Kind = ColumnKind.Int, IsKey = true, ShowSpellPicker = true, FallbackNegativeOption = "Forbid this aura" };
            yield return new WorldColumn { Name = "racemask", IsKey = true, Flags = RaceMask };
            yield return new WorldColumn { Name = "gender", IsKey = true, DefaultValue = "2", Enum = Gender };
            yield return new WorldColumn { Name = "autocast", DefaultValue = "1", Enum = Boolean };
        }
        public override string FallbackEmpty => $"No {ScopeColumn} rule. Use Add to create one for this spell.";
        public override string[] FallbackLabels => new[] { "Spell", ScopeColumn == "map" ? "Map" : "Area", "Start quest", "Quest active", "End quest", "Condition", "Required/forbidden aura", "Race mask", "Gender", "Autocast" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", $"{ScopeColumn} id; zero means global.", "Quest required to start the rule.", "1 requires the start quest to be active.", "Completed quest ending the rule.", "CMaNGOS condition_entry id.", "Positive requires the aura; negative forbids it.", "Playable-race bitmask; zero means every race.", "0 male, 1 female, 2 either.", "1 automatically applies the spell." };
    }

    internal sealed class SpellMapRestrictionTable : TbcSpellTable
    {
        private static readonly EnumSet RestrictionTypes = new EnumSet(null, new long[] { 0, 1 },
            new[] { "Block inside the selected map/area", "Allow only inside the selected map/area" });
        public SpellMapRestrictionTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_map_restriction";
        public override string OrderBy => "`map`, `area`";
        public override string SpellFilter(RankChain chain) => $"`spell` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell("spell");
            yield return new WorldColumn { Name = "map", IsKey = true };
            yield return new WorldColumn { Name = "area", IsKey = true };
            yield return new WorldColumn { Name = "restriction_type", Enum = RestrictionTypes };
            yield return new WorldColumn { Name = "remove_aura", Enum = Boolean };
        }
        public override string FallbackHeader => "Map restrictions";
        public override string FallbackDescription => "Custom MasterWoW cast blocks scoped to a map or area.";
        public override string FallbackEmpty => "No map/area cast restriction. Use Add to create one.";
        public override string[] FallbackLabels => new[] { "Spell", "Map", "Area", "Restriction", "Remove aura" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", "Map id; zero matches every map.", "Area id; zero matches the whole map.", "Core restriction type. Only use values supported by the current core.", "1 removes an already active aura when entering the restricted place." };
    }

    internal sealed class SpellProcItemEnchantTable : TbcSpellTable
    {
        public SpellProcItemEnchantTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_proc_item_enchant";
        public override string OrderBy => "`entry`";
        public override bool IsSingleRow => true;
        public override bool InheritsRankChain => true;
        public override string SpellFilter(RankChain chain) => In("entry", chain.SpellId, chain.FirstSpellId);
        protected override IEnumerable<WorldColumn> CreateColumns() { yield return Spell(); yield return new WorldColumn { Name = "ppmRate", Kind = ColumnKind.Float }; }
        public override string FallbackHeader => "Enchant PPM";
        public override string FallbackDescription => "Weapon-enchant proc rate expressed as procs per minute.";
        public override string FallbackEmpty => "No item-enchant PPM override. Use Add to create one.";
        public override string[] FallbackLabels => new[] { "Spell", "Procs per minute" };
        public override string[] FallbackTooltips => new[] { "First-rank enchant proc spell.", "Expected procs per minute; zero disables this override." };
    }

    internal sealed class SpellScriptTargetTable : TbcSpellTable
    {
        private static readonly EnumSet Types = new EnumSet(null, new long[] { 0, 1, 2, 3, 4 }, new[] { "GameObject entry", "Living creature entry", "Dead creature entry", "Creature DB GUID", "GameObject DB GUID" });
        public SpellScriptTargetTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_script_target";
        public override string OrderBy => "`type`, `targetEntry`";
        public override string SpellFilter(RankChain chain) => $"`entry` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell();
            yield return new WorldColumn { Name = "type", IsKey = true, Enum = Types };
            yield return new WorldColumn { Name = "targetEntry", IsKey = true };
            yield return new WorldColumn { Name = "inverseEffectMask", Flags = WorldEnums.EffectIndexMask };
        }
        public override string FallbackHeader => "Script targets";
        public override string FallbackDescription => "Creature or gameobject targets used by script-target implicit target modes.";
        public override string FallbackEmpty => "No scripted target rows. Use Add when an effect uses a script-target target mode.";
        public override string[] FallbackLabels => new[] { "Spell", "Target kind", "Entry or DB GUID", "Excluded effects" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", "The namespace used for Target entry.", "Creature/gameobject template entry or DB GUID, depending on target kind.", "Effect-index bitmask excluded from this target; zero allows every effect." };
    }

    internal sealed class SpellTargetPositionTable : TbcSpellTable
    {
        public SpellTargetPositionTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_target_position";
        public override string OrderBy => "`id`";
        public override bool IsSingleRow => true;
        public override string SpellFilter(RankChain chain) => $"`id` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            yield return Spell("id");
            yield return new WorldColumn { Name = "target_map" };
            yield return new WorldColumn { Name = "target_position_x", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "target_position_y", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "target_position_z", Kind = ColumnKind.Float };
            yield return new WorldColumn { Name = "target_orientation", Kind = ColumnKind.Float };
        }
        public override string FallbackHeader => "Target position";
        public override string FallbackDescription => "Fixed destination for spells using TARGET_LOCATION_DATABASE (17).";
        public override string FallbackEmpty => "No database target position. Use Add to create one.";
        public override string[] FallbackLabels => new[] { "Spell", "Map", "X", "Y", "Z", "Orientation" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", "Destination map id.", "World X coordinate.", "World Y coordinate.", "World Z coordinate.", "Facing in radians." };
    }

    internal sealed class SpellScriptsTable : TbcSpellTable
    {
        public SpellScriptsTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_scripts";
        public override string OrderBy => "`Id`";
        public override bool IsSingleRow => true;
        public override string SpellFilter(RankChain chain) => $"`Id` = {chain.SpellId}";
        protected override IEnumerable<WorldColumn> CreateColumns() { yield return Spell("Id"); yield return new WorldColumn { Name = "ScriptName", Kind = ColumnKind.Text }; }
        public override string FallbackHeader => "Spell script";
        public override string FallbackDescription => "Binds the selected spell to a compiled CMaNGOS SpellScript registration.";
        public override string FallbackEmpty => "No compiled spell script binding. Add only a ScriptName that exists in this core build.";
        public override string[] FallbackLabels => new[] { "Spell", "Compiled script name" };
        public override string[] FallbackTooltips => new[] { "Selected spell.", "Exact name passed to RegisterSpellScript in the active core." };
    }

    internal sealed class SpellConeTable : TbcSpellTable
    {
        public SpellConeTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_cone";
        public override string OrderBy => "`Id`";
        public override bool IsSingleRow => true;
        public override string SpellFilter(RankChain chain) => In("Id", chain.SpellId, chain.FirstSpellId);
        protected override IEnumerable<WorldColumn> CreateColumns() { yield return Spell("Id"); yield return new WorldColumn { Name = "ConeDegrees", Kind = ColumnKind.Int, DefaultValue = "60" }; }
        public override string FallbackHeader => "Cone";
        public override string FallbackDescription => "Server cone angle override, inherited from the first rank.";
        public override string FallbackEmpty => "No cone override. Use Add to create one.";
        public override string[] FallbackLabels => new[] { "Spell", "Cone degrees" };
        public override string[] FallbackTooltips => new[] { "Spell or first rank.", "Signed cone angle in degrees, exactly as consumed by this core." };
    }

    /// <summary>
    /// The server copy is deliberately separate from the client spell table. Adding a missing row
    /// starts from the selected client record, then exposes the two server-only fields as well.
    /// </summary>
    internal sealed class SpellTemplateTable : TbcSpellTable
    {
        private static readonly FlagSet ServerAttributes = new FlagSet("SpellAttributesServerside", null,
            new uint[] { 1, 2, 4 }, new[] { "Prevent invisibility", "Apply AoE cap", "Ignore evade" });

        public SpellTemplateTable(CMangosEmulator core) : base(core) { }
        public override string Name => "spell_template";
        public override string OrderBy => "`Id`";
        public override bool IsSingleRow => true;
        public override string SpellFilter(RankChain chain) => $"`Id` = {chain.SpellId}";

        private static FlagSet AttributeFlags(string field)
        {
            var entries = SpellAttributeCatalog.ForField(field);
            if (entries == null) return null;
            return new FlagSet(field, null,
                entries.Select(entry => uint.Parse(entry.Mask.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray(),
                entries.Select(entry => entry.DisplayName).ToArray());
        }

        private static string Client(NewRowContext context, string serverName, string fallback = "0")
        {
            var row = context.ClientSpellRow;
            if (row == null) return fallback;

            string clientName = serverName;
            if (serverName == "CastUI") clientName = "castUI";
            else if (serverName == "MaxLevel") clientName = "MaximumLevel";
            else if (serverName == "MaxTargetLevel") clientName = "MaximumTargetLevel";
            else if (serverName == "DmgClass") clientName = "SpellDamageClass";
            else if (serverName.StartsWith("DmgMultiplier")) clientName = serverName.Replace("DmgMultiplier", "EffectDamageMultiplier");
            else if (serverName == "MinFactionId") clientName = "MinimumFactionId";
            else if (serverName == "AreaId") clientName = "AreaID";
            else if (serverName == "SpellVisual") clientName = "SpellVisual1";
            else if (serverName == "SchoolMask") clientName = "School";
            else if (serverName.StartsWith("SpellName"))
            {
                var suffix = serverName == "SpellName" ? 0 : int.Parse(serverName.Substring(9), CultureInfo.InvariantCulture) - 1;
                clientName = "SpellName" + suffix;
            }
            else if (serverName.StartsWith("Rank"))
                clientName = "SpellRank" + (int.Parse(serverName.Substring(4), CultureInfo.InvariantCulture) - 1);

            if (!row.Table.Columns.Contains(clientName)) return fallback;
            var value = row[clientName];
            return value == null || value == DBNull.Value ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static WorldColumn C(string name, ColumnKind kind = ColumnKind.UInt, string defaultValue = "0", FlagSet flags = null)
        {
            return new WorldColumn
            {
                Name = name,
                Kind = kind,
                DefaultValue = defaultValue,
                Flags = flags,
                NewRowValue = context => Client(context, name, defaultValue),
                Label = name,
                Tooltip = "Server spell_template field. A new row is initialized from the selected client Spell.dbc record."
            };
        }

        protected override IEnumerable<WorldColumn> CreateColumns()
        {
            var id = C("Id"); id.IsKey = true; id.IsPrimarySpellKey = true; yield return id;
            foreach (var name in new[] { "Category", "CastUI", "Dispel", "Mechanic" }) yield return C(name);
            foreach (var name in new[] { "Attributes", "AttributesEx", "AttributesEx2", "AttributesEx3", "AttributesEx4", "AttributesEx5", "AttributesEx6" })
                yield return C(name, flags: AttributeFlags(name));
            foreach (var name in new[] { "Stances", "StancesNot", "Targets", "TargetCreatureType", "RequiresSpellFocus", "FacingCasterFlags", "CasterAuraState", "TargetAuraState", "CasterAuraStateNot", "TargetAuraStateNot", "CastingTimeIndex", "RecoveryTime", "CategoryRecoveryTime", "InterruptFlags", "AuraInterruptFlags", "ChannelInterruptFlags", "ProcFlags", "ProcChance", "ProcCharges", "MaxLevel", "BaseLevel", "SpellLevel", "DurationIndex", "PowerType", "ManaCost", "ManaCostPerlevel", "ManaPerSecond", "ManaPerSecondPerLevel", "RangeIndex" }) yield return C(name, defaultValue: name == "RangeIndex" ? "1" : "0");
            yield return C("Speed", ColumnKind.Float);
            foreach (var name in new[] { "ModalNextSpell", "StackAmount", "Totem1", "Totem2" }) yield return C(name);
            for (var i = 1; i <= 8; ++i) yield return C("Reagent" + i, ColumnKind.Int);
            for (var i = 1; i <= 8; ++i) yield return C("ReagentCount" + i);
            yield return C("EquippedItemClass", ColumnKind.Int, "-1");
            yield return C("EquippedItemSubClassMask", ColumnKind.Int);
            yield return C("EquippedItemInventoryTypeMask", ColumnKind.Int);

            for (var i = 1; i <= 3; ++i) yield return C("Effect" + i);
            for (var i = 1; i <= 3; ++i) yield return C("EffectDieSides" + i, ColumnKind.Int);
            for (var i = 1; i <= 3; ++i) yield return C("EffectBaseDice" + i);
            for (var i = 1; i <= 3; ++i) yield return C("EffectDicePerLevel" + i, ColumnKind.Float);
            for (var i = 1; i <= 3; ++i) yield return C("EffectRealPointsPerLevel" + i, ColumnKind.Float);
            for (var i = 1; i <= 3; ++i) yield return C("EffectBasePoints" + i, ColumnKind.Int);
            foreach (var prefix in new[] { "EffectMechanic", "EffectImplicitTargetA", "EffectImplicitTargetB", "EffectRadiusIndex", "EffectApplyAuraName", "EffectAmplitude" })
                for (var i = 1; i <= 3; ++i) yield return C(prefix + i);
            for (var i = 1; i <= 3; ++i) yield return C("EffectMultipleValue" + i, ColumnKind.Float);
            foreach (var prefix in new[] { "EffectChainTarget", "EffectItemType" })
                for (var i = 1; i <= 3; ++i) yield return C(prefix + i);
            foreach (var prefix in new[] { "EffectMiscValue", "EffectMiscValueB" })
                for (var i = 1; i <= 3; ++i) yield return C(prefix + i, ColumnKind.Int);
            for (var i = 1; i <= 3; ++i) yield return C("EffectTriggerSpell" + i);
            for (var i = 1; i <= 3; ++i) yield return C("EffectPointsPerComboPoint" + i, ColumnKind.Float);
            foreach (var name in new[] { "SpellVisual", "SpellIconID", "ActiveIconID", "SpellPriority" }) yield return C(name);

            for (var i = 1; i <= 16; ++i)
            {
                var name = i == 1 ? "SpellName" : "SpellName" + i;
                yield return C(name, ColumnKind.Text, i == 1 ? "New server spell" : "");
            }
            for (var i = 1; i <= 16; ++i) yield return C("Rank" + i, ColumnKind.Text, "");

            foreach (var name in new[] { "ManaCostPercentage", "StartRecoveryCategory", "StartRecoveryTime", "MaxTargetLevel", "SpellFamilyName" }) yield return C(name);
            var family = C("SpellFamilyFlags", ColumnKind.ULong);
            family.NewRowValue = context =>
            {
                var row = context.ClientSpellRow;
                if (row == null || !row.Table.Columns.Contains("SpellFamilyFlags1") || !row.Table.Columns.Contains("SpellFamilyFlags2")) return "0";
                uint.TryParse(row["SpellFamilyFlags1"].ToString(), out var low);
                uint.TryParse(row["SpellFamilyFlags2"].ToString(), out var high);
                return (((ulong)high << 32) | low).ToString(CultureInfo.InvariantCulture);
            };
            yield return family;
            foreach (var name in new[] { "MaxAffectedTargets", "DmgClass", "PreventionType" }) yield return C(name);
            yield return C("StanceBarOrder", ColumnKind.Int);
            for (var i = 1; i <= 3; ++i) yield return C("DmgMultiplier" + i, ColumnKind.Float);
            foreach (var name in new[] { "MinFactionId", "MinReputation", "RequiredAuraVision", "TotemCategory1", "TotemCategory2", "AreaId" }) yield return C(name);
            yield return C("SchoolMask", flags: WorldEnums.SchoolMask, defaultValue: "1");
            yield return C("IsServerSide");
            yield return C("AttributesServerside", flags: ServerAttributes);
        }

        public override string FallbackHeader => "Server spell";
        public override string FallbackDescription => "The complete CMaNGOS spell_template record. This is separate from the client Spell.dbc row. Add clones the selected client values and leaves server-only fields explicit.";
        public override string FallbackEmpty => "No server spell_template row exists. Add creates an editable client-derived row; review every field before saving.";
        public override string[] FallbackLabels => Columns.Select(column => column.Name).ToArray();
        public override string[] FallbackTooltips => Columns.Select(column => column.Tooltip ?? column.Name).ToArray();
    }
}
