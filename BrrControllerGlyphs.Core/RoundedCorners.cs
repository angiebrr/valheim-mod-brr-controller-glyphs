// ------------------------------------------------------------------------------------------------
// RoundedCorners.cs: pixel coverage of a rounded square, for the prefix backing image (F3).
// ------------------------------------------------------------------------------------------------

using System;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Draws a small rounded square, pixel by pixel, for the prefix backings. The plugin stretches
    /// it with fixed corners (9-slice), since Unity has no rounded shape mods can use at runtime.
    /// </summary>
    public static class RoundedCorners
    {
        /// <summary>
        /// How much of a pixel the rounded square covers, for a smooth (anti-aliased) edge.
        /// </summary>
        /// <param name="x">Pixel column, 0 at the left.</param>
        /// <param name="y">Pixel row, 0 at the bottom.</param>
        /// <param name="size">Image width and height in pixels.</param>
        /// <param name="radius">Corner radius in pixels (at most half the size).</param>
        /// <returns>0 (outside) to 1 (inside).</returns>
        public static float Coverage(int x, int y, int size, float radius)
        {
            // Distance from the pixel's centre to the nearest point of the square shrunk by the radius
            float centreX = x + 0.5f, centreY = y + 0.5f;
            float nearestX = Math.Max(radius, Math.Min(centreX, size - radius));
            float nearestY = Math.Max(radius, Math.Min(centreY, size - radius));
            float dx = centreX - nearestX, dy = centreY - nearestY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            return Math.Max(0f, Math.Min(1f, radius - distance + 0.5f));
        }
    }
}
