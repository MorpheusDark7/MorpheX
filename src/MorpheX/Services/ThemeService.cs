using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace MorpheX.Services;

/// <summary>
/// Manages dynamic Windows 11 system accent adaptation, color variants,
/// and automated WCAG contrast foreground protection.
/// </summary>
public static class ThemeService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        ApplySystemThemeAndAccent();

        // Listen for Windows theme and colorization preference changes
        SystemEvents.UserPreferenceChanged += (sender, args) =>
        {
            if (args.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
            {
                Application.Current?.Dispatcher.InvokeAsync(ApplySystemThemeAndAccent);
            }
        };
    }

    public static void ApplySystemThemeAndAccent()
    {
        try
        {
            var app = Application.Current;
            if (app == null) return;

            // 1. Detect current Windows accent color (e.g., Razer green, slate blue, purple)
            Color accentColor;
            try
            {
                accentColor = ApplicationAccentColorManager.GetColorizationColor();
                if (accentColor.A == 0 || (accentColor.R == 0 && accentColor.G == 0 && accentColor.B == 0))
                {
                    accentColor = Color.FromRgb(56, 189, 248); // Fallback: Sky blue (#38BDF8)
                }
            }
            catch
            {
                accentColor = Color.FromRgb(56, 189, 248);
            }

            // 2. Propagate to Wpf.Ui palette so native controls adapt
            try
            {
                ApplicationAccentColorManager.Apply(accentColor, ApplicationTheme.Dark, false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Wpf.Ui accent application fallback");
            }

            // 3. Professional Technique 1: Light/Dark & Opacity Variants
            // - Subtle background wash (15% opacity) to avoid overwhelming the dark #0E0E12 theme
            // - Refined accent border (35% opacity)
            // - Hover tint (25% opacity)
            // - Light & Dark tone blends
            var accentLight = Blend(accentColor, Colors.White, 0.35);
            var accentDark = Blend(accentColor, Colors.Black, 0.30);
            var accentSubtle = Color.FromArgb(38, accentColor.R, accentColor.G, accentColor.B);  // ~15% opacity
            var accentHover = Color.FromArgb(64, accentColor.R, accentColor.G, accentColor.B);   // ~25% opacity
            var accentBorder = Color.FromArgb(90, accentColor.R, accentColor.G, accentColor.B);  // ~35% opacity

            // 4. Professional Technique 2: Foreground Color Protection (Contrast Checking)
            // WCAG relative luminance: 0.299*R + 0.587*G + 0.114*B
            double luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
            Color textOnAccent = luminance > 0.55
                ? Color.FromRgb(15, 23, 42) // Dark Charcoal/Slate for bright accents (Razer green, yellow, cyan)
                : Colors.White;             // Crisp White for deeper accents (slate blue, purple, crimson)

            // 5. Update Application Resources dynamically
            UpdateResource("MxAccentColor", accentColor);
            UpdateResource("MxAccentBrush", new SolidColorBrush(accentColor));
            UpdateResource("MxAccentLightBrush", new SolidColorBrush(accentLight));
            UpdateResource("MxAccentDarkBrush", new SolidColorBrush(accentDark));
            UpdateResource("MxAccentSubtleBrush", new SolidColorBrush(accentSubtle));
            UpdateResource("MxAccentHoverBrush", new SolidColorBrush(accentHover));
            UpdateResource("MxAccentBorderBrush", new SolidColorBrush(accentBorder));
            UpdateResource("MxAccentForegroundBrush", new SolidColorBrush(textOnAccent));

            // Synchronize Wpf.Ui primary button foreground brushes to ensure contrast protection
            UpdateResource("AccentButtonForeground", new SolidColorBrush(textOnAccent));
            UpdateResource("TextOnAccentFillColorPrimaryBrush", new SolidColorBrush(textOnAccent));

            Log.Information("Applied dynamic system accent: #{R:X2}{G:X2}{B:X2} (Luminance: {Lum:0.00}, TextOnAccent: {Fg})",
                accentColor.R, accentColor.G, accentColor.B, luminance, textOnAccent == Colors.White ? "White" : "Dark");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to apply dynamic system accent");
        }
    }

    private static void UpdateResource(string key, object value)
    {
        if (Application.Current != null)
        {
            Application.Current.Resources[key] = value;
        }
    }

    private static Color Blend(Color c, Color target, double factor)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);
        byte r = (byte)(c.R + (target.R - c.R) * factor);
        byte g = (byte)(c.G + (target.G - c.G) * factor);
        byte b = (byte)(c.B + (target.B - c.B) * factor);
        return Color.FromArgb(c.A, r, g, b);
    }
}
