// ------------------------------------------------------------------------------------------------
// MappingStatus.cs: the mapping status lines shown in the settings' Mapping section (F9).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Writes the read-only Mapping.Status and Mapping.MissingKeys values, so users see what the
    /// mapping file did without reading the log.
    /// </summary>
    public static class MappingStatus
    {
        /// <summary>
        /// Describes a successful load.
        /// </summary>
        /// <param name="count">Keys the file maps.</param>
        /// <returns>"Loaded 20 keys", or a hint to edit the file when it maps none.</returns>
        public static string Loaded(int count)
        {
            return count == 0
                ? "No keys mapped yet: edit the mapping file to add your buttons"
                : "Loaded " + Keys(count);
        }

        /// <summary>
        /// Describes a file that couldn't be loaded and what the mod uses instead.
        /// </summary>
        /// <param name="error">The parse or read error, which names the line when known.</param>
        /// <param name="lastGoodCount">Keys in the mapping still in use, or null when none was loaded before.</param>
        /// <returns>The error and its consequence.</returns>
        public static string Failed(string error, int? lastGoodCount)
        {
            return "Error: " + error + ". " + (lastGoodCount.HasValue
                ? $"Still using the last good mapping ({Keys(lastGoodCount.Value)})"
                : "No keys are mapped until it's fixed");
        }

        /// <summary>
        /// Lists the keys hover hints used that the mapping doesn't cover, each with its hint text
        /// (the bracketed key dropped), sorted by key.
        /// </summary>
        /// <param name="missing">Key name to the plain hint line it was seen in.</param>
        /// <param name="map">The current mapping; keys it now covers are left out.</param>
        /// <returns>"E (Fill all), T (Toggle crafting chest)", or "None".</returns>
        public static string MissingKeys(IReadOnlyDictionary<string, string> missing, KeyMap map)
        {
            string[] entries = missing
                .Where(entry => map.Buttons(entry.Key) == null)
                .OrderBy(entry => entry.Key, System.StringComparer.OrdinalIgnoreCase)
                .Select(entry => Entry(entry.Key, entry.Value))
                .ToArray();
            return entries.Length == 0 ? "None" : string.Join(", ", entries);
        }

        /// <summary>
        /// One missing key with what its hint says.
        /// </summary>
        /// <param name="key">Key name.</param>
        /// <param name="line">Plain hint line containing "[key]".</param>
        /// <returns>"E (Fill all)", or "E" when the line has nothing else.</returns>
        private static string Entry(string key, string line)
        {
            string hint = line.Replace("[" + key + "]", "").Trim();
            return hint.Length == 0 ? key : $"{key} ({hint})";
        }

        /// <summary>
        /// A key count with the right plural.
        /// </summary>
        /// <param name="count">Number of keys.</param>
        /// <returns>"1 key" or "20 keys".</returns>
        private static string Keys(int count)
        {
            return count == 1 ? "1 key" : $"{count} keys";
        }
    }
}
