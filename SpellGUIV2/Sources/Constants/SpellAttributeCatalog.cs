using Newtonsoft.Json;
using NLog;
using SpellEditor.Sources.Emulator;
using SpellEditor.Sources.VersionControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpellEditor.Sources.Constants
{
    /// <summary>Version- and emulator-specific labels. Raw DBC masks remain authoritative.</summary>
    internal static class SpellAttributeCatalog
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static Catalog _cmangosTbc;

        public static IReadOnlyList<AttributeEntry> ForField(string field)
        {
            if (WoWVersionManager.GetInstance().SelectedVersion().Identity != 243 ||
                !string.Equals(Emulators.Current?.Id, "CMangosTbc", StringComparison.Ordinal))
                return null;

            return LoadCMangosTbc()?.Fields?
                .FirstOrDefault(candidate => string.Equals(candidate.Field, field, StringComparison.Ordinal))?
                .Attributes;
        }

        private static Catalog LoadCMangosTbc()
        {
            if (_cmangosTbc != null) return _cmangosTbc;
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AttributeCatalog", "cmangos-tbc-spell-attributes.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Documentation", "AttributeCatalog", "cmangos-tbc-spell-attributes.json")
            };
            var path = candidates.FirstOrDefault(File.Exists);
            if (path == null)
            {
                Logger.Warn("CMaNGOS TBC spell attribute catalog was not found. Using generic localized labels.");
                return null;
            }
            try
            {
                _cmangosTbc = JsonConvert.DeserializeObject<Catalog>(File.ReadAllText(path));
                return _cmangosTbc;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Could not load CMaNGOS TBC spell attribute catalog from " + path);
                return null;
            }
        }

        internal sealed class Catalog
        {
            [JsonProperty("fields")] public List<AttributeField> Fields { get; set; }
        }

        internal sealed class AttributeField
        {
            [JsonProperty("field")] public string Field { get; set; }
            [JsonProperty("attributes")] public List<AttributeEntry> Attributes { get; set; }
        }

        internal sealed class AttributeEntry
        {
            [JsonProperty("bit_index")] public int BitIndex { get; set; }
            [JsonProperty("mask")] public string Mask { get; set; }
            [JsonProperty("cmangos_enum")] public string EnumName { get; set; }
            [JsonProperty("display_name")] public string DisplayName { get; set; }
            [JsonProperty("description")] public string Description { get; set; }
            [JsonProperty("support")] public string Support { get; set; }
            [JsonProperty("confidence")] public string Confidence { get; set; }

            public string Tooltip => string.Join(Environment.NewLine, new[]
            {
                $"{DisplayName} ({Mask}, bit {BitIndex})",
                string.IsNullOrWhiteSpace(EnumName) ? "CMaNGOS enum: unnamed" : "CMaNGOS enum: " + EnumName,
                string.IsNullOrWhiteSpace(Description) ? null : Description,
                $"Support: {Support}; confidence: {Confidence}"
            }.Where(value => value != null));
        }
    }
}
