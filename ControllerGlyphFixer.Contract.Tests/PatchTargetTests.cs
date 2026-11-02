// ------------------------------------------------------------------------------------------------
// PatchTargetTests.cs: contract tests that every Valheim member the plugin uses still exists.
// ------------------------------------------------------------------------------------------------

using System.Reflection;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// Everything the plugin patches or calls in Valheim must still exist with the expected shape.
/// Run after every Valheim update, before launching the game.
/// </summary>
// TODO(phase 2): add HotkeyBar.UpdateIcons(Player), Hud.UpdateCrosshair(Player, float) and
// Hud.m_hoverName when the F3/F4 patches are written.
public class PatchTargetTests
{
    /// <summary>Static members, public or not.</summary>
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Instance members, public or not.</summary>
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Valheim's ZInput type.</summary>
    private static Type ZInput => GameAssemblies.Type("assembly_utils", "ZInput");

    /// <summary>The F1/F2 patch target has the signature the postfix expects.</summary>
    [GameFact]
    public void ZInput_GetBoundKeyString_is_the_F1_F2_patch_target()
    {
        MethodInfo method = ZInput.GetMethod("GetBoundKeyString", Instance, null,
            new[] { typeof(string).InGame(), typeof(bool).InGame() }, null);

        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal("System.String", method.ReturnType.FullName);
    }

    /// <summary>Members read by GlyphService, the patches and the self-test exist.</summary>
    [GameFact]
    public void ZInput_members_used_for_glyph_mode_exist()
    {
        AssertStaticProperty(ZInput, "instance", "ZInput");
        AssertStaticMethod(ZInput, "IsGamepadEnabled", "System.Boolean");
        AssertStaticMethod(ZInput, "IsGamepadActive", "System.Boolean");
        AssertStaticMethod(ZInput, "IsMouseActive", "System.Boolean");
    }

    /// <summary>Members read by Glyphs.ValheimGlyph exist with the expected types.</summary>
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

    /// <summary>The enum names SpriteStyle.For switches on still exist.</summary>
    [GameFact]
    public void Glyph_enum_names_match_SpriteStyle()
    {
        // A rename would make SpriteStyle silently fall back to xbox
        Assert.Equal(
            new[] { "Auto", "Xbox", "Playstation", "Switch" },
            EnumNames(GameAssemblies.Type("assembly_utils", "Valheim.SettingsGui.GamepadGlyphs")));

        string[] gamepadTypes = EnumNames(GameAssemblies.Type("assembly_utils", "GamepadType"));
        foreach (string name in new[] { "XInput", "DualSense", "DualShock", "SwitchPro", "JoyCon", "JoyCon2Mouse" })
        {
            Assert.Contains(name, gamepadTypes);
        }
    }

    /// <summary>The F6 patch target and the members its postfix reads and calls exist.</summary>
    [GameFact]
    public void UIInputHint_members_used_by_the_F6_patch_exist()
    {
        Type hint = GameAssemblies.Type("assembly_guiutils", "UIInputHint");

        Assert.NotNull(hint.GetMethod("UpdateInputHints", Instance, null, Type.EmptyTypes, null));
        Assert.NotNull(hint.GetMethod("UpdateVisiblityAndLayout", Instance, null,
            new[] { GameAssemblies.Type("UnityEngine.CoreModule", "UnityEngine.GameObject"), typeof(bool).InGame() }, null));

        AssertField(hint, "m_gamepadHint", "GameObject");
        AssertField(hint, "m_mouseKeyboardHint", "GameObject");
        AssertField(hint, "m_gamepadMouseHint", "GameObject");
        AssertField(hint, "m_button", "Button");
        AssertField(hint, "m_group", "UIGroupHandler");
        AssertField(hint, "alternativeGroupHandler", "UIGroupHandler");

        PropertyInfo isActive = GameAssemblies.Type("assembly_guiutils", "UIGroupHandler").GetProperty("IsActive", Instance);
        Assert.NotNull(isActive);
        Assert.Equal("System.Boolean", isActive.PropertyType.FullName);
    }

    /// <summary>The F3 patch targets on HotkeyBar and the slot fields the postfix reads exist.</summary>
    [GameFact]
    public void HotkeyBar_members_used_by_the_F3_patch_exist()
    {
        Type bar = GameAssemblies.Type("assembly_valheim", "HotkeyBar");

        Assert.NotNull(bar.GetMethod("UpdateIcons", Instance, null,
            new[] { GameAssemblies.Type("assembly_valheim", "Player") }, null));
        Assert.NotNull(bar.GetMethod("ToggleBindingHint", Instance, null, new[] { typeof(bool).InGame() }, null));
        Assert.NotNull(bar.GetMethod("OnEnable", Instance, null, Type.EmptyTypes, null));

        FieldInfo elements = bar.GetField("m_elements", Instance);
        Assert.NotNull(elements);
        Assert.StartsWith("System.Collections.Generic.List`1[[HotkeyBar+ElementData", elements.FieldType.FullName);

        AssertField(GameAssemblies.Type("assembly_valheim", "HotkeyBar+ElementData"), "m_go", "GameObject");
    }

    /// <summary>Settings.instance exists for the "Settings screen open" check.</summary>
    [GameFact]
    public void Settings_instance_exists_for_the_rebinding_screen_check()
    {
        AssertStaticProperty(GameAssemblies.Type("assembly_valheim", "Settings"), "instance", "Settings");
    }

    /// <summary>Localization members the self-test calls exist.</summary>
    [GameFact]
    public void Localization_members_used_by_the_self_test_exist()
    {
        Type localization = GameAssemblies.Type("assembly_guiutils", "Localization");

        AssertStaticProperty(localization, "instance", "Localization");
        MethodInfo localize = localization.GetMethod("Localize", Instance, null, new[] { typeof(string).InGame() }, null);
        Assert.NotNull(localize);
        Assert.Equal("System.String", localize.ReturnType.FullName);
    }

    /// <summary>
    /// Asserts a static property exists with the given type name.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <param name="name">Property name.</param>
    /// <param name="typeName">Expected property type name (short name).</param>
    private static void AssertStaticProperty(Type type, string name, string typeName)
    {
        PropertyInfo property = type.GetProperty(name, Static);
        Assert.True(property != null, $"{type.Name}.{name} not found");
        Assert.Equal(typeName, property.PropertyType.Name);
    }

    /// <summary>
    /// Asserts an instance field exists with the given type name.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <param name="name">Field name.</param>
    /// <param name="typeName">Expected field type name (short name).</param>
    private static void AssertField(Type type, string name, string typeName)
    {
        FieldInfo field = type.GetField(name, Instance);
        Assert.True(field != null, $"{type.Name}.{name} not found");
        Assert.Equal(typeName, field.FieldType.Name);
    }

    /// <summary>
    /// Asserts a parameterless static method exists with the given return type.
    /// </summary>
    /// <param name="type">Type to inspect.</param>
    /// <param name="name">Method name.</param>
    /// <param name="returnType">Expected return type full name.</param>
    private static void AssertStaticMethod(Type type, string name, string returnType)
    {
        MethodInfo method = type.GetMethod(name, Static, null, Type.EmptyTypes, null);
        Assert.True(method != null, $"{type.Name}.{name}() not found");
        Assert.Equal(returnType, method.ReturnType.FullName);
    }

    /// <summary>
    /// Returns an enum's member names in declaration order.
    /// </summary>
    /// <param name="enumType">The enum type.</param>
    /// <returns>Member names.</returns>
    private static string[] EnumNames(Type enumType)
    {
        return enumType.GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => f.Name).ToArray();
    }
}

/// <summary>
/// Helpers for using runtime types with the metadata-only context.
/// </summary>
internal static class GameTypeExtensions
{
    /// <summary>
    /// Runtime types can't be used to look up members in a MetadataLoadContext; translate to
    /// the context's own copy of the type.
    /// </summary>
    /// <param name="runtimeType">A type from the test runtime, e.g. typeof(string).</param>
    /// <returns>The same type from the game's mscorlib.</returns>
    public static Type InGame(this Type runtimeType)
    {
        return GameAssemblies.Type("mscorlib", runtimeType.FullName);
    }
}
