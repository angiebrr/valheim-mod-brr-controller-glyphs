// ------------------------------------------------------------------------------------------------
// SpriteStyleTests.cs: unit tests for picking Valheim's glyph atlas and building sprite tags.
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="SpriteStyle"/>, mirroring ZInput.GetBoundKeyString's atlas choice.
/// </summary>
public class SpriteStyleTests
{
    /// <summary>An explicit glyph setting ignores the connected gamepad type.</summary>
    /// <param name="glyphSetting">GamepadGlyphs name.</param>
    /// <param name="gamepadType">GamepadType name.</param>
    /// <param name="expected">Expected atlas.</param>
    [Theory]
    [InlineData("Xbox", "DualSense", "xbox")]
    [InlineData("Playstation", "XInput", "ps5")]
    [InlineData("Switch", "XInput", "switch2")]
    public void Explicit_glyph_setting_wins(string glyphSetting, string gamepadType, string expected)
    {
        Assert.Equal(expected, SpriteStyle.For(glyphSetting, gamepadType));
    }

    /// <summary>"Auto" picks the atlas from the connected gamepad type.</summary>
    /// <param name="gamepadType">GamepadType name.</param>
    /// <param name="expected">Expected atlas.</param>
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

    /// <summary>Unknown or missing values fall back to xbox, like Valheim.</summary>
    /// <param name="glyphSetting">GamepadGlyphs name.</param>
    /// <param name="gamepadType">GamepadType name.</param>
    [Theory]
    [InlineData("SomeFutureStyle", "XInput")]
    [InlineData(null, null)]
    public void Unknown_values_fall_back_to_xbox(string glyphSetting, string gamepadType)
    {
        Assert.Equal("xbox", SpriteStyle.For(glyphSetting, gamepadType));
    }

    /// <summary>Sprite tags use exactly Valheim's format.</summary>
    [Fact]
    public void Sprite_tag_matches_valheims_format()
    {
        Assert.Equal("<sprite=\"xbox\" name=\"button_a\">", SpriteStyle.Tag("xbox", "button_a"));
    }
}
