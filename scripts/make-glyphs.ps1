# ------------------------------------------------------------------------------------------------
# make-glyphs.ps1: makes the mod's controller glyphs (F5) from Kenney's CC0 Input Prompts.
# ------------------------------------------------------------------------------------------------
#
# Download "Input Prompts" from https://kenney.nl/assets/input-prompts (CC0), unzip it anywhere
# outside the repo, and run:
#
#   pwsh scripts/make-glyphs.ps1 -KenneyDir <unzipped folder>
#
# Each Steam Deck glyph is recolored to match Valheim's dark glyphs: Kenney's plain glyph (letters
# cut out) plus its _outline variant (rim + letters) is the solid shape; it's filled dark, the
# letters and a thin rim along the shape's edge are light, and a soft shadow goes underneath. Output: one
# 144 x 144 PNG per sprite in ControllerGlyphFixer/Glyphs (the 128 px glyph with 8 px for the
# shadow), named as ModGlyphs.SpriteNames, plus Kenney's License.txt. Commit the outputs.

param(
    [Parameter(Mandatory = $true)]
    [string] $KenneyDir
)

$ErrorActionPreference = 'Stop'

# Sprite name -> Kenney base name (Steam Deck/Double). Sticks are left out: no _outline variant.
$Sprites = [ordered]@{
    'a' = 'steamdeck_button_a'; 'b' = 'steamdeck_button_b'; 'x' = 'steamdeck_button_x'; 'y' = 'steamdeck_button_y'
    'l1' = 'steamdeck_button_l1'; 'r1' = 'steamdeck_button_r1'; 'l2' = 'steamdeck_button_l2'; 'r2' = 'steamdeck_button_r2'
    'l4' = 'steamdeck_button_l4'; 'l5' = 'steamdeck_button_l5'; 'r4' = 'steamdeck_button_r4'; 'r5' = 'steamdeck_button_r5'
    'view' = 'steamdeck_button_view'; 'options' = 'steamdeck_button_options'
    'dpad_up' = 'steamdeck_dpad_up'; 'dpad_down' = 'steamdeck_dpad_down'
    'dpad_left' = 'steamdeck_dpad_left'; 'dpad_right' = 'steamdeck_dpad_right'
    'lpad' = 'steamdeck_trackpad_l'; 'lpad_up' = 'steamdeck_trackpad_l_up'; 'lpad_down' = 'steamdeck_trackpad_l_down'
    'lpad_left' = 'steamdeck_trackpad_l_left'; 'lpad_right' = 'steamdeck_trackpad_l_right'
    'rpad' = 'steamdeck_trackpad_r'; 'rpad_up' = 'steamdeck_trackpad_r_up'; 'rpad_down' = 'steamdeck_trackpad_r_down'
    'rpad_left' = 'steamdeck_trackpad_r_left'; 'rpad_right' = 'steamdeck_trackpad_r_right'
}

$Composer = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

/// <summary>Recolors a Kenney glyph pair and adds a shadow (see make-glyphs.ps1).</summary>
public static class GlyphComposer
{
    /// <summary>Width of the rim in Kenney's 128 px outline variants (measured: 6 px), plus a pixel of anti-aliasing.</summary>
    private const float KenneyRim = 7f;

    /// <summary>Composes one glyph onto a padded canvas, with a light rim rimWidth px thick.</summary>
    public static Bitmap Compose(string plainPath, string outlinePath, int pad, Color fill, Color ink,
        int shadowX, int shadowY, int blur, float shadowOpacity, float rimWidth)
    {
        using (var plain = new Bitmap(plainPath))
        using (var outline = new Bitmap(outlinePath))
        {
            int w = plain.Width + 2 * pad, h = plain.Height + 2 * pad;
            var solid = new float[w * h];
            var light = new float[w * h];
            for (int y = 0; y < plain.Height; y++)
            {
                for (int x = 0; x < plain.Width; x++)
                {
                    float o = outline.GetPixel(x, y).A / 255f;
                    int i = (y + pad) * w + x + pad;
                    // Summed, not max: at the letters' anti-aliased edges the cut-out and the letters are
                    // each half there, and max would leave a seam the erosion below spreads
                    solid[i] = Math.Min(1f, plain.GetPixel(x, y).A / 255f + o);
                    light[i] = o;
                }
            }

            // Kenney's rim is ~6 px; darken the part of it more than rimWidth px in from the edge, and
            // keep everything deeper (letters, a pressed D-pad arm) as Kenney drew it
            float[] pastThinRim = Erode(solid, w, h, rimWidth);
            float[] pastKenneyRim = Erode(solid, w, h, KenneyRim);
            bool[] marker = SmallParts(solid, w, h);
            var rim = new float[w * h];
            for (int i = 0; i < rim.Length; i++)
            {
                // Direction arrows are small separate shapes: fully light, or they vanish at hint size
                float keep = marker[i] ? 1f : Math.Max(solid[i] - pastThinRim[i], pastKenneyRim[i]);
                rim[i] = (marker[i] ? solid[i] : light[i]) * Math.Min(1f, keep);
            }

            float[] shadow = Blur(Shift(solid, w, h, shadowX, shadowY), w, h, blur);
            var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    // Shadow (black) under the glyph, then the glyph: fill blended to ink by the rim
                    float sa = shadow[i] * shadowOpacity, ga = solid[i];
                    float a = ga + sa * (1 - ga);
                    if (a <= 0f)
                    {
                        result.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    float t = rim[i];
                    float r = (fill.R + (ink.R - fill.R) * t) * ga / a;
                    float g = (fill.G + (ink.G - fill.G) * t) * ga / a;
                    float b = (fill.B + (ink.B - fill.B) * t) * ga / a;
                    result.SetPixel(x, y, Color.FromArgb(To255(a * 255f), To255(r), To255(g), To255(b)));
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Marks the pixels of shapes much smaller than the glyph's main body (the trackpad direction
    /// arrows), found as connected parts of the mask (half covered or more, 8-connected).
    /// </summary>
    private static bool[] SmallParts(float[] mask, int w, int h)
    {
        var label = new int[w * h];
        var sizes = new System.Collections.Generic.List<int> { 0 };
        var queue = new System.Collections.Generic.Queue<int>();
        for (int start = 0; start < mask.Length; start++)
        {
            if (mask[start] < 0.5f || label[start] != 0)
            {
                continue;
            }

            int id = sizes.Count, size = 0;
            label[start] = id;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                size++;
                int x = i % w, y = i / w;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy, n = ny * w + nx;
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && mask[n] >= 0.5f && label[n] == 0)
                        {
                            label[n] = id;
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            sizes.Add(size);
        }

        int largest = 0;
        foreach (int size in sizes)
        {
            largest = Math.Max(largest, size);
        }

        // Grow the marks by a pixel so the arrows' anti-aliased edges count too
        var small = new bool[w * h];
        for (int i = 0; i < small.Length; i++)
        {
            if (label[i] == 0 || sizes[label[i]] * 5 > largest)
            {
                continue;
            }

            int x = i % w, y = i / w;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                    {
                        small[ny * w + nx] = true;
                    }
                }
            }
        }

        return small;
    }

    /// <summary>Shrinks a mask by a radius: each pixel takes the lowest value within the disc around it.</summary>
    private static float[] Erode(float[] mask, int w, int h, float radius)
    {
        var eroded = new float[w * h];
        int r = (int)Math.Ceiling(radius);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float min = mask[y * w + x];
                for (int dy = -r; dy <= r && min > 0f; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius)
                        {
                            continue;
                        }

                        int sx = x + dx, sy = y + dy;
                        float v = sx >= 0 && sy >= 0 && sx < w && sy < h ? mask[sy * w + sx] : 0f;
                        if (v < min)
                        {
                            min = v;
                        }
                    }
                }

                eroded[y * w + x] = min;
            }
        }

        return eroded;
    }

    /// <summary>Moves an alpha mask by (dx, dy), clearing what moves in from outside.</summary>
    private static float[] Shift(float[] mask, int w, int h, int dx, int dy)
    {
        var shifted = new float[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int sx = x - dx, sy = y - dy;
                if (sx >= 0 && sy >= 0 && sx < w && sy < h)
                {
                    shifted[y * w + x] = mask[sy * w + sx];
                }
            }
        }

        return shifted;
    }

    /// <summary>Box blur with the given radius (outside the canvas counts as empty).</summary>
    private static float[] Blur(float[] mask, int w, int h, int radius)
    {
        var blurred = new float[w * h];
        int n = (2 * radius + 1) * (2 * radius + 1);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = x + dx, sy = y + dy;
                        if (sx >= 0 && sy >= 0 && sx < w && sy < h)
                        {
                            sum += mask[sy * w + sx];
                        }
                    }
                }

                blurred[y * w + x] = sum / n;
            }
        }

        return blurred;
    }

    /// <summary>Rounds and clamps to a color channel.</summary>
    private static int To255(float value)
    {
        return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'GlyphComposer').Type) {
    Add-Type -TypeDefinition $Composer -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Collections
}

$source = Join-Path $KenneyDir 'Steam Deck\Double'
$license = Join-Path $KenneyDir 'License.txt'
if (-not (Test-Path $source) -or -not (Test-Path $license)) {
    throw "Not an unzipped Kenney Input Prompts folder (needs 'Steam Deck\Double' and License.txt): $KenneyDir"
}

$out = Join-Path $PSScriptRoot '..\ControllerGlyphFixer\Glyphs'
New-Item -ItemType Directory -Force $out | Out-Null
Get-ChildItem $out -Filter *.png | Remove-Item

# Look (spec F5, chosen 2026-11-08): dark fill, light rim and letters, soft shadow
$fill = [System.Drawing.Color]::FromArgb(255, 0x14, 0x18, 0x1C)
$ink = [System.Drawing.Color]::FromArgb(255, 0xEB, 0xE6, 0xDC)

foreach ($sprite in $Sprites.Keys) {
    $plain = Join-Path $source "$($Sprites[$sprite]).png"
    $outline = Join-Path $source "$($Sprites[$sprite])_outline.png"
    foreach ($file in $plain, $outline) {
        if (-not (Test-Path $file)) { throw "Missing Kenney file: $file" }
    }

    # Rim 3 px (Kenney's is ~6; in game 2026-11-08 it looked too thick next to Valheim's)
    $glyph = [GlyphComposer]::Compose($plain, $outline, 8, $fill, $ink, 3, 4, 4, 0.8, 3.0)
    $glyph.Save((Join-Path $out "$sprite.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $glyph.Dispose()
}

Copy-Item $license (Join-Path $out 'License.txt') -Force
Write-Host "Wrote $($Sprites.Count) glyphs to $((Resolve-Path $out).Path)"
