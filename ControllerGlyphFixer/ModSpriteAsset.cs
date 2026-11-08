// ------------------------------------------------------------------------------------------------
// ModSpriteAsset.cs: builds the TextMeshPro sprite asset for the mod's Steam Deck glyphs (F5).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using ControllerGlyphFixer.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// The <see cref="ModGlyphs.AssetName"/> sprite asset: the embedded glyph PNGs packed into one
    /// texture and registered with TextMeshPro, so &lt;sprite="cgf_deck" name="r4"&gt; renders in any
    /// text. Sized from Valheim's own xbox sprites so both line up in one label.
    /// </summary>
    internal sealed class ModSpriteAsset
    {
        /// <summary>Size of each embedded image.</summary>
        private const int ImageSize = ModGlyphs.ImageSize;

        /// <summary>Size of the glyph itself inside each image.</summary>
        private const int GlyphSize = ModGlyphs.GlyphSize;

        /// <summary>Padding around the glyph for its shadow.</summary>
        private const int ShadowPad = (ImageSize - GlyphSize) / 2;

        /// <summary>
        /// Our glyph's height relative to Valheim's whole reference sprite (which has transparent
        /// padding of its own). At 1 our square glyphs looked about 1.4 times Valheim's round ones in
        /// the hotbar and hover hints, and at 0.8 still a little big in hover hints (in game 2026-11-08).
        /// </summary>
        private const float SizeFactor = 0.7f;

        /// <summary>Valheim sprite whose size and baseline ours copy.</summary>
        private const string ReferenceSprite = "button_a";

        /// <summary>TMP's "no code point" value: sprites are found by name, not by character.</summary>
        private const uint NoUnicode = 0xFFFE;

        /// <summary>Version TMP expects; without it, UpdateLookupTables "upgrades" and wipes our tables.</summary>
        private const string TmpAssetVersion = "1.1.0";

        /// <summary>Prefix of the glyph PNGs' manifest resource names.</summary>
        private const string ResourcePrefix = "ControllerGlyphFixer.Glyphs.";

        /// <summary>
        /// Creates the holder of a built asset.
        /// </summary>
        /// <param name="atlas">The packed glyph texture.</param>
        /// <param name="material">TMP sprite material drawing the atlas.</param>
        /// <param name="asset">The registered sprite asset.</param>
        /// <param name="metricsSource">Where the glyph size came from.</param>
        private ModSpriteAsset(Texture2D atlas, Material material, TMP_SpriteAsset asset, string metricsSource)
        {
            Atlas = atlas;
            Material = material;
            Asset = asset;
            MetricsSource = metricsSource;
        }

        /// <summary>The packed glyph texture (the self-test saves it as a PNG).</summary>
        internal Texture2D Atlas { get; }

        /// <summary>The sprite asset registered as <see cref="ModGlyphs.AssetName"/>.</summary>
        internal TMP_SpriteAsset Asset { get; }

        /// <summary>Where glyph size and baseline came from, for the self-test.</summary>
        internal string MetricsSource { get; }

        /// <summary>TMP sprite material drawing <see cref="Atlas"/>.</summary>
        private Material Material { get; }

        /// <summary>Whether the texture, material and asset are all still alive (Valheim's asset sweep).</summary>
        internal bool IsLoaded => Atlas && Material && Asset;

        /// <summary>
        /// Builds and registers the sprite asset. Logs and cleans up after itself on failure.
        /// </summary>
        /// <returns>The built asset, or null if it couldn't be built (labels then show [badges]).</returns>
        internal static ModSpriteAsset TryCreate()
        {
            var textures = new List<Texture2D>();
            Texture2D atlas = null;
            Material material = null;
            TMP_SpriteAsset asset = null;
            try
            {
                foreach (string sprite in ModGlyphs.SpriteNames)
                {
                    textures.Add(LoadImage(sprite));
                }

                atlas = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { name = ModGlyphs.AssetName };
                Rect[] uvs = atlas.PackTextures(textures.ToArray(), 2, 2048, makeNoLongerReadable: true);
                atlas.filterMode = FilterMode.Bilinear;

                material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = ModGlyphs.AssetName };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlas);

                asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                asset.name = ModGlyphs.AssetName;
                asset.spriteSheet = atlas;
                // Internal setter; set before the material so UpdateLookupTables doesn't upgrade the asset
                AccessTools.Field(typeof(TMP_Asset), "m_Version").SetValue(asset, TmpAssetVersion);
                asset.material = material;

                string metricsSource = FillTables(asset, uvs, atlas.width, atlas.height);

                atlas.hideFlags = HideFlags.DontUnloadUnusedAsset;
                material.hideFlags = HideFlags.DontUnloadUnusedAsset;
                asset.hideFlags = HideFlags.DontUnloadUnusedAsset;
                MaterialReferenceManager.AddSpriteAsset(asset);
                return new ModSpriteAsset(atlas, material, asset, metricsSource);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Couldn't build the controller glyph sprites; buttons show as [text]: {ex}");
                DestroyAll(asset, material, atlas);
                return null;
            }
            finally
            {
                foreach (Texture2D texture in textures)
                {
                    Object.Destroy(texture);
                }
            }
        }

        /// <summary>
        /// Destroys the asset, material and atlas. TMP can't unregister the asset, so its lookup keeps
        /// a dead entry; plugins are destroyed only when the game quits (spec F5).
        /// </summary>
        internal void Destroy()
        {
            DestroyAll(Asset, Material, Atlas);
        }

        /// <summary>
        /// Adds a glyph and a named character per sprite, sized like Valheim's xbox glyphs.
        /// </summary>
        /// <param name="asset">The new sprite asset.</param>
        /// <param name="uvs">Each sprite's rect in the atlas, in <see cref="ModGlyphs.SpriteNames"/> order (0–1).</param>
        /// <param name="atlasWidth">Atlas width in pixels.</param>
        /// <param name="atlasHeight">Atlas height in pixels.</param>
        /// <returns>Where the size came from ("Valheim xbox button_a" or "fallback").</returns>
        private static string FillTables(TMP_SpriteAsset asset, Rect[] uvs, int atlasWidth, int atlasHeight)
        {
            // Our 128 px glyph matches Valheim's in size and top edge; the shadow padding overhangs
            TMP_SpriteCharacter reference = FindReference();
            SpriteFit fit = SpriteFit.Fallback(ImageSize, GlyphSize);
            string source = "fallback";
            if (reference != null)
            {
                GlyphMetrics metrics = reference.glyph.metrics;
                FaceInfo face = ((TMP_SpriteAsset)reference.textAsset).faceInfo;
                float referenceScale = reference.scale * reference.glyph.scale;
                asset.faceInfo = face;
                fit = SpriteFit.Match(metrics.height, metrics.horizontalBearingY, referenceScale, face.pointSize,
                    ImageSize, GlyphSize, SizeFactor);
                source = $"Valheim {SpriteStyle.Xbox} {ReferenceSprite} (height {metrics.height}, bearing {metrics.horizontalBearingY}, " +
                    $"scale {referenceScale}, point size {face.pointSize}) -> scale {fit.Scale}, bearing {fit.BearingY}";
            }

            var glyphMetrics = new GlyphMetrics(ImageSize, ImageSize, -ShadowPad, fit.BearingY, GlyphSize);
            for (int i = 0; i < uvs.Length; i++)
            {
                var rect = new GlyphRect(
                    Mathf.RoundToInt(uvs[i].x * atlasWidth), Mathf.RoundToInt(uvs[i].y * atlasHeight),
                    Mathf.RoundToInt(uvs[i].width * atlasWidth), Mathf.RoundToInt(uvs[i].height * atlasHeight));
                var glyph = new TMP_SpriteGlyph((uint)i, glyphMetrics, rect, fit.Scale, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(NoUnicode, asset, glyph) { name = ModGlyphs.SpriteNames[i] });
            }

            asset.UpdateLookupTables();
            return source;
        }

        /// <summary>
        /// Finds Valheim's xbox sprite asset: registered once any text used it, else in Resources.
        /// </summary>
        /// <returns>The asset, or null if it isn't available yet.</returns>
        internal static TMP_SpriteAsset FindValheimXbox()
        {
            if (MaterialReferenceManager.TryGetSpriteAsset(TMP_TextUtilities.GetHashCode(SpriteStyle.Xbox), out TMP_SpriteAsset xbox) && xbox)
            {
                return xbox;
            }

            xbox = Resources.Load<TMP_SpriteAsset>(TMP_Settings.defaultSpriteAssetPath + SpriteStyle.Xbox);
            return xbox ? xbox : null;
        }

        /// <summary>
        /// Finds Valheim's xbox "A" sprite, whose size and baseline ours copy.
        /// </summary>
        /// <returns>The sprite character, or null if Valheim's asset isn't available yet.</returns>
        private static TMP_SpriteCharacter FindReference()
        {
            TMP_SpriteAsset xbox = FindValheimXbox();
            if (!xbox)
            {
                return null;
            }

            int index = xbox.GetSpriteIndexFromName(ReferenceSprite);
            TMP_SpriteCharacter character = index >= 0 ? xbox.spriteCharacterTable[index] : null;
            return character != null && character.glyph != null && character.glyph.metrics.height > 0 ? character : null;
        }

        /// <summary>
        /// Reads one embedded glyph PNG into a readable texture (PackTextures needs readable inputs).
        /// </summary>
        /// <param name="sprite">Sprite name (the PNG's file name).</param>
        /// <returns>The texture; the caller destroys it.</returns>
        /// <exception cref="InvalidOperationException">The resource is missing or isn't a PNG.</exception>
        private static Texture2D LoadImage(string sprite)
        {
            using Stream stream = typeof(ModSpriteAsset).Assembly.GetManifestResourceStream(ResourcePrefix + sprite + ".png");
            if (stream == null)
            {
                throw new InvalidOperationException($"Glyph image {sprite}.png isn't embedded");
            }

            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) { name = "cgf_" + sprite };
            if (!PngFiles.Load(texture, bytes.ToArray()) || texture.width != ImageSize || texture.height != ImageSize)
            {
                Object.Destroy(texture);
                throw new InvalidOperationException($"Glyph image {sprite}.png isn't a {ImageSize} x {ImageSize} PNG");
            }

            return texture;
        }

        /// <summary>
        /// Destroys whichever of the created objects exist.
        /// </summary>
        /// <param name="asset">The sprite asset, or null.</param>
        /// <param name="material">The material, or null.</param>
        /// <param name="atlas">The atlas texture, or null.</param>
        private static void DestroyAll(TMP_SpriteAsset asset, Material material, Texture2D atlas)
        {
            if (asset)
            {
                Object.Destroy(asset);
            }

            if (material)
            {
                Object.Destroy(material);
            }

            if (atlas)
            {
                Object.Destroy(atlas);
            }
        }
    }
}
