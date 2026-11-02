using System.Reflection;

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// Everything the plugin patches or calls in Valheim must still exist with the expected shape.
/// Run after every Valheim update, before launching the game.
/// </summary>
public class PatchTargetTests
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Type ZInput => GameAssemblies.Type("assembly_utils", "ZInput");

    [GameFact]
    public void ZInput_GetBoundKeyString_is_the_F1_F2_patch_target()
    {
        MethodInfo method = ZInput.GetMethod("GetBoundKeyString", Instance, null,
            new[] { typeof(string).InGame(), typeof(bool).InGame() }, null);

        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal("System.String", method.ReturnType.FullName);
    }

    [GameFact]
    public void ZInput_members_used_for_glyph_mode_exist()
    {
        AssertStaticProperty(ZInput, "instance", "ZInput");
        AssertStaticMethod(ZInput, "IsGamepadEnabled", "System.Boolean");
        AssertStaticMethod(ZInput, "IsGamepadActive", "System.Boolean");
    }

    [GameFact]
    public void ZInput_members_used_for_sprite_glyphs_exist()
    {
        FieldInfo currentGlyph = ZInput.GetField("CurrentGlyph", Static);
        Assert.NotNull(currentGlyph);
        Assert.Equal("GamepadGlyphs", currentGlyph.FieldType.Name);

        AssertStaticProperty(ZInput, "ConnectedGamepadType", "GamepadType");

        FieldInfo spriteMap = ZInput.GetField("s_gamepadSpriteMap", Static);
        Assert.NotNull(spriteMap);
        Assert.StartsWith("System.Collections.Generic.Dictionary`2[[System.String", spriteMap.FieldType.FullName);
    }

    [GameFact]
    public void Glyph_enum_names_match_SpriteStyle()
    {
        // SpriteStyle.For switches on these names; a rename would silently fall back to xbox
        Assert.Equal(
            new[] { "Auto", "Xbox", "Playstation", "Switch" },
            EnumNames(GameAssemblies.Type("assembly_utils", "Valheim.SettingsGui.GamepadGlyphs")));

        string[] gamepadTypes = EnumNames(GameAssemblies.Type("assembly_utils", "GamepadType"));
        foreach (string name in new[] { "XInput", "DualSense", "DualShock", "SwitchPro", "JoyCon", "JoyCon2Mouse" })
        {
            Assert.Contains(name, gamepadTypes);
        }
    }

    [GameFact]
    public void Settings_instance_exists_for_the_rebinding_screen_check()
    {
        AssertStaticProperty(GameAssemblies.Type("assembly_valheim", "Settings"), "instance", "Settings");
    }

    [GameFact]
    public void Localization_members_used_by_the_self_test_exist()
    {
        Type localization = GameAssemblies.Type("assembly_guiutils", "Localization");

        AssertStaticProperty(localization, "instance", "Localization");
        MethodInfo localize = localization.GetMethod("Localize", Instance, null, new[] { typeof(string).InGame() }, null);
        Assert.NotNull(localize);
        Assert.Equal("System.String", localize.ReturnType.FullName);
    }

    private static void AssertStaticProperty(Type type, string name, string typeName)
    {
        PropertyInfo property = type.GetProperty(name, Static);
        Assert.True(property != null, $"{type.Name}.{name} not found");
        Assert.Equal(typeName, property.PropertyType.Name);
    }

    private static void AssertStaticMethod(Type type, string name, string returnType)
    {
        MethodInfo method = type.GetMethod(name, Static, null, Type.EmptyTypes, null);
        Assert.True(method != null, $"{type.Name}.{name}() not found");
        Assert.Equal(returnType, method.ReturnType.FullName);
    }

    private static string[] EnumNames(Type enumType)
    {
        return enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.Name).ToArray();
    }
}

internal static class GameTypeExtensions
{
    /// <summary>
    /// Runtime types can't be used to look up members in a MetadataLoadContext; translate to
    /// the context's own copy of the type.
    /// </summary>
    public static Type InGame(this Type runtimeType)
    {
        return GameAssemblies.Type("mscorlib", runtimeType.FullName);
    }
}
