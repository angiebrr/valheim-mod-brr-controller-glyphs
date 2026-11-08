// ------------------------------------------------------------------------------------------------
// SpriteFit.cs: scale and baseline that make the mod's glyphs match Valheim's in TextMeshPro (F5).
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// Glyph scale and vertical bearing for the mod's sprites, so the glyph inside each padded image
    /// matches a reference sprite of Valheim's in size and top edge.
    /// </summary>
    public readonly struct SpriteFit
    {
        /// <summary>
        /// Creates a fit.
        /// </summary>
        /// <param name="scale">Glyph scale (TMP_SpriteGlyph.scale).</param>
        /// <param name="bearingY">Vertical bearing in image pixels (GlyphMetrics.horizontalBearingY).</param>
        private SpriteFit(float scale, float bearingY)
        {
            Scale = scale;
            BearingY = bearingY;
        }

        /// <summary>Glyph scale (TMP_SpriteGlyph.scale).</summary>
        public float Scale { get; }

        /// <summary>Vertical bearing in image pixels: baseline to the image's top edge.</summary>
        public float BearingY { get; }

        /// <summary>
        /// Fits our sprites to a reference sprite from the asset whose face info ours copies.
        /// </summary>
        /// <param name="referenceHeight">Reference glyph height in its own pixels (metrics.height).</param>
        /// <param name="referenceBearingY">Reference glyph's vertical bearing in its own pixels.</param>
        /// <param name="referenceScale">Reference character scale times glyph scale.</param>
        /// <param name="facePointSize">The copied face info's point size.</param>
        /// <param name="imageSize">Our image size (glyph plus padding).</param>
        /// <param name="glyphSize">Our glyph size inside the image.</param>
        /// <param name="sizeFactor">Our glyph's height relative to the reference sprite's, kept centred on it.</param>
        /// <returns>The scale and bearing.</returns>
        // TMP_Text: with a point size, a sprite is metrics * scale * fontSize / pointSize. Without one,
        // each sprite's own height is fitted to the font ascent (times its scale), so pixel heights cancel.
        public static SpriteFit Match(float referenceHeight, float referenceBearingY, float referenceScale,
            float facePointSize, int imageSize, int glyphSize, float sizeFactor = 1f)
        {
            // One of our pixels at full size, in reference pixels: our glyph spans the reference's height
            float ourPixelsPerReferencePixel = glyphSize / referenceHeight;
            float scale = facePointSize > 0f
                ? referenceScale / ourPixelsPerReferencePixel
                : referenceScale * imageSize / glyphSize;
            float pad = (imageSize - glyphSize) / 2f;

            // Our glyph's middle sits on the reference's middle; at a smaller size each of our pixels
            // is smaller on screen, so the same height takes more of them
            float referenceMiddle = referenceBearingY - referenceHeight / 2f;
            float bearingY = referenceMiddle * ourPixelsPerReferencePixel / sizeFactor + glyphSize / 2f + pad;
            return new SpriteFit(scale * sizeFactor, bearingY);
        }

        /// <summary>
        /// Fit when Valheim's sprite isn't available: the glyph fills the font ascent, its top on the
        /// ascent line (an asset without a point size).
        /// </summary>
        /// <param name="imageSize">Our image size (glyph plus padding).</param>
        /// <param name="glyphSize">Our glyph size inside the image.</param>
        /// <returns>The scale and bearing.</returns>
        public static SpriteFit Fallback(int imageSize, int glyphSize)
        {
            return Match(glyphSize, glyphSize, 1f, 0f, imageSize, glyphSize);
        }
    }
}
