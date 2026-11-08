// ------------------------------------------------------------------------------------------------
// ShortcutAuditTests.cs: unit tests for finding mod shortcut keys the mapping lacks (F9).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="ShortcutAudit"/>. Shortcuts are real settings from my profile's configs.
/// </summary>
public class ShortcutAuditTests
{
    /// <summary>My Steam Controller layout, loaded from the test fixture file.</summary>
    private static readonly KeyMap SteamController = KeyMap.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-controller.yaml")));

    /// <summary>
    /// Builds a shortcut as the plugin reads it: main key first, then modifiers (KeyCode names).
    /// </summary>
    /// <param name="mod">Plugin name.</param>
    /// <param name="setting">Setting name.</param>
    /// <param name="keys">Key names.</param>
    /// <returns>The shortcut.</returns>
    private static ModShortcut Shortcut(string mod, string setting, params string[] keys) => new (mod, setting, keys);

    /// <summary>Shortcuts whose keys are all mapped (AzuEPI's quick slots, ImpactfulSkills' N) report nothing.</summary>
    [Fact]
    public void Mapped_shortcuts_are_not_reported()
    {
        var shortcuts = new[]
        {
            Shortcut("AzuExtendedPlayerInventory", "Hotkey 1", "Keypad1"),
            Shortcut("ImpactfulSkills", "AOEToggleHotkey", "N"),
            Shortcut("AzuCraftyBoxes", "FillAllModKey", "LeftShift"),
        };

        Assert.Empty(ShortcutAudit.Unmapped(shortcuts, SteamController));
        Assert.Equal("None", ShortcutAudit.Summary(ShortcutAudit.Unmapped(shortcuts, SteamController)));
    }

    /// <summary>Only the unmapped keys of a combo are reported (E of "E + LeftShift", LeftAlt of "Alpha1 + LeftAlt").</summary>
    [Fact]
    public void Combos_report_only_their_unmapped_keys()
    {
        var shortcuts = new[]
        {
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
            Shortcut("AzuExtendedPlayerInventory", "Hotkey 7", "Alpha1", "LeftAlt"),
        };

        Assert.Equal(new[] { "E", "LeftAlt" }, ShortcutAudit.Unmapped(shortcuts, SteamController).Select(key => key.Key));
    }

    /// <summary>A key several mods use is listed once, with each mod once, sorted by key.</summary>
    [Fact]
    public void Summary_lists_each_key_with_the_mods_using_it()
    {
        var shortcuts = new[]
        {
            Shortcut("EpicLoot", "Socket Overlay Shortcut", "E"),
            Shortcut("ConfigurationManager", "Show config manager", "F1"),
            Shortcut("ConfigurationManager", "Reset scale", "F1", "LeftShift"),
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
        };

        Assert.Equal("E (EpicLoot, AzuCraftyBoxes), F1 (ConfigurationManager)",
            ShortcutAudit.Summary(ShortcutAudit.Unmapped(shortcuts, SteamController)));
    }

    /// <summary>The log line names every setting that uses the key, with its whole shortcut.</summary>
    [Fact]
    public void Log_line_names_each_setting_and_its_shortcut()
    {
        var shortcuts = new[]
        {
            Shortcut("EpicLoot", "Socket Overlay Shortcut", "E"),
            Shortcut("AzuCraftyBoxes", "Container Toggle Key", "E", "LeftShift"),
        };

        UnmappedShortcutKey key = Assert.Single(ShortcutAudit.Unmapped(shortcuts, SteamController));
        Assert.Equal(
            "Key E isn't in your mapping; used by EpicLoot > Socket Overlay Shortcut (E), " +
            "AzuCraftyBoxes > Container Toggle Key (E + LeftShift)",
            ShortcutAudit.LogLine(key));
    }

    /// <summary>
    /// The tree has a branch per key and a leaf per setting using it; the keys show only for combos.
    /// </summary>
    [Fact]
    public void Tree_lists_each_key_with_the_settings_using_it()
    {
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
            "├─ Epic Loot > Socket Overlay Shortcut\n" +
            "└─ AzuCraftyBoxes > Container Toggle Key (E + LeftShift)",
            ShortcutAudit.Tree(ShortcutAudit.Unmapped(shortcuts, SteamController)));
    }

    /// <summary>No unmapped keys gives "None", like the summary.</summary>
    [Fact]
    public void Tree_with_no_keys_is_none()
    {
        Assert.Equal("None", ShortcutAudit.Tree(Array.Empty<UnmappedShortcutKey>()));
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
        Assert.Empty(ShortcutAudit.Unmapped(new[] { Shortcut("UnityExplorer", "Some Keybind", key) }, SteamController));
    }

    /// <summary>A key spelled differently by two mods ("e" and "E") is one key.</summary>
    [Fact]
    public void Keys_are_grouped_regardless_of_case()
    {
        var shortcuts = new[]
        {
            Shortcut("EpicLoot", "Socket Overlay Shortcut", "E"),
            Shortcut("BetterArchery", "Bow Draw Cancel Hotkey", "e"),
        };

        Assert.Single(ShortcutAudit.Unmapped(shortcuts, SteamController));
    }
}
