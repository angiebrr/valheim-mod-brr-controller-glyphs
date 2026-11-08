// ------------------------------------------------------------------------------------------------
// AzuEpiContractTests.cs: checks the AzuEPI names we copied still exist in AzuEPI's DLL.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// AzuExtendedPlayerInventory binds its quick-slot shortcuts with string literals; if it renames
/// them, quick-slot labels silently stop working. These read the literals from the installed DLL.
/// </summary>
public class AzuEpiContractTests
{
    /// <summary>The plugin ID we look AzuEPI up by is still in its DLL.</summary>
    [AzuEpiFact]
    public void Plugin_guid_matches()
    {
        Assert.Contains(AzuEpiQuickSlots.PluginGuid, ModAssemblies.StringLiterals(ModAssemblies.AzuEpiPath));
    }

    /// <summary>The quick-slot shortcut section name is still in its DLL.</summary>
    [AzuEpiFact]
    public void Quick_slot_section_matches()
    {
        Assert.Contains(AzuEpiQuickSlots.Section, ModAssemblies.StringLiterals(ModAssemblies.AzuEpiPath));
    }

    /// <summary>
    /// The "Hotkey N Display Text" key format is still in its DLL (AzuEPI builds it with
    /// $"Hotkey {i + 1} Display Text"), so the inventory can find the quick-slot cells by name.
    /// </summary>
    [AzuEpiFact]
    public void Quick_slot_display_text_key_matches()
    {
        List<string> literals = ModAssemblies.StringLiterals(ModAssemblies.AzuEpiPath);
        string format = AzuEpiQuickSlots.KeyPrefix + "{0}" + AzuEpiQuickSlots.DisplayTextSuffix;

        Assert.True(literals.Contains(format) || literals.Contains(AzuEpiQuickSlots.DisplayTextSuffix),
            $"Neither \"{format}\" nor \"{AzuEpiQuickSlots.DisplayTextSuffix}\" found in AzuEPI");
    }

    /// <summary>The quick-slot count setting is still in its DLL.</summary>
    [AzuEpiFact]
    public void Quick_slot_count_setting_matches()
    {
        List<string> literals = ModAssemblies.StringLiterals(ModAssemblies.AzuEpiPath);

        Assert.Contains(AzuEpiQuickSlots.CountSection, literals);
        Assert.Contains(AzuEpiQuickSlots.CountKey, literals);
    }

    /// <summary>
    /// The "Hotkey N" key format is still in its DLL. AzuEPI builds it with $"Hotkey {i + 1}", which
    /// compiles to a format string ("Hotkey {0}") or a literal prefix ("Hotkey ").
    /// </summary>
    [AzuEpiFact]
    public void Quick_slot_key_format_matches()
    {
        List<string> literals = ModAssemblies.StringLiterals(ModAssemblies.AzuEpiPath);

        Assert.True(literals.Contains(AzuEpiQuickSlots.KeyPrefix + "{0}") || literals.Contains(AzuEpiQuickSlots.KeyPrefix),
            $"Neither \"{AzuEpiQuickSlots.KeyPrefix}{{0}}\" nor \"{AzuEpiQuickSlots.KeyPrefix}\" found in AzuEPI");
    }
}
