using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using MorpheX.Core.Models;
using Serilog;

namespace MorpheX;

public partial class FavoritesPage : Page
{
    public FavoritesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshFavorites();
    }

    public void RefreshFavorites()
    {
        var app = (App)Application.Current;
        var favorites = app.LibraryService.Wallpapers.Where(w => w.IsFavorite).ToList();
        foreach (var w in favorites)
        {
            w.IsActive = app.WallpaperService != null && app.WallpaperService.IsWallpaperActive(w.Id);
        }

        FavoritesGrid.ItemsSource = null;
        FavoritesGrid.ItemsSource = favorites;
        FavoriteCountText.Text = $"{favorites.Count} favorite{(favorites.Count != 1 ? "s" : "")}";
        EmptyState.Visibility = favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void WallpaperCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is WallpaperInfo wallpaper)
        {
            await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wallpaper, RefreshFavorites);
        }
    }

    /// <summary>
    /// Gets WallpaperInfo from a context menu item by walking up to the placement target.
    /// ContextMenus are outside the visual tree so DataContext doesn't inherit; Tag does.
    /// </summary>
    private static WallpaperInfo? GetWallpaperFromMenuItem(object sender)
    {
        if (sender is not MenuItem mi) return null;
        // Walk up: MenuItem → ContextMenu → PlacementTarget (the CardAction)
        if (mi.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement target)
            return target.Tag as WallpaperInfo;
        // Fallback: DataContext (works in some templates)
        return mi.DataContext as WallpaperInfo;
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement el && el.DataContext is WallpaperInfo wp)
        {
            var app = (App)Application.Current;
            app.LibraryService.SetFavorite(wp.Id, false);
            Log.Information("Removed '{Name}' from favorites", wp.Name);
            RefreshFavorites();
        }
    }

    private async void ContextMenu_Apply_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp != null)
            await MonitorPickerHelper.ApplyWallpaperWithPickerAsync(wp, RefreshFavorites);
    }

    private void ContextMenu_RemoveFavorite_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        var app = (App)Application.Current;
        app.LibraryService.SetFavorite(wp.Id, false);
        Log.Information("Removed '{Name}' from favorites via context menu", wp.Name);
        RefreshFavorites();
    }

    private void ContextMenu_Info_Click(object sender, RoutedEventArgs e)
    {
        var wp = GetWallpaperFromMenuItem(sender);
        if (wp == null) return;
        var dialog = new WallpaperInfoDialog(wp, RefreshFavorites)
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
