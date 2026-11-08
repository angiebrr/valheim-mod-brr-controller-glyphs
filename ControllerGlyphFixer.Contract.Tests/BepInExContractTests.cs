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
        using MetadataLoadContext context = Context();
        Type configFile = BepInExType(context, "BepInEx.Configuration.ConfigFile");

        PropertyInfo orphans = configFile.GetProperty("OrphanedEntries",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.True(orphans != null, "ConfigFile.OrphanedEntries not found");
        Assert.Equal("Dictionary`2", orphans.PropertyType.Name);
        Assert.Equal(
            new[] { "BepInEx.Configuration.ConfigDefinition", "System.String" },
            orphans.PropertyType.GetGenericArguments().Select(type => type.FullName));
    }

    /// <summary>
    /// ModShortcuts reads other plugins' shortcut settings: the loaded plugins, each one's config
    /// entries, and the keys of a KeyboardShortcut.
    /// </summary>
    [BepInExFact]
    public void Members_read_by_the_shortcut_audit_exist()
    {
        using MetadataLoadContext context = Context();
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

        PropertyInfo pluginInfos = BepInExType(context, "BepInEx.Bootstrap.Chainloader")
            .GetProperty("PluginInfos", BindingFlags.Public | BindingFlags.Static);
        Assert.True(pluginInfos != null, "Chainloader.PluginInfos not found");

        Type pluginInfo = BepInExType(context, "BepInEx.PluginInfo");
        Assert.NotNull(pluginInfo.GetProperty("Instance", Public));
        Assert.NotNull(pluginInfo.GetProperty("Metadata", Public));
        Assert.NotNull(BepInExType(context, "BepInEx.BaseUnityPlugin").GetProperty("Config", Public));

        Type shortcut = BepInExType(context, "BepInEx.Configuration.KeyboardShortcut");
        Assert.Equal("KeyCode", shortcut.GetProperty("MainKey", Public)?.PropertyType.Name);
        Assert.Equal("IEnumerable`1", shortcut.GetProperty("Modifiers", Public)?.PropertyType.Name);
    }

    /// <summary>
    /// Creates a metadata-only context over BepInEx's core and the game's DLLs (for UnityEngine).
    /// </summary>
    /// <returns>The context; dispose it after the test.</returns>
    private static MetadataLoadContext Context()
    {
        var resolver = new PathAssemblyResolver(
            Directory.GetFiles(CoreDir, "*.dll").Concat(Directory.GetFiles(GameAssemblies.ManagedDir, "*.dll")));
        return new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");
    }

    /// <summary>
    /// Returns a type from BepInEx.dll; fails the test if it's missing.
    /// </summary>
    /// <param name="context">From <see cref="Context"/>.</param>
    /// <param name="typeName">Full type name.</param>
    /// <returns>The type.</returns>
    private static Type BepInExType(MetadataLoadContext context, string typeName)
    {
        Type type = context.LoadFromAssemblyPath(Path.Combine(CoreDir, "BepInEx.dll")).GetType(typeName);
        Assert.True(type != null, $"{typeName} not found in BepInEx.dll");
        return type;
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
