// ------------------------------------------------------------------------------------------------
// BoundKeyResolverTests.cs: unit tests for what $KEY_ prompts resolve to (F1 + F2).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="BoundKeyResolver.Resolve"/>, with fake lookups standing in for Valheim.
/// </summary>
public class BoundKeyResolverTests
{
    /// <summary>A Valheim controller glyph, as returned for a Joy binding.</summary>
    private const string ASprite = "<sprite=\"xbox\" name=\"button_a\">";

    /// <summary>
    /// Fake Joy lookup for a game with no controller bindings.
    /// </summary>
    /// <param name="name">Joy binding name.</param>
    /// <returns>Always empty.</returns>
    private static string NoJoy(string name) => "";

    /// <summary>
    /// Fake Joy lookup where only JoyUse is bound (to A).
    /// </summary>
    /// <param name="name">Joy binding name.</param>
    /// <returns>The A sprite for JoyUse, else empty.</returns>
    private static string JoyUseOnly(string name) => name == "JoyUse" ? ASprite : "";

    /// <summary>
    /// Fake mapping where only Q is mapped (to L5).
    /// </summary>
    /// <param name="key">Keyboard key.</param>
    /// <returns>"[L5]" for Q, else null.</returns>
    private static string TranslateQ(string key) => key == "Q" ? "[L5]" : null;

    /// <summary>
    /// Fake lookup that fails the test if it's called.
    /// </summary>
    /// <param name="name">Binding name.</param>
    /// <returns>Never returns.</returns>
    private static string Throws(string name) => throw new InvalidOperationException("should not look up " + name);

    /// <summary>
    /// Fake translation that fails the test if it's called.
    /// </summary>
    /// <param name="key">Keyboard key.</param>
    /// <returns>Never returns.</returns>
    private static string ThrowsTranslate(string key) => throw new InvalidOperationException("should not translate " + key);

    /// <summary>A controller binding is shown instead of the keyboard key.</summary>
    [Fact]
    public void Controller_binding_wins_over_keyboard_key()
    {
        Assert.Equal(ASprite, BoundKeyResolver.Resolve("Use", "E", showGlyphs: true, JoyUseOnly, TranslateQ));
    }

    /// <summary>
    /// Valheim's radial menu binding breaks the "Joy" + name rule (OpenRadial / JoyRadial); My Little
    /// UI's cooking station hint asks for $KEY_OpenRadial and showed the keyboard key G (in game
    /// 2026-11-08).
    /// </summary>
    [Fact]
    public void OpenRadial_uses_the_JoyRadial_binding()
    {
        static string JoyRadialOnly(string name) => name == "JoyRadial" ? ASprite : "";

        Assert.Equal(ASprite, BoundKeyResolver.Resolve("OpenRadial", "G", showGlyphs: true, JoyRadialOnly, TranslateQ));
    }

    /// <summary>Without a controller binding, the mapped Steam button is shown.</summary>
    [Fact]
    public void Keyboard_only_binding_uses_the_mapping()
    {
        Assert.Equal("[L5]", BoundKeyResolver.Resolve("Pickup", "Q", showGlyphs: true, NoJoy, TranslateQ));
    }

    /// <summary>Without a controller binding or a mapping, the key is unchanged.</summary>
    [Fact]
    public void Unmapped_keyboard_only_binding_is_unchanged()
    {
        Assert.Equal("Tab", BoundKeyResolver.Resolve("Inventory", "Tab", showGlyphs: true, NoJoy, TranslateQ));
    }

    /// <summary>With glyphs off, Valheim's result is returned and nothing is looked up.</summary>
    [Fact]
    public void Glyphs_off_returns_the_original_result_without_lookups()
    {
        string result = BoundKeyResolver.Resolve("Use", "E", showGlyphs: false, Throws, ThrowsTranslate);

        Assert.Equal("E", result);
    }

    /// <summary>Joy bindings pass through untouched, so looking one up can't recurse.</summary>
    /// <param name="name">A Joy binding name.</param>
    [Theory]
    [InlineData("JoyUse")]
    [InlineData("JoyHotbar1")]
    public void Controller_bindings_are_left_alone_and_never_looked_up_again(string name)
    {
        string result = BoundKeyResolver.Resolve(name, ASprite, showGlyphs: true, Throws, ThrowsTranslate);

        Assert.Equal(ASprite, result);
    }

    /// <summary>Valheim's missing-binding messages aren't translated.</summary>
    [Fact]
    public void Missing_binding_messages_are_not_translated()
    {
        const string missing = "MISSING BUTTON DEF \"Foo\"";

        Assert.Equal(missing, BoundKeyResolver.Resolve("Foo", missing, showGlyphs: true, NoJoy, TranslateQ));
    }

    /// <summary>Null or empty binding names return Valheim's result without lookups.</summary>
    /// <param name="name">Null or empty name.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_names_are_unchanged(string name)
    {
        Assert.Equal("E", BoundKeyResolver.Resolve(name, "E", showGlyphs: true, Throws, ThrowsTranslate));
    }
}
