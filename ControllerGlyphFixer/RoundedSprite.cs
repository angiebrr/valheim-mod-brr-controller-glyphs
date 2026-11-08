// ------------------------------------------------------------------------------------------------
// RoundedSprite.cs: builds the small rounded-square sprite stretched behind group prefixes (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Builds a white rounded-square sprite with 9-slice borders, so an Image can stretch it to any
    /// size while keeping its corners. Tinted by the Image's colour. Pixels come from
    /// <see cref="RoundedCorners.Coverage"/>.
    /// </summary>
    internal static class RoundedSprite
    {
        /// <summary>
        /// Creates the sprite. Call once (it creates a texture) and share it.
        /// </summary>
        /// <param name="radius">Corner radius in pixels; also the 9-slice border.</param>
        /// <returns>The sprite; at 100 pixels per unit, a pixel is one UI unit.</returns>
        internal static Sprite Create(int radius)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "cgf_RoundedBacking",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(RoundedCorners.Coverage(x, y, size, radius) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
