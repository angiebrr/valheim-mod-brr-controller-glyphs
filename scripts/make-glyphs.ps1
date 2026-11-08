# ------------------------------------------------------------------------------------------------
# make-glyphs.ps1: makes the mod's controller glyphs (F5) from Kenney's CC0 Input Prompts.
# ------------------------------------------------------------------------------------------------
#
# Download "Input Prompts" from https://kenney.nl/assets/input-prompts (CC0), unzip it anywhere
# outside the repo, and run:
#
#   pwsh scripts/make-glyphs.ps1 -KenneyDir <unzipped folder>
#
# Each Steam Deck glyph is recoloured to match Valheim's dark glyphs: Kenney's plain glyph (letters
# cut out) plus its _outline variant (rim + letters) is the solid shape; it's filled dark, the
# outline variant is drawn over it in light, and a soft shadow goes underneath. Output: one
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

/// <summary>Recolours a Kenney glyph pair and adds a shadow (see make-glyphs.ps1).</summary>
public static class GlyphComposer
{
    /// <summary>Composes one glyph onto a padded canvas.</summary>
    public static Bitmap Compose(string plainPath, string outlinePath, int pad, Color fill, Color ink,
        int shadowX, int shadowY, int blur, float shadowOpacity)
    {
        using (var plain = new Bitmap(plainPath))
        using (var outline = new Bitmap(outlinePath))
        {
            int w = plain.Width + 2 * pad, h = plain.Height + 2 * pad;
            var solid = new float[w * h];
            var rim = new float[w * h];
            for (int y = 0; y < plain.Height; y++)
            {
                for (int x = 0; x < plain.Width; x++)
                {
                    float o = outline.GetPixel(x, y).A / 255f;
                    solid[(y + pad) * w + x + pad] = Math.Max(plain.GetPixel(x, y).A / 255f, o);
                    rim[(y + pad) * w + x + pad] = o;
                }
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

    /// <summary>Rounds and clamps to a colour channel.</summary>
    private static int To255(float value)
    {
        return Math.Max(0, Math.Min(255, (int)Math.Round(value)));
    }
}
'@

if (-not ([System.Management.Automation.PSTypeName]'GlyphComposer').Type) {
    Add-Type -TypeDefinition $Composer -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives
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

    $glyph = [GlyphComposer]::Compose($plain, $outline, 8, $fill, $ink, 3, 4, 4, 0.8)
    $glyph.Save((Join-Path $out "$sprite.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $glyph.Dispose()
}

Copy-Item $license (Join-Path $out 'License.txt') -Force
Write-Host "Wrote $($Sprites.Count) glyphs to $((Resolve-Path $out).Path)"
