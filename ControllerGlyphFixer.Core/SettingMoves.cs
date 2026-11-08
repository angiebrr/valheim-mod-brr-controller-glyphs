// ------------------------------------------------------------------------------------------------
// SettingMoves.cs: the config sections and the settings that moved between them.
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// A setting that moved to another config section; its old value is carried over once.
    /// </summary>
    public sealed class SettingMove
    {
        /// <summary>
        /// Creates the move.
        /// </summary>
        /// <param name="key">Setting name, the same in both sections.</param>
        /// <param name="from">Section it used to be in.</param>
        /// <param name="to">Section it is in now.</param>
        public SettingMove(string key, string from, string to)
        {
            Key = key;
            From = from;
            To = to;
        }

        /// <summary>Setting name, the same in both sections.</summary>
        public string Key { get; }

        /// <summary>Section it used to be in.</summary>
        public string From { get; }

        /// <summary>Section it is in now.</summary>
        public string To { get; }
    }

    /// <summary>
    /// Config section names and the settings moved into them (2026-11-08). ConfigurationManager and
    /// Jotunn's Mod Settings sort sections by name, and these read in order.
    /// </summary>
    public static class SettingMoves
    {
        /// <summary>Master switch and the glyph settings.</summary>
        public const string General = "General";

        /// <summary>Hotbar and quick-slot labels and their group prefixes.</summary>
        public const string HotbarLabels = "Hotbar labels";

        /// <summary>The mapping file's path, status and missing keys (read-only).</summary>
        public const string Mapping = "Mapping";

        /// <summary>Every setting that moved, in the order they're listed.</summary>
        public static IReadOnlyList<SettingMove> All { get; } = new[]
        {
            new SettingMove("HotbarLabels", General, HotbarLabels),
            new SettingMove("PrefixColors", General, HotbarLabels),
            new SettingMove("PrefixBackground", General, HotbarLabels),
        };

        /// <summary>
        /// Returns the moves whose old entry is still in the .cfg (BepInEx keeps entries no setting
        /// claims as orphans), i.e. the values to carry over.
        /// </summary>
        /// <param name="orphans">Section and key of each unclaimed .cfg entry.</param>
        /// <returns>The moves to apply, in <see cref="All"/> order.</returns>
        public static IReadOnlyList<SettingMove> Pending(IEnumerable<(string Section, string Key)> orphans)
        {
            var left = new HashSet<(string, string)>(orphans);
            return All.Where(move => left.Contains((move.From, move.Key))).ToArray();
        }
    }
}
