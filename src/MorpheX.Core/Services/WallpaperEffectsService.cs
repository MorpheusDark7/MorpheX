using System.Drawing;
using System.Drawing.Imaging;
using MorpheX.Core.Configuration;
using MorpheX.Core.Providers;
using Serilog;

namespace MorpheX.Core.Services;

/// <summary>
/// Applies brightness / contrast / saturation / color-temperature effects to any
/// wallpaper provider.  Image and GIF providers receive a GDI ColorMatrix; video
/// providers receive VLC's built-in Adjust video filter.
/// </summary>
public static class WallpaperEffectsService
{
    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>Apply (or remove) effects on a provider immediately.</summary>
    public static void Apply(IWallpaperProvider provider, WallpaperEffectsSettings effects,
                             float brightnessOverride = -1f)
    {
        float brightness = brightnessOverride >= 0 ? brightnessOverride : effects.Brightness;

        switch (provider)
        {
            case ImageWallpaperProvider img:
                img.SetColorMatrix(BuildGdiMatrix(brightness, effects.Contrast,
                                                  effects.Saturation, effects.ColorTemperature));
                break;

            case GifWallpaperProvider gif:
                gif.SetColorMatrix(BuildGdiMatrix(brightness, effects.Contrast,
                                                  effects.Saturation, effects.ColorTemperature));
                break;

            case VideoWallpaperProvider vid:
                ApplyVlcAdjust(vid, brightness, effects.Contrast,
                               effects.Saturation, effects.ColorTemperature);
                break;
        }
    }

    // ─── GDI ColorMatrix builder ────────────────────────────────────────────

    /// <summary>
    /// Builds a 5×5 GDI+ ColorMatrix that applies brightness, contrast,
    /// saturation and a warm/cool color-temperature tint.
    /// </summary>
    public static ColorMatrix? BuildGdiMatrix(float brightness, float contrast,
                                              float saturation, int colorTemperature)
    {
        // If everything is at default, return null so providers skip ImageAttributes overhead.
        if (Math.Abs(brightness - 1f) < 0.01f &&
            Math.Abs(contrast  - 1f) < 0.01f &&
            Math.Abs(saturation - 1f) < 0.01f &&
            colorTemperature == 0)
            return null;

        // Luminance weights (ITU-R BT.601)
        const float Lr = 0.299f, Lg = 0.587f, Lb = 0.114f;

        // Saturation matrix
        float s  = saturation;
        float sr = (1 - s) * Lr, sg = (1 - s) * Lg, sb = (1 - s) * Lb;

        // Contrast: scale then translate so mid-gray stays mid-gray
        float c  = contrast;
        float ct = (1f - c) * 0.5f;   // translation to keep midpoint

        // Color-temperature tint: shift red and blue channels
        // +1 unit = very warm, -1 unit = very cool
        float t    = colorTemperature / 50f;   // normalise to -1 … +1
        float rTint = t > 0 ?  t * 0.12f : 0f;
        float bTint = t < 0 ? -t * 0.12f : 0f;
        float gTint = 0f;

        // Final matrix = brightness × contrast × saturation × tint
        // Applied as: [R G B A offset]
        float b = brightness * c;

        var m = new ColorMatrix(new[]
        {
            new float[] { b*(sr+s+rTint), b*sg,     b*sb,     0, 0 },
            new float[] { b*sr,           b*(sg+s+gTint), b*sb, 0, 0 },
            new float[] { b*sr,           b*sg,     b*(sb+s+bTint), 0, 0 },
            new float[] { 0,              0,        0,        1, 0 },
            new float[] { ct,             ct,       ct,       0, 1 },
        });

        return m;
    }

    // ─── VLC Adjust filter ──────────────────────────────────────────────────

    private static void ApplyVlcAdjust(VideoWallpaperProvider vid,
                                       float brightness, float contrast,
                                       float saturation, int colorTemperature)
    {
        try
        {
            // VLC ranges: brightness 0.0–2.0, contrast 0.0–2.0,
            //             saturation 0.0–3.0, hue -180..180 (degrees)
            float hue = colorTemperature * 1.8f;   // map -50…+50 → -90…+90°

            vid.SetVlcAdjust(brightness, contrast, saturation, hue);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "WallpaperEffectsService: failed to apply VLC adjust filter");
        }
    }
}
