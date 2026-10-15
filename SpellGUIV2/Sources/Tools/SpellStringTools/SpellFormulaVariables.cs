using SpellEditor.Sources.DBC;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace SpellEditor.Sources.SpellStringTools
{
    // The 140 variables that can appear inside a ${}. Case sensitive, and a different vocabulary
    // to the $s1 style tokens in SpellStringReferenceResolver.
    public static class SpellFormulaVariables
    {
        public delegate double VariableResolver(DataRow record);

        private static readonly Dictionary<string, VariableResolver> Resolvers = Build();
        private static readonly HashSet<string> DynamicVariables = BuildDynamicVariables();

        public static bool IsKnown(string name) => name != null && Resolvers.ContainsKey(name);
        public static bool IsDynamic(string name) => name != null && DynamicVariables.Contains(name);

        public static bool TryResolve(string name, DataRow record, out double value)
        {
            value = 0;
            if (name == null || !Resolvers.TryGetValue(name, out var resolver))
                return false;
            value = resolver(record);
            return true;
        }

        private static Dictionary<string, VariableResolver> Build()
        {
            var map = new Dictionary<string, VariableResolver>(StringComparer.Ordinal);

            for (int i = 1; i <= 3; ++i)
            {
                var index = i;
                // Signed, unlike the $m token, which prints the absolute value.
                Add(map, "m" + index, record => Column(record, "EffectBasePoints" + index) + 1);
                Add(map, "M" + index, record => Column(record, "EffectBasePoints" + index) + Column(record, "EffectDieSides" + index));
                AddPair(map, "a" + index, "A" + index, record => Radius(record, index));
                AddPair(map, "x" + index, "X" + index, record => Column(record, "EffectChainTarget" + index));
                // Milliseconds, unlike the $t token, which prints seconds.
                AddPair(map, "t" + index, "T" + index, record => Column(record, "EffectAmplitude" + index));
                AddPair(map, "b" + index, "B" + index, record => Column(record, "EffectPointsPerComboPoint" + index));
                AddPair(map, "e" + index, "E" + index, record => Column(record, "EffectMultipleValue" + index));
                AddPair(map, "f" + index, "F" + index, record => Column(record, "EffectDamageMultiplier" + index));
                AddPair(map, "q" + index, "Q" + index, record => Column(record, "EffectMiscValue" + index));
                AddPair(map, "bc" + index, "BC" + index, record => Column(record, "EffectBonusMultiplier" + index));
            }

            AddPair(map, "d", "D", Duration);
            // A spell costing a percentage of base mana has nothing useful in this column.
            AddPair(map, "c", "C", record => Column(record, "ManaCost"));
            AddPair(map, "p", "P", record => Column(record, "ManaPerSecond"));
            AddPair(map, "h", "H", record => Column(record, "ProcChance"));
            AddPair(map, "n", "N", record => Column(record, "ProcCharges"));
            AddPair(map, "u", "U", record => Column(record, "StackAmount"));
            AddPair(map, "v", "V", record => Column(record, "MaximumTargetLevel"));
            AddPair(map, "i", "I", record => Column(record, "MaximumAffectedTargets"));

            return map;
        }

        private static HashSet<string> BuildDynamicVariables()
        {
            return new HashSet<string>(new[] {
                "STR", "AGI", "STA", "INT", "SPI", "str", "agi", "sta", "int", "spi",
                "ap", "AP", "rap", "RAP", "mwb", "MWB", "owb", "OWB", "rwb", "RWB",
                "mw", "MW", "ow", "OW", "rw", "RW", "ar", "AR", "mws", "MWS",
                "ows", "OWS", "rws", "RWS", "pl", "PL", "hnd", "HND", "sp", "SP",
                "sph", "spfi", "spn", "spfr", "sps", "spa", "SPH", "SPFI", "SPN",
                "SPFR", "SPS", "SPA", "bh", "BH", "ph", "pfi", "pn", "pfr", "ps",
                "pa", "PH", "PFI", "PN", "PFR", "PS", "PA", "pbh", "pBH", "pbhd", "pBHD"
            }, StringComparer.Ordinal);
        }

        private static void Add(Dictionary<string, VariableResolver> map, string name, VariableResolver resolver)
        {
            map[name] = resolver;
        }

        private static void AddPair(Dictionary<string, VariableResolver> map, string lower, string upper, VariableResolver resolver)
        {
            map[lower] = resolver;
            map[upper] = resolver;
        }

        private static double Column(DataRow record, string column)
        {
            if (record == null || !record.Table.Columns.Contains(column))
                return 0;
            var raw = record[column];
            if (raw == null || raw == DBNull.Value)
                return 0;
            try
            {
                return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Seconds in a formula, unlike the $d token, which prints "N seconds".
        private static double Duration(DataRow record)
        {
            var dbc = DBCManager.GetInstance().FindDbcForBinding("SpellDuration");
            if (dbc == null)
                return 0;
            var entry = dbc.LookupRecord((uint)Column(record, "DurationIndex"));
            if (entry == null || !entry.ContainsKey("BaseDuration"))
                return 0;
            if (!double.TryParse(entry["BaseDuration"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var milliseconds))
                return 0;
            // An infinite duration is stored as -1, which reads back as uint max.
            if (milliseconds < 0 || milliseconds >= uint.MaxValue)
                return 0;
            return milliseconds / 1000d;
        }

        private static double Radius(DataRow record, int index)
        {
            var dbc = DBCManager.GetInstance().FindDbcForBinding("SpellRadius") as SpellRadius;
            if (dbc == null)
                return 0;
            return dbc.LookupRadius((uint)Column(record, "EffectRadiusIndex" + index));
        }
    }
}
