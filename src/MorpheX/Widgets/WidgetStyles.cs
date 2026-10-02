using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;

namespace MorpheX.Widgets;

public static class WidgetStyles
{
    private static FontFamily? _anuratiFont;
    private static FontFamily? _aquaticoFont;
    private static FontFamily? _quicksandFont;

    /// <summary>
    /// Reliably loads the Anurati futuristic font.
    /// Handles installed system fonts, app directories, and local extraction.
    /// </summary>
    public static FontFamily GetAnuratiFont()
    {
        if (_anuratiFont != null) return _anuratiFont;

        try
        {
            // 1. Check if Anurati is installed in Windows system fonts
            if (System.Windows.Media.Fonts.SystemFontFamilies.Any(f => f.Source.Equals("Anurati", StringComparison.OrdinalIgnoreCase)))
            {
                return _anuratiFont = new FontFamily("Anurati");
            }

            // 2. Locate directory with font files
            string? fontDir = FindFontDirectory("Anurati-Regular.otf", "Anurati.otf");
            if (fontDir != null)
            {
                string dirUri = Path.GetFullPath(fontDir).Replace('\\', '/');
                if (!dirUri.EndsWith('/')) dirUri += '/';
                return _anuratiFont = new FontFamily(new Uri(dirUri), "./#Anurati");
            }
        }
        catch
        {
            // fallback
        }

        return _anuratiFont = new FontFamily("Bahnschrift Light, Segoe UI Light, Segoe UI");
    }

    public static FontFamily GetAquaticoFont()
    {
        if (_aquaticoFont != null) return _aquaticoFont;
        try
        {
            if (System.Windows.Media.Fonts.SystemFontFamilies.Any(f => f.Source.Equals("Aquatico", StringComparison.OrdinalIgnoreCase)))
                return _aquaticoFont = new FontFamily("Aquatico");

            string? fontDir = FindFontDirectory("Aquatico.otf");
            if (fontDir != null)
            {
                string dirUri = Path.GetFullPath(fontDir).Replace('\\', '/');
                if (!dirUri.EndsWith('/')) dirUri += '/';
                return _aquaticoFont = new FontFamily(new Uri(dirUri), "./#Aquatico");
            }
        }
        catch { }
        return _aquaticoFont = new FontFamily("Segoe UI Light, Segoe UI");
    }

    public static FontFamily GetQuicksandFont()
    {
        if (_quicksandFont != null) return _quicksandFont;
        try
        {
            if (System.Windows.Media.Fonts.SystemFontFamilies.Any(f => f.Source.Equals("Quicksand", StringComparison.OrdinalIgnoreCase)))
                return _quicksandFont = new FontFamily("Quicksand");

            string? fontDir = FindFontDirectory("Quicksand.otf");
            if (fontDir != null)
            {
                string dirUri = Path.GetFullPath(fontDir).Replace('\\', '/');
                if (!dirUri.EndsWith('/')) dirUri += '/';
                return _quicksandFont = new FontFamily(new Uri(dirUri), "./#Quicksand");
            }
        }
        catch { }
        return _quicksandFont = new FontFamily("Segoe UI Light, Segoe UI");
    }

    private static string? FindFontDirectory(params string[] fontFileNames)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string appAssets = Path.Combine(baseDir, "Assets", "Fonts");

        foreach (var fn in fontFileNames)
        {
            if (File.Exists(Path.Combine(appAssets, fn)))
                return appAssets;
        }

        // Check AppData directory
        string appDataFonts = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MorpheX", "Fonts");

        Directory.CreateDirectory(appDataFonts);

        foreach (var fn in fontFileNames)
        {
            string targetPath = Path.Combine(appDataFonts, fn);
            if (File.Exists(targetPath))
                return appDataFonts;

            // Try extract from embedded resource
            try
            {
                var asm = typeof(WidgetStyles).Assembly;
                string resName = "assets/fonts/" + fn.ToLowerInvariant();
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using var fs = File.Create(targetPath);
                    stream.CopyTo(fs);
                    return appDataFonts;
                }
            }
            catch { }
        }

        // Check user's Downloads Mond extracted directory
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] searchDirs = [
            Path.Combine(userProfile, "Downloads", "Mond_Extracted", "Skins", "Mond", "@Resources", "Fonts"),
            Path.Combine(userProfile, "Downloads", "Anurati_Extracted", "ANURATI Free Font")
        ];

        foreach (var sd in searchDirs)
        {
            if (!Directory.Exists(sd)) continue;
            foreach (var fn in fontFileNames)
            {
                if (File.Exists(Path.Combine(sd, fn)))
                {
                    // Copy to appDataFonts for permanent access
                    try
                    {
                        File.Copy(Path.Combine(sd, fn), Path.Combine(appDataFonts, fn), true);
                        return appDataFonts;
                    }
                    catch { return sd; }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Applies an ultra-clean frosted glass style to a widget border,
    /// automatically switching between dark and light themes.
    /// </summary>
    public static void ApplyFrostedGlass(Border border, bool showBackground, double cornerRadius = 14,
        Thickness? padding = null, bool isLightTheme = false)
    {
        if (showBackground)
        {
            LinearGradientBrush bgBrush;
            LinearGradientBrush borderBrush;
            DropShadowEffect shadow;

            if (isLightTheme)
            {
                // ── Light theme — frosted cream/white glass ──────────────────
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint   = new System.Windows.Point(0, 1)
                };
                // High-alpha white so it reads on bright wallpapers too
                bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xCC, 0xFF, 0xFD, 0xF7), 0.0)); // warm cream top
                bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0xBB, 0xF5, 0xF3, 0xEE), 1.0)); // soft ivory bottom

                borderBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint   = new System.Windows.Point(0, 1)
                };
                borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF), 0.0)); // bright specular top
                borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x20, 0xA0, 0x90, 0x70), 1.0)); // warm tinted bottom

                shadow = new DropShadowEffect
                {
                    Color       = Color.FromRgb(0xA0, 0x90, 0x70),
                    BlurRadius  = 18,
                    ShadowDepth = 2,
                    Opacity     = 0.18
                };
            }
            else
            {
                // ── Dark theme — smoked dark glass (original) ────────────────
                bgBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint   = new System.Windows.Point(0, 1)
                };
                bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x2E, 0x10, 0x14, 0x1E), 0.0));
                bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0x08, 0x0A, 0x12), 1.0));

                borderBrush = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint   = new System.Windows.Point(0, 1)
                };
                borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), 0.0));
                borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF), 1.0));

                shadow = new DropShadowEffect
                {
                    Color       = Color.FromRgb(0, 0, 0),
                    BlurRadius  = 24,
                    ShadowDepth = 2,
                    Opacity     = 0.28
                };
            }

            border.Background        = bgBrush;
            border.BorderBrush       = borderBrush;
            border.BorderThickness   = new Thickness(1);
            border.CornerRadius      = new CornerRadius(cornerRadius);
            border.Effect            = shadow;

            if (padding.HasValue)
                border.Padding = padding.Value;
        }
        else
        {
            border.Background      = Brushes.Transparent;
            border.BorderBrush     = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
            border.Effect          = null;

            if (padding.HasValue)
                border.Padding = new Thickness(4, 2, 4, 2);
        }
    }

    /// <summary>
    /// Applies the appropriate text foreground color for a widget based on the current theme.
    /// Dark theme → semi-transparent white; Light theme → dark charcoal.
    /// </summary>
    public static System.Windows.Media.Brush GetWidgetForeground(bool isLightTheme, double opacity = 1.0)
    {
        if (isLightTheme)
        {
            // Dark warm charcoal — readable on the cream background
            return new SolidColorBrush(Color.FromArgb(
                (byte)(0xD8 * opacity), 0x22, 0x1E, 0x18));
        }
        return new SolidColorBrush(Color.FromArgb(
            (byte)(0xCC * opacity), 0xFF, 0xFF, 0xFF));
    }

    /// <summary>Returns a muted/secondary foreground color (labels, subtitles).</summary>
    public static System.Windows.Media.Brush GetWidgetSubtleForeground(bool isLightTheme)
    {
        if (isLightTheme)
            return new SolidColorBrush(Color.FromArgb(0x80, 0x44, 0x38, 0x28));
        return new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
    }


    /// <summary>
    /// Updates widget locking state: removes resize grips and switches cursors when locked.
    /// </summary>
    public static void ApplyLockState(Window window, FrameworkElement? dragHandle, bool isLocked, bool hasResizeGrip = false)
    {
        if (hasResizeGrip)
        {
            window.ResizeMode = isLocked ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        }

        if (dragHandle != null)
        {
            dragHandle.Cursor = isLocked ? Cursors.Arrow : Cursors.SizeAll;
        }
    }
}
