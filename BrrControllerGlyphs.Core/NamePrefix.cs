// ------------------------------------------------------------------------------------------------
// NamePrefix.cs: the prefix on every name the mod adds to the game (commands, assets, objects).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Starts the console commands, sprite asset, textures and UI objects the mod creates, so they
    /// can't clash with another mod's and are easy to spot in the console and the scene.
    /// </summary>
    public static class NamePrefix
    {
        /// <summary>"brrcg_": Brr Controller Glyphs.</summary>
        public const string Value = "brrcg_";
    }
}
