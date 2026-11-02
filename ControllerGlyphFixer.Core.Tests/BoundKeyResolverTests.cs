using ControllerGlyphFixer.Core;

namespace ControllerGlyphFixer.Core.Tests;

public class BoundKeyResolverTests
{
    private const string ASprite = "<sprite=\"xbox\" name=\"button_a\">";

    private static string NoJoy(string name) => "";

    private static string JoyUseOnly(string name) => name == "JoyUse" ? ASprite : "";

    private static string TranslateQ(string key) => key == "Q" ? "[L5]" : null;

    [Fact]
    public void Controller_binding_wins_over_keyboard_key()
    {
        Assert.Equal(ASprite, BoundKeyResolver.Resolve("Use", "E", showGlyphs: true, JoyUseOnly, TranslateQ));
    }

    [Fact]
    public void Keyboard_only_binding_uses_the_mapping()
    {
        Assert.Equal("[L5]", BoundKeyResolver.Resolve("Pickup", "Q", showGlyphs: true, NoJoy, TranslateQ));
    }

    [Fact]
    public void Unmapped_keyboard_only_binding_is_unchanged()
    {
        Assert.Equal("Tab", BoundKeyResolver.Resolve("Inventory", "Tab", showGlyphs: true, NoJoy, TranslateQ));
    }

    [Fact]
    public void Glyphs_off_returns_the_original_result_without_lookups()
    {
        string result = BoundKeyResolver.Resolve("Use", "E", showGlyphs: false, Throws, ThrowsTranslate);

        Assert.Equal("E", result);
    }

    [Theory]
    [InlineData("JoyUse")]
    [InlineData("JoyHotbar1")]
    public void Controller_bindings_are_left_alone_and_never_looked_up_again(string name)
    {
        // Recursion guard: looking up "Joy" + name calls GetBoundKeyString again with a Joy name
        string result = BoundKeyResolver.Resolve(name, ASprite, showGlyphs: true, Throws, ThrowsTranslate);

        Assert.Equal(ASprite, result);
    }

    [Fact]
    public void Missing_binding_messages_are_not_translated()
    {
        const string missing = "MISSING BUTTON DEF \"Foo\"";

        Assert.Equal(missing, BoundKeyResolver.Resolve("Foo", missing, showGlyphs: true, NoJoy, TranslateQ));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_names_are_unchanged(string name)
    {
        Assert.Equal("E", BoundKeyResolver.Resolve(name, "E", showGlyphs: true, Throws, ThrowsTranslate));
    }

    private static string Throws(string name) => throw new InvalidOperationException("should not look up " + name);

    private static string ThrowsTranslate(string key) => throw new InvalidOperationException("should not translate " + key);
}
