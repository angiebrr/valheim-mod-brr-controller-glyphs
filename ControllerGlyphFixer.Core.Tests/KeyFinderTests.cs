// ------------------------------------------------------------------------------------------------
// KeyFinderTests.cs: unit tests for naming keys pressed while cgf_keylog is on (F9).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="KeyFinder"/>.
/// </summary>
public class KeyFinderTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>Unity Input System key names become the spelling the mapping file uses.</summary>
    /// <param name="inputSystemKey">Name of a UnityEngine.InputSystem.Key value.</param>
    /// <param name="fileName">Name to write in the mapping file.</param>
    [Theory]
    [InlineData("Digit1", "1")]
    [InlineData("Digit0", "0")]
    [InlineData("Numpad1", "Keypad1")]
    [InlineData("NumpadEnter", "NumpadEnter")]
    [InlineData("LeftShift", "LeftShift")]
    [InlineData("Q", "Q")]
    [InlineData("F5", "F5")]
    public void File_name_uses_the_mapping_spelling(string inputSystemKey, string fileName)
    {
        Assert.Equal(fileName, KeyFinder.FileName(inputSystemKey));
    }

    /// <summary>A mapped key shows its buttons as written in the mapping file.</summary>
    /// <param name="inputSystemKey">Key the game received.</param>
    /// <param name="expected">Message line.</param>
    [Theory]
    [InlineData("Numpad1", "Keypad1: R5 + DPadUp")]
    [InlineData("Digit3", "3: R4 + DPadDown")]
    [InlineData("Q", "Q: L5")]
    [InlineData("LeftShift", "LeftShift: LPadDown")]
    public void Mapped_key_shows_its_buttons(string inputSystemKey, string expected)
    {
        Assert.Equal(expected, KeyFinder.Describe(inputSystemKey, SteamController));
    }

    /// <summary>A key the mapping lacks says so, under the name to add.</summary>
    [Fact]
    public void Unmapped_key_says_it_is_missing()
    {
        Assert.Equal("Numpad7: Keypad7 isn't in your mapping", KeyFinder.Describe("Numpad7", SteamController));
    }

    /// <summary>A key whose file name matches Unity's says it once.</summary>
    [Fact]
    public void Unmapped_key_with_the_same_name()
    {
        Assert.Equal("E isn't in your mapping", KeyFinder.Describe("E", SteamController));
    }
}
