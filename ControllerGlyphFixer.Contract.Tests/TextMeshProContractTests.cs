// ------------------------------------------------------------------------------------------------
// TextMeshProContractTests.cs: contract tests for the TextMeshPro members the F5 sprite asset uses.
// ------------------------------------------------------------------------------------------------

using System.Reflection;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Contract.Tests;

/// <summary>
/// The plugin builds a TextMeshPro sprite asset at runtime (ModSpriteAsset, F5); every member it
/// sets or calls in Valheim's Unity.TextMeshPro must still exist with the expected shape.
/// </summary>
public class TextMeshProContractTests
{
    /// <summary>Static members, public or not.</summary>
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>Instance members, public or not.</summary>
    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Returns a TextMeshPro type.
    /// </summary>
    /// <param name="name">Type name in the TMPro namespace.</param>
    /// <returns>The type, from the metadata-only context.</returns>
    private static Type Tmp(string name) => GameAssemblies.Type("Unity.TextMeshPro", "TMPro." + name);

    /// <summary>
    /// Returns a TextCore type (glyph metrics and rects).
    /// </summary>
    /// <param name="name">Type name in the UnityEngine.TextCore namespace.</param>
    /// <returns>The type, from the metadata-only context.</returns>
    private static Type TextCore(string name) =>
        GameAssemblies.Type("UnityEngine.TextCoreFontEngineModule", "UnityEngine.TextCore." + name);

    /// <summary>Our asset is registered by name, so &lt;sprite="cgf_deck"&gt; finds it.</summary>
    [GameFact]
    public void MaterialReferenceManager_registers_sprite_assets()
    {
        MethodInfo add = Tmp("MaterialReferenceManager").GetMethod("AddSpriteAsset", Static, null,
            new[] { Tmp("TMP_SpriteAsset") }, null);

        Assert.NotNull(add);
    }

    /// <summary>
    /// The version field we set so UpdateLookupTables doesn't run UpgradeSpriteAsset, which would
    /// rebuild the tables from the (empty) legacy sprite list.
    /// </summary>
    [GameFact]
    public void TMP_Asset_version_field_exists()
    {
        FieldInfo version = Tmp("TMP_Asset").GetField("m_Version", Instance);

        Assert.True(version != null, "TMP_Asset.m_Version not found");
        Assert.Equal("System.String", version.FieldType.FullName);
    }

    /// <summary>The sprite-asset members the plugin fills in and calls.</summary>
    [GameFact]
    public void TMP_SpriteAsset_members_exist()
    {
        Type asset = Tmp("TMP_SpriteAsset");

        Assert.Equal("Texture", asset.GetField("spriteSheet", Instance)?.FieldType.Name);
        Assert.Equal("List`1", asset.GetProperty("spriteGlyphTable", Instance)?.PropertyType.Name);
        Assert.Equal("List`1", asset.GetProperty("spriteCharacterTable", Instance)?.PropertyType.Name);
        Assert.NotNull(asset.GetMethod("UpdateLookupTables", Instance, null, Type.EmptyTypes, null));
        MethodInfo byName = asset.GetMethod("GetSpriteIndexFromName", Instance, null, new[] { typeof(string).InGame() }, null);
        Assert.Equal("System.Int32", byName?.ReturnType.FullName);
    }

    /// <summary>The constructors and properties used to add each glyph, and the default asset path.</summary>
    [GameFact]
    public void Sprite_glyph_and_character_constructors_exist()
    {
        Type glyph = Tmp("TMP_SpriteGlyph");
        Type character = Tmp("TMP_SpriteCharacter");

        Assert.NotNull(glyph.GetConstructor(new[]
        {
            typeof(uint).InGame(), TextCore("GlyphMetrics"), TextCore("GlyphRect"), typeof(float).InGame(), typeof(int).InGame(),
        }));
        Assert.NotNull(character.GetConstructor(new[] { typeof(uint).InGame(), Tmp("TMP_SpriteAsset"), glyph }));
        Assert.NotNull(character.GetProperty("name", Instance)?.SetMethod);
        Assert.Equal("System.String", Tmp("TMP_Settings").GetProperty("defaultSpriteAssetPath", Static)?.PropertyType.FullName);
    }
}
