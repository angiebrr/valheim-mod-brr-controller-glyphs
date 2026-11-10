// ------------------------------------------------------------------------------------------------
// ModAssemblies.cs: finds other mods' DLLs in the Gale profile for contract tests, and [AzuEpiFact].
// ------------------------------------------------------------------------------------------------

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Contract.Tests;

/// <summary>
/// Finds other mods' DLLs in the Gale profile's plugins folder and reads them as metadata only.
/// </summary>
public static class ModAssemblies
{
    /// <summary>AzuExtendedPlayerInventory's DLL file name.</summary>
    private const string AzuEpiFileName = "AzuExtendedPlayerInventory.dll";

    /// <summary>
    /// The Gale profile's BepInEx\plugins folder: from the GALE_PROFILE environment variable, else
    /// from Environment.props (baked in at build time); null if neither is set.
    /// </summary>
    public static string PluginsDir
    {
        get
        {
            string profile = Environment.GetEnvironmentVariable("GALE_PROFILE");
            if (string.IsNullOrEmpty(profile))
            {
                profile = typeof(ModAssemblies).Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "GaleProfile")?.Value;
            }

            return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, "BepInEx", "plugins");
        }
    }

    /// <summary>Path of AzuExtendedPlayerInventory's DLL, or null if it isn't installed.</summary>
    public static string AzuEpiPath =>
        PluginsDir != null && Directory.Exists(PluginsDir)
            ? Directory.GetFiles(PluginsDir, AzuEpiFileName, SearchOption.AllDirectories).FirstOrDefault()
            : null;

    /// <summary>
    /// Returns every string literal in an assembly (the #US heap: the strings its code loads).
    /// </summary>
    /// <param name="path">The DLL.</param>
    /// <returns>The literals, in heap order.</returns>
    public static List<string> StringLiterals(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();

        var literals = new List<string>();
        int heapSize = reader.GetHeapSize(HeapIndex.UserString);
        for (UserStringHandle handle = MetadataTokens.UserStringHandle(1);
             !handle.IsNil && MetadataTokens.GetHeapOffset(handle) < heapSize;
             handle = reader.GetNextHandle(handle))
        {
            literals.Add(reader.GetUserString(handle));
        }

        return literals;
    }
}

/// <summary>
/// A fact that is skipped when AzuExtendedPlayerInventory isn't in the Gale profile.
/// </summary>
public sealed class AzuEpiFactAttribute : FactAttribute
{
    /// <summary>
    /// Marks the test skipped when <see cref="ModAssemblies.AzuEpiPath"/> is null.
    /// </summary>
    public AzuEpiFactAttribute()
    {
        if (ModAssemblies.AzuEpiPath == null)
        {
            Skip = "AzuExtendedPlayerInventory not found (set GALE_PROFILE or create Environment.props)";
        }
    }
}
