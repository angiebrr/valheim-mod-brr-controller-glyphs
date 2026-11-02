using System.Reflection;

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// Loads Valheim's managed DLLs for inspection only (MetadataLoadContext never runs game code).
/// </summary>
public static class GameAssemblies
{
    private static readonly Lazy<MetadataLoadContext> Context = new(Create);

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

    public static bool Available => ManagedDir != null && File.Exists(Path.Combine(ManagedDir, "assembly_valheim.dll"));

    public static Type Type(string assembly, string typeName)
    {
        Type type = Context.Value.LoadFromAssemblyPath(Path.Combine(ManagedDir, assembly + ".dll")).GetType(typeName);
        Assert.True(type != null, $"{typeName} not found in {assembly}.dll");
        return type;
    }

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
    public GameFactAttribute()
    {
        if (!GameAssemblies.Available)
        {
            Skip = "Valheim install not found (set VALHEIM_INSTALL or create Environment.props)";
        }
    }
}
