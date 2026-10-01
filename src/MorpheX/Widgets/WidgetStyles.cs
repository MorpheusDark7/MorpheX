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
    /// Applies an ultra-clean, elegant frosted acrylic glass styling to a widget container border.
    /// Unlike opaque black boxes, this lets the user's wallpaper shine through with gentle contrast,
    /// soft specular highlight borders, and ambient depth.
    /// </summary>
    public static void ApplyFrostedGlass(Border border, bool showBackground, double cornerRadius = 14, Thickness? padding = null)
    {
        if (showBackground)
        {
            // Translucent smoked glass gradient (lets wallpaper radiate through, not an opaque box)
            var bgBrush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x2E, 0x10, 0x14, 0x1E), 0.0)); // ~18% slate glass
            bgBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0x08, 0x0A, 0x12), 1.0)); // ~10% light acrylic

            // Specular refraction border (subtle light on top rim, soft fade below)
            var borderBrush = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), 0.0)); // subtle hairline top
            borderBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF), 1.0)); // soft fade bottom

            border.Background = bgBrush;
            border.BorderBrush = borderBrush;
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(cornerRadius);
            border.Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 24,
                ShadowDepth = 2,
                Opacity = 0.28
            };

            if (padding.HasValue)
                border.Padding = padding.Value;
        }
        else
        {
            border.Background = Brushes.Transparent;
            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
            border.Effect = null;

            if (padding.HasValue)
                border.Padding = new Thickness(4, 2, 4, 2);
        }
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
