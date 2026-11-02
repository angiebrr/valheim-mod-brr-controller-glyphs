using ControllerGlyphFixer.Core;

namespace ControllerGlyphFixer.Core.Tests;

public class SpriteStyleTests
{
    [Theory]
    [InlineData("Xbox", "DualSense", "xbox")]
    [InlineData("Playstation", "XInput", "ps5")]
    [InlineData("Switch", "XInput", "switch2")]
    public void Explicit_glyph_setting_wins(string glyphSetting, string gamepadType, string expected)
    {
        Assert.Equal(expected, SpriteStyle.For(glyphSetting, gamepadType));
    }

    [Theory]
    [InlineData("XInput", "xbox")]
    [InlineData("DualSense", "ps5")]
    [InlineData("DualShock", "ps5")]
    [InlineData("SwitchPro", "switch2")]
    [InlineData("JoyCon", "switch2")]
    [InlineData("JoyCon2Mouse", "switch2")]
    [InlineData("Generic", "xbox")]
    [InlineData("None", "xbox")]
    public void Auto_follows_the_connected_gamepad(string gamepadType, string expected)
    {
        Assert.Equal(expected, SpriteStyle.For("Auto", gamepadType));
    }

    [Theory]
    [InlineData("SomeFutureStyle", "XInput")]
    [InlineData(null, null)]
    public void Unknown_values_fall_back_to_xbox(string glyphSetting, string gamepadType)
    {
        Assert.Equal("xbox", SpriteStyle.For(glyphSetting, gamepadType));
    }

    [Fact]
    public void Sprite_tag_matches_valheims_format()
    {
        Assert.Equal("<sprite=\"xbox\" name=\"button_a\">", SpriteStyle.Tag("xbox", "button_a"));
    }
}
