// ------------------------------------------------------------------------------------------------
// SettingNames.cs: the config's section and setting names, as users see them.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Section and setting names as users see them in the settings UIs and the .cfg (renamed for new
    /// users 2026-11-08): sections in Title Case, settings in sentence case. ConfigurationManager and
    /// Jotunn's Mod Settings sort sections by name, and these read in order.
    /// </summary>
    public static class SettingNames
    {
        /// <summary>Master switch and the art the mod's glyphs use.</summary>
        public const string General = "General";

        /// <summary>Glyphs in Valheim's hints and other mods' hover hints.</summary>
        public const string HintGlyphs = "Hint Glyphs";

        /// <summary>Hotbar and quick-slot labels and their groups.</summary>
        public const string HotbarGlyphs = "Hotbar Glyphs";

        /// <summary>The mapping file and the keys it's missing (read-only).</summary>
        public const string KeyGlyphMapping = "Key Glyph Mapping";

        /// <summary>Master switch.</summary>
        public const string Enabled = "Enabled";

        /// <summary>
        /// Controller glyphs in Valheim's hints even after keyboard or mouse input (was KeyPrompts).
        /// "Force": off leaves Valheim's own choice, not keyboard keys.
        /// </summary>
        public const string ForceGlyphsInValheimHints = "Force controller glyphs in Valheim hints";

        /// <summary>Mapped buttons for keys in other mods' hover hints (was HoverText).</summary>
        public const string ForceGlyphsInModHints = "Force controller glyphs in mod hints";

        /// <summary>Which art the mod's own glyphs use (was ButtonGlyphs).</summary>
        public const string GlyphsUsedByMod = "Glyphs used by mod";

        /// <summary>Mapped buttons on hotbar and quick slots (was HotbarLabels).</summary>
        public const string ForceHotbarGlyphs = "Force hotbar controller glyphs";

        /// <summary>Color of each group's held button and line (was PrefixColors).</summary>
        public const string HotbarGroupColors = "Hotbar group colors";

        /// <summary>Backing behind each group's held button and line (was PrefixBackground).</summary>
        public const string HotbarGroupBackground = "Hotbar group background";

        /// <summary>The mapping file's path (was File).</summary>
        public const string MappingFile = "Mapping file";

        /// <summary>Loaded key count or parse error (was Status).</summary>
        public const string MappingStatus = "Mapping status";

        /// <summary>Keys in hover hints and mod keybindings the mapping lacks (was MissingKeys and UnmappedShortcuts).</summary>
        public const string KeysMissingFromMapping = "Keys missing from mapping";
    }
}
