namespace SpellEditor.Sources.Emulator.CMangos.Tbc
{
    /// <summary>
    /// CMaNGOS TBC, the 2.4.3 branch. It takes the shared table set as it is, TBC only widened a
    /// couple of the value lists.
    /// </summary>
    public class CMangosTbcEmulator : CMangosEmulator
    {
        public override string Id => "CMangosTbc";

        public override string Name => "CMaNGOS TBC";

        public override string Description => "CMaNGOS TBC world database, the stock tbcmangos schema.";

        public override string DefaultDatabase => "tbcmangos";

        /// <summary>TBC added the death proc.</summary>
        public override FlagSet ProcFlags => CMangosEnums.ProcFlags;

        public override FlagSet ProcFlagsEx => CMangosEnums.ProcFlagsEx;

        /// <summary>TBC added the unstable and Shattrath elixirs.</summary>
        public override FlagSet ElixirMask => CMangosEnums.ElixirMask;

        protected override void ConfigureTables(WorldTableSet tables)
        {
            base.ConfigureTables(tables);
            tables.Add(new SpellBonusCpDataTable(this));
            tables.Add(new SpellAreaTable(this));
            tables.Add(new SpellMapTable(this));
            tables.Add(new SpellMapRestrictionTable(this));
            tables.Add(new SpellProcItemEnchantTable(this));
            tables.Add(new SpellScriptTargetTable(this));
            tables.Add(new SpellTargetPositionTable(this));
            tables.Add(new SpellScriptsTable(this));
            tables.Add(new SpellConeTable(this));
            tables.Add(new SpellTemplateTable(this));
        }
    }
}
