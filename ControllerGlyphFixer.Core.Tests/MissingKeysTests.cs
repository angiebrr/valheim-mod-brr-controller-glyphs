// ------------------------------------------------------------------------------------------------
// MissingKeysTests.cs: unit tests for the "Keys missing from mapping" list (F9).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="MissingKeys"/>. Keybindings are real settings from my profile's configs;
/// hints are lines mods really write.
/// </summary>
public class MissingKeysTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>No hints and no keybindings.</summary>
    private static readonly HintKeyUse[] NoHints = Array.Empty<HintKeyUse>();

    /// <summary>No keybindings.</summary>
    private static readonly ModShortcut[] NoShortcuts = Array.Empty<ModShortcut>();

    /// <summary>
    /// Builds a keybinding as the plugin reads it: main key first, then modifiers (KeyCode names).
    /// </summary>
    /// <param name="mod">Plugin name.</param>
    /// <param name="setting">Setting name.</param>
    /// <param name="keys">Key names.</param>
    /// <returns>The keybinding.</returns>
    private static ModShortcut Shortcut(string mod, string setting, params string[] keys) => new (mod, setting, keys);

    /// <summary>
    /// Builds a key seen in a hover hint.
    /// </summary>
    /// <param name="key">Key name in the hint.</param>
    /// <param name="line">The plain hint line.</param>
    /// <param name="mods">Mods that patch the hovered object's hover text.</param>
    /// <returns>The hint use.</returns>
    private static HintKeyUse Hint(string key, string line, params string[] mods) => new (key, line, mods);

    /// <summary>Keys the mapping has (AzuEPI's quick slots, ImpactfulSkills' N, AutoFuel's B) aren't listed.</summary>
    [Fact]
    public void Mapped_keys_are_not_listed()
    {
        var shortcuts = new[]
        {
            Shortcut("AzuExtendedPlayerInventory", "Hotkey 1", "Keypad1"),
            Shortcut("ImpactfulSkills", "AOEToggleHotkey", "N"),
        };

        IReadOnlyList<MissingKey> missing = MissingKeys.Find(new[] { Hint("B", "[B] AutoFuel: On", "AutomaticFuel") },
            shortcuts, SteamController);

        Assert.Empty(missing);
        Assert.Equal("None", MissingKeys.Summary(missing));
        Assert.Equal("None", MissingKeys.Tree(missing));
    }

    /// <summary>Only the unmapped keys of a combo are listed (E of "E + LeftShift", LeftAlt of "Alpha1 + LeftAlt").</summary>
    [Fact]
    public void Combos_list_only_their_unmapped_keys()
    {
        var shortcuts = new[]
        {
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
            Shortcut("AzuExtendedPlayerInventory", "Hotkey 7", "Alpha1", "LeftAlt"),
        };

        Assert.Equal(new[] { "E", "LeftAlt" },
            MissingKeys.Find(NoHints, shortcuts, SteamController).Select(key => key.Key));
    }

    /// <summary>
    /// The tree has a branch per key: hints first (with the mods that patch that hover text), then
    /// keybindings (the whole shortcut only for combos).
    /// </summary>
    [Fact]
    public void Tree_lists_hints_then_keybindings_under_each_key()
    {
        var hints = new[] { Hint("E", "[E] Fill all", "AzuCraftyBoxes") };
        var shortcuts = new[]
        {
            Shortcut("PlantEasily", "DecreaseYKey", "DownArrow"),
            Shortcut("Epic Loot", "Socket Overlay Shortcut", "E"),
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
        };

        Assert.Equal(
            "DownArrow\n" +
            "└─ PlantEasily > DecreaseYKey\n" +
            "E\n" +
            "├─ hint (AzuCraftyBoxes): Fill all\n" +
            "├─ Epic Loot > Socket Overlay Shortcut\n" +
            "└─ AzuCraftyBoxes > Container Toggle Key (E + LeftShift)",
            MissingKeys.Tree(MissingKeys.Find(hints, shortcuts, SteamController)));
    }

    /// <summary>A hint names every mod that may have written it, or none when no mod patches that hover text.</summary>
    /// <param name="mods">Mods patching the hovered object's hover text.</param>
    /// <param name="expected">The tree's hint line.</param>
    [Theory]
    [InlineData(new string[0], "└─ hint: Toggle crafting chest")]
    [InlineData(new[] { "AzuCraftyBoxes" }, "└─ hint (AzuCraftyBoxes): Toggle crafting chest")]
    [InlineData(new[] { "AzuCraftyBoxes", "My Little UI" }, "└─ hint (AzuCraftyBoxes or My Little UI): Toggle crafting chest")]
    public void Hint_names_the_mods_that_may_have_written_it(string[] mods, string expected)
    {
        var hints = new[] { Hint("T", "[T] Toggle crafting chest", mods) };

        Assert.Equal("T\n" + expected, MissingKeys.Tree(MissingKeys.Find(hints, NoShortcuts, SteamController)));
    }

    /// <summary>A hint that is only the key (nothing else on the line) shows "hint" alone.</summary>
    [Fact]
    public void Hint_with_no_other_text()
    {
        var hints = new[] { Hint("E", "[E]") };

        Assert.Equal("E\n└─ hint", MissingKeys.Tree(MissingKeys.Find(hints, NoShortcuts, SteamController)));
    }

    /// <summary>
    /// The one-line summary (the .cfg and Jotunn's Mod Settings) lists each key with its mods, each
    /// once; a hint no mod patches counts as "hint".
    /// </summary>
    [Fact]
    public void Summary_lists_each_key_with_its_mods()
    {
        var hints = new[] { Hint("E", "[E] Fill all", "AzuCraftyBoxes"), Hint("T", "[T] Toggle crafting chest") };
        var shortcuts = new[]
        {
            Shortcut("Epic Loot", "Socket Overlay Shortcut", "E"),
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
            Shortcut("Valheim Configuration Manager", "Show config manager", "F1"),
        };

        Assert.Equal("E (AzuCraftyBoxes, Epic Loot), F1 (Valheim Configuration Manager), T (hint)",
            MissingKeys.Summary(MissingKeys.Find(hints, shortcuts, SteamController)));
    }

    /// <summary>The log line names every use of the key, as in the tree.</summary>
    [Fact]
    public void Log_line_names_each_use()
    {
        var shortcuts = new[]
        {
            Shortcut("Epic Loot", "Socket Overlay Shortcut", "E"),
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
        };

        MissingKey key = Assert.Single(MissingKeys.Find(NoHints, shortcuts, SteamController));
        Assert.Equal(
            "Key E isn't in your mapping; used by Epic Loot > Socket Overlay Shortcut, " +
            "AzuCraftyBoxes > Container Toggle Key (E + LeftShift)",
            MissingKeys.LogLine(key));
    }

    /// <summary>Unbound keys, mouse buttons and joystick buttons aren't keyboard keys a layout sends.</summary>
    /// <param name="key">KeyCode name.</param>
    [Theory]
    [InlineData("None")]
    [InlineData("Mouse0")]
    [InlineData("Mouse2")]
    [InlineData("JoystickButton4")]
    public void Non_keyboard_keys_are_skipped(string key)
    {
        Assert.Empty(MissingKeys.Find(NoHints, new[] { Shortcut("UnityExplorer", "Some Keybind", key) }, SteamController));
    }

    /// <summary>A key spelled differently by two sources ("e" and "E") is one key.</summary>
    [Fact]
    public void Keys_are_grouped_regardless_of_case()
    {
        var shortcuts = new[]
        {
            Shortcut("Epic Loot", "Socket Overlay Shortcut", "E"),
            Shortcut("BetterArchery", "Bow Draw Cancel Hotkey", "e"),
        };

        Assert.Single(MissingKeys.Find(new[] { Hint("e", "[e] Fill all") }, shortcuts, SteamController));
    }
}
