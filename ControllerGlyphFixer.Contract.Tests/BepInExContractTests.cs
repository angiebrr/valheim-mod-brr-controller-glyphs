// ------------------------------------------------------------------------------------------------
// BepInExContractTests.cs: contract tests for the BepInEx members the plugin reads by reflection.
// ------------------------------------------------------------------------------------------------

using System.Reflection;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// BepInEx internals the plugin relies on, read from the Gale profile's BepInEx\core (skipped
/// when the profile isn't found). Run after a BepInEx update.
/// </summary>
public class BepInExContractTests
{
    /// <summary>The Gale profile's BepInEx\core folder, or null if it isn't found.</summary>
    private static string CoreDir =>
        ModAssemblies.PluginsDir is { } plugins && Directory.Exists(Path.Combine(plugins, "..", "core"))
            ? Path.GetFullPath(Path.Combine(plugins, "..", "core"))
            : null;

    /// <summary>
    /// ConfigFile.OrphanedEntries holds .cfg entries no setting claims; CarryOverMovedSettings
    /// reads the old values of moved settings from it and removes them.
    /// </summary>
    [BepInExFact]
    public void ConfigFile_keeps_unclaimed_entries_as_orphans()
    {
        var resolver = new PathAssemblyResolver(
            Directory.GetFiles(CoreDir, "*.dll").Concat(Directory.GetFiles(GameAssemblies.ManagedDir, "*.dll")));
        using var context = new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");
        Type configFile = context.LoadFromAssemblyPath(Path.Combine(CoreDir, "BepInEx.dll"))
            .GetType("BepInEx.Configuration.ConfigFile");

        PropertyInfo orphans = configFile.GetProperty("OrphanedEntries",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.True(orphans != null, "ConfigFile.OrphanedEntries not found");
        Assert.Equal("Dictionary`2", orphans.PropertyType.Name);
        Assert.Equal(
            new[] { "BepInEx.Configuration.ConfigDefinition", "System.String" },
            orphans.PropertyType.GetGenericArguments().Select(type => type.FullName));
    }

    /// <summary>
    /// A fact that is skipped when the Gale profile's BepInEx core or the game isn't found.
    /// </summary>
    private sealed class BepInExFactAttribute : FactAttribute
    {
        /// <summary>
        /// Marks the test skipped when BepInEx's core folder or the game's DLLs are missing.
        /// </summary>
        public BepInExFactAttribute()
        {
            if (CoreDir == null || !GameAssemblies.Available)
            {
                Skip = "BepInEx core or Valheim not found (set GALE_PROFILE and VALHEIM_INSTALL or create Environment.props)";
            }
        }
    }
}
