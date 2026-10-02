using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Application = System.Windows.Application;
using System.Windows.Controls;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using MorpheX.Core.Models;
using MorpheX.Core.Services;
using MorpheX.Views;
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

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        // ComboBox selection events can fire while XAML is still creating this
        // page. Do not access named controls until initialization is complete.
        if (WallpaperGrid == null || WallpaperCountText == null ||
            EmptyState == null || SearchEmptyState == null)
        {
            return;
        }

        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        IEnumerable<WallpaperInfo> filtered = string.IsNullOrEmpty(query)
            ? _allWallpapers
            : _allWallpapers.Where(w =>
                w.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Type.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (w.Tags?.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ?? false));

        var filter = (FilterCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "All";
        filtered = filter switch
        {
            "Image" or "Video" or "AnimatedImage" or "Scene" when Enum.TryParse<WallpaperType>(filter, out var type) =>
                filtered.Where(w => w.Type == type),
            _ => filtered
        };

        var sort = (SortCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Newest";
        var results = sort switch
        {
            "Name" => filtered.OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => filtered.OrderByDescending(w => w.DateAdded).ToList()
        };

        WallpaperGrid.ItemsSource = null;
        WallpaperGrid.ItemsSource = results;

        bool isSearching = !string.IsNullOrEmpty(query) || filter != "All";
        WallpaperCountText.Text = isSearching
            ? $"{results.Count} of {_allWallpapers.Count} wallpaper{(_allWallpapers.Count != 1 ? "s" : "")}"
            : $"{_allWallpapers.Count} wallpaper{(_allWallpapers.Count != 1 ? "s" : "")}";
        EmptyState.Visibility = (!isSearching && _allWallpapers.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
        SearchEmptyState.Visibility = (isSearching && results.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
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

    private async void ImportFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose a folder to scan for supported wallpapers",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
            !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            await ImportFilesAsync(new[] { dialog.SelectedPath });
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
        var knownIds = app.LibraryService.Wallpapers.Select(w => w.Id).ToHashSet();
        int importedCount = 0;
        int duplicateCount = 0;
        bool shownSceneWarning = false;

        foreach (var file in ExpandImportFiles(files, failedFiles))
        {
            try
            {
                // Show a one-time resource warning for Scene wallpapers before importing
                var ext = Path.GetExtension(file);
                bool isSceneFile = (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) && LibraryService.IsSceneZip(file)) ||
                                   ext.Equals(".pkg", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(Path.GetFileName(file), "project.json", StringComparison.OrdinalIgnoreCase) ||
                                   LibraryService.IsSceneFolder(file);
                if (isSceneFile && !shownSceneWarning)
                {
                    shownSceneWarning = true;
                    bool confirmed = ModernDialogWindow.Confirm(
                        title:         "Wallpaper Engine Scene",
                        heading:       "Scene Wallpaper Import",
                        message:       "This wallpaper will be imported with its animated preview and original scene audio.\n\n" +
                                       "Note: Proprietary 3D particle physics and shader scripts are replaced by the high-resolution animated preview to ensure low CPU usage and stability.\n\n" +
                                       "Audio is cached locally in your library (~1–5 MB).",
                        primaryText:   "Import",
                        secondaryText: "Cancel",
                        icon:          ModernDialogIcon.Info);
                    if (!confirmed) break;
                }

                var added = await app.LibraryService.AddWallpaperAsync(file);
                if (added == null)
                {
                    var ext2 = Path.GetExtension(file);
                    if (!ext2.Equals(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !ext2.Equals(".pkg", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(Path.GetFileName(file), "project.json", StringComparison.OrdinalIgnoreCase))
                    {
                        failedFiles.Add($"{Path.GetFileName(file)} (unsupported format '{ext2}')");
                    }
                }
                else if (knownIds.Add(added.Id))
                {
                    importedCount++;
                }
                else
                {
                    duplicateCount++;
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
            ModernDialogWindow.Warning(
                title:      "Import Failed",
                heading:    $"{failedFiles.Count} file{(failedFiles.Count == 1 ? "" : "s")} could not be added",
                message:    "The following file(s) could not be added to the library:\n" +
                            string.Join("\n", failedFiles.Select(f => $"  • {f}")) +
                            "\n\nSupported formats: Wallpaper Engine Scenes (*.pkg, *.zip, project.json), " +
                            "Video (MP4, WEBM, MKV, MOV, AVI), Images & GIF (PNG, JPG, BMP, WEBP, TIFF, GIF), Web (HTML, HTM).");
        }

        if (importedCount > 1 || duplicateCount > 0)
        {
            var summary = $"Added {importedCount} wallpaper{(importedCount == 1 ? string.Empty : "s")} to your library.";
            if (duplicateCount > 0)
                summary += $" {duplicateCount} duplicate{(duplicateCount == 1 ? string.Empty : "s")} were skipped.";
            ModernDialogWindow.Success(
                title:   "Import Complete",
                heading: "Wallpapers imported successfully",
                message: summary);
        }

        RefreshWallpaperList();
    }

    private static IEnumerable<string> ExpandImportFiles(IEnumerable<string> entries, List<string> failedFiles)
    {
        foreach (var entry in entries)
        {
            if (Directory.Exists(entry))
            {
                // 1. If entry itself is a WE scene folder, yield it directly
                if (LibraryService.IsSceneFolder(entry))
                {
                    yield return entry;
                    continue;
                }

                // 2. Scan directory recursively
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(entry, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false
                    });
                }
                catch (Exception ex)
                {
                    failedFiles.Add($"{Path.GetFileName(entry)} ({ex.Message})");
                    continue;
                }

                var seenSceneDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var dir = Path.GetDirectoryName(file);
                    if (dir != null)
                    {
                        // If file is inside an already recognized scene directory, ignore internal files
                        if (seenSceneDirs.Any(s => dir.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // Check if dir or an ancestor under entry is a scene folder
                        if (LibraryService.IsSceneFolder(dir))
                        {
                            seenSceneDirs.Add(dir);
                            yield return dir;
                            continue;
                        }
                    }

                    var ext = Path.GetExtension(file);
                    if (ext.Equals(".pkg", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) && !LibraryService.IsSceneZip(file))
                        continue;

                    if (LibraryService.IsSupportedExtension(ext))
                    {
                        yield return file;
                    }
                }
                continue;
            }

            // Regular file
            yield return entry;
        }
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

    private void FavoriteButton_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        ToggleFavorite(sender);
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleFavorite(sender);
    }

    private void ToggleFavorite(object sender)
    {
        if (sender is FrameworkElement el && el.DataContext is WallpaperInfo wp)
        {
            var app = (App)Application.Current;
            app.LibraryService.SetFavorite(wp.Id, !wp.IsFavorite);
            Log.Information("Toggled favorite for '{Name}' → {State}", wp.Name, wp.IsFavorite ? "favorited" : "unfavorited");
            RefreshWallpaperList();
        }
    }

    private static WallpaperInfo? GetWallpaperFromMenuItem(object sender)
    {
        if (sender is not MenuItem mi) return null;
        if (mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement target)
            return target.Tag as WallpaperInfo;
        return mi.DataContext as WallpaperInfo;
    }

    private async void ContextMenu_Apply_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp != null) await ApplyWallpaperAsync(wp);
    }

    private void ContextMenu_Favorite_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        var app = (App)Application.Current;
        app.LibraryService.SetFavorite(wp.Id, !wp.IsFavorite);
        Log.Information("Context menu: toggled favorite for '{Name}'", wp.Name);
        RefreshWallpaperList();
    }

    private void ContextMenu_Info_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        var dialog = new WallpaperInfoDialog(wp, RefreshWallpaperList)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void ContextMenu_OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        var path = wp.EffectivePath;
        if (File.Exists(path))
        {
            Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        else if (Directory.Exists(path))
        {
            Process.Start("explorer.exe", $"\"{path}\"");
        }
        else
        {
            Log.Warning("Cannot open file location — file not found: {Path}", path);
            ModernDialogWindow.Warning(
                title:   "File Not Found",
                heading: "Wallpaper file missing",
                message: $"The wallpaper file could not be found on disk:\n{path}\n\nIt may have been moved, renamed, or deleted.");
        }
    }

    private async void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        bool confirmed = ModernDialogWindow.Confirm(
            title:         "Remove Wallpaper",
            heading:       $"Remove \u201c{wp.Name}\u201d?",
            message:       "This will remove the wallpaper from your library. The original file on disk will not be deleted.",
            primaryText:   "Remove",
            secondaryText: "Cancel",
            icon:          ModernDialogIcon.Warning);

        if (confirmed)
        {
            var app = (App)Application.Current;
            await app.LibraryService.RemoveWallpaperAsync(wp.Id);
            Log.Information("Removed wallpaper '{Name}' from library", wp.Name);
            RefreshWallpaperList();
        }
    }

    private async Task ApplyWallpaperAsync(WallpaperInfo wallpaper)
    {
        if (wallpaper.Type is not (WallpaperType.Image or WallpaperType.Video
                                   or WallpaperType.AnimatedImage or WallpaperType.Scene))
        {
            ModernDialogWindow.Information(
                title:   "Not Supported",
                heading: $"{wallpaper.Type} type not supported",
                message: $"{wallpaper.Type} wallpapers are not yet supported by the MorpheX playback engine.");
            return;
        }
        await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wallpaper, RefreshWallpaperList);
    }
}
