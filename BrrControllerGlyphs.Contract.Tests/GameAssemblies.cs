// ------------------------------------------------------------------------------------------------
// GameAssemblies.cs: loads Valheim's DLLs read-only for contract tests, and the [GameFact] attribute.
// ------------------------------------------------------------------------------------------------

using System.Reflection;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Contract.Tests;

/// <summary>
/// Loads Valheim's managed DLLs for inspection only (MetadataLoadContext never runs game code).
/// </summary>
public static class GameAssemblies
{
    /// <summary>One shared context for all tests, created on first use.</summary>
    private static readonly Lazy<MetadataLoadContext> Context = new(Create);

    /// <summary>
    /// Valheim's valheim_Data/Managed folder: from the VALHEIM_INSTALL environment variable, else
    /// from Environment.props (baked in at build time); null if neither is set.
    /// </summary>
    public static string ManagedDir
    {
        get
        {
            string install = Environment.GetEnvironmentVariable("VALHEIM_INSTALL");
            if (string.IsNullOrEmpty(install))
            {
                install = typeof(GameAssemblies).Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "ValheimInstall")?.Value;
            }

            return string.IsNullOrEmpty(install) ? null : Path.Combine(install, "valheim_Data", "Managed");
        }
    }

    /// <summary>Whether the game's DLLs were found.</summary>
    public static bool Available => ManagedDir != null && File.Exists(Path.Combine(ManagedDir, "assembly_valheim.dll"));

    /// <summary>
    /// Returns a type from one of the game's assemblies; fails the test if it's missing.
    /// </summary>
    /// <param name="assembly">Assembly file name without ".dll" (e.g. "assembly_utils").</param>
    /// <param name="typeName">Full type name (e.g. "ZInput").</param>
    /// <returns>The type, from the metadata-only context.</returns>
    public static Type Type(string assembly, string typeName)
    {
        Type type = Context.Value.LoadFromAssemblyPath(Path.Combine(ManagedDir, assembly + ".dll")).GetType(typeName);
        Assert.True(type != null, $"{typeName} not found in {assembly}.dll");
        return type;
    }

    /// <summary>
    /// Creates the context, resolving references from the game's Managed folder (mscorlib included).
    /// </summary>
    /// <returns>The metadata-only load context.</returns>
    private static MetadataLoadContext Create()
    {
        var resolver = new PathAssemblyResolver(Directory.GetFiles(ManagedDir, "*.dll"));
        return new MetadataLoadContext(resolver, coreAssemblyName: "mscorlib");
    }
}

/// <summary>
/// A fact that is skipped when the Valheim install can't be found (e.g. on another machine).
/// </summary>
public sealed class GameFactAttribute : FactAttribute
{
    /// <summary>
    /// Marks the test skipped when <see cref="GameAssemblies.Available"/> is false.
    /// </summary>
    public GameFactAttribute()
    {
        if (!GameAssemblies.Available)
        {
            Skip = "Valheim install not found (set VALHEIM_INSTALL or create Environment.props)";
        }
    }
}
