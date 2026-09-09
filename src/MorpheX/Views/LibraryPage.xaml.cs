using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using System.Windows.Controls;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX;

public partial class LibraryPage : Page
{
    public LibraryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshWallpaperList();
    }

    private List<WallpaperInfo> _allWallpapers = new();

    public void RefreshWallpaperList()
    {
        var app = (App)Application.Current;
        var wallpapers = app.LibraryService.Wallpapers;

        foreach (var w in wallpapers)
        {
            w.IsActive = app.WallpaperService != null && app.WallpaperService.IsWallpaperActive(w.Id);
        }

        _allWallpapers = wallpapers.ToList();
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? _allWallpapers
            : _allWallpapers.Where(w =>
                w.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Type.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        WallpaperGrid.ItemsSource = null;
        WallpaperGrid.ItemsSource = filtered;

        bool isSearching = !string.IsNullOrEmpty(query);
        WallpaperCountText.Text = isSearching
            ? $"{filtered.Count} of {_allWallpapers.Count} wallpaper{(_allWallpapers.Count != 1 ? "s" : "")}"
            : $"{_allWallpapers.Count} wallpaper{(_allWallpapers.Count != 1 ? "s" : "")}";
        EmptyState.Visibility = (!isSearching && _allWallpapers.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
        SearchEmptyState.Visibility = (isSearching && filtered.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddWallpaperButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add Wallpaper",
            Filter = LibraryService.GetFileFilter(),
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            await ImportFilesAsync(dialog.FileNames);
        }
    }

    private void Grid_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void Grid_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                await ImportFilesAsync(files);
            }
        }
    }

    private async Task ImportFilesAsync(IEnumerable<string> files)
    {
        var app = (App)Application.Current;
        var failedFiles = new List<string>();

        foreach (var file in files)
        {
            if (Directory.Exists(file))
            {
                try
                {
                    var innerFiles = Directory.GetFiles(file);
                    foreach (var inner in innerFiles)
                    {
                        var ext = Path.GetExtension(inner);
                        if (LibraryService.IsSupportedExtension(ext))
                        {
                            var res = await app.LibraryService.AddWallpaperAsync(inner);
                            if (res == null)
                                failedFiles.Add($"{Path.GetFileName(inner)} (failed to load)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedFiles.Add($"{Path.GetFileName(file)} ({ex.Message})");
                }
                continue;
            }

            try
            {
                var added = await app.LibraryService.AddWallpaperAsync(file);
                if (added == null)
                {
                    var ext = Path.GetExtension(file);
                    failedFiles.Add($"{Path.GetFileName(file)} (unsupported format '{ext}')");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add wallpaper: {File}", file);
                failedFiles.Add($"{Path.GetFileName(file)} ({ex.Message})");
            }
        }

        if (failedFiles.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "The following file(s) could not be added to the library:\n\n" +
                string.Join("\n", failedFiles) +
                "\n\nSupported video formats: MP4, WEBM, MKV, MOV, AVI\nSupported image formats: PNG, JPG, JPEG, BMP, WEBP, TIFF, GIF",
                "Add Wallpaper",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshWallpaperList();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWallpaperList();
    }

    private async void WallpaperCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WallpaperInfo wallpaper)
        {
            Log.Information("User clicked wallpaper card: '{Name}' ({Type})", wallpaper.Name, wallpaper.Type);
            await ApplyWallpaperAsync(wallpaper);
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is FrameworkElement el && el.DataContext is WallpaperInfo wp)
        {
            var app = (App)Application.Current;
            app.LibraryService.SetFavorite(wp.Id, !wp.IsFavorite);
            Log.Information("Toggled favorite for '{Name}' → {State}", wp.Name, wp.IsFavorite ? "favorited" : "unfavorited");
            RefreshWallpaperList();
        }
    }

    private async void ContextMenu_Apply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is WallpaperInfo wp)
        {
            await ApplyWallpaperAsync(wp);
        }
    }

    private void ContextMenu_Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is WallpaperInfo wp)
        {
            var app = (App)Application.Current;
            app.LibraryService.SetFavorite(wp.Id, !wp.IsFavorite);
            Log.Information("Context menu: toggled favorite for '{Name}'", wp.Name);
            RefreshWallpaperList();
        }
    }

    private void ContextMenu_OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is WallpaperInfo wp)
        {
            var path = wp.EffectivePath;
            if (File.Exists(path))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else
            {
                Log.Warning("Cannot open file location — file not found: {Path}", path);
                System.Windows.MessageBox.Show(
                    $"The wallpaper file was not found on disk:\n\n{path}\n\nIt may have been moved, renamed, or deleted.",
                    "File Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    private async void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is WallpaperInfo wp)
        {
            var result = System.Windows.MessageBox.Show(
                $"Remove \"{wp.Name}\" from the library?\n\nThis will NOT delete the original file.",
                "Remove Wallpaper",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var app = (App)Application.Current;
                await app.LibraryService.RemoveWallpaperAsync(wp.Id);
                Log.Information("Removed wallpaper '{Name}' from library", wp.Name);
                RefreshWallpaperList();
            }
        }
    }

    private async Task ApplyWallpaperAsync(WallpaperInfo wallpaper)
    {
        await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wallpaper, RefreshWallpaperList);
    }
}
