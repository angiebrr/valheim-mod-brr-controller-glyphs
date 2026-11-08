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

    /// <summary>
    /// What ModSpriteAsset reads to find Valheim's xbox sprites and copy their size, and what the
    /// self-test uses to resolve our tag the way TMP does.
    /// </summary>
    [GameFact]
    public void Sprite_lookup_and_sizing_members_exist()
    {
        Type spriteAsset = Tmp("TMP_SpriteAsset");
        Type spriteAssetByRef = spriteAsset.MakeByRefType();

        Assert.NotNull(Tmp("MaterialReferenceManager").GetMethod("TryGetSpriteAsset", Static, null,
            new[] { typeof(int).InGame(), spriteAssetByRef }, null));
        Assert.Equal("System.Int32", Tmp("TMP_TextUtilities").GetMethod("GetHashCode", Static, null,
            new[] { typeof(string).InGame() }, null)?.ReturnType.FullName);
        Assert.Equal("FaceInfo", Tmp("TMP_Asset").GetProperty("faceInfo", Instance)?.PropertyType.Name);
        Assert.Equal("System.Int32", Tmp("TMP_Asset").GetProperty("hashCode", Instance)?.PropertyType.FullName);
        Assert.Equal("TMP_Asset", Tmp("TMP_TextElement").GetProperty("textAsset", Instance)?.PropertyType.Name);
        Assert.Equal("System.Single", TextCore("Glyph").GetProperty("scale", Instance)?.PropertyType.FullName);
    }

    /// <summary>The character info PrefixLabels measures prefixes with (mesh corners, not the line box).</summary>
    [GameFact]
    public void Character_info_fields_used_for_prefix_bounds_exist()
    {
        Type info = Tmp("TMP_CharacterInfo");

        Assert.Equal("TMP_Vertex", info.GetField("vertex_BL", Instance)?.FieldType.Name);
        Assert.Equal("TMP_Vertex", info.GetField("vertex_TR", Instance)?.FieldType.Name);
        Assert.Equal("TMP_TextElement", info.GetField("textElement", Instance)?.FieldType.Name);
        Assert.Equal("TMP_TextElementType", info.GetField("elementType", Instance)?.FieldType.Name);
        Assert.Equal("System.Boolean", info.GetField("isVisible", Instance)?.FieldType.FullName);
        Assert.Equal("Vector3", Tmp("TMP_Vertex").GetField("position", Instance)?.FieldType.Name);
    }

    /// <summary>
    /// The PNG decoder, called through reflection: its module is built against netstandard 2.1,
    /// which the net48 plugin can't reference at compile time.
    /// </summary>
    [GameFact]
    public void ImageConversion_LoadImage_decodes_pngs()
    {
        Type imageConversion = GameAssemblies.Type("UnityEngine.ImageConversionModule", "UnityEngine.ImageConversion");
        Type texture = GameAssemblies.Type("UnityEngine.CoreModule", "UnityEngine.Texture2D");

        MethodInfo loadImage = imageConversion.GetMethod("LoadImage", Static, null, new[] { texture, typeof(byte[]).InGame() }, null);

        Assert.Equal("System.Boolean", loadImage?.ReturnType.FullName);
    }

    /// <summary>The PNG encoder the self-test saves sprite sheets with, also called through reflection.</summary>
    [GameFact]
    public void ImageConversion_EncodeToPNG_saves_textures()
    {
        Type imageConversion = GameAssemblies.Type("UnityEngine.ImageConversionModule", "UnityEngine.ImageConversion");
        Type texture = GameAssemblies.Type("UnityEngine.CoreModule", "UnityEngine.Texture2D");

        MethodInfo encode = imageConversion.GetMethod("EncodeToPNG", Static, null, new[] { texture }, null);

        Assert.Equal("System.Byte[]", encode?.ReturnType.FullName);
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
