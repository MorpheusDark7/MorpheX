using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX;

public partial class WallpaperInfoDialog : Window
{
    private readonly WallpaperInfo _wallpaper;
    private Action? _onChanged;

    public WallpaperInfoDialog(WallpaperInfo wallpaper, Action? onChanged = null)
    {
        InitializeComponent();
        _wallpaper = wallpaper;
        _onChanged = onChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateFields();
    }

    private void PopulateFields()
    {
        var w = _wallpaper;

        // Thumbnail
        if (!string.IsNullOrEmpty(w.PreviewImagePath) && File.Exists(w.PreviewImagePath))
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(w.PreviewImagePath, UriKind.Absolute);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                ThumbnailImage.Source = bmp;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not load thumbnail for info dialog");
            }
        }

        // Header
        HeaderNameText.Text = w.Name;
        TypeBadge.Text = w.Type.ToString().ToUpperInvariant();
        ActiveBadge.Visibility = w.IsActive ? Visibility.Visible : Visibility.Collapsed;

        // Editable fields
        NameTextBox.Text = w.Name;
        TagsTextBox.Text = w.Tags is { Count: > 0 } ? string.Join(", ", w.Tags) : string.Empty;

        // Metadata
        ResolutionText.Text = w.ResolutionText ?? "—";
        DurationText.Text = w.Duration.HasValue
            ? FormatDuration(w.Duration.Value)
            : "—";
        FileSizeText.Text = w.FileSize > 0 ? FormatBytes(w.FileSize) : "—";
        DateAddedText.Text = w.DateAdded.ToLocalTime().ToString("MMM d, yyyy");
        FilePathText.Text = w.EffectivePath;
        IdText.Text = w.Id;
    }

    private async void SaveNameButton_Click(object sender, RoutedEventArgs e)
    {
        var newName = NameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            System.Windows.MessageBox.Show("Name cannot be empty.", "Rename",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var app = (App)Application.Current;
        await app.LibraryService.RenameWallpaperAsync(_wallpaper.Id, newName);
        Log.Information("Renamed wallpaper '{Old}' → '{New}'", _wallpaper.Name, newName);

        // Update header
        HeaderNameText.Text = newName;
        _onChanged?.Invoke();
    }

    private async void SaveTagsButton_Click(object sender, RoutedEventArgs e)
    {
        var raw = TagsTextBox.Text ?? string.Empty;
        var tags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var app = (App)Application.Current;
        await app.LibraryService.SetTagsAsync(_wallpaper.Id, tags);
        Log.Information("Updated tags for '{Name}': {Tags}", _wallpaper.Name, string.Join(", ", tags));
        _onChanged?.Invoke();
    }

    private void OpenLocationButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _wallpaper.EffectivePath;
        if (File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else
        {
            System.Windows.MessageBox.Show(
                $"The wallpaper file was not found on disk:\n\n{path}",
                "File Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void FilePathText_Click(object sender, MouseButtonEventArgs e)
    {
        OpenLocationButton_Click(sender, e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static string FormatDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:0.0} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:0.0} MB",
            >= 1024 => $"{bytes / 1024.0:0.0} KB",
            _ => $"{bytes} B"
        };
    }
}
