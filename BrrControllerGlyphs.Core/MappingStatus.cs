// ------------------------------------------------------------------------------------------------
// MappingStatus.cs: the "Mapping status" line shown in the settings (F9).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Writes the read-only "Mapping status" value, so users see what the mapping file did without
    /// reading the log.
    /// </summary>
    public static class MappingStatus
    {
        /// <summary>Shown when a list of missing keys is empty.</summary>
        public const string None = "None";

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
