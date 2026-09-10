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
            "Favorites" => filtered.Where(w => w.IsFavorite),
            "Tagged" => filtered.Where(w => w.Tags is { Count: > 0 }),
            "Image" or "Video" or "AnimatedImage" when Enum.TryParse<WallpaperType>(filter, out var type) =>
                filtered.Where(w => w.Type == type),
            _ => filtered
        };

        var sort = (SortCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Newest";
        var results = sort switch
        {
            "Name" => filtered.OrderBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "Favorites" => filtered.OrderByDescending(w => w.IsFavorite).ThenBy(w => w.Name, StringComparer.OrdinalIgnoreCase).ToList(),
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

        foreach (var file in ExpandImportFiles(files, failedFiles))
        {
            try
            {
                var added = await app.LibraryService.AddWallpaperAsync(file);
                if (added == null)
                {
                    var ext = Path.GetExtension(file);
                    failedFiles.Add($"{Path.GetFileName(file)} (unsupported format '{ext}')");
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
            System.Windows.MessageBox.Show(
                "The following file(s) could not be added to the library:\n\n" +
                string.Join("\n", failedFiles) +
                "\n\nSupported video formats: MP4, WEBM, MKV, MOV, AVI\nSupported image formats: PNG, JPG, JPEG, BMP, WEBP, TIFF, GIF",
                "Add Wallpaper",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (importedCount > 1 || duplicateCount > 0)
        {
            var summary = $"Added {importedCount} wallpaper{(importedCount == 1 ? string.Empty : "s")}.";
            if (duplicateCount > 0)
            {
                summary += $"\n\nSkipped {duplicateCount} duplicate{(duplicateCount == 1 ? string.Empty : "s")} already in your library.";
            }
            System.Windows.MessageBox.Show(summary, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        RefreshWallpaperList();
    }

    private static IEnumerable<string> ExpandImportFiles(IEnumerable<string> entries, List<string> failedFiles)
    {
        foreach (var entry in entries)
        {
            if (!Directory.Exists(entry))
            {
                yield return entry;
                continue;
            }

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

            foreach (var file in files)
            {
                if (LibraryService.IsSupportedExtension(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
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

    private async void ContextMenu_EditTags_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WallpaperInfo wallpaper }) return;

        var value = SimpleInputDialog.Show(
            "Edit Wallpaper Tags",
            "Separate tags with commas. Tags make this wallpaper easier to find.",
            string.Join(", ", wallpaper.Tags ?? new List<string>()));
        if (value == null) return;

        var tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        await ((App)Application.Current).LibraryService.SetTagsAsync(wallpaper.Id, tags);
        RefreshWallpaperList();
    }

    private async void ContextMenu_AddToCollection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WallpaperInfo wallpaper }) return;

        var collectionName = SimpleInputDialog.Show(
            "Add to Collection",
            "Enter a collection name. An existing collection with this name will be used.");
        if (string.IsNullOrWhiteSpace(collectionName)) return;

        var app = (App)Application.Current;
        var collection = await app.LibraryService.CreateCollectionAsync(collectionName);
        if (collection != null)
        {
            await app.LibraryService.AddWallpaperToCollectionAsync(collection.Id, wallpaper.Id);
            Log.Information("Added wallpaper '{Name}' to collection '{Collection}'", wallpaper.Name, collection.Name);
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
        if (wallpaper.Type is not (WallpaperType.Image or WallpaperType.Video or WallpaperType.AnimatedImage))
        {
            System.Windows.MessageBox.Show(
                $"{wallpaper.Type} wallpapers are not yet supported by the playback engine.",
                "Wallpaper Type Not Supported",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wallpaper, RefreshWallpaperList);
    }
}
